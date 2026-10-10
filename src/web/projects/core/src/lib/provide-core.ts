import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideRuntimeConfig } from './config/runtime-config';
import { apiInterceptor } from './http/api.interceptor';

/** Everything an app needs from `core`: runtime configuration and the platform HTTP layer. */
export function provideScsCore(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideHttpClient(withInterceptors([apiInterceptor])),
    provideRuntimeConfig(),
  ]);
}
