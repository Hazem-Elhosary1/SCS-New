import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  ScsAuthFrameComponent,
  ScsButtonDirective,
  ScsCodeInputComponent,
  ScsEnvStripComponent,
  ScsFilterPopoverComponent,
  ScsLanguageToggleComponent,
  ScsListToolbarComponent,
  ScsNoAccessComponent,
  ScsPageHeaderComponent,
  ScsPasswordRulesComponent,
  PlaceholderPanelComponent,
  ScsCopyButtonComponent,
  ScsSortDirection,
  ScsSortHeaderComponent,
  ScsTableDirective,
} from 'ui';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * "Other parts": the parts the console uses that the component pages do not draw, kept as they are
 * (contracts/components.md), each in its states — the code boxes, the password rules, the language
 * toggle, the environment strip, the sign-in frame, "no access", the page header, the list's toolbar
 * and its filter panel, and the placeholder panel.
 */
@Component({
  selector: 'app-gallery-other-parts-section',
  imports: [
    ScsCodeInputComponent,
    ScsPasswordRulesComponent,
    ScsLanguageToggleComponent,
    ScsEnvStripComponent,
    ScsAuthFrameComponent,
    ScsNoAccessComponent,
    ScsPageHeaderComponent,
    ScsListToolbarComponent,
    ScsFilterPopoverComponent,
    PlaceholderPanelComponent,
    ScsButtonDirective,
    ScsSortHeaderComponent,
    ScsCopyButtonComponent,
    ScsTableDirective,
    GalleryPartComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-lg);
      inline-size: 100%;
    }
    /* The address and its copy button on one line, as the detail's row draws them (2330:3095). */
    .row.copy {
      align-items: center;
      gap: var(--scs-spacing-sm);
    }
    .state {
      display: grid;
      gap: 6px;
      min-inline-size: 0;
    }
    .state > span {
      font-size: 12px;
      color: var(--scs-color-text-tertiary);
    }
    .wide {
      inline-size: 100%;
    }
    /* A kept part keeps its own width: on a phone it scrolls inside this box, never the page. */
    .fit {
      inline-size: 100%;
      overflow-x: auto;
      contain: inline-size;
    }
    .frame {
      inline-size: 100%;
      block-size: 640px;
      overflow: auto;
      contain: inline-size;
      border: 1px solid var(--scs-color-border-default);
      border-radius: 12px;
    }
    .stack {
      display: grid;
      gap: var(--scs-spacing-md);
      inline-size: 100%;
    }
  `,
  template: `
    <app-gallery-part [name]="words().code" testId="specimen-other-parts/parts/code-input" [shared]="true">
      <div class="fit"><div class="row">
        @for (one of codes(); track one.name) {
          <div class="state">
            <span>{{ one.name }}</span>
            <scs-code-input [label]="words().code" [value]="one.value" [state]="one.state" />
          </div>
        }
      </div></div>
    </app-gallery-part>

    <app-gallery-part [name]="words().rules" testId="specimen-other-parts/parts/password-rules">
      <div class="row">
        <div class="state"><span>{{ words().typing }}</span><scs-password-rules [rules]="rules()" /></div>
        <div class="state"><span>{{ words().submitted }}</span><scs-password-rules submitted [rules]="rules()" /></div>
      </div>
    </app-gallery-part>

    <app-gallery-part [name]="words().toggle" testId="specimen-other-parts/parts/language-toggle">
      <div class="row">
        <scs-language-toggle current="ar" [label]="words().toggle" />
        <scs-language-toggle current="en" [label]="words().toggle" />
      </div>
    </app-gallery-part>

    <app-gallery-part [name]="words().strip" testId="specimen-other-parts/parts/env-strip">
      <scs-env-strip class="wide">{{ words().stripText }}</scs-env-strip>
    </app-gallery-part>

    <app-gallery-part [name]="words().frame" testId="specimen-other-parts/parts/auth-frame">
      <div class="frame">
        <scs-auth-frame [panelTitle]="words().panelTitle" [panelLine]="words().panelLine">
          <scs-code-input [label]="words().code" />
        </scs-auth-frame>
      </div>
    </app-gallery-part>

    <app-gallery-part [name]="words().noAccess" testId="specimen-other-parts/parts/no-access">
      <div class="fit"><scs-no-access [permissionName]="words().permission" [roleName]="words().role" /></div>
    </app-gallery-part>

    <app-gallery-part [name]="words().header" testId="specimen-other-parts/parts/page-header">
      <div class="fit"><scs-page-header [title]="words().operators" [parent]="words().settings" [note]="words().headerNote">
        <button type="button" scsButton="primary">{{ words().invite }}</button>
      </scs-page-header></div>
    </app-gallery-part>

    <app-gallery-part [name]="words().toolbar" testId="specimen-other-parts/parts/list-toolbar">
      <div class="stack fit">
        <scs-list-toolbar [searchPlaceholder]="words().search" [clearLabel]="words().clear" [filterLabel]="words().filter" [(search)]="query" [(filterOpen)]="filterOpen">
          <scs-filter-popover [heading]="words().filter" [applyLabel]="words().apply" [clearLabel]="words().clear" [closeLabel]="words().close" (closed)="filterOpen.set(false)" (apply)="filterOpen.set(false)" />
        </scs-list-toolbar>
      </div>
    </app-gallery-part>

    <!-- Spec 015: the clinics list's sortable column and the sign-in address's copy button. -->
    <app-gallery-part [name]="words().sort" testId="specimen-other-parts/parts/sort-header">
      <div class="fit">
        <table scsTable>
          <thead><tr>
            <th>{{ words().clinic }}</th>
            <th><button scsSortHeader [direction]="sort()" (sorted)="sort.set($event)">{{ words().ends }}</button></th>
          </tr></thead>
        </table>
      </div>
    </app-gallery-part>

    <app-gallery-part [name]="words().copy" testId="specimen-other-parts/parts/copy-button">
      <div class="row copy">
        <bdi dir="ltr">{{ address }}</bdi>
        <scs-copy-button [value]="address" [label]="words().copy" [copiedText]="words().copied" [failedText]="words().copyFailed" />
      </div>
    </app-gallery-part>

    <app-gallery-part [name]="words().placeholder" testId="specimen-other-parts/parts/placeholder-panel">
      <div class="fit"><scs-placeholder-panel appNameKey="app.shell.name" /></div>
    </app-gallery-part>
  `,
})
export class OtherPartsSection {
  private readonly state = inject(GalleryState);
  protected readonly query = signal('');
  protected readonly filterOpen = signal(false);
  protected readonly sort = signal<ScsSortDirection>('none');
  /** An address, the same in both languages (the clinic's sign-in address of spec 015). */
  protected readonly address = 'https://app.scs-heta.com';

  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          code: 'رمز التحقق',
          rules: 'شروط كلمة المرور',
          typing: 'أثناء الكتابة',
          submitted: 'بعد الإرسال',
          toggle: 'تغيير اللغة',
          strip: 'شريط البيئة',
          stripText: 'بيئة التطوير — ليست المنتج الحقيقي',
          frame: 'إطار تسجيل الدخول',
          panelTitle: 'لوحة تشغيل SCS',
          panelLine: 'إدارة العيادات والاشتراكات والدعم من مكان واحد.',
          noAccess: 'لا صلاحية',
          permission: 'إدارة المشغلين',
          role: 'مسؤول الدعم',
          header: 'رأس الصفحة',
          operators: 'المشغلون',
          settings: 'الإعدادات',
          headerNote: 'من يعمل في لوحة التشغيل، وما يحق لكل منهم.',
          invite: 'دعوة مشغل',
          toolbar: 'شريط القائمة ولوحة التصفية',
          search: 'ابحث بالاسم أو البريد',
          clear: 'مسح',
          filter: 'تصفية',
          apply: 'تطبيق',
          close: 'إغلاق',
          placeholder: 'لوحة المكان المؤقت',
          sort: 'عمود قابل للترتيب',
          clinic: 'العيادة',
          ends: 'ينتهي في',
          copy: 'نسخ عنوان دخول العيادة',
          copied: 'تم نسخ عنوان دخول العيادة.',
          copyFailed: 'تعذر النسخ. حدّد الرابط وانسخه يدويًا.',
          states: ['فارغ', 'مكتمل', 'خطأ', 'مقبول'],
          ruleNames: ['8 أحرف على الأقل', 'حرف كبير وحرف صغير', 'رقم واحد على الأقل', 'رمز واحد على الأقل'],
        }
      : {
          code: 'Verification code',
          rules: 'Password rules',
          typing: 'While typing',
          submitted: 'After submitting',
          toggle: 'Change language',
          strip: 'Environment strip',
          stripText: 'Development environment — not the real product',
          frame: 'Sign-in frame',
          panelTitle: 'SCS Ops Console',
          panelLine: 'Run clinics, subscriptions and support from one place.',
          noAccess: 'No access',
          permission: 'Manage operators',
          role: 'Support admin',
          header: 'Page header',
          operators: 'Operators',
          settings: 'Settings',
          headerNote: 'Who works in the ops console, and what each may do.',
          invite: 'Invite operator',
          toolbar: 'List toolbar and filter panel',
          search: 'Search by name or email',
          clear: 'Clear',
          filter: 'Filter',
          apply: 'Apply',
          close: 'Close',
          placeholder: 'Placeholder panel',
          sort: 'Sortable column',
          clinic: 'Clinic',
          ends: 'Expires in',
          copy: 'Copy the clinic sign-in address',
          copied: 'The clinic sign-in address was copied.',
          copyFailed: 'Copying failed. Select the address and copy it by hand.',
          states: ['Empty', 'Filled', 'Error', 'Accepted'],
          ruleNames: ['At least 8 characters', 'An upper and a lower case letter', 'At least one digit', 'At least one symbol'],
        },
  );

  protected readonly codes = computed(() => {
    const names = this.words().states;
    return [
      { name: names[0], value: '', state: 'idle' as const },
      { name: names[1], value: '482913', state: 'idle' as const },
      { name: names[2], value: '482913', state: 'error' as const },
      { name: names[3], value: '482913', state: 'accepted' as const },
    ];
  });

  protected readonly rules = computed(() =>
    ['length', 'cases', 'digit', 'symbol'].map((key, index) => ({ key, label: this.words().ruleNames[index], met: index < 2 })),
  );
}
