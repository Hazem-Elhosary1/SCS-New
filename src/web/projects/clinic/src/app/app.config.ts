import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideScsCore } from 'core';
import { routes } from './app.routes';
import { provideSampleSources } from './dev/sample-sources';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideScsCore({ app: 'clinic' }),
    provideRouter(routes),
    // Outside production the frame's sources can be given sample data; see the file.
    provideSampleSources(),
  ],
};
