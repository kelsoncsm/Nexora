import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { APP_CONFIG } from './core/config/app-config';
import { AuthService } from './core/auth/auth.service';

import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]),provideHttpClient(),{provide:APP_CONFIG,useValue:{apiBaseUrl:'/api/v1'}}]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the application shell', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.nx-public')).toBeTruthy();
  });
});

describe('App sidebar gating (permission AND feature)', () => {
  function buildAuth(overrides: Record<string, unknown>) {
    return {
      userLabel: () => 'u@nexora.test',
      isAuthenticated: () => true,
      isPlatformAdmin: () => false,
      tenantId: () => 't1',
      permissions: () => [] as string[],
      hasFeature: () => false,
      loadFeatures: () => of({}),
      clearFeatures: () => {},
      logout: () => of(void 0),
      ...overrides,
    };
  }

  function navItems(auth: unknown): string[] {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), { provide: APP_CONFIG, useValue: { apiBaseUrl: '/api/v1' } }, { provide: AuthService, useValue: auth }],
    });
    const app = TestBed.createComponent(App).componentInstance;
    return app.navGroups().flatMap(g => g.items.map(i => i.label));
  }

  it('shows a module when the role grants the permission and the plan includes the feature', () => {
    const items = navItems(buildAuth({ permissions: () => ['customers.read'], hasFeature: (c: string) => c === 'CUSTOMERS' }));
    expect(items).toContain('Clientes');
  });

  it('hides a module when the permission is granted but the plan omits the feature', () => {
    const items = navItems(buildAuth({ permissions: () => ['customers.read'], hasFeature: () => false }));
    expect(items).not.toContain('Clientes');
  });

  it('hides a module when the feature is present but the role lacks the permission', () => {
    const items = navItems(buildAuth({ permissions: () => [], hasFeature: () => true }));
    expect(items).not.toContain('Clientes');
  });

  it('shows team and billing only with tenant.manage (audit P2.1/P2.2)', () => {
    const withManage = navItems(buildAuth({ permissions: () => ['tenant.manage'] }));
    expect(withManage).toContain('Usuários e Equipe');
    expect(withManage).toContain('Plano e Assinatura');
  });

  it('hides team and billing without tenant.manage, keeping Configurações', () => {
    const withoutManage = navItems(buildAuth({ permissions: () => ['customers.read'] }));
    expect(withoutManage).not.toContain('Usuários e Equipe');
    expect(withoutManage).not.toContain('Plano e Assinatura');
    expect(withoutManage).toContain('Configurações');
  });
});
