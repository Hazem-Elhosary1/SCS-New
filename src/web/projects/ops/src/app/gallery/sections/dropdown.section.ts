import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsSelectComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Dropdown page (57:2): the trigger's six states and the four lists drawn beside it — default
 * with hover and chosen, several chosen, a search with results and with none; working examples.
 */
@Component({
  selector: 'app-gallery-dropdown-section',
  imports: [ScsSelectComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective, GalleryPartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .field {
      inline-size: 100%;
      max-inline-size: 280px;
    }
    .list {
      inline-size: 280px;
      max-inline-size: 100%;
    }
    .live {
      display: grid;
      gap: var(--scs-spacing-md);
      max-inline-size: 320px;
    }
  `,
  template: `
    <app-gallery-component page="Dropdown" set="Select Trigger">
      <ng-template appGallerySpecimen let-values>
        <scs-select
          class="field"
          required
          [label]="words().label"
          [placeholder]="words().placeholder"
          [options]="branches()"
          [value]="values['State'] === 'Filled' ? 'nasr' : ''"
          [disabled]="values['State'] === 'Disabled'"
          [error]="values['State'] === 'Error' ? words().error : ''"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <scs-select testId="live-branch" required [label]="words().label" [placeholder]="words().placeholder" [options]="branches()" [(value)]="branch" />
          <scs-select
            testId="live-services"
            multiple
            [label]="words().services"
            [placeholder]="words().servicesPlaceholder"
            [options]="services()"
            [(values)]="chosen"
          />
          <scs-select
            testId="live-clinic"
            searchable
            [label]="words().clinic"
            [placeholder]="words().clinicPlaceholder"
            [searchLabel]="words().search"
            [emptyLabel]="words().none"
            [options]="clinics()"
            [(value)]="clinic"
          />
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Default / Hover / Selected" node="58:3" testId="specimen-dropdown/parts/list">
      <scs-select class="list" listInline required [label]="words().label" [options]="drawnList()" value="maadi" />
    </app-gallery-part>
    <app-gallery-part name="Multi Select" node="58:14" testId="specimen-dropdown/parts/multi-select">
      <scs-select class="list" listInline multiple [label]="words().services" [options]="services()" [values]="['general', 'cleaning']" />
    </app-gallery-part>
    <app-gallery-part name="Searchable / Empty" node="58:30" testId="specimen-dropdown/parts/searchable-empty">
      <scs-select
        class="list"
        listInline
        searchable
        [label]="words().clinic"
        [searchLabel]="words().search"
        [emptyLabel]="words().none"
        [options]="[]"
      />
    </app-gallery-part>
    <app-gallery-part name="Searchable / Results" node="2224:70" testId="specimen-dropdown/parts/searchable-results">
      <scs-select class="list" listInline searchable [label]="words().clinic" [searchLabel]="words().search" [emptyLabel]="words().none" [options]="clinics()" [searchText]="words().query" value="new-noor" />
    </app-gallery-part>
  `,
})
export class DropdownSection {
  private readonly state = inject(GalleryState);
  /** The page's words (Select Trigger 57:43 and its Arabic twin 303:40; the lists 58:3 … 2224:70). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          label: 'الفرع',
          placeholder: 'اختر فرعاً',
          error: 'يرجى اختيار فرع',
          services: 'الخدمات',
          servicesPlaceholder: 'اختر الخدمات',
          clinic: 'العيادة',
          clinicPlaceholder: 'ابحث عن عيادة',
          search: 'بحث...',
          none: 'لا توجد نتائج',
          query: 'النور',
          branchNames: ['فرع مدينة نصر', 'فرع وسط البلد', 'فرع المعادي', 'فرع السادس من أكتوبر'],
          serviceNames: ['كشف عام', 'تنظيف الأسنان', 'أشعة', 'متابعة'],
          clinicNames: ['عيادة النور', 'عيادة النور الجديدة', 'عيادة الشفاء'],
        }
      : {
          label: 'Branch',
          placeholder: 'Select a branch',
          error: 'Please select a branch',
          services: 'Services',
          servicesPlaceholder: 'Select services',
          clinic: 'Clinic',
          clinicPlaceholder: 'Search for a clinic',
          search: 'Search...',
          none: 'No results found',
          query: 'noor',
          branchNames: ['Nasr City Branch', 'Downtown Branch', 'Maadi Branch', '6th of October Branch'],
          serviceNames: ['General Consultation', 'Dental Cleaning', 'X-Ray', 'Follow-up'],
          clinicNames: ['Al Noor Clinic', 'New Al Noor Clinic', 'Al Shifa Clinic'],
        },
  );

  protected readonly branches = computed(() =>
    ['nasr', 'downtown', 'maadi', 'october'].map((value, index) => ({ value, label: this.words().branchNames[index] })),
  );
  protected readonly services = computed(() =>
    ['general', 'cleaning', 'xray', 'follow'].map((value, index) => ({ value, label: this.words().serviceNames[index] })),
  );
  protected readonly clinics = computed(() =>
    ['noor', 'new-noor', 'shifa'].map((value, index) => ({ value, label: this.words().clinicNames[index] })),
  );
  /** The page's first list: Maadi chosen, the last branch refused. */
  protected readonly drawnList = computed(() => this.branches().map((branch) => ({ ...branch, disabled: branch.value === 'october' })));

  protected readonly branch = signal('');
  protected readonly chosen = signal<readonly string[]>(['general']);
  protected readonly clinic = signal('');
}
