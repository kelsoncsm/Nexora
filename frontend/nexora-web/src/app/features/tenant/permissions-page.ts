import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { TenantAdminService, TenantPermissionDescriptor, TenantRole } from './tenant-admin.service';

const MODULE_LABELS: Record<string, string> = {
  customers: 'Clientes',
  professionals: 'Profissionais',
  services: 'Serviços',
  appointments: 'Agenda',
  reports: 'Relatórios',
  tenant: 'Administração da empresa',
};
const ACTION_LABELS: Record<string, string> = {
  read: 'Visualizar',
  create: 'Criar',
  update: 'Editar',
  delete: 'Excluir',
  cancel: 'Cancelar',
  manage: 'Gerenciar',
};

interface ModuleGroup {
  module: string;
  label: string;
  items: TenantPermissionDescriptor[];
}

@Component({
  selector: 'app-permissions-page',
  imports: [FormsModule],
  templateUrl: './permissions-page.html',
  styleUrl: './permissions-page.scss',
})
export class PermissionsPage {
  private readonly service = inject(TenantAdminService);
  private readonly route = inject(ActivatedRoute);

  readonly roles = signal<TenantRole[]>([]);
  readonly catalog = signal<TenantPermissionDescriptor[]>([]);
  readonly selectedRoleId = signal('');
  readonly selectedKeys = signal<Set<string>>(new Set());
  readonly busy = signal(false);
  readonly error = signal('');
  readonly saved = signal(false);

  readonly current = computed(
    () => this.roles().find((r) => r.id === this.selectedRoleId()) ?? null,
  );
  readonly groups = computed<ModuleGroup[]>(() => {
    const byModule = new Map<string, TenantPermissionDescriptor[]>();
    for (const p of this.catalog()) {
      const list = byModule.get(p.module) ?? [];
      list.push(p);
      byModule.set(p.module, list);
    }
    return [...byModule.entries()].map(([module, items]) => ({
      module,
      label: MODULE_LABELS[module] ?? module,
      items,
    }));
  });
  readonly dirty = computed(() => {
    const role = this.current();
    if (!role) return false;
    const original = new Set(role.permissions);
    const sel = this.selectedKeys();
    if (original.size !== sel.size) return true;
    for (const k of sel) if (!original.has(k)) return true;
    return false;
  });

  constructor() {
    forkJoin({ roles: this.service.roles(), catalog: this.service.permissionCatalog() }).subscribe({
      next: (x) => {
        this.roles.set(x.roles);
        this.catalog.set(x.catalog);
        const wanted = this.route.snapshot.queryParamMap.get('role');
        const initial = x.roles.find((r) => r.id === wanted) ?? x.roles[0];
        if (initial) this.selectRole(initial.id);
      },
      error: (e) =>
        this.error.set(
          e?.status === 403
            ? 'Você não tem permissão para gerenciar permissões.'
            : 'Não foi possível carregar os dados.',
        ),
    });
  }

  actionLabel(action: string) {
    return ACTION_LABELS[action] ?? action;
  }

  selectRole(id: string) {
    this.saved.set(false);
    this.selectedRoleId.set(id);
    this.reset();
  }

  reset() {
    const role = this.roles().find((r) => r.id === this.selectedRoleId());
    this.selectedKeys.set(new Set(role?.permissions ?? []));
  }

  toggle(key: string, checked: boolean) {
    const next = new Set(this.selectedKeys());
    if (checked) next.add(key);
    else next.delete(key);
    this.selectedKeys.set(next);
  }

  toggleGroup(g: ModuleGroup, checked: boolean) {
    const next = new Set(this.selectedKeys());
    for (const p of g.items) {
      if (checked) next.add(p.key);
      else next.delete(p.key);
    }
    this.selectedKeys.set(next);
  }

  allChecked(g: ModuleGroup) {
    return g.items.every((p) => this.selectedKeys().has(p.key));
  }
  someChecked(g: ModuleGroup) {
    return g.items.some((p) => this.selectedKeys().has(p.key));
  }

  save() {
    const role = this.current();
    if (!role || role.isSystem || this.busy() || !this.dirty()) return;
    this.busy.set(true);
    this.error.set('');
    this.saved.set(false);
    this.service.setRolePermissions(role.id, [...this.selectedKeys()]).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.saved.set(true);
        this.roles.set(this.roles().map((r) => (r.id === updated.id ? updated : r)));
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(
          e?.status === 403
            ? 'Você não tem permissão para gerenciar permissões.'
            : e?.status === 404
              ? 'Papel não encontrado nesta empresa.'
              : (e?.error?.title ?? e?.error ?? 'Não foi possível salvar as permissões.'),
        );
      },
    });
  }
}
