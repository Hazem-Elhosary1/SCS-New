import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { PlaceholderPanelComponent, ScsPageHeaderComponent } from 'ui';

/**
 * The platform-status panel of spec 001, kept for people building the product: is the platform
 * reachable, which version, how dates and money are written, and every screen state.
 */
@Component({
  selector: 'app-platform-page',
  imports: [TranslocoPipe, PlaceholderPanelComponent, ScsPageHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <scs-page-header [title]="'app.nav.platform' | transloco" />
    <scs-placeholder-panel appNameKey="app.shell.name" />
  `,
})
export class PlatformPage {}
