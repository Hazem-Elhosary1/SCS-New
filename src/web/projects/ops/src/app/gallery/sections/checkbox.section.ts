import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsCheckboxComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryState } from '../gallery-state';

/** The Checkbox page (35:2): seven states and a working group with "select all". */
@Component({
  selector: 'app-gallery-checkbox-section',
  imports: [ScsCheckboxComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-gallery-component page="Checkbox" set="Checkbox">
      <ng-template appGallerySpecimen let-values>
        <scs-checkbox
          [label]="words().label"
          [checked]="values['State'] === 'Checked'"
          [indeterminate]="values['State'] === 'Indeterminate'"
          [disabled]="values['State'] === 'Disabled'"
          [error]="values['State'] === 'Error' ? words().required : ''"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <scs-checkbox
          testId="live-all"
          [label]="words().all"
          [checked]="all()"
          [indeterminate]="some()"
          (checkedChange)="setAll($event)"
        />
        @for (item of items(); track item.name; let index = $index) {
          <scs-checkbox
            [testId]="'live-item-' + index"
            [label]="item.name"
            [checked]="item.on"
            (checkedChange)="toggle(index, $event)"
          />
        }
      </ng-template>
    </app-gallery-component>
  `,
})
export class CheckboxSection {
  private readonly state = inject(GalleryState);

  /** The page's own words (Checkbox and its Arabic twin 297:34). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? { label: 'تذكرني', required: 'هذا الحقل مطلوب', all: 'كل الصلاحيات', items: ['عرض العيادات', 'تعديل بيانات العيادات', 'إيقاف العيادات'] }
      : { label: 'Remember me', required: 'This field is required', all: 'Every permission', items: ['View clinics', 'Edit clinic data', 'Suspend clinics'] },
  );

  private readonly chosen = signal([true, false, false]);
  protected readonly items = computed(() => this.words().items.map((name, index) => ({ name, on: this.chosen()[index] })));
  protected readonly all = computed(() => this.chosen().every(Boolean));
  protected readonly some = computed(() => this.chosen().some(Boolean) && !this.all());

  protected setAll(on: boolean): void {
    this.chosen.set(this.chosen().map(() => on));
  }

  protected toggle(index: number, on: boolean): void {
    this.chosen.update((chosen) => chosen.map((value, at) => (at === index ? on : value)));
  }
}
