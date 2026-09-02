import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, map, Observable, of, tap } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';

interface TokenResponse { accessToken: string; accessTokenExpiresAt: string; }
export interface Credentials { email: string; password: string; }
/** An active company the authenticated user belongs to (GET /me/tenants). */
export interface UserTenant { id: string; name: string; slug: string; roleName: string; }
/** Effective modules for the current tenant session (from GET /identity/me). Backend stays the authority. */
export type TenantFeatures = Record<string, { enabled: boolean; limit: number | null }>;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(APP_CONFIG);
  private readonly tokenState = signal<string | null>(null);
  readonly isAuthenticated = computed(() => this.tokenState() !== null);
  private readonly claims = computed(() => this.readClaims(this.tokenState()));
  readonly permissions = computed(() => {const value=this.claims()['permission'];return Array.isArray(value)?value:typeof value==='string'?[value]:[];});
  readonly isPlatformAdmin = computed(() => this.permissions().includes('platform.access'));
  readonly tenantId = computed(() => typeof this.claims()['tenant_id']==='string' ? this.claims()['tenant_id'] as string : null);
  readonly userLabel = computed(() => {const claims=this.claims();return String(claims['email']??claims['sub']??'Conta Nexora');});
  readonly accessToken = this.tokenState.asReadonly();
  private readonly featureState = signal<TenantFeatures>({});
  readonly features = this.featureState.asReadonly();

  register(credentials: Credentials): Observable<void> { return this.authenticate('register', credentials); }
  login(credentials: Credentials): Observable<void> { return this.authenticate('login', credentials); }
  refresh(): Observable<boolean> {
    return this.http.post<TokenResponse>(`${this.config.apiBaseUrl}/identity/refresh`, {}, { withCredentials: true }).pipe(
      tap(x => this.tokenState.set(x.accessToken)), map(() => true),
      catchError(() => { this.tokenState.set(null); return of(false); })
    );
  }
  logout(): Observable<void> {
    return this.http.post<void>(`${this.config.apiBaseUrl}/identity/logout`, {}, { withCredentials: true })
      .pipe(tap(() => { this.tokenState.set(null); this.featureState.set({}); }));
  }
  /**
   * Loads the effective modules for the active tenant from GET /identity/me. Used to hide menu items
   * and routes; the backend endpoint filters remain the authority (ADR-0019). Returns the map so a
   * route guard can wait for it. Resilient: any failure yields an empty map (nothing hidden pre-load).
   */
  loadFeatures(): Observable<TenantFeatures> {
    if (!this.tenantId()) { this.featureState.set({}); return of({}); }
    return this.http.get<{ features?: TenantFeatures }>(`${this.config.apiBaseUrl}/identity/me`).pipe(
      map(x => x.features ?? {}),
      tap(features => this.featureState.set(features)),
      catchError(() => { this.featureState.set({}); return of({} as TenantFeatures); }),
    );
  }
  clearFeatures(): void { this.featureState.set({}); }
  hasFeature(code: string): boolean { return this.featureState()[code]?.enabled === true; }
  selectTenant(slug:string):Observable<void>{return this.http.post<TokenResponse>(`${this.config.apiBaseUrl}/t/${encodeURIComponent(slug)}/session`,{},{withCredentials:true}).pipe(tap(x=>this.tokenState.set(x.accessToken)),map(()=>undefined));}
  /** Companies the current user is an active member of, used to re-enter a tenant without typing a slug. */
  listMyTenants(): Observable<UserTenant[]> {
    return this.http.get<UserTenant[]>(`${this.config.apiBaseUrl}/me/tenants`);
  }
  private authenticate(action: string, credentials: Credentials): Observable<void> {
    return this.http.post<TokenResponse>(`${this.config.apiBaseUrl}/identity/${action}`, credentials, { withCredentials: true })
      .pipe(tap(x => this.tokenState.set(x.accessToken)), map(() => undefined));
  }
  private readClaims(token: string | null): Record<string,unknown> {
    if (!token) return {};
    try {
      return JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as Record<string,unknown>;
    } catch { return {}; }
  }
}
