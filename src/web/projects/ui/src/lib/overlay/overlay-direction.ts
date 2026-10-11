import { Directionality } from '@angular/cdk/bidi';
import { DOCUMENT } from '@angular/common';
import {
  EnvironmentProviders,
  Injectable,
  OnDestroy,
  inject,
  makeEnvironmentProviders,
  provideEnvironmentInitializer,
} from '@angular/core';

/**
 * Keeps everything that opens over the page — menus, the bell's panel, popovers — on the right
 * side when the language is switched without a reload.
 *
 * The CDK reads the page's direction once, when it is first asked, and never again. A language
 * switched afterwards (or remembered and applied a moment after the CDK looked) left it placing
 * panels for the other direction: aligned to the wrong edge of what opened them, and cut off by
 * the window. This follows the `dir` of the page instead: every change is passed to the CDK,
 * panels that are open are told their new direction, and their place is worked out again.
 */
@Injectable({ providedIn: 'root' })
export class OverlayDirection implements OnDestroy {
  private readonly document = inject(DOCUMENT);
  private readonly directionality = inject(Directionality);
  private readonly observer?: MutationObserver;

  constructor() {
    this.follow();

    const view = this.document.defaultView;
    if (view !== null && 'MutationObserver' in view) {
      this.observer = new view.MutationObserver(() => this.follow());
      this.observer.observe(this.document.documentElement, { attributes: true, attributeFilter: ['dir'] });
    }
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  private follow(): void {
    const direction = this.document.documentElement.dir === 'rtl' ? 'rtl' : 'ltr';
    if (this.directionality.value === direction) {
      return;
    }

    this.directionality.valueSignal.set(direction);
    this.directionality.change.emit(direction);

    // Panels that are open carry the direction they were opened with.
    this.document
      .querySelectorAll('.cdk-overlay-container [dir]')
      .forEach((host) => host.setAttribute('dir', direction));

    // Their place depends on the direction: have it worked out again, as on a resize.
    this.document.defaultView?.dispatchEvent(new Event('resize'));
  }
}

/** Add to an app's providers once; every panel the `ui` library opens then follows the language. */
export function provideOverlayDirection(): EnvironmentProviders {
  return makeEnvironmentProviders([provideEnvironmentInitializer(() => void inject(OverlayDirection))]);
}
