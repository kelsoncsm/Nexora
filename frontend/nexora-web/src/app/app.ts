import { Component, computed, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './core/auth/auth.service';
import { HttpClient } from '@angular/common/http';
import { APP_CONFIG } from './core/config/app-config';

interface ShellPlan {
  planCode: string;
  status: string;
}
type Theme = 'light' | 'dark';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
})
export class App {
  readonly auth = inject(AuthService);
  private router = inject(Router);
  private http = inject(HttpClient);
  private cfg = inject(APP_CONFIG);

  readonly navOpen = signal(false);
  readonly collapsed = signal(false);
  readonly userMenuOpen = signal(false);
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

  constructor() {
    this.applyTheme(this.theme());
    this.router.events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe((e) => {
        this.currentUrl.set(e.urlAfterRedirects);
        this.navOpen.set(false);
        this.userMenuOpen.set(false);
      });
    effect(() => {
      if (this.auth.tenantId()) {
        this.http
          .get<ShellPlan>(`${this.cfg.apiBaseUrl}/subscription`)
          .subscribe({ next: (x) => this.plan.set(x), error: () => this.plan.set(null) });
      } else {
        this.plan.set(null);
      }
    });
  }

  has(permission: string) {
    return this.auth.permissions().includes(permission);
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
    this.userMenuOpen.set(false);
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
