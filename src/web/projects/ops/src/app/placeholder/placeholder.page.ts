import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PlaceholderPanelComponent } from 'ui';

@Component({
  selector: 'app-placeholder-page',
  imports: [PlaceholderPanelComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '<scs-placeholder-panel appNameKey="app.shell.name" />',
})
export class PlaceholderPage {}
