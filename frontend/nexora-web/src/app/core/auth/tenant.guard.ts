import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Blocks tenant-scoped routes when the session has no active company (platform admins,
 * users who have not selected a tenant). authGuard runs first and restores the session,
 * so by the time this runs `tenantId()` is authoritative.
 */
export const tenantGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.tenantId() ? true : router.createUrlTree(['/']);
};
