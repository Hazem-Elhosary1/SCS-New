import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsSwitchComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/** The Switch page (35:4): six states and a working switch that is busy while it "saves". */
@Component({
  selector: 'app-gallery-switch-section',
  imports: [ScsSwitchComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-gallery-component page="Switch" set="Switch">
      <ng-template appGallerySpecimen let-values>
        <scs-switch
          [label]="words().label"
          [on]="['On', 'Focus', 'Loading'].includes(values['State'])"
          [disabled]="values['State'] === 'Disabled'"
          [busy]="values['State'] === 'Loading'"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <scs-switch testId="live-switch" [label]="words().label" [on]="on()" [busy]="busy()" (onChange)="save($event)" />
      </ng-template>
    </app-gallery-component>
  `,
})
export class SwitchSection {
  private readonly state = inject(GalleryState);

  /** The page's own words (Switch and its Arabic twin 297:96). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar' ? { label: 'تفعيل الإشعارات' } : { label: 'Enable notifications' },
  );

  protected readonly on = signal(true);
  protected readonly busy = signal(false);

  /** A change is "saved" for a moment, as a real one would be. */
  protected save(on: boolean): void {
    this.on.set(on);
    this.busy.set(true);
    setTimeout(() => this.busy.set(false), 1200);
  }
}
