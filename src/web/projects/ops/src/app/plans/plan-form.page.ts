import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from 'core';
import {
  ScsBannerComponent,
  ScsBreadcrumbComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsSelectComponent,
  ScsSelectOption,
  ScsSkeletonComponent,
  ScsSwitchComponent,
  ScsTextFieldComponent,
  ScsTextareaComponent,
  ToastService,
} from 'ui';
import { codeOf, fieldErrors } from '../operators/operators.service';
import { FEATURE_GROUPS } from './features.service';
import { planName } from './plan-words';
import {
  GridFeature,
  PLAN_BADGES,
  PlanBadge,
  PlanCodes,
  PlanDetail,
  PlanGrid,
  PlansService,
} from './plans.service';

const NAME_MAX = 100;
const TEXT_MAX = 500;
const AMOUNT = /^\d{1,10}(\.\d{1,2})?$/u;
const AMOUNT_MAX = 9_999_999_999.99;
const LIMIT_MAX = 1_000_000;

type Field =
  | 'nameAr'
  | 'nameEn'
  | 'monthlyEgp'
  | 'annualEgp'
  | 'monthlyUsd'
  | 'annualUsd'
  | 'description'
  | 'note';

/** One row of the grid as the form holds it: switched on or not, and a limit as typed. */
interface GridLine {
  readonly feature: GridFeature;
  readonly on: boolean;
  readonly limit: string;
}

/**
 * «إضافة باقة جديدة» and «تعديل <الاسم>» (spec 013, User Stories 2 and 3; FR-014 to FR-020;
 * frames `1202:70`/`1202:116`, `1214:70`/`1214:159`). One form in two modes, in the clarified
 * order: the names, the prices before tax (EGP, then the reserved USD), the description, the
 * badge, the whole catalogue's grid, and an optional change note. The platform checks the whole
 * save against the catalogue again and refuses what the form let through.
 */
@Component({
  selector: 'app-plan-form-page',
  imports: [
    TranslocoPipe,
    ScsBreadcrumbComponent,
    ScsCardComponent,
    ScsTextFieldComponent,
    ScsSelectComponent,
    ScsSwitchComponent,
    ScsTextareaComponent,
    ScsButtonDirective,
    ScsBannerComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // The frames of the operators' template draw a form's labels small, grey and regular (spec 011).
  host: { class: 'scs-form--template' },
  styles: `
    :host {
      display: block;
    }
    h1 {
      margin: 0;
      font-size: var(--scs-text-title-sm);
      font-weight: 500;
      overflow-wrap: anywhere;
    }
    .note {
      margin: var(--scs-spacing-2) 0 var(--scs-spacing-md);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    scs-banner {
      display: block;
      margin-block-end: var(--scs-spacing-md);
    }
    .stale {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-sm);
    }
    scs-card {
      display: block;
      max-inline-size: 760px;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--scs-spacing-md) var(--scs-spacing-lg);
    }
    .wide {
      grid-column: 1 / -1;
    }
    .line {
      margin: 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
      line-height: 1.6;
    }
    .badge-holder {
      margin: var(--scs-spacing-xs) 0 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-feedback-warning-text);
      line-height: 1.6;
    }
    .features {
      margin-block-start: var(--scs-spacing-lg);
    }
    .features h2 {
      margin: 0;
      font-size: var(--scs-text-body);
      font-weight: 600;
    }
    .group {
      margin: var(--scs-spacing-md) 0 var(--scs-spacing-xs);
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary, var(--scs-color-text-secondary));
    }
    .row {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      min-block-size: 44px;
      border-block-start: 1px solid var(--scs-color-border-subtle);
    }
    .row .what {
      display: flex;
      flex: 1 1 auto;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-sm);
      min-inline-size: 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-primary);
    }
    .row .what bdi {
      overflow-wrap: anywhere;
    }
    .tag {
      display: inline-flex;
      align-items: center;
      block-size: 20px;
      padding-inline: var(--scs-spacing-sm);
      border-radius: 999px;
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-secondary);
      font-size: 11px;
    }
    .tag[data-tone='warning'] {
      background: var(--scs-color-feedback-warning-bg);
      color: var(--scs-color-feedback-warning-text);
    }
    .limit {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-xs);
      flex: none;
    }
    .limit input {
      box-sizing: border-box;
      inline-size: 76px;
      block-size: 32px;
      padding-inline: var(--scs-spacing-sm);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-sm, 6px);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-text-body-sm);
      text-align: end;
    }
    .limit input:disabled {
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-tertiary);
    }
    .limit input[aria-invalid='true'] {
      border-color: var(--scs-color-feedback-danger-border, var(--scs-color-feedback-danger-text));
    }
    .limit input:focus-visible {
      outline: 2px solid var(--scs-color-border-focus, var(--scs-color-brand-default));
      outline-offset: 1px;
    }
    .unit {
      min-inline-size: 64px;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary, var(--scs-color-text-secondary));
    }
    .row-error {
      margin: 0 0 var(--scs-spacing-xs);
      font-size: var(--scs-text-label);
      color: var(--scs-color-feedback-danger-text);
    }
    .note-field {
      margin-block-start: var(--scs-spacing-lg);
    }
    .buttons {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-lg);
    }
    .buttons button {
      min-block-size: 40px;
      padding-block: 0;
      padding-inline: var(--scs-spacing-lg);
      font-size: var(--scs-text-body);
    }
    @media (max-width: 720px) {
      .grid {
        grid-template-columns: minmax(0, 1fr);
      }
      /* A phone's row: the name and the switch on one line, the limit and its unit under them. */
      .row {
        display: grid;
        grid-template-columns: minmax(0, 1fr) auto;
        column-gap: var(--scs-spacing-12);
        padding-block: var(--scs-spacing-xs);
      }
      .row .what {
        grid-column: 1;
        grid-row: 1;
      }
      .row scs-switch {
        grid-column: 2;
        grid-row: 1;
      }
      .row .limit {
        grid-column: 1 / -1;
        grid-row: 2;
        padding-block-end: var(--scs-spacing-xs);
      }
      .buttons {
        flex-direction: column-reverse;
      }
    }
  `,
  template: `
    <scs-breadcrumb
      [label]="'app.plans.plan.form.crumbs' | transloco"
      [crumbs]="[{ label: 'app.plans.plan.section' | transloco, link: '/plans' }, { label: title() }]"
    />
    <h1 data-testid="form-title"><bdi>{{ title() }}</bdi></h1>
    <p class="note" data-testid="form-note">
      {{ (editing ? 'app.plans.plan.form.editNote' : 'app.plans.plan.form.addNote') | transloco }}
    </p>

    @switch (state()) {
      @case ('loading') {
        <scs-card
          ><scs-skeleton
            data-testid="form-loading"
            [rows]="8"
            [label]="'app.plans.plan.form.loading' | transloco"
        /></scs-card>
      }
      @case ('failed') {
        <scs-card>
          <scs-empty-state
            context="error"
            data-testid="form-failed"
            [title]="'app.plans.plan.form.failedTitle' | transloco"
            [text]="'app.plans.plan.form.failedText' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="form-retry" (click)="load()">
              {{ 'app.plans.retry' | transloco }}
            </button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @if (failure(); as words) {
          <scs-banner tone="danger" data-testid="form-problem">
            <span class="stale">
              <span>{{ words | transloco }}</span>
              @if (stale()) {
                <span data-testid="form-stale">{{ 'app.operators.form.changed' | transloco }}</span>
                <button
                  type="button"
                  scsButton="secondary"
                  data-testid="form-reload"
                  (click)="load()"
                >
                  {{ 'app.operators.form.reload' | transloco }}
                </button>
              }
            </span>
          </scs-banner>
        }

        <scs-card>
          <form novalidate data-testid="plan-form" (submit)="$event.preventDefault(); save()">
            <div class="grid">
              <!-- Arabic text runs right to left, and English left to right, whatever the page's direction. -->
              <scs-text-field
                rtl
                testId="field-name-ar"
                required
                [label]="'app.plans.plan.form.nameAr' | transloco"
                [placeholder]="'app.plans.plan.form.nameArHint' | transloco"
                [error]="said('nameAr')"
                [(value)]="nameAr"
              />
              <scs-text-field
                ltr
                testId="field-name-en"
                required
                [label]="'app.plans.plan.form.nameEn' | transloco"
                [placeholder]="'app.plans.plan.form.nameEnHint' | transloco"
                [error]="said('nameEn')"
                [(value)]="nameEn"
              />
              <p class="line wide" data-testid="tax-line">{{ 'app.plans.plan.form.tax' | transloco }}</p>
              <scs-select
                testId="field-currency"
                [label]="'app.plans.plan.form.currency' | transloco"
                [options]="currencies()"
                [value]="'egp'"
              />
              <scs-text-field
                ltr
                testId="field-monthly-egp"
                required
                [label]="'app.plans.plan.form.monthlyEgp' | transloco"
                [placeholder]="'app.plans.plan.form.monthlyHint' | transloco"
                [error]="said('monthlyEgp')"
                [(value)]="monthlyEgp"
              />
              <scs-text-field
                ltr
                class="wide"
                testId="field-annual-egp"
                required
                [label]="'app.plans.plan.form.annualEgp' | transloco"
                [placeholder]="'app.plans.plan.form.annualHint' | transloco"
                [hint]="'app.plans.plan.form.annualNote' | transloco"
                [error]="said('annualEgp')"
                [(value)]="annualEgp"
              />
              <scs-text-field
                ltr
                testId="field-monthly-usd"
                required
                [label]="'app.plans.plan.form.monthlyUsd' | transloco"
                [error]="said('monthlyUsd')"
                [(value)]="monthlyUsd"
              />
              <scs-text-field
                ltr
                testId="field-annual-usd"
                required
                [label]="'app.plans.plan.form.annualUsd' | transloco"
                [error]="said('annualUsd')"
                [(value)]="annualUsd"
              />
              <p class="line wide" data-testid="usd-line">{{ 'app.plans.plan.form.usdNote' | transloco }}</p>
              <scs-textarea
                class="wide"
                testId="field-description"
                required
                [label]="'app.plans.plan.form.description' | transloco"
                [placeholder]="'app.plans.plan.form.descriptionHint' | transloco"
                [max]="500"
                [rows]="2"
                [error]="said('description')"
                [(value)]="description"
              />
              <div class="wide" data-testid="badge-field">
                @if (disabledPlan()) {
                  <p class="line" data-testid="badge-disabled">
                    {{ 'app.plans.plan.form.badgeDisabled' | transloco }}
                  </p>
                } @else {
                  <scs-select
                    testId="field-badge"
                    [label]="'app.plans.plan.form.badge' | transloco"
                    [options]="badgeOptions()"
                    [placeholder]="'app.plans.plan.badge.none' | transloco"
                    [hint]="'app.plans.plan.form.badgeNote' | transloco"
                    [(value)]="badge"
                  />
                  @if (holder(); as name) {
                    <p class="badge-holder" data-testid="badge-holder" role="status">
                      {{ 'app.plans.plan.form.badgeHolder' | transloco: { name } }}
                    </p>
                  }
                }
              </div>
            </div>

            <section class="features" data-testid="plan-grid">
              <h2>{{ 'app.plans.plan.form.features' | transloco }}</h2>
              <p class="line">{{ 'app.plans.plan.form.featuresNote' | transloco }}</p>
              @for (group of groups(); track group.key) {
                <p class="group" data-testid="grid-group">
                  {{ 'app.plans.group.' + group.key | transloco }}
                </p>
                @for (line of group.lines; track line.feature.key) {
                  <div class="row" data-testid="grid-row" [attr.data-key]="line.feature.key">
                    <span class="what">
                      <bdi data-testid="grid-name">{{ name(line.feature) }}</bdi>
                      @if (line.feature.status === 'upcoming') {
                        <span class="tag" data-testid="grid-upcoming">{{
                          'app.plans.plan.form.upcoming' | transloco
                        }}</span>
                      }
                      @if (line.feature.retired) {
                        <span class="tag" data-tone="warning" data-testid="grid-retired">{{
                          'app.plans.plan.form.retired' | transloco
                        }}</span>
                      } @else if (!line.feature.sold) {
                        <span class="tag" data-tone="warning" data-testid="grid-not-sold">{{
                          'app.plans.plan.form.notSold' | transloco
                        }}</span>
                      }
                    </span>
                    @if (line.feature.kind === 'limit') {
                      <span class="limit">
                        <input
                          type="text"
                          inputmode="numeric"
                          dir="ltr"
                          data-testid="grid-limit"
                          [attr.aria-label]="
                            'app.plans.plan.form.limitOf' | transloco: { name: name(line.feature) }
                          "
                          [attr.aria-invalid]="limitError(line.feature.key) ? 'true' : null"
                          [attr.aria-describedby]="
                            limitError(line.feature.key) ? 'limit-error-' + line.feature.key : null
                          "
                          [placeholder]="'app.plans.plan.form.noLimit' | transloco"
                          [disabled]="!line.on"
                          [value]="line.limit"
                          (input)="setLimit(line.feature.key, $any($event.target).value)"
                        />
                        <span class="unit">{{ unit(line.feature) }}</span>
                      </span>
                    }
                    <scs-switch
                      labelHidden
                      testId="grid-switch"
                      [label]="'app.plans.plan.form.include' | transloco: { name: name(line.feature) }"
                      [on]="line.on"
                      (onChange)="setOn(line.feature.key, $event)"
                    />
                  </div>
                  @if (limitError(line.feature.key); as words) {
                    <!-- Read with its field: the focus lands on the field, and this says why (T051). -->
                    <p class="row-error" data-testid="grid-limit-error" [id]="'limit-error-' + line.feature.key">
                      {{ words | transloco }}
                    </p>
                  }
                }
              }
            </section>

            <scs-textarea
              class="note-field"
              testId="field-note"
              [label]="'app.plans.plan.form.note' | transloco"
              [hint]="'app.plans.plan.form.noteHint' | transloco"
              [max]="500"
              [rows]="2"
              [error]="said('note')"
              [(value)]="note"
            />

            <div class="buttons">
              <button
                type="button"
                scsButton="secondary"
                data-testid="form-cancel"
                (click)="leave()"
              >
                {{ 'app.plans.plan.form.cancel' | transloco }}
              </button>
              <button
                type="submit"
                scsButton
                data-testid="plan-save"
                [busy]="saving()"
                [disabled]="saving()"
              >
                {{
                  (saving()
                    ? 'app.plans.plan.form.saving'
                    : editing
                      ? 'app.plans.plan.form.save'
                      : 'app.plans.plan.form.add'
                  ) | transloco
                }}
              </button>
            </div>
          </form>
        </scs-card>
      }
    }
  `,
})
export class PlanFormPage {
  private readonly plans = inject(PlansService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly spoken = toSignal(this.words.selectTranslation(), { initialValue: null });

  private readonly editingId = this.route.snapshot.paramMap.get('id');
  protected readonly editing = this.editingId !== null;

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  private readonly original = signal<PlanDetail | null>(null);
  private readonly grid = signal<PlanGrid | null>(null);

  protected readonly nameAr = signal('');
  protected readonly nameEn = signal('');
  protected readonly monthlyEgp = signal('');
  protected readonly annualEgp = signal('');
  protected readonly monthlyUsd = signal('');
  protected readonly annualUsd = signal('');
  protected readonly description = signal('');
  protected readonly badge = signal('');
  protected readonly note = signal('');
  protected readonly lines = signal<readonly GridLine[]>([]);

  protected readonly saving = signal(false);
  protected readonly failure = signal('');
  protected readonly stale = signal(false);
  private readonly errors = signal<Partial<Record<Field, string>>>({});
  private readonly limitErrors = signal<Record<string, string>>({});

  protected readonly title = computed(() => {
    this.spoken();
    const plan = this.original();
    return plan === null
      ? this.words.translate('app.plans.plan.form.addTitle')
      : this.words.translate('app.plans.plan.form.editTitle', {
          name: planName(plan, this.language.language()),
        });
  });

  /** A disabled plan holds no badge: its field is removed and a line says why (clarified). */
  protected readonly disabledPlan = computed(() => this.original()?.state === 'disabled');

  protected readonly currencies = computed<ScsSelectOption[]>(() => {
    this.spoken();
    return [{ value: 'egp', label: this.words.translate('app.plans.plan.form.currencyEgp') }];
  });

  protected readonly badgeOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    // «بلا شارة» is the select's own empty choice: its placeholder.
    return PLAN_BADGES.map((badge) => ({
      value: badge,
      label: this.words.translate(`app.plans.plan.badge.${badge}`),
    }));
  });

  /** The plan that holds a badge now, when this save would take it from it (ق-٤٥). */
  protected readonly holder = computed(() => {
    const holder = this.grid()?.badgeHolder ?? null;
    if (this.badge() === '' || holder === null || holder.id === this.editingId) {
      return null;
    }
    return planName(holder, this.language.language());
  });

  /** The grid by the catalogue's seven groups; a group with nothing offered is left out. */
  protected readonly groups = computed(() =>
    FEATURE_GROUPS.map((key) => ({
      key,
      lines: this.lines().filter((line) => line.feature.group === key),
    })).filter((group) => group.lines.length > 0),
  );

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    this.failure.set('');
    this.stale.set(false);
    this.errors.set({});
    this.limitErrors.set({});
    try {
      const [grid, plan] = await Promise.all([
        this.plans.grid(this.editingId),
        this.editingId === null ? Promise.resolve(null) : this.plans.read(this.editingId),
      ]);
      this.grid.set(grid);
      this.original.set(plan);
      const included = new Map((plan?.features ?? []).map((row) => [row.key, row.limit]));
      this.lines.set(
        grid.items.map((feature) => ({
          feature,
          on: included.has(feature.key),
          limit: included.get(feature.key) == null ? '' : String(included.get(feature.key)),
        })),
      );
      if (plan !== null) {
        this.nameAr.set(plan.nameAr);
        this.nameEn.set(plan.nameEn);
        this.monthlyEgp.set(String(plan.monthlyEgp));
        this.annualEgp.set(String(plan.annualEgp));
        this.monthlyUsd.set(String(plan.monthlyUsd));
        this.annualUsd.set(String(plan.annualUsd));
        this.description.set(plan.description);
        this.badge.set(plan.badge ?? '');
        this.note.set('');
      }
      this.state.set('ready');
    } catch {
      this.state.set('failed');
    }
  }

  protected name(feature: GridFeature): string {
    return this.language.language() === 'ar' ? feature.nameAr : feature.nameEn;
  }

  protected unit(feature: GridFeature): string {
    return (this.language.language() === 'ar' ? feature.unitAr : feature.unitEn) ?? '';
  }

  protected said(field: Field): string {
    const key = this.errors()[field];
    return key === undefined ? '' : this.words.translate(key);
  }

  protected limitError(key: string): string {
    return this.limitErrors()[key] ?? '';
  }

  protected setOn(key: string, on: boolean): void {
    this.lines.update((lines) => lines.map((line) => (line.feature.key === key ? { ...line, on } : line)));
  }

  protected setLimit(key: string, limit: string): void {
    this.lines.update((lines) =>
      lines.map((line) => (line.feature.key === key ? { ...line, limit } : line)),
    );
  }

  protected leave(): void {
    void this.router.navigateByUrl(this.editingId === null ? '/plans' : `/plans/${this.editingId}`);
  }

  /** What can be told without asking: the required fields, the amounts, the limits (FR-016). */
  private check(): { fields: Partial<Record<Field, string>>; limits: Record<string, string> } {
    const fields: Partial<Record<Field, string>> = {};
    const text = (field: Field, value: string, max: number, needed: boolean) => {
      const trimmed = value.trim();
      if (trimmed === '' && needed) {
        fields[field] = 'app.plans.plan.form.required';
      } else if (trimmed.length > max) {
        fields[field] = 'app.plans.plan.form.tooLong';
      }
    };
    const money = (field: Field, value: string) => {
      const trimmed = value.trim();
      if (trimmed === '') {
        fields[field] = 'app.plans.plan.form.required';
      } else if (!AMOUNT.test(trimmed) || Number(trimmed) > AMOUNT_MAX) {
        fields[field] = 'app.plans.plan.form.price';
      }
    };

    text('nameAr', this.nameAr(), NAME_MAX, true);
    text('nameEn', this.nameEn(), NAME_MAX, true);
    text('description', this.description(), TEXT_MAX, true);
    text('note', this.note(), TEXT_MAX, false);
    money('monthlyEgp', this.monthlyEgp());
    money('annualEgp', this.annualEgp());
    money('monthlyUsd', this.monthlyUsd());
    money('annualUsd', this.annualUsd());

    const limits: Record<string, string> = {};
    for (const line of this.lines()) {
      const typed = line.limit.trim();
      if (!line.on || line.feature.kind !== 'limit' || typed === '') {
        continue;
      }
      if (!/^\d+$/u.test(typed) || Number(typed) < 1 || Number(typed) > LIMIT_MAX) {
        limits[line.feature.key] = 'app.plans.plan.form.limit';
      }
    }
    return { fields, limits };
  }

  /** Every feature switched on; a limit's number, or none for no limit — never one on a switch. */
  private features(): { key: string; limit: number | null }[] {
    return this.lines()
      .filter((line) => line.on)
      .map((line) => ({
        key: line.feature.key,
        limit:
          line.feature.kind === 'limit' && line.limit.trim() !== '' ? Number(line.limit.trim()) : null,
      }));
  }

  protected async save(): Promise<void> {
    const { fields, limits } = this.check();
    this.errors.set(fields);
    this.limitErrors.set(limits);
    this.failure.set('');
    this.stale.set(false);
    if (Object.keys(fields).length > 0 || Object.keys(limits).length > 0) {
      this.focusFirstError();
      return;
    }

    const features = this.features();
    const body = {
      nameAr: this.nameAr().trim(),
      nameEn: this.nameEn().trim(),
      description: this.description().trim(),
      monthlyEgp: Number(this.monthlyEgp().trim()),
      annualEgp: Number(this.annualEgp().trim()),
      monthlyUsd: Number(this.monthlyUsd().trim()),
      annualUsd: Number(this.annualUsd().trim()),
      badge: this.disabledPlan() ? null : ((this.badge() || null) as PlanBadge | null),
      note: this.note().trim() || null,
      features,
    };

    this.saving.set(true);
    try {
      if (this.editingId === null) {
        await this.plans.add(body);
        this.toasts.show(this.words.translate('app.plans.plan.form.added'));
        await this.router.navigateByUrl('/plans');
      } else {
        await this.plans.edit(this.editingId, { ...body, version: this.original()?.version });
        this.toasts.show(this.words.translate('app.plans.plan.form.updated'));
        await this.router.navigateByUrl(`/plans/${this.editingId}`);
      }
    } catch (error) {
      this.refused(error, features);
    } finally {
      this.saving.set(false);
    }
  }

  /** A refusal of the platform is said at the top of the form and, where it has a field, beside it. */
  private refused(error: unknown, sent: readonly { key: string }[]): void {
    const code = codeOf(error);
    this.failure.set(this.editing ? 'app.plans.plan.form.saveFailed' : 'app.plans.plan.form.addFailed');
    this.stale.set(code === PlanCodes.changedElsewhere);
    if (code !== PlanCodes.validation) {
      return;
    }

    const known: readonly string[] = [
      'nameAr',
      'nameEn',
      'monthlyEgp',
      'annualEgp',
      'monthlyUsd',
      'annualUsd',
      'description',
      'note',
    ];
    const fields: Partial<Record<Field, string>> = {};
    const limits: Record<string, string> = {};
    for (const [field, reason] of Object.entries(fieldErrors(error))) {
      const row = /^features\[(\d+)\]\.limit$/u.exec(field);
      if (row !== null) {
        const key = sent[Number(row[1])]?.key;
        if (key !== undefined) {
          limits[key] = 'app.plans.plan.form.limit';
        }
      } else if (known.includes(field)) {
        fields[field as Field] =
          reason === 'TOO_LONG'
            ? 'app.plans.plan.form.tooLong'
            : reason === 'INVALID'
              ? 'app.plans.plan.form.price'
              : 'app.plans.plan.form.required';
      }
    }
    this.errors.set(fields);
    this.limitErrors.set(limits);
    if (Object.keys(fields).length > 0 || Object.keys(limits).length > 0) {
      this.focusFirstError();
    }
  }

  /** The first field in error takes the focus, so a keyboard or a screen reader lands on it. */
  private focusFirstError(): void {
    setTimeout(() =>
      this.host.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(),
    );
  }
}
