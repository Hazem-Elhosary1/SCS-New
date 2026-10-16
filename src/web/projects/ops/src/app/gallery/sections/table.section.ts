import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsAvatarComponent, ScsBadgeComponent, ScsBadgeIntent, ScsCheckboxComponent, ScsIconComponent, ScsTableDirective } from 'ui';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

interface Clinic {
  readonly key: string;
  readonly name: string;
  readonly sub: string;
  readonly status: string;
  readonly intent: ScsBadgeIntent;
  readonly mrr: string;
}

/**
 * The Table page (273:2): a row by default, under the pointer and chosen; the composed table of
 * 273:55; working: a table whose rows are chosen with their boxes.
 */
@Component({
  selector: 'app-gallery-table-section',
  imports: [
    NgTemplateOutlet,
    ScsTableDirective,
    ScsCheckboxComponent,
    ScsAvatarComponent,
    ScsBadgeComponent,
    ScsIconComponent,
    GalleryComponentComponent,
    GallerySpecimenDirective,
    GalleryLiveDirective,
    GalleryPartComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .rows {
      --gallery-cell: 100%;
    }
    /* A wide table scrolls inside its frame on a phone; the frame never widens the page. */
    .frame {
      inline-size: 100%;
      overflow-x: auto;
      contain: inline-size;
      border: 1px solid var(--scs-color-border-control, var(--scs-color-border-default));
      border-radius: var(--scs-table-frame-radius, 12px);
      background: var(--scs-color-bg-surface);
    }
    .bare {
      inline-size: 100%;
      overflow-x: auto;
      contain: inline-size;
    }
    table {
      min-inline-size: 760px;
    }
    .who {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .who strong {
      font-weight: 600;
    }
    .pick {
      inline-size: 20px;
    }
    .more {
      color: var(--scs-color-icon-default);
    }
  `,
  template: `
    <ng-template #row let-clinic let-chosen="chosen" let-toggle="toggle">
      <td class="pick">
        <scs-checkbox labelHidden [label]="words().choose + ' ' + clinic.name" [checked]="chosen" (checkedChange)="toggle?.()" />
      </td>
      <td>
        <span class="who">
          <scs-avatar size="sm" [name]="clinic.name" />
          <span><strong>{{ clinic.name }}</strong><span class="sub">{{ clinic.sub }}</span></span>
        </span>
      </td>
      <td>{{ words().plan }}</td>
      <td>{{ words().users }}</td>
      <td><scs-badge [intent]="clinic.intent">{{ clinic.status }}</scs-badge></td>
      <td data-kind="number"><strong>{{ clinic.mrr }}</strong></td>
      <td data-kind="menu"><scs-icon class="more" name="more" [size]="20" /></td>
    </ng-template>

    <app-gallery-component class="rows" page="Table" set="Table Row">
      <ng-template appGallerySpecimen let-values>
        <div class="bare">
          <table scsTable>
            <tbody>
              <tr [attr.aria-selected]="values['State'] === 'Selected' ? 'true' : null" data-opens="true">
                <ng-container [ngTemplateOutlet]="row" [ngTemplateOutletContext]="{ $implicit: clinics()[0], chosen: values['State'] === 'Selected' }" />
              </tr>
            </tbody>
          </table>
        </div>
      </ng-template>
      <ng-template appGalleryLive>
        <ng-container *ngTemplateOutlet="table; context: { $implicit: true }" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Table — Composed Example" node="273:55" testId="specimen-table/parts/table-example">
      <ng-container *ngTemplateOutlet="table; context: { $implicit: false }" />
    </app-gallery-part>

    <ng-template #table let-working>
      <div class="frame">
        <table scsTable>
          <thead>
            <tr>
              <th class="pick"><span class="sub">{{ words().choose }}</span></th>
              <th>{{ words().clinic }}</th>
              <th>{{ words().planHead }}</th>
              <th>{{ words().usersHead }}</th>
              <th>{{ words().status }}</th>
              <th data-kind="number">{{ words().mrrHead }}</th>
              <th data-kind="menu"></th>
            </tr>
          </thead>
          <tbody>
            @for (clinic of clinics(); track clinic.key) {
              <tr data-opens="true" [attr.aria-selected]="isChosen(clinic.key, working) ? 'true' : null">
                <ng-container
                  *ngTemplateOutlet="row; context: { $implicit: clinic, chosen: isChosen(clinic.key, working), toggle: working ? toggleOf(clinic.key) : null }"
                />
              </tr>
            }
          </tbody>
        </table>
      </div>
    </ng-template>
  `,
})
export class TableSection {
  private readonly state = inject(GalleryState);

  /** Chosen rows: the working table's, or the second row as the page's composed table draws it. */
  protected isChosen(key: string, working: boolean): boolean {
    return working ? this.picked().includes(key) : key === this.clinics()[1]?.key;
  }
  /** The page's words (Table 273:52 and its Arabic twin 309:166; the composed table 273:55). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          choose: 'اختر',
          clinic: 'العيادة',
          planHead: 'الباقة',
          usersHead: 'المستخدمون',
          status: 'الحالة',
          mrrHead: 'الإيراد الشهري',
          plan: 'الاحترافية',
          users: '8 مستخدمين',
          rows: [
            ['عيادة النور', 'مدينة نصر · الباقة الاحترافية', 'نشط', 'success', '499 ج.م'],
            ['مركز النيل للأسنان', 'المعادي · باقة البداية', 'نشط', 'success', '199 ج.م'],
            ['عيادة سيتي كير', 'الزمالك · الباقة الاحترافية', 'قيد الانتظار', 'warning', '499 ج.م'],
            ['مركز طب الأسرة', 'مصر الجديدة · باقة البداية', 'موقوف', 'danger', '199 ج.م'],
          ],
        }
      : {
          choose: 'Choose',
          clinic: 'Clinic',
          planHead: 'Plan',
          usersHead: 'Users',
          status: 'Status',
          mrrHead: 'MRR',
          plan: 'Professional',
          users: '8 users',
          rows: [
            ['Al Noor Clinic', 'Nasr City · Professional plan', 'Active', 'success', 'EGP 499'],
            ['Nile Dental Center', 'Maadi · Starter plan', 'Active', 'success', 'EGP 199'],
            ['City Care Clinic', 'Zamalek · Professional plan', 'Pending', 'warning', 'EGP 499'],
            ['Family Med Center', 'Heliopolis · Starter plan', 'Suspended', 'danger', 'EGP 199'],
          ],
        },
  );

  protected readonly clinics = computed<Clinic[]>(() =>
    this.words().rows.map(([name, sub, status, intent, mrr], index) => ({
      key: String(index),
      name,
      sub,
      status,
      intent: intent as ScsBadgeIntent,
      mrr,
    })),
  );

  protected readonly picked = signal<readonly string[]>(['0']);

  protected toggleOf(key: string): () => void {
    return () => this.picked.update((keys) => (keys.includes(key) ? keys.filter((kept) => kept !== key) : [...keys, key]));
  }
}
