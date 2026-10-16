import { CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsIconComponent, ScsIconName, ScsMenuDirective, ScsMenuItemDirective } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

interface MenuRow {
  readonly icon: ScsIconName;
  readonly label: string;
  readonly danger?: boolean;
}

interface RowMenu {
  readonly name: string;
  readonly note: string;
  readonly rows: readonly MenuRow[];
}

/** The menu icons the page draws (2200:86), in its order. */
const MENU_ICONS = [
  'more',
  'view',
  'edit',
  'renew',
  'suspend',
  'delete',
  'disable',
  'enable',
  'copy',
  'send',
  'invoice',
  'print',
  'void',
  'refund',
  'configure',
  'key',
  'unlock',
  'audit',
] as const;

/**
 * The Menu page (368:2): a choice by default, under the pointer, destructive and destructive under the
 * pointer; the panel (368:26); the 16-pixel menu icons (2200:86); the row-actions menus of every table
 * and their button (2201:86); working: a row's button that opens its menu.
 */
@Component({
  selector: 'app-gallery-menu-section',
  imports: [CdkMenuTrigger, ScsMenuDirective, ScsMenuItemDirective, ScsIconComponent, GalleryComponentComponent, GallerySpecimenDirective, GalleryLiveDirective, GalleryPartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .still {
      margin: 0;
      inline-size: 100%;
      max-inline-size: 220px;
      box-sizing: border-box;
    }
    .icons {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-md) var(--scs-spacing-lg);
    }
    .icon {
      display: grid;
      justify-items: center;
      gap: 6px;
      font-size: 11px;
      color: var(--scs-color-text-tertiary);
    }
    .icon scs-icon {
      color: var(--scs-color-text-primary);
    }
    .states {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-lg);
      margin-block-end: var(--scs-spacing-lg);
    }
    .state {
      display: grid;
      justify-items: center;
      gap: 6px;
      font-size: 11px;
      color: var(--scs-color-text-tertiary);
    }
    .whole {
      inline-size: 100%;
    }
    .menus {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(min(240px, 100%), 1fr));
      gap: var(--scs-spacing-lg);
      inline-size: 100%;
    }
    .menu-name {
      margin: 0;
      font-size: 13px;
      font-weight: 600;
    }
    .menu-note {
      margin: 2px 0 8px;
      font-size: 11px;
      color: var(--scs-color-text-tertiary);
    }
    .live {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-md);
    }
  `,
  template: `
    <app-gallery-component page="Menu" set="Menu Item">
      <ng-template appGallerySpecimen let-values>
        <div class="scs-menu still">
          @if (values['State'] === 'Danger' || values['State'] === 'Danger Hover') {
            <button type="button" class="scs-menu-item" data-danger="true"><scs-icon name="menu-delete" [size]="16" />{{ words().delete }}</button>
          } @else {
            <button type="button" class="scs-menu-item"><scs-icon name="menu-edit" [size]="16" />{{ words().edit }}</button>
          }
        </div>
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <span>{{ menus()[0].name }}</span>
          <button type="button" class="scs-row-menu" data-testid="live-row-menu" [attr.aria-label]="words().actionsFor" [cdkMenuTriggerFor]="liveMenu">
            <scs-icon name="menu-more" [size]="16" />
          </button>
          <ng-template #liveMenu>
            <div scsMenu>
              @for (row of menus()[0].rows; track row.label) {
                @if (row.danger) {
                  <div class="scs-menu-separator" role="separator"></div>
                }
                <button type="button" scsMenuItem [danger]="!!row.danger" (chosen)="chosen.set(row.label)">
                  <scs-icon [name]="row.icon" [size]="16" />{{ row.label }}
                </button>
              }
              <button type="button" scsMenuItem disabled>
                <scs-icon name="menu-audit" [size]="16" />
                <span class="words">{{ words().audit }}<span class="reason">{{ words().auditReason }}</span></span>
              </button>
            </div>
          </ng-template>
          @if (chosen()) {
            <span data-testid="live-menu-chosen">{{ chosen() }}</span>
          }
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Composed Example" node="368:26" testId="specimen-menu/parts/menu-panel">
      <div class="scs-menu still" role="menu" [attr.aria-label]="words().actionsFor">
        <button type="button" class="scs-menu-item" role="menuitem"><scs-icon name="menu-view" [size]="16" />{{ words().view }}</button>
        <button type="button" class="scs-menu-item" role="menuitem"><scs-icon name="menu-edit" [size]="16" />{{ words().edit }}</button>
        <button type="button" class="scs-menu-item" role="menuitem"><scs-icon name="menu-copy" [size]="16" />{{ words().copy }}</button>
        <div class="scs-menu-separator" role="separator"></div>
        <button type="button" class="scs-menu-item" role="menuitem" data-danger="true"><scs-icon name="menu-delete" [size]="16" />{{ words().delete }}</button>
      </div>
    </app-gallery-part>

    <app-gallery-part name="Menu Icons — 16px line" node="2200:86" testId="specimen-menu/parts/menu-icons">
      <div class="icons">
        @for (icon of icons; track icon.name) {
          <span class="icon"><scs-icon [name]="icon.icon" [size]="16" />{{ icon.name }}</span>
        }
      </div>
    </app-gallery-part>

    <app-gallery-part name="Row Actions Menus — every table (kebab)" node="2201:86" testId="specimen-menu/parts/row-menus">
      <div class="whole">
        <div class="states">
          @for (state of kebabStates(); track state.label) {
            <span class="state">
              <span [attr.data-scs-state]="state.still"><button type="button" class="scs-row-menu" tabindex="-1" [attr.aria-label]="words().actionsFor" [attr.aria-expanded]="state.open ? 'true' : null"><scs-icon name="menu-more" [size]="16" /></button></span>
              {{ state.label }}
            </span>
          }
        </div>
        <div class="menus">
          @for (menu of menus(); track menu.name) {
            <div>
              <p class="menu-name">{{ menu.name }}</p>
              <p class="menu-note">{{ menu.note }}</p>
              <div class="scs-menu still" role="menu" [attr.aria-label]="menu.name">
                @for (row of menu.rows; track row.label) {
                  @if (row.danger) {
                    <div class="scs-menu-separator" role="separator"></div>
                  }
                  <button type="button" class="scs-menu-item" role="menuitem" tabindex="-1" [attr.data-danger]="row.danger ? 'true' : null">
                    <scs-icon [name]="row.icon" [size]="16" />{{ row.label }}
                  </button>
                }
              </div>
            </div>
          }
        </div>
      </div>
    </app-gallery-part>
  `,
})
export class MenuSection {
  private readonly state = inject(GalleryState);
  protected readonly icons = MENU_ICONS.map((name) => ({ name, icon: `menu-${name}` as ScsIconName }));
  protected readonly chosen = signal('');

  /** The page's words (Menu 368:23 and its Arabic twin 370:38). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          edit: 'تعديل',
          delete: 'حذف',
          view: 'عرض التفاصيل',
          copy: 'نسخ',
          actionsFor: 'إجراءات عيادة النور',
          audit: 'سجل التدقيق',
          auditReason: 'يحتاج صلاحية عرض التقارير',
        }
      : {
          edit: 'Edit',
          delete: 'Delete',
          view: 'View Details',
          copy: 'Copy',
          actionsFor: 'Actions for Al Noor Clinic',
          audit: 'Audit log',
          auditReason: 'Needs reports.view',
        },
  );

  protected readonly kebabStates = computed(() =>
    this.state.language() === 'ar'
      ? [
          { label: 'افتراضي', still: null, open: false },
          { label: 'تحويم / مفتوح', still: 'hover', open: true },
          { label: 'تركيز لوحة المفاتيح', still: 'focus', open: false },
        ]
      : [
          { label: 'Default', still: null, open: false },
          { label: 'Hover / Open', still: 'hover', open: true },
          { label: 'Keyboard focus', still: 'focus', open: false },
        ],
  );

  /** The ten menus of 2201:86, with their notes. */
  protected readonly menus = computed<RowMenu[]>(() =>
    this.state.language() === 'ar'
      ? [
          { name: 'قائمة العيادات', note: 'التعديل والتجديد والإيقاف تحتاج صلاحية تعديل العيادات، والإيقاف لا يظهر لعيادة متأخرة.', rows: [{ icon: 'menu-view', label: 'عرض التفاصيل' }, { icon: 'menu-edit', label: 'تعديل العيادة' }, { icon: 'menu-renew', label: 'تجديد الاشتراك' }, { icon: 'menu-suspend', label: 'إيقاف مؤقت', danger: true }] },
          { name: 'قائمة التذاكر', note: 'كل تغيير يتم من صفحة التذكرة.', rows: [{ icon: 'menu-view', label: 'عرض التفاصيل' }] },
          { name: 'قائمة المشغلين', note: 'التعديل يحتاج صلاحية إدارة المشغلين.', rows: [{ icon: 'menu-view', label: 'عرض التفاصيل' }, { icon: 'menu-edit', label: 'تعديل المشغل' }] },
          { name: 'الأدوار — المخصصة', note: 'أدوار النظام بلا قائمة.', rows: [{ icon: 'menu-edit', label: 'تعديل الدور' }, { icon: 'menu-delete', label: 'حذف الدور', danger: true }] },
          { name: 'الباقات — باقة مفعّلة', note: 'التعديل والتعطيل يحتاجان صلاحية إدارة الباقات.', rows: [{ icon: 'menu-view', label: 'عرض تفاصيل الباقة' }, { icon: 'menu-edit', label: 'تعديل الباقة' }, { icon: 'menu-disable', label: 'تعطيل الباقة', danger: true }] },
          { name: 'الباقات — باقة معطّلة', note: 'التفعيل آمن، فيبقى مع باقي الإجراءات.', rows: [{ icon: 'menu-view', label: 'عرض تفاصيل الباقة' }, { icon: 'menu-edit', label: 'تعديل الباقة' }, { icon: 'menu-enable', label: 'تفعيل الباقة' }] },
          { name: 'الفواتير — فاتورة مفتوحة', note: 'الرابط والحالة يحتاجان فوري وصلاحية المدفوعات، والتفاصيل تحتاج عرض التقارير.', rows: [{ icon: 'menu-copy', label: 'نسخ رابط الدفع' }, { icon: 'menu-send', label: 'إرسال رابط الدفع للعيادة' }, { icon: 'menu-renew', label: 'تحديث حالة الدفع من فوري' }, { icon: 'menu-invoice', label: 'عرض تفاصيل الفاتورة' }, { icon: 'menu-void', label: 'إلغاء الفاتورة', danger: true }] },
          { name: 'الفواتير — فاتورة مدفوعة', note: 'الاسترداد متاح ما دام هناك مبلغ يمكن رده.', rows: [{ icon: 'menu-invoice', label: 'عرض تفاصيل الفاتورة' }, { icon: 'menu-print', label: 'طباعة الفاتورة' }, { icon: 'menu-refund', label: 'استرداد مبلغ', danger: true }] },
          { name: 'الإعدادات — التكاملات', note: 'الإعداد والاختبار يحتاجان تعديل الإعدادات، والاختبار لقنوات الرسائل فقط.', rows: [{ icon: 'menu-view', label: 'عرض التفاصيل' }, { icon: 'menu-configure', label: 'إعداد' }, { icon: 'menu-send', label: 'إرسال رسالة تجريبية' }] },
          { name: 'مستخدمو العيادة', note: 'المعروض: حساب مقفول. الدعوة المعلّقة: إعادة الإرسال فقط. المعطّل: تفعيل فقط.', rows: [{ icon: 'menu-key', label: 'إرسال رابط كلمة السر' }, { icon: 'menu-unlock', label: 'فك القفل' }, { icon: 'menu-disable', label: 'تعطيل', danger: true }] },
        ]
      : [
          { name: 'Clinics list', note: "Edit, Renew and Suspend need clinics.edit; Suspend isn't offered for an overdue clinic.", rows: [{ icon: 'menu-view', label: 'View Details' }, { icon: 'menu-edit', label: 'Edit Clinic' }, { icon: 'menu-renew', label: 'Renew Subscription' }, { icon: 'menu-suspend', label: 'Suspend', danger: true }] },
          { name: 'Tickets list', note: 'Every change happens on the ticket page.', rows: [{ icon: 'menu-view', label: 'View Details' }] },
          { name: 'Operators list', note: 'Edit needs operators.manage.', rows: [{ icon: 'menu-view', label: 'View Details' }, { icon: 'menu-edit', label: 'Edit Operator' }] },
          { name: 'Roles — custom roles', note: 'System roles have no menu.', rows: [{ icon: 'menu-edit', label: 'Edit Role' }, { icon: 'menu-delete', label: 'Delete Role', danger: true }] },
          { name: 'Plans — active plan', note: 'Edit and Disable need billing.managePlans.', rows: [{ icon: 'menu-view', label: 'View Plan Details' }, { icon: 'menu-edit', label: 'Edit Plan' }, { icon: 'menu-disable', label: 'Disable Plan', danger: true }] },
          { name: 'Plans — disabled plan', note: 'Enabling is safe, so it stays with the others.', rows: [{ icon: 'menu-view', label: 'View Plan Details' }, { icon: 'menu-edit', label: 'Edit Plan' }, { icon: 'menu-enable', label: 'Enable Plan' }] },
          { name: 'Invoices — open invoice', note: 'Link and status items need Fawry and clinics.payments; details need reports.view.', rows: [{ icon: 'menu-copy', label: 'Copy payment link' }, { icon: 'menu-send', label: 'Send payment link to clinic' }, { icon: 'menu-renew', label: 'Refresh status from Fawry' }, { icon: 'menu-invoice', label: 'View invoice details' }, { icon: 'menu-void', label: 'Void invoice', danger: true }] },
          { name: 'Invoices — paid invoice', note: 'Refund while money is left to give back.', rows: [{ icon: 'menu-invoice', label: 'View invoice details' }, { icon: 'menu-print', label: 'Print invoice' }, { icon: 'menu-refund', label: 'Refund payment', danger: true }] },
          { name: 'Settings — Integrations', note: 'Configure and Test need settings.edit; Test is for message channels.', rows: [{ icon: 'menu-view', label: 'View Details' }, { icon: 'menu-configure', label: 'Configure' }, { icon: 'menu-send', label: 'Send test message' }] },
          { name: 'Clinic users', note: 'Shown: a locked account. Pending invite: Resend only. Disabled: Enable only.', rows: [{ icon: 'menu-key', label: 'Send Password Reset' }, { icon: 'menu-unlock', label: 'Unlock' }, { icon: 'menu-disable', label: 'Disable', danger: true }] },
        ],
  );
}
