import { Component, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { APP_CONFIG } from '../../core/config/app-config';
import { AuthService } from '../../core/auth/auth.service';
import { TenantAdminService, TenantRole } from '../tenant/tenant-admin.service';
import {
  NxAvatar,
  NxBadge,
  NxConfirmDialog,
  NxDataTable,
  NxPageHeader,
  NxSearchInput,
} from '../../shared/ui';

interface TenantMember {
  id: string;
  userId: string;
  email: string;
  role: string;
  roleId: string;
  isActive: boolean;
}

@Component({
  selector: 'app-team-page',
  imports: [
    FormsModule,
    NxPageHeader,
    NxSearchInput,
    NxDataTable,
    NxAvatar,
    NxBadge,
    NxConfirmDialog,
  ],
  templateUrl: './team-page.html',
  styleUrl: './team-page.scss',
})
export class TeamPage {
  private http = inject(HttpClient);
  private base = inject(APP_CONFIG).apiBaseUrl;
  private auth = inject(AuthService);
  private tenantAdmin = inject(TenantAdminService);
  members = signal<TenantMember[]>([]);
  roles = signal<TenantRole[]>([]);
  error = signal('');
  saved = signal(false);
  busyId = signal('');
  q = signal('');
  confirming = signal<TenantMember | null>(null);
  readonly canManage = computed(() => this.auth.permissions().includes('tenant.manage'));
  readonly visible = computed(() => {
    const term = this.q().trim().toLowerCase();
    const list = this.members();
    return term ? list.filter((m) => m.email.toLowerCase().includes(term)) : list;
  });
  constructor() {
    this.load();
  }
  load() {
    this.http.get<TenantMember[]>(`${this.base}/tenant/members`).subscribe({
      next: (x) => {
        this.members.set(x);
        this.error.set('');
      },
      error: () => this.error.set('Não foi possível carregar a equipe.'),
    });
    if (this.canManage()) {
      this.tenantAdmin.roles().subscribe({
        next: (r) => this.roles.set(r),
        error: () => {
          /* dropdown hidden without roles */
        },
      });
    }
  }
  assign(m: TenantMember, roleId: string) {
    if (!roleId || roleId === m.roleId || this.busyId()) return;
    this.busyId.set(m.id);
    this.error.set('');
    this.saved.set(false);
    this.tenantAdmin.assignMemberRole(m.id, roleId).subscribe({
      next: () => {
        this.busyId.set('');
        this.saved.set(true);
        this.load();
      },
      error: (e) => {
        this.busyId.set('');
        this.error.set(
          e?.status === 403
            ? 'Você não tem permissão para alterar papéis.'
            : e?.status === 404
              ? 'Membro ou papel não encontrado nesta empresa.'
              : 'Não foi possível alterar o papel do membro.',
        );
        this.load();
      },
    });
  }
  deactivate(m: TenantMember) {
    this.confirming.set(null);
    this.http.patch<void>(`${this.base}/tenant/members/${m.id}/deactivate`, {}).subscribe({
      next: () => this.load(),
      error: (e) =>
        this.error.set(
          e?.status === 403 || e?.status === 404
            ? 'Você não tem permissão para desativar membros.'
            : 'Não foi possível desativar o acesso.',
        ),
    });
  }
}
