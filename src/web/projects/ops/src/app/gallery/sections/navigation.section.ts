import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsBreadcrumbComponent, ScsButtonDirective, ScsIconComponent, ScsStepperComponent, ScsTableFooterComponent, ScsTabsComponent } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Navigation page (284:2): a tab, a crumb, a page of the paging and a step, each in its states;
 * the folded paging of more than six pages (2111:195); working: tabs, a trail, a table's paging and a
 * stepper to move through.
 */
@Component({
  selector: 'app-gallery-navigation-section',
  imports: [
    ScsTabsComponent,
    ScsBreadcrumbComponent,
    ScsTableFooterComponent,
    ScsStepperComponent,
    ScsIconComponent,
    ScsButtonDirective,
    GalleryComponentComponent,
    GallerySpecimenDirective,
    GalleryLiveDirective,
    GalleryPartComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    app-gallery-component + app-gallery-component {
      display: block;
      margin-block-start: var(--scs-spacing-xl, 32px);
    }
    /* Room above the footer for the pages' menu drawn open. */
    .menu-room {
      padding-block-start: 188px;
      max-inline-size: 640px;
    }
    app-gallery-part {
      display: block;
      margin-block-start: var(--scs-spacing-xl, 32px);
    }
    .live {
      display: grid;
      gap: var(--scs-spacing-md);
      max-inline-size: 640px;
    }
    .row {
      display: flex;
      gap: var(--scs-spacing-sm);
    }
    .note {
      margin: 0 0 var(--scs-spacing-sm);
      color: var(--scs-color-text-tertiary);
      font-size: 12px;
    }
    .folded {
      display: grid;
      gap: var(--scs-spacing-lg);
      inline-size: 100%;
      max-inline-size: 520px;
    }
  `,
  template: `
    <app-gallery-component page="Navigation" set="Tab Item">
      <ng-template appGallerySpecimen let-values>
        <scs-tabs [tabs]="[{ key: 'overview', label: words().overview }]" [active]="values['State'] === 'Active' ? 'overview' : ''" />
      </ng-template>
      <ng-template appGalleryLive>
        <scs-tabs [tabs]="tabs()" [(active)]="tab" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-component page="Navigation" set="Breadcrumb Item">
      <ng-template appGallerySpecimen let-values>
        @if (values['State'] === 'Current') {
          <scs-breadcrumb [crumbs]="[{ label: words().edit }]" />
        } @else {
          <scs-breadcrumb [crumbs]="[{ label: words().clinics, link: '/dev/components/navigation' }]" />
        }
      </ng-template>
      <ng-template appGalleryLive>
        <scs-breadcrumb
          [label]="words().trail"
          [crumbs]="[{ label: words().clinics, link: '/dev/components' }, { label: words().clinic, link: '/dev/components/navigation' }, { label: words().edit }]"
        />
      </ng-template>
    </app-gallery-component>

    <app-gallery-component page="Navigation" set="Pagination Item">
      <ng-template appGallerySpecimen let-values>
        @switch (values['State']) {
          @case ('Active') {
            <button type="button" class="scs-page-item" aria-current="page">1</button>
          }
          @case ('Disabled') {
            <button type="button" class="scs-page-item" disabled>1</button>
          }
          @case ('Ellipsis') {
            <button type="button" class="scs-page-item more" aria-expanded="false" [attr.aria-label]="words().more">…<scs-icon name="chevron-down" [size]="14" /></button>
          }
          @case ('Ellipsis Open') {
            <button type="button" class="scs-page-item more" aria-expanded="true" [attr.aria-label]="words().more">…<scs-icon name="chevron-down" [size]="14" /></button>
          }
          @default {
            <button type="button" class="scs-page-item">1</button>
          }
        }
      </ng-template>
      <ng-template appGalleryLive>
        <scs-table-footer
          [count]="words().count"
          [previousLabel]="words().previous"
          [nextLabel]="words().next"
          [moreLabel]="words().more"
          [pagingLabel]="words().paging"
          [pages]="10"
          [(page)]="page"
        />
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Pagination Menu" [node]="state.language() === 'ar' ? '2104:160' : '2104:140'" testId="specimen-navigation/parts/pagination-menu">
      <div class="menu-room">
        <scs-table-footer
          menuShown
          [count]="words().count"
          [previousLabel]="words().previous"
          [nextLabel]="words().next"
          [moreLabel]="words().more"
          [pagingLabel]="words().paging"
          [pages]="10"
          [page]="2"
        />
      </div>
    </app-gallery-part>

    <app-gallery-component page="Navigation" set="Stepper Step">
      <ng-template appGallerySpecimen let-values>
        <scs-stepper
          [steps]="[{ label: words().plan }]"
          [current]="values['State'] === 'Complete' ? 1 : values['State'] === 'Active' ? 0 : -1"
          [digits]="second"
        />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <scs-stepper [label]="words().steps" [doneLabel]="words().done" [steps]="steps()" [current]="step()" />
          <div class="row">
            <button type="button" scsButton="secondary" size="sm" [disabled]="step() === 0" (click)="step.set(step() - 1)">{{ words().back }}</button>
            <button type="button" scsButton="primary" size="sm" [disabled]="step() === steps().length" (click)="step.set(step() + 1)">{{ words().onward }}</button>
          </div>
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-part
      [name]="state.language() === 'ar' ? 'Example — folded pagination (عربي)' : 'Example — folded pagination'"
      [node]="state.language() === 'ar' ? '2111:285' : '2111:195'"
      testId="specimen-navigation/parts/folded-pagination"
    >
      <div class="folded">
        <div>
          <p class="note">{{ words().onTwo }}</p>
          <scs-table-footer count="" [previousLabel]="words().previous" [nextLabel]="words().next" [moreLabel]="words().more" [pages]="10" [page]="2" />
        </div>
        <div>
          <p class="note">{{ words().onFive }}</p>
          <scs-table-footer count="" [previousLabel]="words().previous" [nextLabel]="words().next" [moreLabel]="words().more" [pages]="10" [page]="5" />
        </div>
      </div>
    </app-gallery-part>
  `,
})
export class NavigationSection {
  protected readonly state = inject(GalleryState);
  /** The page's words (Navigation 284:2 and its Arabic twins 312:62 … 2111:285). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          overview: 'نظرة عامة',
          clinics: 'العيادات',
          clinic: 'عيادة النور',
          edit: 'تعديل',
          trail: 'مسار الصفحة',
          plan: 'الباقة والفواتير',
          more: 'صفحات أخرى',
          previous: 'السابق',
          next: 'التالي',
          paging: 'الصفحات',
          count: '11–20 من 100',
          steps: 'خطوات إضافة عيادة',
          done: 'مكتملة',
          back: 'السابق',
          onward: 'التالي',
          onTwo: 'أكثر من 6 صفحات — الصفحة 2: ثلاث صفحات حول الحالية · «…» · آخر 3',
          onFive: 'الصفحة 5: الأرقام تتحرك مع الحالية (4 5 6)، و«…» يفتح لأعلى ويعرض كل الصفحات المخفية',
          tabs: ['نظرة عامة', 'المشتركون', 'الفواتير'],
          stepNames: ['العيادة', 'الباقة والفواتير', 'المسؤول', 'المراجعة'],
        }
      : {
          overview: 'Overview',
          clinics: 'Clinics',
          clinic: 'Al Noor Clinic',
          edit: 'Edit',
          trail: 'Page trail',
          plan: 'Plan & Billing',
          more: 'More pages',
          previous: 'Previous',
          next: 'Next',
          paging: 'Pages',
          count: '11–20 of 100',
          steps: 'Steps to add a clinic',
          done: 'done',
          back: 'Back',
          onward: 'Next',
          onTwo: 'More than 6 pages — on page 2: three pages around the current one · "…" · last 3',
          onFive: 'On page 5: the numbers travel with the current page (4 5 6); "…" opens upwards with every hidden page',
          tabs: ['Overview', 'Subscribers', 'Invoices'],
          stepNames: ['Clinic', 'Plan & Billing', 'Admin', 'Review'],
        },
  );
  protected readonly tabs = computed(() => this.words().tabs.map((label, index) => ({ key: String(index), label })));
  protected readonly steps = computed(() => this.words().stepNames.map((label) => ({ label })));
  /** The page draws its step as the second one. */
  protected readonly second = () => '2';

  protected readonly tab = signal('0');
  protected readonly page = signal(2);
  protected readonly step = signal(1);
}
