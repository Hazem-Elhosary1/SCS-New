import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService, OnlineService } from 'core';
import {
  ScsBannerComponent,
  ScsBreadcrumbComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsSelectComponent,
  ScsSelectOption,
  ScsSkeletonComponent,
  ScsTextFieldComponent,
  ScsTextareaComponent,
  ScsWriteDirective,
  ToastService,
} from 'ui';
import { codeOf, fieldErrors } from '../operators/operators.service';
import { clinicName } from './clinic-words';
import { ClinicCodes, ClinicDetail, ClinicFormOptions, ClinicsService } from './clinics.service';

const NAME_MAX = 200;
const ADDRESS_MAX = 300;
const NOTE_MAX = 500;
const MOBILE = /^\+?\d{8,15}$/u;
const NINE = /^\d{9}$/u;

type Field = 'nameAr' | 'nameEn' | 'ownerName' | 'email' | 'mobile' | 'address' | 'planId' | 'taxNumber' | 'specialty' | 'note';

/** Server field codes the form has words for (data-model.md §7). */
const REASONS = ['REQUIRED', 'TOO_LONG', 'TAX_NUMBER', 'MOBILE', 'TAKEN', 'INVALID_FORMAT', 'INVALID'] as const;

/**
 * «إضافة عيادة جديدة» and «تعديل العيادة» (spec 015, User Stories 1 and 4; FR-002, FR-003, FR-020 –
 * FR-022; frames `655:1140`/`649:1040`, `655:1230`/`649:1124`, `2263:2467`, `2263:2661`). One form
 * in two modes, in the drawn order, two to a row; the two names share the first row (the owner's
 * answer). Editing adds the language, the fixed time zone and a note; the plan is shown and cannot
 * be changed here (spec 020). The platform checks the whole save again.
 */
@Component({
  selector: 'app-clinic-form-page',
  imports: [
    TranslocoPipe,
    ScsBreadcrumbComponent,
    ScsCardComponent,
    ScsTextFieldComponent,
    ScsSelectComponent,
    ScsTextareaComponent,
    ScsButtonDirective,
    ScsWriteDirective,
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
    /* A banner keeps its own layout (icon beside words): the page gives it room only. */
    scs-banner {
      margin-block-end: var(--scs-spacing-md);
      max-inline-size: 648px;
    }
    .stale {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-sm);
    }
    scs-card {
      display: block;
      max-inline-size: 648px;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--scs-spacing-lg) var(--scs-spacing-lg);
    }
    .wide {
      grid-column: 1 / -1;
    }
    .hint {
      grid-column: 1 / -1;
      margin: calc(-1 * var(--scs-spacing-md)) 0 0;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
      line-height: 1.6;
    }
    /* At the card's start, the main action first, as the add and edit frames draw them (the owner's answer of 17 Oct 2026). */
    .buttons {
      display: flex;
      justify-content: flex-start;
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
      /* The cells that keep the wide form's two columns in step take no room in one column. */
      .spacer {
        display: none;
      }
      .buttons {
        flex-direction: column;
      }
    }
  `,
  template: `
    <scs-breadcrumb
      [label]="'app.clinics.form.crumb' | transloco"
      [crumbs]="[
        { label: 'app.clinics.form.crumb' | transloco, link: '/clinics' },
        { label: (editing ? 'app.clinics.form.editCrumb' : 'app.clinics.form.addCrumb') | transloco },
      ]"
    />
    <h1 data-testid="form-title">
      {{ (editing ? 'app.clinics.form.editTitle' : 'app.clinics.form.addTitle') | transloco }}
    </h1>
    <p class="note" data-testid="form-note"><bdi>{{ line() }}</bdi></p>

    @switch (state()) {
      @case ('loading') {
        <scs-card><scs-skeleton data-testid="form-loading" [rows]="8" [label]="'app.clinics.form.loading' | transloco" /></scs-card>
      }
      @case ('failed') {
        <scs-card>
          <scs-empty-state
            context="error"
            data-testid="form-failed"
            [title]="'app.clinics.detail.failedTitle' | transloco"
            [text]="'app.clinics.form.loadFailed' | transloco"
          >
            <button type="button" scsButton="secondary" data-testid="form-retry" (click)="load()">
              {{ 'app.clinics.retry' | transloco }}
            </button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @if (failure(); as words) {
          <scs-banner tone="danger" data-testid="form-problem">
            <span class="stale">
              <span>{{ words }}</span>
              @if (stale()) {
                <span data-testid="form-stale">{{ 'app.clinics.form.changed' | transloco }}</span>
                <button type="button" scsButton="secondary" data-testid="form-reload" (click)="load()">
                  {{ 'app.clinics.form.reload' | transloco }}
                </button>
              }
            </span>
          </scs-banner>
        }
        <scs-card>
          <form novalidate data-testid="clinic-form" (submit)="$event.preventDefault(); save()">
            <div class="grid">
              <scs-text-field
                rtl
                required
                testId="clinic-name-ar"
                [label]="'app.clinics.form.nameAr' | transloco"
                [error]="said('nameAr')"
                [(value)]="nameAr"
              />
              <scs-text-field
                ltr
                testId="clinic-name-en"
                [label]="'app.clinics.form.nameEn' | transloco"
                [hint]="'app.clinics.form.nameEnHint' | transloco"
                [error]="said('nameEn')"
                [(value)]="nameEn"
              />
              @if (hasOwner()) {
              <scs-text-field
                required
                testId="clinic-owner-name"
                [label]="'app.clinics.form.ownerName' | transloco"
                [error]="said('ownerName')"
                [(value)]="ownerName"
              />
              <scs-text-field
                ltr
                required
                kind="email"
                testId="clinic-email"
                autocomplete="off"
                [label]="'app.clinics.form.email' | transloco"
                [error]="said('email')"
                [(value)]="email"
              />
              <scs-text-field
                ltr
                required
                testId="clinic-mobile"
                autocomplete="off"
                [label]="'app.clinics.form.mobile' | transloco"
                [error]="said('mobile')"
                [(value)]="mobile"
              />
              <span class="spacer" aria-hidden="true"></span>
              }
              <scs-text-field
                class="wide"
                required
                testId="clinic-address"
                [label]="'app.clinics.form.address' | transloco"
                [error]="said('address')"
                [(value)]="address"
              />
              <scs-select
                testId="clinic-plan"
                [required]="!editing"
                [disabled]="editing"
                [label]="'app.clinics.form.plan' | transloco"
                [placeholder]="'app.clinics.form.planPlaceholder' | transloco"
                [options]="planOptions()"
                [error]="said('planId')"
                [(value)]="planId"
              />
              <scs-text-field
                ltr
                testId="clinic-tax"
                [label]="'app.clinics.form.tax' | transloco"
                [hint]="'app.clinics.form.taxHint' | transloco"
                [error]="said('taxNumber')"
                [value]="taxNumber()"
                (valueChange)="typeTax($event)"
              />
              <scs-select
                testId="clinic-specialty"
                [required]="!editing"
                [label]="'app.clinics.form.specialty' | transloco"
                [placeholder]="(editing ? 'app.clinics.form.unrecorded' : 'app.clinics.form.specialtyPlaceholder') | transloco"
                [options]="specialtyOptions()"
                [error]="said('specialty')"
                [(value)]="specialty"
              />
              <span class="spacer" aria-hidden="true"></span>
              <p class="hint" data-testid="clinic-specialty-hint">{{ 'app.clinics.form.specialtyHint' | transloco }}</p>
              @if (editing) {
                <scs-select
                  testId="clinic-language"
                  [label]="'app.clinics.form.language' | transloco"
                  [options]="languageOptions()"
                  [(value)]="language"
                />
                <scs-select
                  testId="clinic-time-zone"
                  disabled
                  [label]="'app.clinics.form.timeZone' | transloco"
                  [options]="zoneOptions()"
                  value="cairo"
                />
                <scs-textarea
                  class="wide"
                  testId="clinic-note"
                  [max]="noteMax"
                  [label]="'app.clinics.form.note' | transloco"
                  [placeholder]="'app.clinics.form.notePlaceholder' | transloco"
                  [hint]="'app.clinics.form.noteHint' | transloco"
                  [error]="said('note')"
                  [(value)]="note"
                />
              }
            </div>
            <div class="buttons">
              <button type="submit" scsButton [scsWrite]="online()" data-testid="clinic-save" [busy]="saving()" [disabled]="saving()">
                {{ saveWords() | transloco }}
              </button>
              <button type="button" scsButton="secondary" data-testid="clinic-cancel" (click)="leave()">
                {{ 'app.clinics.form.cancel' | transloco }}
              </button>
            </div>
          </form>
        </scs-card>
      }
    }
  `,
})
export class ClinicFormPage {
  private readonly clinics = inject(ClinicsService);
  private readonly router = inject(Router);
  private readonly words = inject(TranslocoService);
  private readonly languages = inject(LanguageService);
  private readonly toasts = inject(ToastService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly spoken = toSignal(this.words.selectTranslation(), { initialValue: null });
  /** Nothing is saved while offline: the shell says so, and the save waits (constitution VI). */
  protected readonly online = inject(OnlineService).online;

  protected readonly editingId = inject(ActivatedRoute).snapshot.paramMap.get('id');
  protected readonly editing = this.editingId !== null;
  protected readonly noteMax = NOTE_MAX;

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly saving = signal(false);
  protected readonly failure = signal('');
  protected readonly stale = signal(false);
  private readonly errors = signal<Partial<Record<Field, string>>>({});

  private readonly options = signal<ClinicFormOptions | null>(null);
  private readonly original = signal<ClinicDetail | null>(null);

  protected readonly nameAr = signal('');
  protected readonly nameEn = signal('');
  protected readonly ownerName = signal('');
  protected readonly email = signal('');
  protected readonly mobile = signal('');
  protected readonly address = signal('');
  protected readonly planId = signal('');
  protected readonly taxNumber = signal('');
  protected readonly specialty = signal('');
  protected readonly language = signal('ar');
  protected readonly note = signal('');
  /** The tax number is sent only once it was touched: left out keeps the stored one (FR-022). */
  private readonly taxTouched = signal(false);

  /** An add always has an owner; an edit only when the clinic has an owner account (spec 015, edge cases). */
  protected readonly hasOwner = computed(() => !this.editing || this.original()?.owner !== null);

  protected readonly line = computed(() => {
    this.spoken();
    const clinic = this.original();
    return clinic === null
      ? this.words.translate('app.clinics.form.addLine')
      : this.words.translate('app.clinics.form.editLine', { name: clinicName(clinic, this.languages.language()) });
  });

  protected readonly saveWords = computed(() =>
    this.editing
      ? this.saving()
        ? 'app.clinics.form.saving'
        : 'app.clinics.form.save'
      : this.saving()
        ? 'app.clinics.form.adding'
        : 'app.clinics.form.add',
  );

  protected readonly planOptions = computed<ScsSelectOption[]>(() => {
    const language = this.languages.language();
    const clinic = this.original();
    if (clinic !== null) {
      // Editing shows the clinic's plan, whatever its state, and offers nothing else (spec 020).
      return clinic.plan === null ? [] : [{ value: clinic.plan.id, label: language === 'ar' ? clinic.plan.nameAr : clinic.plan.nameEn }];
    }
    return (this.options()?.plans ?? []).map((plan) => ({ value: plan.id, label: language === 'ar' ? plan.nameAr : plan.nameEn }));
  });

  protected readonly specialtyOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    const keys = this.options()?.specialties ?? [];
    const named = keys.map((key) => ({ value: key, label: this.words.translate(`app.plans.specialty.${key}`) }));
    // «غير مسجّل» is a choice in editing only (OP-A-020, OP-A-044): the select's own nothing-chosen
    // row, its placeholder, since the field is not required there.
    return named;
  });

  protected readonly languageOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    return ['ar', 'en'].map((value) => ({ value, label: this.words.translate(`app.clinics.form.languages.${value}`) }));
  });

  protected readonly zoneOptions = computed<ScsSelectOption[]>(() => {
    this.spoken();
    return [{ value: 'cairo', label: this.words.translate('app.clinics.form.cairo') }];
  });

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    this.failure.set('');
    this.stale.set(false);
    this.errors.set({});
    try {
      const [options, clinic] = await Promise.all([
        this.editing ? Promise.resolve({ plans: [], specialties: SPECIALTIES }) : this.clinics.form(),
        this.editingId === null ? Promise.resolve(null) : this.clinics.read(this.editingId),
      ]);
      this.options.set(options);
      this.original.set(clinic);
      if (clinic !== null) {
        this.nameAr.set(clinic.nameAr);
        this.nameEn.set(clinic.nameEn === clinic.nameAr ? '' : clinic.nameEn);
        this.ownerName.set(clinic.owner?.name ?? '');
        this.email.set(clinic.owner?.email ?? '');
        this.mobile.set(clinic.owner?.mobile ?? '');
        this.address.set(clinic.address ?? '');
        this.planId.set(clinic.plan?.id ?? '');
        this.taxNumber.set(clinic.taxNumber ?? '');
        this.specialty.set(clinic.specialty ?? '');
        this.language.set(clinic.defaultLanguage);
        this.note.set('');
        this.taxTouched.set(false);
      }
      this.state.set('ready');
    } catch {
      this.state.set('failed');
    }
  }

  protected typeTax(value: string): void {
    this.taxNumber.set(value);
    this.taxTouched.set(true);
  }

  protected said(field: Field): string {
    this.spoken();
    const reason = this.errors()[field];
    return reason === undefined ? '' : this.words.translate(`app.clinics.form.errors.${reason}`);
  }

  protected leave(): void {
    void this.router.navigateByUrl(this.editingId === null ? '/clinics' : `/clinics/${this.editingId}`);
  }

  /** What can be told without asking (FR-003): required fields, lengths, the tax number and the mobile. */
  private check(): Partial<Record<Field, string>> {
    const errors: Partial<Record<Field, string>> = {};
    const text = (field: Field, value: string, max: number, needed: boolean) => {
      const trimmed = value.trim();
      if (trimmed === '' && needed) {
        errors[field] = 'REQUIRED';
      } else if (trimmed.length > max) {
        errors[field] = 'TOO_LONG';
      }
    };
    text('nameAr', this.nameAr(), NAME_MAX, true);
    text('nameEn', this.nameEn(), NAME_MAX, false);
    text('ownerName', this.ownerName(), NAME_MAX, this.original()?.owner !== null);
    text('address', this.address(), ADDRESS_MAX, true);
    text('note', this.note(), NOTE_MAX, false);

    const ownerNeeded = this.original() === null || this.original()?.owner !== null;
    if (ownerNeeded) {
      const email = this.email().trim();
      if (email === '') {
        errors.email = 'REQUIRED';
      } else if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/u.test(email)) {
        errors.email = 'INVALID_FORMAT';
      }
      const mobile = this.mobile().replace(/[\s-]/gu, '');
      if (mobile === '') {
        errors.mobile = 'REQUIRED';
      } else if (!MOBILE.test(mobile)) {
        errors.mobile = 'MOBILE';
      }
    }

    const tax = this.taxNumber().replace(/[\s-]/gu, '');
    const storedUnchanged = this.editing && this.taxNumber().trim() === (this.original()?.taxNumber ?? '');
    if (tax !== '' && !NINE.test(tax) && !storedUnchanged) {
      errors.taxNumber = 'TAX_NUMBER';
    }
    if (!this.editing) {
      if (this.planId() === '') {
        errors.planId = 'REQUIRED';
      }
      if (this.specialty() === '') {
        errors.specialty = 'REQUIRED';
      }
    }
    return errors;
  }

  protected async save(): Promise<void> {
    const errors = this.check();
    this.errors.set(errors);
    this.failure.set('');
    this.stale.set(false);
    if (Object.keys(errors).length > 0) {
      this.focusFirstError();
      return;
    }

    this.saving.set(true);
    try {
      if (this.editingId === null) {
        const added = await this.clinics.add({
          nameAr: this.nameAr().trim(),
          nameEn: this.nameEn().trim(),
          ownerName: this.ownerName().trim(),
          email: this.email().trim(),
          mobile: this.mobile().trim(),
          address: this.address().trim(),
          planId: this.planId() || null,
          taxNumber: this.taxNumber().trim(),
          specialty: this.specialty() || null,
        });
        this.toasts.show(this.words.translate('app.clinics.form.added'));
        await this.router.navigateByUrl(`/clinics/${added.id}`);
      } else {
        await this.clinics.edit(this.editingId, {
          version: this.original()?.version ?? '',
          nameAr: this.nameAr().trim(),
          nameEn: this.nameEn().trim(),
          ownerName: this.ownerName().trim(),
          email: this.email().trim(),
          mobile: this.mobile().trim(),
          address: this.address().trim(),
          taxNumberSent: this.taxTouched(),
          ...(this.taxTouched() ? { taxNumber: this.taxNumber().trim() } : {}),
          specialty: this.specialty() || null,
          defaultLanguage: this.language(),
          note: this.note().trim(),
        });
        this.toasts.show(this.words.translate('app.clinics.form.saved'));
        await this.router.navigateByUrl(`/clinics/${this.editingId}`);
      }
    } catch (error) {
      this.refused(error);
    } finally {
      this.saving.set(false);
    }
  }

  /** A refusal is said at the top of the form and, where it has a field, beside it. */
  private refused(error: unknown): void {
    const code = codeOf(error);
    const words =
      code === ClinicCodes.busy
        ? 'app.clinics.form.busy'
        : this.editing
          ? 'app.clinics.form.saveFailed'
          : 'app.clinics.form.addFailed';
    // The plan's limit names itself: the platform's own words (spec 014).
    const problem = (error as { problem?: { title?: string } } | null)?.problem;
    this.failure.set(code === ClinicCodes.limitReached && problem?.title ? problem.title : this.words.translate(words));
    this.stale.set(code === ClinicCodes.changedElsewhere);
    if (code !== ClinicCodes.validation) {
      return;
    }
    const fields: Partial<Record<Field, string>> = {};
    for (const [field, reason] of Object.entries(fieldErrors(error))) {
      if ((REASONS as readonly string[]).includes(reason)) {
        fields[field as Field] = reason;
      }
    }
    this.errors.set(fields);
    this.focusFirstError();
  }

  private focusFirstError(): void {
    setTimeout(() => this.host.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus());
  }
}

/** The fifteen specialties in the BRD's order (SH-013), as the platform's form answer gives them. */
const SPECIALTIES = [
  'dental',
  'orthopedics',
  'neurology',
  'pediatrics',
  'internal_medicine',
  'general_surgery',
  'dermatology',
  'obgyn',
  'ophthalmology',
  'ent',
  'urology',
  'cardiology',
  'psychiatry',
  'nutrition',
  'physiotherapy',
] as const;
