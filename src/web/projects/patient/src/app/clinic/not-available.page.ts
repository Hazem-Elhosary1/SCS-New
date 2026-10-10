import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsStateComponent } from 'ui';

/** Shown at the bare address of the patient site, which names no clinic. */
@Component({
  selector: 'app-not-available-page',
  imports: [TranslocoPipe, ScsStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="mx-auto max-w-xl p-6" data-testid="clinic-not-available">
      <scs-state state="empty" [message]="'app.clinic.notAvailable' | transloco" />
    </main>
  `,
})
export class NotAvailablePage {}
