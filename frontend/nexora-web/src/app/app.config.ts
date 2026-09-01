import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './core/auth/auth.interceptor';
import { providePrimeNG } from 'primeng/config';

import { APP_CONFIG } from './core/config/app-config';
import { NexoraPreset } from './shared/ui/theme/nexora-preset';
import { environment } from '../environments/environment';
import { routes } from './app.routes';

declare global { interface Window { __NEXORA_CONFIG__?: { apiBaseUrl?: string }; } }

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    { provide: APP_CONFIG, useValue: { ...environment, apiBaseUrl: window.__NEXORA_CONFIG__?.apiBaseUrl ?? environment.apiBaseUrl } },
    providePrimeNG({
      theme: {
        preset: NexoraPreset,
        options: {
          cssLayer: {
            name: 'primeng',
            order: 'reset, primeng, nexora'
          }
        }
      }
    })
  ]
};
