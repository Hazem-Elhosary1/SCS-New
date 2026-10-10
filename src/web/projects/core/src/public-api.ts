/*
 * Public API of the `core` library: what every front end shares that is not visual.
 */
export * from './lib/app-token';
export * from './lib/clinic/clinic.service';
export * from './lib/config/runtime-config';
export * from './lib/http/api.interceptor';
export * from './lib/http/problem';
export * from './lib/http/trace';
export * from './lib/platform/platform-status.service';
export * from './lib/provide-core';
export * from './lib/startup/startup-failure';
export * from './lib/version/version-check.service';
export * from './lib/errors/global-error.handler';
export * from './lib/format/format';
export * from './lib/i18n/i18n.providers';
export * from './lib/i18n/language.service';
export * from './lib/network/online.service';
export * from './lib/auth/auth.service';
export * from './lib/auth/support.service';
export * from './lib/auth/password-rules';
