import { Component, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/auth/auth.service';
import { APP_CONFIG } from '../../core/config/app-config';
import { NxAvatar, NxBadge, NxCard, NxPageHeader } from '../../shared/ui';

interface CurrentUser {
  id: string;
  email: string;
  permissions: string[];
}

@Component({
  selector: 'app-profile-page',
  imports: [NxPageHeader, NxCard, NxAvatar, NxBadge],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
})
export class ProfilePage {
  readonly auth = inject(AuthService);
  private http = inject(HttpClient);
  private base = inject(APP_CONFIG).apiBaseUrl;
  user = signal<CurrentUser | null>(null);
  // In a tenant session the token carries the tenant-scoped grants; outside one it carries the
  // global identity permissions. Either way the token claims are the effective session permissions.
  readonly permissions = computed(() =>
    this.auth.tenantId()
      ? this.auth.permissions()
      : (this.user()?.permissions ?? this.auth.permissions()),
  );
  readonly initials = computed(() => {
    const label = this.user()?.email ?? this.auth.userLabel();
    const base = label.includes('@') ? label.split('@')[0] : label;
    const p = base.split(/[.\s_-]+/).filter(Boolean);
    return ((p[0]?.[0] ?? 'N') + (p[1]?.[0] ?? p[0]?.[1] ?? 'X')).toUpperCase();
  });
  readonly contextLabel = computed(() =>
    this.auth.isPlatformAdmin()
      ? 'Administrador da plataforma'
      : this.auth.tenantId()
        ? 'Operação · empresa ativa'
        : 'Sem empresa selecionada',
  );
  constructor() {
    // GET /identity/me requires the `identity.profile` permission, which now survives the tenant
    // permission swap, so it is reachable in both plain and tenant sessions. Token claims remain
    // the fallback if the call fails.
    this.http.get<CurrentUser>(`${this.base}/identity/me`).subscribe({
      next: (x) => this.user.set(x),
      error: () => {
        /* token claims are the fallback */
      },
    });
  }
}
