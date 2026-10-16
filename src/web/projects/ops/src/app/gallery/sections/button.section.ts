import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsButtonDirective, ScsButtonSize, ScsButtonType, ScsIconComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/** The Button page (8:37): 108 variants, the four arrangements of words and icons, a working button. */
@Component({
  selector: 'app-gallery-button-section',
  imports: [ScsButtonDirective, ScsIconComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective, GalleryPartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-md);
      align-items: center;
    }
    .said {
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
  `,
  template: `
    <app-gallery-component page="Button" set="Button">
      <ng-template appGallerySpecimen let-values>
        <button
          type="button"
          [scsButton]="type(values['Type'])"
          [size]="size(values['Size'])"
          [disabled]="values['State'] === 'Disabled'"
          [busy]="values['State'] === 'Loading'"
        >
          @if (values['State'] !== 'Loading') {
            {{ word(values['Type']) }}
          }
        </button>
      </ng-template>
      <ng-template appGalleryLive>
        <div class="row">
          <button type="button" scsButton size="md" [busy]="busy()" (click)="press()">{{ words().Primary }}</button>
          <button type="button" scsButton="secondary" size="md">{{ words().Secondary }}</button>
          <button type="button" scsButton="destructive" size="md">{{ words().Destructive }}</button>
          <button type="button" scsButton="ghost" size="sm" [label]="words().add"><scs-icon name="plus" [size]="16" /></button>
          <span class="said" role="status">{{ busy() ? words().working : '' }}</span>
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Leading Icon" node="32:3" testId="specimen-button/parts/leading-icon">
      <button type="button" scsButton size="md"><scs-icon name="plus" [size]="16" />{{ words().addClinic }}</button>
    </app-gallery-part>
    <app-gallery-part name="Trailing Icon" node="32:9" testId="specimen-button/parts/trailing-icon">
      <button type="button" scsButton size="md">{{ words().next }}<scs-icon name="chevron-down" [size]="12" /></button>
    </app-gallery-part>
    <app-gallery-part name="Text Only" node="32:14" testId="specimen-button/parts/text-only">
      <button type="button" scsButton size="md">{{ words().addClinic }}</button>
    </app-gallery-part>
    <app-gallery-part name="Icon Only" node="32:19" testId="specimen-button/parts/icon-only">
      <button type="button" scsButton size="md" [label]="words().add"><scs-icon name="plus" [size]="16" /></button>
    </app-gallery-part>
  `,
})
export class ButtonSection {
  private readonly state = inject(GalleryState);

  /** The design's own words for each type, in the page's language (Button and its Arabic twin 316:470). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          Primary: 'زر أساسي',
          Secondary: 'زر ثانوي',
          Tertiary: 'زر ثالثي',
          Ghost: 'زر شفاف',
          Link: 'رابط',
          Destructive: 'حذف',
          addClinic: 'إضافة عيادة',
          next: 'التالي',
          add: 'إضافة',
          working: 'جارٍ التنفيذ…',
        }
      : {
          Primary: 'Primary Button',
          Secondary: 'Secondary Button',
          Tertiary: 'Tertiary Button',
          Ghost: 'Ghost Button',
          Link: 'Link Button',
          Destructive: 'Destructive Button',
          addClinic: 'Add Clinic',
          next: 'Next',
          add: 'Add',
          working: 'Working…',
        },
  );

  protected word(type: string): string {
    return (this.words() as Record<string, string>)[type];
  }

  protected readonly busy = signal(false);

  protected type(value: string): ScsButtonType {
    return value.toLowerCase() as ScsButtonType;
  }

  protected size(value: string): ScsButtonSize {
    return ({ Small: 'sm', Medium: 'md', Large: 'lg' } as const)[value as 'Small' | 'Medium' | 'Large'];
  }

  /** The working example shows busy for a moment, as a real action would. */
  protected press(): void {
    this.busy.set(true);
    setTimeout(() => this.busy.set(false), 1500);
  }
}
