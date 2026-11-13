// Role: Checks server session identity before entering ERP routes; API authorization remains authoritative.
import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from './auth.service';
import { safeErpReturnUrl } from './auth-policy';

export const erpGuard: CanActivateFn & CanActivateChildFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const signIn = () => router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: safeErpReturnUrl(state.url) } });
  return auth.loadSession().pipe(
    map(identity => identity?.accountType === 'erp' ? true : identity ? router.createUrlTree(['/access-denied']) : signIn()),
    catchError(() => of(router.createUrlTree(['/sign-in'], { queryParams: { sessionError: '1' } })))
  );
};
