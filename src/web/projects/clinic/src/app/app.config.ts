import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideScsCore } from 'core';
import { provideOverlayDirection } from 'ui';
import { routes } from './app.routes';
import { provideSampleSources } from './dev/sample-sources';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideScsCore({ app: 'clinic' }),
    provideRouter(routes),
    // Menus and panels follow the language when it is switched without a reload.
    provideOverlayDirection(),
    // Outside production the frame's sources can be given sample data; see the file.
    provideSampleSources(),
  ],
};
