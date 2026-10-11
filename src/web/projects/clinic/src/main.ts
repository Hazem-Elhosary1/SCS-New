import { bootstrapApplication } from '@angular/platform-browser';
import { renderStartupFailure } from 'core';
import { App } from './app/app';
import { appConfig } from './app/app.config';

bootstrapApplication(App, appConfig).catch((error: unknown) => renderStartupFailure(error));
