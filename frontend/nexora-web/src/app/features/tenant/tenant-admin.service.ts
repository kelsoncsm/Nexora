import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { APP_CONFIG } from '../../core/config/app-config';

export interface TenantProfile { id: string; name: string; slug: string; timeZoneId: string; isActive: boolean; createdAt: string; }
export interface TenantRole { id: string; name: string; description: string; isSystem: boolean; memberCount: number; permissions: string[]; }
export interface TenantPermissionDescriptor { key: string; module: string; action: string; }

/** A role the current actor is allowed to assign (backend already filters out roles above their authority). */
export interface AssignableRole { id: string; name: string; isSystem: boolean; }
export interface TenantInvitation {
  id: string;
  email: string;
  roleId: string;
  roleName: string;
  /** 'Pending' | 'Accepted' | 'Cancelled' | 'Expired' — server-derived. */
  status: string;
  invitedByEmail: string;
  createdAt: string;
  expiresAt: string;
  acceptedAt: string | null;
  cancelledAt: string | null;
}
export interface CreateInvitationInput { email: string; roleId: string; }

@Injectable({ providedIn: 'root' })
export class TenantAdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(APP_CONFIG).apiBaseUrl}/tenant`;

  profile() { return this.http.get<TenantProfile>(`${this.base}`); }
  updateProfile(name: string, timeZoneId: string) { return this.http.put<TenantProfile>(`${this.base}`, { name, timeZoneId }); }

  permissionCatalog() { return this.http.get<TenantPermissionDescriptor[]>(`${this.base}/permissions`); }
  roles() { return this.http.get<TenantRole[]>(`${this.base}/roles`); }
  createRole(name: string, description: string, permissions: string[]) {
    return this.http.post<TenantRole>(`${this.base}/roles`, { name, description, permissions });
  }
  updateRole(roleId: string, name: string, description: string) {
    return this.http.put<TenantRole>(`${this.base}/roles/${roleId}`, { name, description });
  }
  setRolePermissions(roleId: string, permissions: string[]) {
    return this.http.put<TenantRole>(`${this.base}/roles/${roleId}/permissions`, { permissions });
  }
  assignMemberRole(memberId: string, roleId: string) {
    return this.http.patch<void>(`${this.base}/members/${memberId}/role`, { roleId });
  }

  // ---- Member invitations (tenant.members.* CRUD) --------------------------------------
  /** Roles the current actor can hand out — the picker in "Incluir usuário" uses only these. */
  assignableRoles() { return this.http.get<AssignableRole[]>(`${this.base}/members/assignable-roles`); }
  invitations() { return this.http.get<TenantInvitation[]>(`${this.base}/members/invitations`); }
  createInvitation(input: CreateInvitationInput) {
    return this.http.post<TenantInvitation>(`${this.base}/members/invitations`, input);
  }
  resendInvitation(invitationId: string) {
    return this.http.post<TenantInvitation>(`${this.base}/members/invitations/${invitationId}/resend`, {});
  }
  cancelInvitation(invitationId: string) {
    return this.http.delete<void>(`${this.base}/members/invitations/${invitationId}`);
  }
}
