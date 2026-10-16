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
import { ScsProblemError } from 'core';
import { codeOf, fieldErrors } from '../operators/operators.service';
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
import {
  FEATURE_GROUPS,
  FeatureCodes,
  FeatureKind,
  FeatureRow,
  FeatureStatus,
  FeaturesService,
  SPECIALTIES,
} from './features.service';

const KEY = /^[a-z][a-z0-9_]*$/u;
const NAME_MAX = 120;
const DESCRIPTION_MAX = 500;
const UNIT_MAX = 40;
const BASELINE_MAX = 1_000_000;

type Field =
  | 'nameAr'
  | 'nameEn'
  | 'key'
  | 'unitAr'
  | 'unitEn'
  | 'baseline'
  | 'descriptionAr'
  | 'descriptionEn';

/**
 * «إضافة ميزة» and «تعديل ميزة» (spec 012, User Stories 2 and 3; FR-014 to FR-016; frames
 * `2509:1305`/`2509:1139`). One form in two modes, in the drawn order. The units and «بدون هذه
 * القدرة» are a limit's alone; the key is given once and never changed. The platform checks
 * everything again and refuses what the form let through.
 */
@Component({
  selector: 'app-feature-form-page',
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
    }
    .note {
      margin: var(--scs-spacing-2) 0 var(--scs-spacing-md);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    scs-banner {
      margin-block-end: var(--scs-spacing-md);
    }
    scs-card {
      display: block;
      max-inline-size: 648px;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--scs-spacing-md) var(--scs-spacing-lg);
    }
    .wide {
      grid-column: 1 / -1;
    }
    .sold {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      margin-block-start: var(--scs-spacing-md);
      padding-block: var(--scs-spacing-md);
      border-block: 1px solid var(--scs-color-border-subtle);
    }
    .sold p {
      margin: var(--scs-spacing-2) 0 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
      line-height: 1.6;
    }
    .descriptions {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--scs-spacing-md) var(--scs-spacing-lg);
      margin-block-start: var(--scs-spacing-md);
    }
    .buttons {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-md);
    }
    .buttons button {
      min-block-size: 40px;
      padding-block: 0;
      padding-inline: var(--scs-spacing-lg);
      font-size: var(--scs-text-body);
    }
    @media (max-width: 720px) {
      .grid,
      .descriptions {
        grid-template-columns: minmax(0, 1fr);
      }
      .buttons {
        flex-direction: column-reverse;
      }
    }
  `,
  template: `
    <scs-breadcrumb
      [label]="'app.plans.form.crumbs' | transloco"
      [crumbs]="[
        { label: 'app.plans.section' | transloco, link: '/plans/features' },
        { label: title() | transloco },
      ]"
    />
    <h1 data-testid="form-title">{{ title() | transloco }}</h1>
    <p class="note" data-testid="form-note">
      {{ (editing ? 'app.plans.form.editNote' : 'app.plans.form.addNote') | transloco }}
    </p>

    @switch (state()) {
      @case ('loading') {
        <scs-card
          ><scs-skeleton
            data-testid="form-loading"
            [rows]="6"
            [label]="'app.plans.form.loading' | transloco"
        /></scs-card>
      }
      @case ('missing') {
        <scs-card>
          <scs-empty-state
            data-testid="form-missing"
            [title]="'app.plans.form.missing' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="form-back" (click)="leave()">
              {{ 'app.plans.form.back' | transloco }}
            </button>
          </scs-empty-state>
        </scs-card>
      }
      @case ('failed') {
        <scs-card>
          <scs-empty-state
            context="error"
            data-testid="form-failed"
            [title]="'app.plans.form.failed' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="form-retry" (click)="load()">
              {{ 'app.plans.retry' | transloco }}
            </button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @if (failure(); as words) {
          <scs-banner tone="danger" data-testid="form-problem">{{ words | transloco }}</scs-banner>
        }

        <scs-card>
          <form novalidate (submit)="$event.preventDefault(); save()">
            <div class="grid">
              <!-- Arabic text runs right to left, and English left to right, whatever the page's direction. -->
              <scs-text-field
                rtl
                testId="field-name-ar"
                required
                [label]="'app.plans.form.nameAr' | transloco"
                [placeholder]="'app.plans.form.nameArHint' | transloco"
                [error]="said('nameAr')"
                [(value)]="nameAr"
              />
              <scs-text-field
                testId="field-name-en"
                required
                ltr
                [label]="'app.plans.form.nameEn' | transloco"
                [placeholder]="'app.plans.form.nameEnHint' | transloco"
                [error]="said('nameEn')"
                [(value)]="nameEn"
              />
              <scs-text-field
                testId="field-key"
                required
                ltr
                [readonly]="editing"
                [label]="'app.plans.form.key' | transloco"
                [placeholder]="'app.plans.form.keyHint' | transloco"
                [hint]="editing ? ('app.plans.form.keyFixed' | transloco) : ''"
                [error]="said('key')"
                [(value)]="key"
              />
              <scs-select
                testId="field-group"
                [label]="'app.plans.form.group' | transloco"
                [options]="groupOptions()"
                [(value)]="group"
              />
              <scs-select
                testId="field-kind"
                [label]="'app.plans.form.kind' | transloco"
                [options]="kindOptions()"
                [(value)]="kind"
              />
              @if (isLimit()) {
                <scs-text-field
                  rtl
                  testId="field-unit-ar"
                  required
                  [label]="'app.plans.form.unitAr' | transloco"
                  [placeholder]="'app.plans.form.unitArHint' | transloco"
                  [error]="said('unitAr')"
                  [(value)]="unitAr"
                />
                <scs-text-field
                  testId="field-baseline"
                  ltr
                  [label]="'app.plans.form.baseline' | transloco"
                  [placeholder]="'app.plans.form.baselineHint' | transloco"
                  [hint]="'app.plans.form.baselineNote' | transloco"
                  [error]="said('baseline')"
                  [(value)]="baseline"
                />
                <scs-text-field
                  testId="field-unit-en"
                  required
                  ltr
                  [label]="'app.plans.form.unitEn' | transloco"
                  [placeholder]="'app.plans.form.unitEnHint' | transloco"
                  [error]="said('unitEn')"
                  [(value)]="unitEn"
                />
              }
              <scs-select
                testId="field-specialty"
                [label]="'app.plans.form.specialty' | transloco"
                [placeholder]="'app.plans.form.allSpecialties' | transloco"
                [options]="specialtyOptions()"
                [(value)]="specialty"
              />
              <scs-select
                testId="field-status"
                [label]="'app.plans.form.status' | transloco"
                [options]="statusOptions()"
                [(value)]="status"
              />
            </div>

            <div class="sold">
              <div>
                <scs-switch
                  testId="switch-sold"
                  [label]="'app.plans.form.sold' | transloco"
                  [disabled]="retired()"
                  [reason]="retired() ? ('app.plans.form.soldRetired' | transloco) : ''"
                  [(on)]="isSold"
                />
                <p data-testid="sold-hint">{{ 'app.plans.form.soldHint' | transloco }}</p>
              </div>
            </div>

            <div class="descriptions">
              <scs-textarea
                [dir]="'rtl'"
                testId="field-description-ar"
                [label]="'app.plans.form.descriptionAr' | transloco"
                [placeholder]="'app.plans.form.descriptionHint' | transloco"
                [max]="500"
                [rows]="2"
                [error]="said('descriptionAr')"
                [(value)]="descriptionAr"
              />
              <scs-textarea
                [dir]="'ltr'"
                testId="field-description-en"
                [label]="'app.plans.form.descriptionEn' | transloco"
                [placeholder]="'app.plans.form.descriptionEnHint' | transloco"
                [max]="500"
                [rows]="2"
                [error]="said('descriptionEn')"
                [(value)]="descriptionEn"
              />
            </div>

            <div class="buttons">
              <button
                type="button"
                scsButton="secondary"
                data-testid="form-cancel"
                (click)="leave()"
              >
                {{ 'app.plans.cancel' | transloco }}
              </button>
              <button
                type="submit"
                scsButton
                data-testid="form-save"
                [busy]="saving()"
                [disabled]="saving()"
              >
                {{ (editing ? 'app.plans.form.saveChanges' : 'app.plans.form.add') | transloco }}
              </button>
            </div>
          </form>
        </scs-card>
      }
    }
  `,
})
export class FeatureFormPage {
  private readonly features = inject(FeaturesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly spoken = toSignal(this.words.selectTranslation(), { initialValue: null });

  private readonly editingKey = this.route.snapshot.paramMap.get('key');
  protected readonly editing = this.editingKey !== null;
  protected readonly title = computed(() =>
    this.editing ? 'app.plans.form.editTitle' : 'app.plans.form.addTitle',
  );

  protected readonly state = signal<'loading' | 'failed' | 'missing' | 'ready'>(
    this.editing ? 'loading' : 'ready',
  );
  private readonly original = signal<FeatureRow | null>(null);

  protected readonly nameAr = signal('');
  protected readonly nameEn = signal('');
  protected readonly key = signal('');
  protected readonly group = signal<string>(FEATURE_GROUPS[0]);
  protected readonly kind = signal<string>('switch');
  protected readonly unitAr = signal('');
  protected readonly unitEn = signal('');
  protected readonly baseline = signal('');
  protected readonly specialty = signal('');
  protected readonly status = signal<string>('upcoming');
  protected readonly isSold = signal(true);
  protected readonly descriptionAr = signal('');
  protected readonly descriptionEn = signal('');

  protected readonly saving = signal(false);
  protected readonly failure = signal('');
  private readonly errors = signal<Partial<Record<Field, string>>>({});

  protected readonly isLimit = computed(() => this.kind() === 'limit');
  /** A retired feature stays sold: «تُباع في الباقات» is locked on it, and says why. */
  protected readonly retired = computed(() => this.original()?.isRetired ?? false);

  protected readonly groupOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    return FEATURE_GROUPS.map((group) => ({
      value: group,
      label: this.words.translate(`app.plans.group.${group}`),
    }));
  });

  protected readonly kindOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    return (['switch', 'limit'] as const).map((kind) => ({
      value: kind,
      label: this.words.translate(`app.plans.type.${kind}`),
    }));
  });

  protected readonly statusOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    return (['upcoming', 'built'] as const).map((status) => ({
      value: status,
      label: this.words.translate(`app.plans.status.${status}`),
    }));
  });

  protected readonly specialtyOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    return [
      ...SPECIALTIES.map((specialty) => ({
        value: specialty,
        label: this.words.translate(`app.plans.specialty.${specialty}`),
      })),
    ];
  });

  constructor() {
    if (this.editing) {
      void this.load();
    }
  }

  protected async load(): Promise<void> {
    if (this.editingKey === null) {
      return;
    }
    this.state.set('loading');
    this.failure.set('');
    this.errors.set({});
    try {
      const feature = await this.features.read(this.editingKey);
      this.original.set(feature);
      this.nameAr.set(feature.nameAr);
      this.nameEn.set(feature.nameEn);
      this.key.set(feature.key);
      this.group.set(feature.group);
      this.kind.set(feature.kind);
      this.unitAr.set(feature.unitAr ?? '');
      this.unitEn.set(feature.unitEn ?? '');
      this.baseline.set(feature.baseline === null ? '' : String(feature.baseline));
      this.specialty.set(feature.specialty ?? '');
      this.status.set(feature.status);
      this.isSold.set(feature.isSold);
      this.descriptionAr.set(feature.descriptionAr ?? '');
      this.descriptionEn.set(feature.descriptionEn ?? '');
      this.state.set('ready');
    } catch (error) {
      this.state.set(
        error instanceof ScsProblemError && error.problem.code === FeatureCodes.notFound
          ? 'missing'
          : 'failed',
      );
    }
  }

  protected said(field: Field): string {
    const key = this.errors()[field];
    return key === undefined ? '' : this.words.translate(key);
  }

  protected leave(): void {
    void this.router.navigateByUrl('/plans/features');
  }

  /** What can be told without asking: the required fields, the limits, the shape of a key (FR-015). */
  private check(): Partial<Record<Field, string>> {
    const errors: Partial<Record<Field, string>> = {};
    const required = 'app.plans.form.required';
    const tooLong = 'app.plans.form.tooLong';
    const text = (field: Field, value: string, max: number, needed: boolean) => {
      const trimmed = value.trim();
      if (trimmed === '' && needed) {
        errors[field] = required;
      } else if (trimmed.length > max) {
        errors[field] = tooLong;
      }
    };

    text('nameAr', this.nameAr(), NAME_MAX, true);
    text('nameEn', this.nameEn(), NAME_MAX, true);
    text('descriptionAr', this.descriptionAr(), DESCRIPTION_MAX, false);
    text('descriptionEn', this.descriptionEn(), DESCRIPTION_MAX, false);

    if (!this.editing) {
      const key = this.key().trim();
      if (key === '') {
        errors.key = required;
      } else if (key.length < 2 || key.length > 64 || !KEY.test(key)) {
        errors.key = 'app.plans.form.keyInvalid';
      }
    }

    if (this.isLimit()) {
      for (const field of ['unitAr', 'unitEn'] as const) {
        const value = (field === 'unitAr' ? this.unitAr() : this.unitEn()).trim();
        if (value === '') {
          errors[field] = 'app.plans.form.unitsRequired';
        } else if (value.length > UNIT_MAX) {
          errors[field] = tooLong;
        }
      }
      const baseline = this.baseline().trim();
      if (baseline !== '' && (!/^\d+$/u.test(baseline) || Number(baseline) > BASELINE_MAX)) {
        errors.baseline = 'app.plans.form.baselineInvalid';
      }
    }
    return errors;
  }

  protected async save(): Promise<void> {
    const errors = this.check();
    this.errors.set(errors);
    this.failure.set('');
    if (Object.keys(errors).length > 0) {
      this.focusFirstError();
      return;
    }

    const limit = this.isLimit();
    const words = (value: string) => value.trim() || null;
    const body = {
      nameAr: this.nameAr().trim(),
      nameEn: this.nameEn().trim(),
      descriptionAr: words(this.descriptionAr()),
      descriptionEn: words(this.descriptionEn()),
      group: this.group(),
      kind: this.kind() as FeatureKind,
      unitAr: limit ? words(this.unitAr()) : null,
      unitEn: limit ? words(this.unitEn()) : null,
      baseline: limit && this.baseline().trim() !== '' ? Number(this.baseline().trim()) : null,
      specialty: this.specialty() || null,
      status: this.status() as FeatureStatus,
      isSold: this.isSold(),
    };

    this.saving.set(true);
    try {
      if (this.editingKey === null) {
        await this.features.add({ ...body, key: this.key().trim() });
        this.toasts.show(this.words.translate('app.plans.form.added'));
      } else {
        await this.features.edit(this.editingKey, { ...body, version: this.original()?.version });
        this.toasts.show(this.words.translate('app.plans.form.saved'));
      }
      await this.router.navigateByUrl('/plans/features');
    } catch (error) {
      this.refused(error);
    } finally {
      this.saving.set(false);
    }
  }

  /** A refusal of the platform is said at the top of the form and, where it has a field, beside it (FR-015). */
  private refused(error: unknown): void {
    const code = codeOf(error);
    if (code === FeatureCodes.notFound) {
      this.failure.set('app.plans.form.missing');
      return;
    }
    this.failure.set('app.plans.form.saveFailed');

    const errors: Partial<Record<Field, string>> = {};
    if (code === FeatureCodes.keyTaken) {
      errors.key = 'app.plans.form.keyTaken';
    } else if (code === FeatureCodes.unitsRequired) {
      errors.unitAr = 'app.plans.form.unitsRequired';
      errors.unitEn = 'app.plans.form.unitsRequired';
    } else if (code === FeatureCodes.validation) {
      // The platform names the field and its reason, as the form's own checks do.
      const known: readonly string[] = [
        'nameAr',
        'nameEn',
        'key',
        'unitAr',
        'unitEn',
        'baseline',
        'descriptionAr',
        'descriptionEn',
      ];
      const words: Record<string, string> = {
        REQUIRED: 'app.plans.form.required',
        TOO_LONG: 'app.plans.form.tooLong',
      };
      for (const [field, reason] of Object.entries(fieldErrors(error))) {
        if (!known.includes(field)) {
          continue;
        }
        errors[field as Field] =
          field === 'key' && reason === 'INVALID'
            ? 'app.plans.form.keyInvalid'
            : field === 'baseline'
              ? 'app.plans.form.baselineInvalid'
              : (words[reason] ?? 'app.plans.form.required');
      }
    }
    this.errors.set(errors);
    if (Object.keys(errors).length > 0) {
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
