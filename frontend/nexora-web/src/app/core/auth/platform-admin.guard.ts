import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from './auth.service';

export const platformAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthService); const router = inject(Router);
  if (auth.isPlatformAdmin()) return true;
  if (auth.isAuthenticated()) return router.createUrlTree(['/403']);
  return auth.refresh().pipe(map(ok => (ok && auth.isPlatformAdmin() ? true : router.createUrlTree([ok ? '/403' : '/login']))));
};
