import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, map, Observable, of, tap } from 'rxjs';
import { APP_CONFIG } from '../config/app-config';

interface TokenResponse { accessToken: string; accessTokenExpiresAt: string; }
export interface Credentials { email: string; password: string; }

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
      .pipe(tap(() => this.tokenState.set(null)));
  }
  selectTenant(slug:string):Observable<void>{return this.http.post<TokenResponse>(`${this.config.apiBaseUrl}/t/${encodeURIComponent(slug)}/session`,{},{withCredentials:true}).pipe(tap(x=>this.tokenState.set(x.accessToken)),map(()=>undefined));}
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
