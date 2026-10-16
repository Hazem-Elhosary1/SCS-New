import { Routes } from '@angular/router';

/**
 * The design components' gallery (spec 095, `contracts/gallery.md`): in development only. A
 * production build replaces this file with `gallery.routes.prod.ts` (angular.json,
 * `fileReplacements`), so nothing of the gallery is delivered. No guard and no sign-in: it shows
 * example data and calls nothing.
 */
export const galleryRoutes: Routes = [
  {
    path: 'dev/components',
    loadComponent: () => import('./gallery.page').then((file) => file.GalleryPage),
  },
  {
    path: 'dev/components/:page',
    loadComponent: () => import('./gallery.page').then((file) => file.GalleryPage),
  },
];
