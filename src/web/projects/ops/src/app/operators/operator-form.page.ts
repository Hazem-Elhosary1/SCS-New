import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  ScsBannerComponent,
  ScsBreadcrumbComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsEmptyStateComponent,
  ScsSelectComponent,
  ScsSelectOption,
  ScsSkeletonComponent,
  ScsBadgeComponent,
  ScsSwitchComponent,
  ScsTextFieldComponent,
  ScsTextareaComponent,
  ToastService,
} from 'ui';
import { OpsAccess } from '../access/ops-access.service';
import { OpsAuthService } from '../auth/ops-auth.service';
import { OperatorDetail, OperatorsService, RoleOption, TeamCodes, codeOf, fieldErrors } from './operators.service';

const DEPARTMENTS = ['support', 'billing', 'success', 'sales', 'management'] as const;
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/u;

type Field = 'fullName' | 'email' | 'roleId' | 'mobile' | 'department' | 'jobTitle' | 'language' | 'note';

/**
 * «إضافة مشغل جديد» and «تعديل بيانات المشغل» (spec 011, User Stories 2 and 4; frames
 * `1690:70`/`1692:70`, `1703:70`/`1705:70` and the three refusals `2260:1012`, `2260:1105`,
 * `2260:1198`). One form in two modes. The role picker is drawn from what the platform says
 * this person may grant; the platform refuses the rest whatever the form did. No link is ever
 * shown: the invitation goes by email.
 */
@Component({
  selector: 'app-operator-form-page',
  imports: [
    TranslocoPipe,
    ScsBreadcrumbComponent,
    ScsCardComponent,
    ScsTextFieldComponent,
    ScsSelectComponent,
    ScsSwitchComponent,
    ScsTextareaComponent,
    ScsBadgeComponent,
    ScsButtonDirective,
    ScsBannerComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // The frames of this template draw a form's labels small, grey and regular, and an empty drop-down
  // dark: the owner's choice of 14 Oct 2026, as the named option of spec 095 (research R13).
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
    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--scs-spacing-md) var(--scs-spacing-lg);
    }
    .static .label {
      display: block;
      margin-block-end: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .static .value {
      display: flex;
      align-items: center;
      min-block-size: 46px;
      padding-inline: var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body-lg);
      overflow-wrap: anywhere;
    }
    .sr-only {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
    .held {
      grid-column: 1 / -1;
      padding-block-start: var(--scs-spacing-md);
      border-block-start: 1px solid var(--scs-color-border-subtle);
    }
    .held h2 {
      margin: 0 0 var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      font-weight: 400;
      color: var(--scs-color-text-secondary);
    }
    .marks {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-sm);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .row {
      margin-block-start: var(--scs-spacing-md);
      padding-block-start: var(--scs-spacing-md);
      border-block-start: 1px solid var(--scs-color-border-subtle);
    }
    .buttons {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-md);
      padding-block-start: var(--scs-spacing-md);
      border-block-start: 1px solid var(--scs-color-border-subtle);
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
      .buttons {
        flex-direction: column-reverse;
      }
    }
  `,
  template: `
    <scs-breadcrumb
      [label]="'app.operators.crumbs' | transloco"
      [crumbs]="[
        { label: 'app.operators.title' | transloco, link: '/operators' },
        { label: (editing ? 'app.operators.form.editTitle' : 'app.operators.form.addTitle') | transloco },
      ]"
    />
    <h1 data-testid="form-title">{{ (editing ? 'app.operators.form.editTitle' : 'app.operators.form.addTitle') | transloco }}</h1>
    <p class="note" data-testid="form-note">
      @if (editing) {
        {{ 'app.operators.form.editNote' | transloco: { name: original()?.fullName ?? '' } }}
      } @else {
        {{ 'app.operators.form.addNote' | transloco }}
      }
    </p>

    @switch (state()) {
      @case ('loading') {
        <scs-card><scs-skeleton data-testid="form-loading" [rows]="6" [label]="'app.operators.detail.loading' | transloco" /></scs-card>
      }
      @case ('failed') {
        <scs-card>
          <scs-empty-state context="error" data-testid="form-failed" [title]="'app.operators.detail.failed' | transloco">
            <button type="button" scsButton="secondary" data-testid="form-retry" (click)="load()">
              {{ 'app.operators.retry' | transloco }}
            </button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @if (changed()) {
          <scs-banner
            tone="warning"
            data-testid="form-changed"
            [actionLabel]="'app.operators.form.reload' | transloco"
            (action)="load()"
            >{{ 'app.operators.form.changed' | transloco }}</scs-banner
          >
        }
        @if (failure(); as words) {
          <scs-banner tone="danger" data-testid="form-problem">{{ words | transloco }}</scs-banner>
        }

        <scs-card>
          <form novalidate (submit)="$event.preventDefault(); save()">
            <div class="grid">
              <scs-text-field
                testId="field-name"
                [label]="'app.operators.form.fullName' | transloco"
                [placeholder]="'app.operators.form.fullNameHint' | transloco"
                [error]="said('fullName')"
                [(value)]="fullName"
              />
              @if (editing) {
                <!-- The account's identity: shown, and never a field (ق-59). -->
                <div class="static">
                  <span class="label" id="email-label">{{ 'app.operators.form.email' | transloco }}</span>
                  <span
                    class="value"
                    data-testid="field-email"
                    aria-labelledby="email-label"
                    aria-describedby="email-fixed"
                    [attr.title]="'app.operators.form.emailFixed' | transloco"
                    ><bdi dir="ltr">{{ email() }}</bdi></span
                  >
                  <!-- The frame draws no line here: the reason is read out, and shown on hover. -->
                  <span class="sr-only" id="email-fixed" data-testid="email-fixed">{{ 'app.operators.form.emailFixed' | transloco }}</span>
                </div>
              } @else {
                <scs-text-field
                  kind="email"
                  testId="field-email"
                  [lead]="false"
                  [label]="'app.operators.form.email' | transloco"
                  [placeholder]="'app.operators.form.emailPlaceholder' | transloco"
                  [error]="said('email')"
                  [(value)]="email"
                />
              }
              <scs-select
                testId="field-role"
                required
                [label]="'app.operators.form.role' | transloco"
                [placeholder]="'app.operators.form.rolePick' | transloco"
                [options]="roleOptions()"
                [disabled]="ownAccount()"
                [hint]="ownAccount() ? ('app.operators.form.roleOwn' | transloco) : ''"
                [error]="said('roleId')"
                [(value)]="roleId"
              />
              <scs-text-field
                testId="field-mobile"
                ltr
                [label]="'app.operators.form.mobile' | transloco"
                [placeholder]="'app.operators.form.mobilePlaceholder' | transloco"
                [error]="said('mobile')"
                [(value)]="mobile"
              />
              <scs-select
                testId="field-department"
                [label]="'app.operators.form.department' | transloco"
                [placeholder]="'app.operators.form.departmentPick' | transloco"
                [options]="departments()"
                [error]="said('department')"
                [(value)]="department"
              />
              <scs-text-field
                testId="field-job"
                [label]="'app.operators.form.jobTitle' | transloco"
                [placeholder]="'app.operators.form.jobTitleHint' | transloco"
                [error]="said('jobTitle')"
                [(value)]="jobTitle"
              />
              <scs-select
                testId="field-language"
                [label]="'app.operators.form.language' | transloco"
                [options]="languages()"
                [(value)]="language"
              />

              @if (heldCodes().length > 0) {
                <div class="held" data-testid="role-permissions">
                  <h2>{{ 'app.operators.form.permissions' | transloco }}</h2>
                  <ul class="marks">
                    @for (code of heldCodes(); track code) {
                      <li><scs-badge intent="brand" [dot]="false">{{ access.nameOfPermission(code) }}</scs-badge></li>
                    }
                  </ul>
                </div>
              }
            </div>

            <div class="row">
              <scs-switch
                testId="switch-two-step"
                disabled
                [on]="true"
                [label]="(editing ? 'app.operators.form.twoStepEdit' : 'app.operators.form.twoStepAdd') | transloco"
                [reason]="'app.operators.form.twoStepLocked' | transloco"
              />
            </div>
            @if (!editing) {
              <div class="row">
                <scs-switch testId="switch-invite" [label]="'app.operators.form.invite' | transloco" [(on)]="sendInvitation" />
              </div>
            } @else {
              <div class="row">
                <scs-textarea
                  testId="field-note"
                  [label]="'app.operators.form.note' | transloco"
                  [placeholder]="'app.operators.form.notePlaceholder' | transloco"
                  [hint]="'app.operators.form.noteHint' | transloco"
                  [max]="500"
                  [rows]="2"
                  [error]="said('note')"
                  [(value)]="note"
                />
              </div>
            }

            <div class="buttons">
              <button type="button" scsButton="secondary" data-testid="form-cancel" (click)="leave()">
                {{ 'app.operators.cancel' | transloco }}
              </button>
              <button type="submit" scsButton data-testid="form-save" [busy]="saving()" [disabled]="saving()">
                {{ saveLabel() | transloco }}
              </button>
            </div>
          </form>
        </scs-card>
      }
    }
  `,
})
export class OperatorFormPage {
  private readonly team = inject(OperatorsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly auth = inject(OpsAuthService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly access = inject(OpsAccess);

  private readonly id = this.route.snapshot.paramMap.get('id');
  protected readonly editing = this.id !== null;

  protected readonly state = signal<'loading' | 'failed' | 'ready'>('loading');
  protected readonly original = signal<OperatorDetail | null>(null);
  private readonly roles = signal<readonly RoleOption[]>([]);

  protected readonly fullName = signal('');
  protected readonly email = signal('');
  protected readonly roleId = signal('');
  protected readonly mobile = signal('');
  protected readonly department = signal('');
  protected readonly jobTitle = signal('');
  protected readonly language = signal('ar');
  protected readonly sendInvitation = signal(true);
  protected readonly note = signal('');

  protected readonly saving = signal(false);
  protected readonly changed = signal(false);
  protected readonly failure = signal('');
  private readonly errors = signal<Partial<Record<Field, string>>>({});

  /** Nobody changes their own role: the field is locked and says so (FR-037). */
  protected readonly ownAccount = computed(() => this.editing && this.id === this.auth.session()?.operator.id);

  protected readonly roleOptions = computed<ScsSelectOption[]>(() => {
    const held = this.original()?.role.id ?? null;
    return this.roles().map((role) => ({
      value: role.id,
      label: this.access.nameOfRole(role),
      // The role the account already holds stays choosable: keeping it grants nothing.
      disabled: !role.grantable && role.id !== held,
      reason:
        role.grantable || role.id === held
          ? undefined
          : this.words.translate(role.reason === 'super-admin-only' ? 'app.operators.form.roleSuperOnly' : 'app.operators.form.roleHoldsMore'),
    }));
  });

  protected readonly departments = computed<ScsSelectOption[]>(() =>
    DEPARTMENTS.map((key) => ({ value: key, label: this.words.translate(`app.operators.department.${key}`) })),
  );

  protected readonly languages = computed<ScsSelectOption[]>(() => [
    { value: 'ar', label: this.words.translate('app.operators.form.arabic') },
    { value: 'en', label: this.words.translate('app.operators.form.english') },
  ]);

  /** What the chosen role holds, shown under the fields once one is chosen. */
  protected readonly heldCodes = computed(() => this.roles().find((role) => role.id === this.roleId())?.codes ?? []);

  protected readonly saveLabel = computed(() =>
    this.saving()
      ? 'app.operators.form.saving'
      : this.editing
        ? 'app.operators.form.saveChanges'
        : this.sendInvitation()
          ? 'app.operators.form.send'
          : 'app.operators.form.save',
  );

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    this.changed.set(false);
    this.failure.set('');
    this.errors.set({});
    try {
      const [roles, operator] = await Promise.all([
        this.team.roleOptions(),
        this.id === null ? Promise.resolve(null) : this.team.read(this.id),
      ]);
      this.roles.set(roles);
      this.original.set(operator);
      if (operator !== null) {
        this.fullName.set(operator.fullName);
        this.email.set(operator.email);
        this.roleId.set(operator.role.id ?? '');
        this.mobile.set(operator.mobile ?? '');
        this.department.set(operator.department ?? '');
        this.jobTitle.set(operator.jobTitle ?? '');
        this.language.set(operator.language);
        this.note.set('');
      }
      this.state.set('ready');
    } catch {
      this.state.set('failed');
    }
  }

  protected said(field: Field): string {
    const key = this.errors()[field];
    return key === undefined ? '' : this.words.translate(key);
  }

  protected leave(): void {
    void this.router.navigateByUrl(this.id === null ? '/operators' : `/operators/${this.id}`);
  }

  /** What can be told without asking: the required fields, the limits, the look of an address. */
  private check(): Partial<Record<Field, string>> {
    const errors: Partial<Record<Field, string>> = {};
    const required = 'app.operators.required';
    const tooLong = 'app.operators.tooLong';

    const name = this.fullName().trim();
    if (name === '') {
      errors.fullName = required;
    } else if (name.length > 200) {
      errors.fullName = tooLong;
    }
    if (!this.editing) {
      const email = this.email().trim();
      if (email === '') {
        errors.email = required;
      } else if (email.length > 254 || !EMAIL.test(email)) {
        errors.email = 'app.operators.form.emailInvalid';
      }
    }
    if (this.roleId() === '') {
      errors.roleId = required;
    }
    if (this.mobile().trim().length > 30) {
      errors.mobile = tooLong;
    }
    if (this.jobTitle().trim().length > 150) {
      errors.jobTitle = tooLong;
    }
    if (this.note().trim().length > 500) {
      errors.note = tooLong;
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

    this.saving.set(true);
    const body = {
      fullName: this.fullName().trim(),
      roleId: this.roleId(),
      mobile: this.mobile().trim() || null,
      department: this.department() || null,
      jobTitle: this.jobTitle().trim() || null,
      language: this.language(),
      twoStep: true,
    };

    try {
      if (this.id === null) {
        const id = await this.team.invite({ ...body, email: this.email().trim(), sendInvitation: this.sendInvitation() });
        this.toasts.show(this.words.translate(this.sendInvitation() ? 'app.operators.form.invited' : 'app.operators.form.created'));
        await this.router.navigate(['/operators', id]);
      } else {
        await this.team.edit(this.id, { ...body, note: this.note().trim() || null, version: this.original()?.version });
        this.toasts.show(this.words.translate('app.operators.form.updated'));
        await this.router.navigate(['/operators', this.id]);
      }
    } catch (error) {
      this.refused(error);
    } finally {
      this.saving.set(false);
    }
  }

  /** Each refusal is said where it belongs: at its field when it has one. */
  private refused(error: unknown): void {
    const atRole: Record<string, string> = {
      [TeamCodes.roleNotGrantable]: 'app.operators.form.roleRefused',
      [TeamCodes.ownRole]: 'app.operators.form.roleOwn',
      [TeamCodes.lastSuperAdmin]: 'app.operators.form.roleLast',
    };
    const code = codeOf(error);

    if (code in atRole) {
      this.errors.set({ roleId: atRole[code] });
    } else if (code === TeamCodes.emailTaken) {
      this.errors.set({ email: 'app.operators.form.emailTaken' });
    } else if (code === TeamCodes.changedElsewhere) {
      this.changed.set(true);
      return;
    } else if (code === TeamCodes.validation) {
      const named = fieldErrors(error);
      const words: Record<string, string> = {
        REQUIRED: 'app.operators.required',
        TOO_LONG: 'app.operators.tooLong',
        INVALID: 'app.operators.required',
      };
      const errors: Partial<Record<Field, string>> = {};
      for (const [field, reason] of Object.entries(named)) {
        errors[field as Field] =
          field === 'email' && reason === 'INVALID'
            ? 'app.operators.form.emailInvalid'
            : field === 'roleId' && reason === 'INVALID'
              ? 'app.operators.form.roleGone'
              : (words[reason] ?? 'app.operators.required');
      }
      this.errors.set(errors);
    } else {
      this.failure.set(this.editing ? 'app.operators.form.saveFailed' : 'app.operators.form.addFailed');
      return;
    }
    this.focusFirstError();
  }

  /** The first field in error takes the focus, so a keyboard or a screen reader lands on it. */
  private focusFirstError(): void {
    setTimeout(() => this.host.nativeElement.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus());
  }
}
