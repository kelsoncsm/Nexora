import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { tenantGuard } from './core/auth/tenant.guard';
import { featureGuard } from './core/auth/feature.guard';
import { AuthPage } from './features/identity/auth-page';
import { HomePage } from './features/identity/home-page';
import { TenantSelectPage } from './features/identity/tenant-select-page';
import { ProfilePage } from './features/identity/profile-page';
import { platformAdminGuard } from './core/auth/platform-admin.guard';
import { AdminPage } from './features/administration/admin-page';
import { CustomerPage } from './features/customers/customer-page';
import { CatalogPage } from './features/catalog/catalog-page';
import { SchedulePage } from './features/scheduling/schedule-page';
import { SubscriptionAdminPage } from './features/administration/subscription-admin-page';
import { BillingAdminPage } from './features/administration/billing-admin-page';
import { BillingPage } from './features/billing/billing-page';
import { ReportsPage } from './features/reports/reports-page';
import { OnboardingPage } from './features/onboarding/onboarding-page';
import { VerticalSetupPage } from './features/verticals/vertical-setup-page';
import { TeamPage } from './features/team/team-page';
import { EmpresaPage } from './features/tenant/empresa-page';
import { RolesPage } from './features/tenant/roles-page';
import { PermissionsPage } from './features/tenant/permissions-page';
import { SettingsHubPage } from './features/tenant/settings-hub-page';
import { ErrorPage } from './features/errors/error-page';

export const routes: Routes = [
  { path: '', component: HomePage, canActivate: [authGuard] },
  { path: 'login', component: AuthPage, data: { mode: 'login' } },
  { path: 'cadastro', component: AuthPage, data: { mode: 'register' } },
  { path: 'selecionar-empresa', component: TenantSelectPage, canActivate: [authGuard] },
  { path: 'onboarding', component: OnboardingPage, canActivate: [authGuard] },
  { path: 'configuracao-inicial', component: VerticalSetupPage, canActivate: [authGuard, tenantGuard] },
  { path: 'admin', component: AdminPage, canActivate: [platformAdminGuard] },
  { path: 'admin/assinaturas', component: SubscriptionAdminPage, canActivate: [platformAdminGuard] },
  { path: 'admin/billing', component: BillingAdminPage, canActivate: [platformAdminGuard] },
  { path: 'admin/relatorios', component: ReportsPage, data: { platform: true }, canActivate: [platformAdminGuard] },
  { path: 'assinatura', component: BillingPage, canActivate: [authGuard, tenantGuard] },
  { path: 'clientes', component: CustomerPage, canActivate: [authGuard, tenantGuard, featureGuard('CUSTOMERS')] },
  { path: 'profissionais', component: CatalogPage, data: { kind: 'professionals' }, canActivate: [authGuard, tenantGuard, featureGuard('PROFESSIONALS')] },
  { path: 'servicos', component: CatalogPage, data: { kind: 'services' }, canActivate: [authGuard, tenantGuard, featureGuard('SERVICES')] },
  { path: 'agenda', component: SchedulePage, canActivate: [authGuard, tenantGuard, featureGuard('SCHEDULING')] },
  { path: 'relatorios', component: ReportsPage, canActivate: [authGuard, tenantGuard, featureGuard('REPORTS')] },
  { path: 'equipe', component: TeamPage, canActivate: [authGuard, tenantGuard] },
  { path: 'configuracoes', component: SettingsHubPage, canActivate: [authGuard, tenantGuard] },
  { path: 'empresa', component: EmpresaPage, canActivate: [authGuard, tenantGuard] },
  { path: 'perfis', component: RolesPage, canActivate: [authGuard, tenantGuard] },
  { path: 'permissoes', component: PermissionsPage, canActivate: [authGuard, tenantGuard] },
  { path: 'perfil', component: ProfilePage, canActivate: [authGuard] },
  { path: '403', component: ErrorPage, data: { code: 403 } },
  { path: 'erro', component: ErrorPage, data: { code: 500 } },
  { path: '**', component: ErrorPage, data: { code: 404 } }
];
