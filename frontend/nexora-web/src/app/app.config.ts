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
    // The runtime config.js override is for deployed builds (nginx injects the real value).
    // In dev, environment.development.ts wins so `ng serve` talks straight to localhost:8080.
    { provide: APP_CONFIG, useValue: { ...environment, apiBaseUrl: (environment.production ? window.__NEXORA_CONFIG__?.apiBaseUrl : undefined) ?? environment.apiBaseUrl } },
    providePrimeNG({
      theme: {
        preset: NexoraPreset,
        options: {
          // Nexora é tema claro fixo: desliga o dark mode do PrimeNG (por padrão ele
          // segue prefers-color-scheme do sistema operacional).
          darkModeSelector: false,
          cssLayer: {
            name: 'primeng',
            order: 'reset, primeng, nexora'
          }
        }
      }
    })
  ]
};
