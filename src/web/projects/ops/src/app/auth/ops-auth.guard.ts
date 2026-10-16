import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { OpsAuthService } from './ops-auth.service';

/**
 * Inside the console: a signed-in operator only. Anyone else is shown the sign-in screen and
 * comes back to where they were going (spec 010, US1).
 */
export const signedInOnly: CanActivateFn = async (_route, state) => {
  const auth = inject(OpsAuthService);
  const router = inject(Router);
  if (await auth.load()) {
    return true;
  }
  return router.createUrlTree(['/sign-in'], state.url === '/' ? {} : { queryParams: { next: state.url } });
};

/** The screens before sign-in: a signed-in operator has nothing to do there. */
export const signedOutOnly: CanActivateFn = async () => {
  const auth = inject(OpsAuthService);
  // Taken before waiting: after an await there is no injection context to ask in.
  const router = inject(Router);
  return (await auth.load()) ? router.createUrlTree(['/']) : true;
};
