import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  EnvironmentProviders,
  ErrorHandler,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { SCS_APP, ScsApp } from './app-token';
import { provideRuntimeConfig } from './config/runtime-config';
import { ScsErrorHandler } from './errors/global-error.handler';
import { apiInterceptor } from './http/api.interceptor';
import { provideScsI18n } from './i18n/i18n.providers';
import { VersionCheckService } from './version/version-check.service';

/**
 * Everything an app needs from `core`: runtime configuration, the platform HTTP layer, the
 * two languages, the reporting of unhandled failures and the check for a newer release.
 */
export function provideScsCore(options: { app: ScsApp }): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: SCS_APP, useValue: options.app },
    { provide: ErrorHandler, useClass: ScsErrorHandler },
    provideHttpClient(withInterceptors([apiInterceptor])),
    provideRuntimeConfig(),
    provideScsI18n(),
    // Not awaited: the app must start even when version.json cannot be read.
    provideAppInitializer(() => void inject(VersionCheckService).start()),
  ]);
}
