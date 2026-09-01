import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { APP_CONFIG } from '../config/app-config';

export function isNexoraApiRequest(requestUrl: string, apiBaseUrl: string, origin = globalThis.location?.origin ?? 'http://localhost'): boolean {
  const request = new URL(requestUrl, origin);
  const api = new URL(apiBaseUrl, origin);
  const prefix = api.pathname.replace(/\/+$/, '');
  return request.origin === api.origin && (request.pathname === prefix || request.pathname.startsWith(`${prefix}/`));
}

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(AuthService).accessToken();
  const config = inject(APP_CONFIG);
  return next(token && isNexoraApiRequest(request.url, config.apiBaseUrl)
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request);
};
