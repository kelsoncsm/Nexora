import { Component, computed, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './core/auth/auth.service';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from './core/config/app-config';
import { NxSidebar } from './shared/ui/shell/nx-sidebar';
import { NxTopbar } from './shared/ui/shell/nx-topbar';
import { NxNavGroup, NxShellPlan, NxShellUser } from './shared/ui/shell/nav';

interface ShellPlan {
  planCode: string;
  status: string;
}
type Theme = 'light' | 'dark';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, NxSidebar, NxTopbar],
  templateUrl: './app.html',
})
export class App {
  readonly auth = inject(AuthService);
  private router = inject(Router);
  private http = inject(HttpClient);
  private cfg = inject(APP_CONFIG);

  readonly navOpen = signal(false);
  readonly collapsed = signal(false);
  readonly theme = signal<Theme>(this.readTheme());
  readonly plan = signal<ShellPlan | null>(null);
  readonly currentUrl = signal(this.router.url);

  readonly showShell = computed(
    () =>
      this.auth.isAuthenticated() &&
      !['/login', '/cadastro'].includes(this.currentUrl().split('?')[0]),
  );
  readonly tenantLabel = computed(() =>
    this.auth.tenantId()
      ? `Empresa · ${this.auth.tenantId()!.slice(0, 8)}`
      : 'Escopo da plataforma',
  );
  readonly roleLabel = computed(() =>
    this.auth.isPlatformAdmin()
      ? 'Administrador da plataforma'
      : this.auth.tenantId()
        ? 'Operação'
        : 'Conta',
  );
  readonly pageTitle = computed(() => {
    const p = this.currentUrl().split('?')[0];
    if (p === '/') return 'Visão Geral';
    if (p.startsWith('/agenda')) return 'Agenda';
    if (p.startsWith('/clientes')) return 'Clientes';
    if (p.startsWith('/profissionais')) return 'Profissionais';
    if (p.startsWith('/servicos')) return 'Serviços';
    if (p.startsWith('/relatorios')) return 'Relatórios';
    if (p.startsWith('/assinatura')) return 'Plano e Assinatura';
    if (p.startsWith('/equipe')) return 'Usuários e Equipe';
    if (p.startsWith('/configuracoes')) return 'Configurações';
    if (p.startsWith('/empresa')) return 'Empresa';
    if (p.startsWith('/perfis')) return 'Perfis e Papéis';
    if (p.startsWith('/permissoes')) return 'Perfis e Permissões';
    if (p.startsWith('/perfil')) return 'Meu Perfil';
    if (p === '/403') return 'Acesso negado';
    if (p === '/erro') return 'Erro';
    if (p.startsWith('/onboarding')) return 'Configuração inicial';
    if (p.startsWith('/configuracao-inicial')) return 'Configuração inicial';
    if (p === '/admin') return 'Administração';
    if (p.startsWith('/admin/assinaturas')) return 'Assinaturas';
    if (p.startsWith('/admin/billing')) return 'Billing';
    if (p.startsWith('/admin/relatorios')) return 'Relatórios da plataforma';
    return 'Nexora';
  });
  readonly initials = computed(() => {
    const label = this.auth.userLabel();
    const base = label.includes('@') ? label.split('@')[0] : label;
    const parts = base.split(/[.\s_-]+/).filter(Boolean);
    return ((parts[0]?.[0] ?? 'N') + (parts[1]?.[0] ?? parts[0]?.[1] ?? 'X')).toUpperCase();
  });
  readonly planActive = computed(() =>
    ['active', 'trialing'].includes((this.plan()?.status ?? '').toLowerCase()),
  );
  readonly planStatusLabel = computed(() => {
    const map: Record<string, string> = {
      trialing: 'Em teste',
      active: 'Ativo',
      pastdue: 'Pagamento pendente',
      canceled: 'Cancelado',
      expired: 'Expirado',
    };
    const status = this.plan()?.status ?? '';
    return map[status.toLowerCase()] ?? status;
  });

  readonly shellUser = computed<NxShellUser>(() => ({
    label: this.auth.userLabel(),
    role: this.roleLabel(),
    initials: this.initials(),
  }));
  readonly shellPlan = computed<NxShellPlan | null>(() => {
    const p = this.plan();
    if (!p) return null;
    return {
      code: p.planCode,
      statusLabel: this.planStatusLabel(),
      active: this.planActive(),
      tenantLabel: this.tenantLabel(),
    };
  });
  readonly navGroups = computed<NxNavGroup[]>(() => {
    const groups: NxNavGroup[] = [
      { label: 'PRINCIPAL', items: [{ label: 'Visão Geral', route: '/', icon: 'i-home', exact: true }] },
    ];
    if (this.auth.tenantId()) {
      const principal = groups[0].items;
      // An item shows only when the role grants the permission AND the plan includes the module.
      if (this.allowed('appointments.read', 'SCHEDULING')) principal.push({ label: 'Agenda', route: '/agenda', icon: 'i-calendar' });
      if (this.allowed('customers.read', 'CUSTOMERS')) principal.push({ label: 'Clientes', route: '/clientes', icon: 'i-users' });
      if (this.allowed('professionals.read', 'PROFESSIONALS')) principal.push({ label: 'Profissionais', route: '/profissionais', icon: 'i-briefcase' });
      if (this.allowed('services.read', 'SERVICES')) principal.push({ label: 'Serviços', route: '/servicos', icon: 'i-scissors' });
      if (this.allowed('reports.read', 'REPORTS')) principal.push({ label: 'Relatórios', route: '/relatorios', icon: 'i-chart' });
      groups.push({
        label: 'GESTÃO',
        items: [
          { label: 'Usuários e Equipe', route: '/equipe', icon: 'i-user' },
          { label: 'Plano e Assinatura', route: '/assinatura', icon: 'i-card' },
          { label: 'Configurações', route: '/configuracoes', icon: 'i-gear' },
        ],
      });
    }
    if (this.auth.isPlatformAdmin()) {
      groups.push({
        label: 'PLATAFORMA',
        items: [
          { label: 'Administração', route: '/admin', icon: 'i-shield', exact: true },
          { label: 'Assinaturas', route: '/admin/assinaturas', icon: 'i-repeat' },
          { label: 'Billing', route: '/admin/billing', icon: 'i-dollar' },
          { label: 'Relatórios da plataforma', route: '/admin/relatorios', icon: 'i-chart' },
        ],
      });
    }
    return groups;
  });

  constructor() {
    this.applyTheme(this.theme());
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        this.currentUrl.set(e.urlAfterRedirects);
        this.navOpen.set(false);
      });
    effect(() => {
      if (this.auth.tenantId()) {
        this.http
          .get<ShellPlan>(`${this.cfg.apiBaseUrl}/subscription`)
          .subscribe({ next: (x) => this.plan.set(x), error: () => this.plan.set(null) });
        this.auth.loadFeatures().subscribe();
      } else {
        this.plan.set(null);
        this.auth.clearFeatures();
      }
    });
  }

  has(permission: string) {
    return this.auth.permissions().includes(permission);
  }

  private allowed(permission: string, feature: string) {
    return this.has(permission) && this.auth.hasFeature(feature);
  }

  toggleNav() {
    if (window.innerWidth <= 992) this.navOpen.set(!this.navOpen());
    else this.collapsed.set(!this.collapsed());
  }

  toggleTheme() {
    const next: Theme = this.theme() === 'dark' ? 'light' : 'dark';
    this.theme.set(next);
    this.applyTheme(next);
    try {
      localStorage.setItem('nx-theme', next);
    } catch {
      /* storage indisponível */
    }
  }

  logout() {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/login'));
  }

  private applyTheme(theme: Theme) {
    document.documentElement.setAttribute('data-theme', theme);
  }

  private readTheme(): Theme {
    try {
      const stored = localStorage.getItem('nx-theme');
      if (stored === 'light' || stored === 'dark') return stored;
    } catch {
      /* storage indisponível */
    }
    return 'light';
  }
}
