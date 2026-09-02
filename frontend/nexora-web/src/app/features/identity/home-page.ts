import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { AuthService, UserTenant } from '../../core/auth/auth.service';
import { APP_CONFIG } from '../../core/config/app-config';
import { NxButton, NxCard, NxEmptyState, NxStatCard } from '../../shared/ui';

interface OperationalReport {
  customers?: number;
  appointments?: { total: number; completed: number; cancelled: number; noShow: number };
}

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, NxButton, NxCard, NxEmptyState, NxStatCard],
  templateUrl: './home-page.html',
})
export class HomePage {
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);
  private readonly cfg = inject(APP_CONFIG);

  readonly report = signal<OperationalReport | null>(null);
  readonly state = signal<'idle' | 'loading' | 'ready' | 'error'>('idle');
  readonly loading = computed(() => this.state() === 'loading');
  readonly error = signal('');

  /** Companies the user can re-enter without typing a slug (shown when no tenant is active). */
  readonly tenants = signal<UserTenant[]>([]);
  readonly tenantsState = signal<'loading' | 'ready' | 'error'>('loading');
  readonly busySlug = signal<string | null>(null);

  readonly firstName = computed(() => {
    const label = this.auth.userLabel();
    const base = label.includes('@') ? label.split('@')[0] : label;
    const first = base.split(/[.\s_-]+/).filter(Boolean)[0] ?? base;
    return first.charAt(0).toUpperCase() + first.slice(1);
  });
  readonly completionRate = computed(() => {
    const a = this.report()?.appointments;
    if (!a || !a.total) return '0%';
    return `${Math.round((a.completed / a.total) * 100)}%`;
  });

  constructor() {
    effect(() => {
      if (this.auth.tenantId() && this.state() === 'idle') this.loadReport();
    });
    if (!this.auth.tenantId()) this.loadTenants();
  }

  private loadTenants(): void {
    this.tenantsState.set('loading');
    this.auth.listMyTenants().subscribe({
      next: (tenants) => {
        this.tenants.set(tenants);
        this.tenantsState.set('ready');
      },
      error: () => this.tenantsState.set('error'),
    });
  }

  private loadReport() {
    this.state.set('loading');
    const from = new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10);
    const to = new Date(Date.now() + 86400000).toISOString().slice(0, 10);
    this.http
      .get<OperationalReport>(
        `${this.cfg.apiBaseUrl}/reports/overview?from=${from}T00:00:00Z&to=${to}T00:00:00Z`,
      )
      .subscribe({
        next: (x) => {
          this.report.set(x);
          this.state.set('ready');
        },
        error: () => {
          this.report.set(null);
          this.state.set('error');
        },
      });
  }

  greeting(): string {
    const h = new Date().getHours();
    return h < 12 ? 'Bom dia' : h < 18 ? 'Boa tarde' : 'Boa noite';
  }

  select(slug: string): void {
    const normalized = slug?.trim();
    if (!normalized) {
      this.error.set('Informe o slug da empresa.');
      return;
    }
    this.error.set('');
    this.busySlug.set(normalized);
    this.auth.selectTenant(normalized).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: () => {
        this.busySlug.set(null);
        this.error.set('Não foi possível abrir esta empresa. Verifique o slug.');
      },
    });
  }
  onboarding(): void {
    void this.router.navigateByUrl('/onboarding');
  }
  logout(): void {
    this.auth.logout().subscribe(() => void this.router.navigateByUrl('/login'));
  }
}
