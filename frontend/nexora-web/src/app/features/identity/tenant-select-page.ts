import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService, UserTenant } from '../../core/auth/auth.service';
import { NxButton, NxCard, NxEmptyState } from '../../shared/ui';

/**
 * Company picker shown after login when the user belongs to more than one tenant.
 * Selecting a company re-issues the session server-side against the user's real
 * memberships (POST /t/{slug}/session) — the slug is only a lookup key, never authority.
 */
@Component({
  selector: 'app-tenant-select-page',
  imports: [NxButton, NxCard, NxEmptyState],
  templateUrl: './tenant-select-page.html',
})
export class TenantSelectPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly tenants = signal<UserTenant[]>([]);
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly busySlug = signal<string | null>(null);

  constructor() {
    this.auth.listMyTenants().subscribe({
      next: (tenants) => {
        if (tenants.length === 0) {
          void this.router.navigateByUrl('/');
          return;
        }
        if (tenants.length === 1) {
          this.open(tenants[0].slug);
          return;
        }
        this.tenants.set(tenants);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  open(slug: string): void {
    this.busySlug.set(slug);
    this.auth.selectTenant(slug).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: () => {
        this.busySlug.set(null);
        this.state.set('error');
      },
    });
  }

  createCompany(): void {
    void this.router.navigateByUrl('/onboarding');
  }

  retry(): void {
    void this.router.navigateByUrl('/');
  }
}
