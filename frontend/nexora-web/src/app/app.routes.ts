import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { AuthPage } from './features/identity/auth-page';
import { HomePage } from './features/identity/home-page';
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

export const routes: Routes = [
  { path: '', component: HomePage, canActivate: [authGuard] },
  { path: 'login', component: AuthPage, data: { mode: 'login' } },
  { path: 'cadastro', component: AuthPage, data: { mode: 'register' } },
  { path: 'onboarding', component: OnboardingPage, canActivate: [authGuard] },
  { path: 'configuracao-inicial', component: VerticalSetupPage, canActivate: [authGuard] },
  { path: 'admin', component: AdminPage, canActivate: [platformAdminGuard] },
  { path: 'admin/assinaturas', component: SubscriptionAdminPage, canActivate: [platformAdminGuard] },
  { path: 'admin/billing', component: BillingAdminPage, canActivate: [platformAdminGuard] },
  { path: 'admin/relatorios', component: ReportsPage, data:{platform:true}, canActivate: [platformAdminGuard] },
  { path: 'assinatura', component: BillingPage, canActivate: [authGuard] },
  { path: 'clientes', component: CustomerPage, canActivate: [authGuard] },
  { path: 'profissionais', component: CatalogPage, data:{kind:'professionals'}, canActivate:[authGuard] },
  { path: 'servicos', component: CatalogPage, data:{kind:'services'}, canActivate:[authGuard] },
  { path: 'agenda', component: SchedulePage, canActivate:[authGuard] },
  { path: 'relatorios', component: ReportsPage, canActivate:[authGuard] },
  { path: '**', redirectTo: '' }
];
