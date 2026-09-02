import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APP_CONFIG } from '../config/app-config';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: APP_CONFIG, useValue: { apiBaseUrl: '/api/v1', production: false } }
    ] });
    service = TestBed.inject(AuthService); http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('keeps the access token only in memory after login', () => {
    service.login({ email: 'user@nexora.test', password: 'Correct-Horse-42' }).subscribe();
    const request = http.expectOne('/api/v1/identity/login');
    expect(request.request.withCredentials).toBe(true);
    request.flush({ accessToken: 'jwt', accessTokenExpiresAt: new Date().toISOString() });
    expect(service.accessToken()).toBe('jwt');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('clears the in-memory session when refresh is rejected', () => {
    let result = true;
    service.refresh().subscribe(value => result = value);
    http.expectOne('/api/v1/identity/refresh').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(result).toBe(false);
    expect(service.accessToken()).toBeNull();
  });

  it('lists the user companies from GET /me/tenants with the bearer token', () => {
    let result: unknown;
    service.listMyTenants().subscribe(x => result = x);
    const request = http.expectOne('/api/v1/me/tenants');
    expect(request.request.method).toBe('GET');
    request.flush([{ id: 't1', name: 'Alpha', slug: 'alpha-co', roleName: 'ADMIN' }]);
    expect(result).toEqual([{ id: 't1', name: 'Alpha', slug: 'alpha-co', roleName: 'ADMIN' }]);
  });

  it('loads the effective modules from GET /identity/me in a tenant session', () => {
    const payload = btoa(JSON.stringify({ tenant_id: 't-1', permission: ['customers.read'] }))
      .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
    service.login({ email: 'u@nexora.test', password: 'Correct-Horse-42' }).subscribe();
    http.expectOne('/api/v1/identity/login').flush({ accessToken: `h.${payload}.s`, accessTokenExpiresAt: new Date().toISOString() });

    service.loadFeatures().subscribe();
    const request = http.expectOne('/api/v1/identity/me');
    expect(request.request.method).toBe('GET');
    request.flush({ id: 'u', email: 'u@nexora.test', permissions: [], features: { CUSTOMERS: { enabled: true, limit: null }, REPORTS: { enabled: false, limit: null } } });

    expect(service.hasFeature('CUSTOMERS')).toBe(true);
    expect(service.hasFeature('REPORTS')).toBe(false);
    expect(service.hasFeature('SERVICES')).toBe(false);
  });

  it('does not call /identity/me and reports an empty map outside a tenant session', () => {
    let result: unknown;
    service.loadFeatures().subscribe(x => result = x);
    http.expectNone('/api/v1/identity/me');
    expect(result).toEqual({});
    expect(service.hasFeature('CUSTOMERS')).toBe(false);
  });

  it('recognizes platform access only from the signed session payload', () => {
    const payload = btoa(JSON.stringify({ permission: ['platform.access', 'platform.users.read'] }))
      .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
    service.login({ email: 'admin@nexora.test', password: 'Correct-Horse-42' }).subscribe();
    http.expectOne('/api/v1/identity/login').flush({ accessToken: `header.${payload}.signature`, accessTokenExpiresAt: new Date().toISOString() });
    expect(service.isPlatformAdmin()).toBe(true);
  });
});
