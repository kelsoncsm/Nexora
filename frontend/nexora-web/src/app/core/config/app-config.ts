import { InjectionToken } from '@angular/core';

export interface AppConfig {
  readonly production: boolean;
  readonly apiBaseUrl: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
