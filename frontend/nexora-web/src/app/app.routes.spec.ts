import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { APP_CONFIG } from './core/config/app-config';
import { routes } from './app.routes';
import { platformAdminGuard } from './core/auth/platform-admin.guard';
import { tenantGuard } from './core/auth/tenant.guard';
import { AuthService } from './core/auth/auth.service';
import { ErrorPage } from './features/errors/error-page';
import { TeamPage } from './features/team/team-page';
import { InvitationAcceptPage } from './features/identity/invitation-accept-page';
import { ProfilePage } from './features/identity/profile-page';
import { EmpresaPage } from './features/tenant/empresa-page';
import { RolesPage } from './features/tenant/roles-page';
import { PermissionsPage } from './features/tenant/permissions-page';
import { SettingsHubPage } from './features/tenant/settings-hub-page';
import { TenantSelectPage } from './features/identity/tenant-select-page';

function pathsOf() { return routes.map(r => r.path); }

describe('app routes', () => {
  it('exposes the new tenant and error routes', () => {
    const paths = pathsOf();
    expect(paths).toContain('equipe');
    expect(paths).toContain('perfil');
    expect(paths).toContain('configuracoes');
    expect(paths).toContain('empresa');
    expect(paths).toContain('perfis');
    expect(paths).toContain('permissoes');
    expect(paths).toContain('403');
    expect(paths).toContain('erro');
    expect(paths).toContain('convite/aceitar');
  });

  it('exposes the invitation-accept page with no guards (anonymous flow)', () => {
    const route = routes.find(r => r.path === 'convite/aceitar')!;
    expect(route.component).toBe(InvitationAcceptPage);
    expect(route.canActivate).toBeUndefined();
  });

  it('binds /equipe and /perfil to their pages, /equipe also behind the tenant guard', () => {
    const equipe = routes.find(r => r.path === 'equipe')!;
    const perfil = routes.find(r => r.path === 'perfil')!;
    expect(equipe.component).toBe(TeamPage);
    expect(perfil.component).toBe(ProfilePage);
    expect(equipe.canActivate?.length).toBe(2); // authGuard + tenantGuard
    expect(perfil.canActivate?.length).toBe(1); // authGuard only
  });

  it('binds the tenant administration routes behind auth + tenant guards', () => {
    const cases: [string, unknown][] = [
      ['configuracoes', SettingsHubPage], ['empresa', EmpresaPage],
      ['perfis', RolesPage], ['permissoes', PermissionsPage]
    ];
    for (const [path, component] of cases) {
      const route = routes.find(r => r.path === path)!;
      expect(route.component).toBe(component);
      expect(route.canActivate?.length).toBe(2); // authGuard + tenantGuard
    }
  });

  it('exposes the company picker behind the auth guard only', () => {
    const route = routes.find(r => r.path === 'selecionar-empresa')!;
    expect(route.component).toBe(TenantSelectPage);
    expect(route.canActivate?.length).toBe(1); // authGuard only — no tenant yet
  });

  it('gates the module routes behind auth + tenant + feature guards', () => {
    for (const path of ['clientes', 'profissionais', 'servicos', 'agenda', 'relatorios']) {
      const route = routes.find(r => r.path === path)!;
      expect(route.canActivate?.length).toBe(3); // authGuard + tenantGuard + featureGuard
    }
  });

  it('catch-all renders the 404 error page instead of redirecting', () => {
    const wildcard = routes.find(r => r.path === '**')!;
    expect(wildcard.component).toBe(ErrorPage);
    expect(wildcard.data?.['code']).toBe(404);
    expect(wildcard.redirectTo).toBeUndefined();
  });
});

describe('platformAdminGuard', () => {
  function run(auth: Partial<AuthService>) {
    return TestBed.runInInjectionContext(() =>
      platformAdminGuard({} as never, { url: '/admin' } as never)) as boolean | UrlTree;
  }

  it('sends an authenticated non-admin to /403', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes), provideHttpClient(),
        { provide: APP_CONFIG, useValue: { apiBaseUrl: '/api/v1' } },
        { provide: AuthService, useValue: { isPlatformAdmin: () => false, isAuthenticated: () => true, refresh: () => { throw new Error('unused'); } } }
      ]
    });
    const result = run({});
    const router = TestBed.inject(Router);
    expect(result instanceof UrlTree).toBe(true);
    expect(router.serializeUrl(result as UrlTree)).toBe('/403');
  });

  it('allows a platform admin', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes), provideHttpClient(),
        { provide: APP_CONFIG, useValue: { apiBaseUrl: '/api/v1' } },
        { provide: AuthService, useValue: { isPlatformAdmin: () => true, isAuthenticated: () => true } }
      ]
    });
    expect(run({})).toBe(true);
  });
});

describe('tenantGuard', () => {
  function run() {
    return TestBed.runInInjectionContext(() => tenantGuard({} as never, {} as never)) as boolean | UrlTree;
  }
  it('redirects to / when there is no active company', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes), provideHttpClient(),
        { provide: APP_CONFIG, useValue: { apiBaseUrl: '/api/v1' } },
        { provide: AuthService, useValue: { tenantId: () => null } }
      ]
    });
    const result = run();
    expect(result instanceof UrlTree).toBe(true);
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/');
  });
  it('allows a tenant session', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes), provideHttpClient(),
        { provide: APP_CONFIG, useValue: { apiBaseUrl: '/api/v1' } },
        { provide: AuthService, useValue: { tenantId: () => 'abc' } }
      ]
    });
    expect(run()).toBe(true);
  });
});
