import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { APP_CONFIG } from '../../core/config/app-config';

export interface AcceptInvitationResult {
  tenantId: string;
  tenantSlug: string;
  tenantName: string;
  accountCreated: boolean;
}

/**
 * Accepting a tenant invitation. Anonymous flow: the raw token from the e-mail link is the only
 * credential and travels in the body. The e-mail, tenant and role all come from the invitation
 * server-side — never from here.
 */
@Injectable({ providedIn: 'root' })
export class InvitationAcceptService {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(APP_CONFIG).apiBaseUrl}/tenant/members/invitations`;

  accept(token: string, password: string, passwordConfirmation: string | null) {
    return this.http.post<AcceptInvitationResult>(`${this.base}/accept`, {
      token,
      password,
      passwordConfirmation,
    });
  }
}
