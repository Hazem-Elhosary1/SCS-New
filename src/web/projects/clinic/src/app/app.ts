import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { RuntimeConfigService } from 'core';
import { ScsEnvStripComponent } from 'ui';

/**
 * The root of the console. Outside production a strip across the top of every screen — before
 * sign-in and after it — says the data is not real (spec 006, FR-023).
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TranslocoPipe, ScsEnvStripComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[style.--scs-env-strip]': 'strip ? "32px" : "0px"' },
  styles: `
    :host {
      display: block;
      /* As wide as the widest thing inside: in a window narrower than the console's least
         width the page scrolls sideways, and the strip must reach across all of it. */
      min-inline-size: min-content;
    }
  `,
  template: `
    @if (strip) {
      <scs-env-strip>{{ 'app.shell.environment' | transloco }}</scs-env-strip>
    }
    <router-outlet />
  `,
})
export class App {
  protected readonly strip = inject(RuntimeConfigService).value.environment !== 'production';
}
