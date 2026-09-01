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

  it('recognizes platform access only from the signed session payload', () => {
    const payload = btoa(JSON.stringify({ permission: ['platform.access', 'platform.users.read'] }))
      .replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_');
    service.login({ email: 'admin@nexora.test', password: 'Correct-Horse-42' }).subscribe();
    http.expectOne('/api/v1/identity/login').flush({ accessToken: `header.${payload}.signature`, accessTokenExpiresAt: new Date().toISOString() });
    expect(service.isPlatformAdmin()).toBe(true);
  });
});
