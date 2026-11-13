// Role: Adds cookies and CSRF protection exclusively to the configured API and clears expired sessions.
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, of, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { isApiUrl, safeErpReturnUrl } from './auth-policy';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isApiUrl(request.url, environment.apiBaseUrl)) return next(request);
  const auth = inject(AuthService);
  const router = inject(Router);
  const mutation = !['GET', 'HEAD', 'OPTIONS'].includes(request.method);
  const tokenRequest: Observable<string | null> = mutation ? auth.csrf() : of(null);
  return tokenRequest.pipe(
    switchMap(token => next(request.clone({
      withCredentials: true,
      ...(token ? { setHeaders: { 'X-CSRF-TOKEN': token } } : {})
    }))),
    catchError((error: { status?: number }) => {
      if (error.status === 401) {
        auth.clearSession();
        if (!new URL(request.url, environment.apiBaseUrl).pathname.includes('/api/auth/')) {
          void router.navigate(['/sign-in'], { queryParams: { returnUrl: safeErpReturnUrl(router.url) } });
        }
      }
      return throwError(() => error);
    })
  );
};
