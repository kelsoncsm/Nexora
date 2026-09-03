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

describe('App — tema único (claro)', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), { provide: APP_CONFIG, useValue: { apiBaseUrl: '/api/v1' } }],
    }).compileComponents();
  });

  it('não expõe alternância de tema no shell', () => {
    const app = TestBed.createComponent(App).componentInstance as unknown as Record<string, unknown>;
    expect(app['toggleTheme']).toBeUndefined();
    expect(app['theme']).toBeUndefined();
  });

  it('fixa data-theme="light" no documento e descarta preferência antiga do navegador', () => {
    try {
      localStorage.setItem('nx-theme', 'dark');
    } catch {
      /* storage indisponível no runner */
    }
    document.documentElement.setAttribute('data-theme', 'dark');

    TestBed.createComponent(App);

    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
    expect(localStorage.getItem('nx-theme')).toBeNull();
  });

  it('não renderiza botão de alternar tema na topbar', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();
    const html = (fixture.nativeElement as HTMLElement).innerHTML;
    expect(html).not.toContain('Alternar tema');
    expect(html).not.toContain('#i-moon');
    expect(html).not.toContain('#i-sun');
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

  it('shows billing with tenant.manage and the team with tenant.members.read', () => {
    // A tenant.manage role is backfilled with tenant.members.* (migration 20260902150000), so in
    // practice it sees both; the nav gates them on their own keys.
    const full = navItems(buildAuth({ permissions: () => ['tenant.manage', 'tenant.members.read'] }));
    expect(full).toContain('Usuários e Equipe');
    expect(full).toContain('Plano e Assinatura');
  });

  it('shows the team to a custom role holding only tenant.members.read (no billing)', () => {
    const memberViewer = navItems(buildAuth({ permissions: () => ['tenant.members.read'] }));
    expect(memberViewer).toContain('Usuários e Equipe');
    expect(memberViewer).not.toContain('Plano e Assinatura');
  });

  it('hides team and billing without any tenant admin permission, keeping Configurações', () => {
    const withoutManage = navItems(buildAuth({ permissions: () => ['customers.read'] }));
    expect(withoutManage).not.toContain('Usuários e Equipe');
    expect(withoutManage).not.toContain('Plano e Assinatura');
    expect(withoutManage).toContain('Configurações');
  });
});
