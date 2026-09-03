import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { APP_CONFIG } from '../../core/config/app-config';
import { AuthService } from '../../core/auth/auth.service';
import {
  AssignableRole,
  TenantAdminService,
  TenantInvitation,
  TenantRole,
} from '../tenant/tenant-admin.service';
import {
  NxAvatar,
  NxBadge,
  NxButton,
  NxConfirmDialog,
  NxDataTable,
  NxFormField,
  NxModal,
  NxPageHeader,
  NxSearchInput,
  NxToastService,
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
    DatePipe,
    NxPageHeader,
    NxSearchInput,
    NxDataTable,
    NxAvatar,
    NxBadge,
    NxButton,
    NxConfirmDialog,
    NxModal,
    NxFormField,
  ],
  templateUrl: './team-page.html',
  styleUrl: './team-page.scss',
})
export class TeamPage {
  private http = inject(HttpClient);
  private base = inject(APP_CONFIG).apiBaseUrl;
  private auth = inject(AuthService);
  private tenantAdmin = inject(TenantAdminService);
  private toasts = inject(NxToastService);

  members = signal<TenantMember[]>([]);
  roles = signal<TenantRole[]>([]);
  invitations = signal<TenantInvitation[]>([]);
  assignableRoles = signal<AssignableRole[]>([]);
  error = signal('');
  busyId = signal('');
  q = signal('');
  confirming = signal<TenantMember | null>(null);
  cancelling = signal<TenantInvitation | null>(null);

  // Member management CRUD permissions (backend is the authority; these only shape the UI).
  readonly canRead = computed(() => this.has('tenant.members.read'));
  readonly canInvite = computed(() => this.has('tenant.members.create'));
  readonly canUpdateRole = computed(() => this.has('tenant.members.update'));
  readonly canRemove = computed(() => this.has('tenant.members.delete'));

  // "Incluir usuário" modal state.
  inviteOpen = signal(false);
  inviteEmail = signal('');
  inviteRoleId = signal('');
  inviteBusy = signal(false);
  inviteError = signal('');

  readonly pendingInvitations = computed(() =>
    this.invitations().filter((i) => i.status === 'Pending' || i.status === 'Expired'),
  );
  readonly visible = computed(() => {
    const term = this.q().trim().toLowerCase();
    const list = this.members();
    return term ? list.filter((m) => m.email.toLowerCase().includes(term)) : list;
  });

  constructor() {
    this.load();
  }

  private has(permission: string) {
    return this.auth.permissions().includes(permission);
  }

  load() {
    this.http.get<TenantMember[]>(`${this.base}/tenant/members`).subscribe({
      next: (x) => {
        this.members.set(x);
        this.error.set('');
      },
      error: (e) =>
        this.error.set(
          e?.status === 403
            ? 'Você não tem permissão para ver a equipe desta empresa.'
            : 'Não foi possível carregar a equipe.',
        ),
    });
    this.loadInvitations();
    if (this.canUpdateRole()) {
      this.tenantAdmin.roles().subscribe({
        next: (r) => this.roles.set(r),
        error: () => {
          /* role dropdown simply stays hidden */
        },
      });
    }
    if (this.canInvite()) {
      this.tenantAdmin.assignableRoles().subscribe({
        next: (r) => this.assignableRoles.set(r),
        error: () => this.assignableRoles.set([]),
      });
    }
  }

  loadInvitations() {
    if (!this.canRead()) return;
    this.tenantAdmin.invitations().subscribe({
      next: (x) => this.invitations.set(x),
      error: () => this.invitations.set([]),
    });
  }

  assign(m: TenantMember, roleId: string) {
    if (!roleId || roleId === m.roleId || this.busyId()) return;
    this.busyId.set(m.id);
    this.tenantAdmin.assignMemberRole(m.id, roleId).subscribe({
      next: () => {
        this.busyId.set('');
        this.toasts.success(`Papel de ${m.email} atualizado.`);
        this.load();
      },
      error: (e) => {
        this.busyId.set('');
        this.toasts.error(
          e?.status === 403
            ? 'Você não pode atribuir um papel com permissões acima das suas.'
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
      next: () => {
        this.toasts.success(`${m.email} não tem mais acesso a esta empresa.`);
        this.load();
      },
      error: (e) =>
        this.toasts.error(
          e?.status === 403 || e?.status === 404
            ? 'Você não tem permissão para desativar membros.'
            : 'Não foi possível desativar o acesso.',
        ),
    });
  }

  // ---- Invitations -------------------------------------------------------------------

  openInvite() {
    this.inviteEmail.set('');
    this.inviteRoleId.set(this.assignableRoles()[0]?.id ?? '');
    this.inviteError.set('');
    this.inviteOpen.set(true);
  }

  submitInvite() {
    const email = this.inviteEmail().trim();
    const roleId = this.inviteRoleId();
    if (this.inviteBusy() || !email || !roleId) return;
    this.inviteBusy.set(true);
    this.inviteError.set('');
    this.tenantAdmin.createInvitation({ email, roleId }).subscribe({
      next: () => {
        this.inviteBusy.set(false);
        this.inviteOpen.set(false);
        this.toasts.success(`Convite enviado para ${email}.`);
        this.loadInvitations();
      },
      error: (e) => {
        this.inviteBusy.set(false);
        this.inviteError.set(
          e?.status === 409
            ? 'Já existe um convite pendente ou um membro com esse e-mail.'
            : e?.status === 403
              ? 'Você não pode convidar para um papel com permissões acima das suas.'
              : e?.status === 400
                ? (e?.error?.detail ?? e?.error?.title ?? 'Verifique o e-mail e o papel informados.')
                : 'Não foi possível enviar o convite.',
        );
      },
    });
  }

  resend(inv: TenantInvitation) {
    if (this.busyId()) return;
    this.busyId.set(inv.id);
    this.tenantAdmin.resendInvitation(inv.id).subscribe({
      next: () => {
        this.busyId.set('');
        this.toasts.success(`Convite reenviado para ${inv.email}. O link anterior deixou de valer.`);
        this.loadInvitations();
      },
      error: (e) => {
        this.busyId.set('');
        this.toasts.error(
          e?.status === 403
            ? 'Você não pode reenviar este convite.'
            : e?.status === 404
              ? 'Convite não encontrado.'
              : 'Não foi possível reenviar o convite.',
        );
        this.loadInvitations();
      },
    });
  }

  cancel(inv: TenantInvitation) {
    this.cancelling.set(null);
    this.busyId.set(inv.id);
    this.tenantAdmin.cancelInvitation(inv.id).subscribe({
      next: () => {
        this.busyId.set('');
        this.toasts.success(`Convite de ${inv.email} cancelado.`);
        this.loadInvitations();
      },
      error: (e) => {
        this.busyId.set('');
        this.toasts.error(
          e?.status === 403 || e?.status === 404
            ? 'Você não tem permissão para cancelar este convite.'
            : 'Não foi possível cancelar o convite.',
        );
        this.loadInvitations();
      },
    });
  }
}
