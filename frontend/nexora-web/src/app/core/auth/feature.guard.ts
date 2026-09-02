import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * Blocks a tenant route when the tenant's plan does not include the module (ADR-0019). Pure UX:
 * the backend endpoint filters reject the calls regardless. Runs after authGuard + tenantGuard,
 * so a session is present; it refreshes the effective modules and redirects to /403 when off.
 */
export function featureGuard(code: string): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.tenantId()) return true; // tenantGuard already handles the no-tenant case
    return auth.loadFeatures().pipe(
      map(features => (features[code]?.enabled === true ? true : router.createUrlTree(['/403']))),
    );
  };
}
