import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { NxButton, NxDataTable, NxPageHeader, NxStatCard } from '../../shared/ui';
import {
  AdministrationService,
  AdminDashboard,
  AdminTenant,
  AdminUser,
  Segment,
  AuditLog,
  Feature,
  Plan,
} from './administration.service';

type Section = 'dashboard' | 'tenants' | 'users' | 'segments' | 'features' | 'plans' | 'audit';

@Component({
  selector: 'app-admin-page',
  imports: [RouterLink, NxPageHeader, NxStatCard, NxDataTable, NxButton],
  templateUrl: './admin-page.html',
  styleUrl: './admin-page.scss',
})
export class AdminPage {
  private readonly api = inject(AdministrationService);
  readonly section = signal<Section>('dashboard');
  readonly loading = signal(false);
  readonly error = signal('');
  readonly dashboard = signal<AdminDashboard | null>(null);
  readonly rows = signal<(AdminTenant | AdminUser | Segment | AuditLog | Feature | Plan)[]>([]);
  draftCode = '';
  draftName = '';
  readonly menu: { key: Section; label: string }[] = [
    { key: 'dashboard', label: 'Dashboard' },
    { key: 'tenants', label: 'Tenants' },
    { key: 'users', label: 'Usuários' },
    { key: 'segments', label: 'Segmentos' },
    { key: 'features', label: 'Features' },
    { key: 'plans', label: 'Planos' },
    { key: 'audit', label: 'Auditoria' },
  ];
  constructor() {
    this.open('dashboard');
  }
  title() {
    return this.menu.find((x) => x.key === this.section())?.label ?? '';
  }
  open(section: Section) {
    this.section.set(section);
    this.loading.set(true);
    this.error.set('');
    const request: Observable<unknown> =
      section === 'dashboard'
        ? this.api.dashboard()
        : section === 'tenants'
          ? this.api.tenants()
          : section === 'users'
            ? this.api.users()
            : section === 'segments'
              ? this.api.segments()
              : section === 'features'
                ? this.api.features()
                : section === 'plans'
                  ? this.api.plans()
                  : this.api.auditLogs();
    request.subscribe({
      next: (value) => {
        if (section === 'dashboard') this.dashboard.set(value as AdminDashboard);
        else this.rows.set(value as never[]);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Não foi possível carregar esta seção.');
        this.loading.set(false);
      },
    });
  }
  create(event: Event) {
    event.preventDefault();
    const s = this.section();
    const request =
      s === 'features'
        ? this.api.createFeature(this.draftCode, this.draftName)
        : s === 'segments'
          ? this.api.createSegment(this.draftCode, this.draftName)
          : this.api.createPlan(this.draftCode, this.draftName);
    request.subscribe({
      next: () => this.open(s),
      error: () => this.error.set('Não foi possível salvar.'),
    });
  }
  toggleTenant(tenant: AdminTenant) {
    this.api
      .setTenantActive(tenant.id, !tenant.isActive)
      .subscribe({
        next: () => this.open('tenants'),
        error: () => this.error.set('Não foi possível alterar o status do tenant.'),
      });
  }
  identifier(row: any) {
    return row.code ?? row.slug ?? row.email ?? row.targetType;
  }
  description(row: any) {
    return row.name ?? row.email ?? row.action;
  }
  detail(row: any) {
    return typeof row.isActive === 'boolean'
      ? row.isActive
        ? 'Ativo'
        : 'Inativo'
      : new Date(row.occurredAt).toLocaleString('pt-BR');
  }
  isPlan(row: AdminTenant | AdminUser | Segment | AuditLog | Feature | Plan): row is Plan {
    return 'features' in row;
  }
  asTenant(row: AdminTenant | AdminUser | Segment | AuditLog | Feature | Plan): AdminTenant | null {
    return this.section() === 'tenants' ? (row as AdminTenant) : null;
  }
  togglePlan(plan: Plan, field: 'public' | 'trial'): void {
    const isPublic = field === 'public' ? !plan.isPublic : plan.isPublic;
    const trial = field === 'trial' ? !plan.isTrialEligible : plan.isTrialEligible;
    this.api
      .configurePlan(plan, isPublic, trial)
      .subscribe({
        next: () => this.open('plans'),
        error: () => this.error.set('Não foi possível configurar o plano.'),
      });
  }
}
