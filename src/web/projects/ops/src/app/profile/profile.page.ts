import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService, ScsLanguage, ScsProblemError } from 'core';
import {
  ScsButtonDirective,
  ScsDialogComponent,
  ScsIconComponent,
  ScsStateComponent,
  ScsTextFieldComponent,
} from 'ui';
import { firstValueFrom } from 'rxjs';
import { OpsAuthService, OpsCodeStep, OpsCodes } from '../auth/ops-auth.service';
import { passwordRules } from '../password/password.pages';
import { CodeProblem, CodeStepComponent } from '../sign-in/code-step.component';
import { initialsOf } from '../shell/ops-shell.component';

interface RecentSession {
  readonly device: string;
  readonly address: string | null;
  readonly current: boolean;
  readonly startedAt: string;
  readonly endedAt: string | null;
}

interface Profile {
  readonly fullName: string;
  readonly roleName: string;
  readonly email: string;
  readonly mobile: string | null;
  readonly twoFactor: { readonly on: boolean; readonly locked: boolean };
  readonly sessions: readonly RecentSession[];
}

/** "Two hours ago", as the design writes it: «منذ ساعتين», in the reader's digits. */
export function agoLabel(from: string, now: number, language: ScsLanguage): string {
  const seconds = Math.max(0, Math.round((now - Date.parse(from)) / 1000));
  const [value, unit] =
    seconds < 3600
      ? [Math.max(1, Math.round(seconds / 60)), 'minute' as const]
      : seconds < 86_400
        ? [Math.round(seconds / 3600), 'hour' as const]
        : [Math.round(seconds / 86_400), 'day' as const];
  const text = new Intl.RelativeTimeFormat(language === 'ar' ? 'ar-EG' : 'en-GB', {
    numeric: 'always',
  }).format(-value, unit);
  return language === 'ar' ? text.replace(/^قبل\s/u, 'منذ ') : text;
}

/**
 * The signed-in operator's own account (spec 010, FR-041 to FR-046; design frames
 * `1849:2791`, `2185:567`, the change-password frames `1851:2003`, `1852:2042`, `1852:2169`
 * and their English twins): account details, the two-factor switch — on, and locked — recent
 * sessions, signing out of all devices, and changing the password, which asks for a code by
 * email before anything changes (FR-045a).
 */
@Component({
  selector: 'app-profile-page',
  imports: [
    TranslocoPipe,
    ScsButtonDirective,
    ScsDialogComponent,
    ScsIconComponent,
    ScsStateComponent,
    ScsTextFieldComponent,
    CodeStepComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    h1 {
      margin: 0;
      font-size: var(--scs-text-title);
      font-weight: 700;
    }
    .lead {
      margin: var(--scs-spacing-xs) 0 var(--scs-spacing-lg);
      color: var(--scs-color-text-secondary);
    }
    section {
      margin-block-end: var(--scs-spacing-lg);
      padding: var(--scs-spacing-lg);
      border: 1px solid var(--scs-color-border-default);
      border-radius: 12px;
      background: var(--scs-color-bg-surface);
    }
    h2 {
      margin: 0 0 var(--scs-spacing-md);
      padding-block-end: var(--scs-spacing-md);
      border-block-end: 1px solid var(--scs-color-border-default);
      font-size: var(--scs-text-heading-sm);
      font-weight: 600;
    }
    .account {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-lg);
    }
    .who {
      flex: 1.3 1 160px;
    }
    .fact {
      flex: 1 1 160px;
      min-inline-size: 0;
    }
    .who strong {
      font-size: 18px;
    }
    .fact strong {
      font-weight: 400;
      color: var(--scs-color-text-secondary);
      overflow-wrap: anywhere;
    }
    /* An email and a number are written left to right, and still begin with their caption. */
    :host-context([dir='rtl']) .fact strong {
      text-align: right;
    }
    .initials {
      display: grid;
      place-items: center;
      inline-size: 56px;
      block-size: 56px;
      border-radius: 12px;
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
      font-weight: 700;
    }
    .who strong,
    .fact strong {
      display: block;
      font-weight: 600;
    }
    .who span {
      color: var(--scs-color-brand-default);
      font-size: var(--scs-text-body-sm);
    }
    .fact span {
      font-size: var(--scs-text-caption);
      color: var(--scs-color-text-tertiary);
    }
    .with-mark {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
    }
    .switch {
      display: flex;
      align-items: flex-start;
      gap: var(--scs-spacing-12);
    }
    .track {
      position: relative;
      flex: none;
      inline-size: 36px;
      block-size: 20px;
      margin-block-start: 2px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-brand-default);
      opacity: 0.7;
    }
    .track::after {
      content: '';
      position: absolute;
      inset-block-start: 2px;
      right: 2px;
      inline-size: 16px;
      block-size: 16px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface);
    }
    .switch p,
    .note {
      margin: var(--scs-spacing-xs) 0 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-tertiary);
    }
    .sessions-head {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: var(--scs-spacing-md);
      margin-block: var(--scs-spacing-lg) var(--scs-spacing-sm);
      padding-block-start: var(--scs-spacing-md);
      border-block-start: 1px solid var(--scs-color-border-default);
    }
    .sessions-head h3 {
      margin: 0;
      font-size: var(--scs-text-body);
      font-weight: 600;
    }
    .sessions-head .note {
      margin: 0;
    }
    table {
      inline-size: 100%;
      border-collapse: collapse;
    }
    th {
      padding: var(--scs-spacing-sm) 0;
      border-block-end: 1px solid var(--scs-color-border-default);
      font-size: var(--scs-text-caption);
      font-weight: 500;
      color: var(--scs-color-text-tertiary);
      text-align: start;
    }
    td {
      padding: var(--scs-spacing-12) 0;
      border-block-end: 1px solid var(--scs-color-border-subtle);
      vertical-align: middle;
    }
    td small {
      display: block;
      color: var(--scs-color-text-tertiary);
    }
    .now {
      display: inline-block;
      padding: 2px var(--scs-spacing-sm);
      border-radius: var(--scs-radius-sm);
      background: var(--scs-color-feedback-success-bg);
      color: var(--scs-color-feedback-success-text);
      font-size: var(--scs-text-caption);
    }
    .muted {
      color: var(--scs-color-text-tertiary);
      font-size: var(--scs-text-body-sm);
    }
    .everywhere {
      display: flex;
      justify-content: center;
      margin-block-start: var(--scs-spacing-lg);
    }
    .dialog {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-md);
      /* Never wider than the window it sits in: it wraps rather than scrolls sideways. */
      min-inline-size: 0;
    }
    .dialog p {
      margin: 0;
      color: var(--scs-color-text-secondary);
    }
    .hint {
      margin: calc(var(--scs-spacing-sm) * -1) 0 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-tertiary);
    }
    /* Named with the dialog, or its rule for every paragraph would win. */
    .dialog .hint.unmet,
    .dialog .failed {
      color: var(--scs-color-feedback-danger-text);
    }
    .buttons {
      display: flex;
      gap: var(--scs-spacing-sm);
    }
    /* The design's pair: the main button first, both the same width, across the dialog. */
    .buttons.even {
      display: grid;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    }
    /* One button alone is as wide as the one above it. */
    .buttons > button {
      flex: 1;
      justify-content: center;
      padding-inline: var(--scs-spacing-md);
      text-align: center;
    }
    .centre {
      text-align: center;
    }
    .done {
      text-align: center;
    }
    .done .ops-tick {
      align-self: center;
    }
    .done strong {
      font-size: var(--scs-text-heading-sm);
    }
  `,
  template: `
    <h1 data-testid="profile-title">{{ 'app.profile.title' | transloco }}</h1>
    <p class="lead">{{ 'app.profile.lead' | transloco }}</p>

    @if (state() !== 'ready') {
      <scs-state [state]="state() === 'loading' ? 'loading' : 'error'" (retry)="load()" />
    } @else if (profile(); as me) {
      <section data-testid="profile-account">
        <h2>{{ 'app.profile.account.title' | transloco }}</h2>
        <div class="account">
          <span class="initials" aria-hidden="true">{{ initials() }}</span>
          <div class="who">
            <strong dir="auto" data-testid="profile-name">{{ me.fullName }}</strong>
            <span data-testid="profile-role">{{ me.roleName }}</span>
          </div>
          <div class="fact">
            <strong dir="ltr" data-testid="profile-email">{{ me.email }}</strong>
            <span>{{ 'app.profile.account.email' | transloco }}</span>
          </div>
          <div class="fact">
            <strong dir="ltr" data-testid="profile-mobile">{{ me.mobile ?? '—' }}</strong>
            <span>{{ 'app.profile.account.mobile' | transloco }}</span>
          </div>
          <button type="button" scsButton="secondary" class="with-mark" data-testid="change-password" (click)="openChange()">
            <scs-icon name="key" [size]="16" />
            <span>{{ 'app.profile.account.changePassword' | transloco }}</span>
          </button>
        </div>
      </section>

      <section data-testid="profile-security">
        <h2>{{ 'app.profile.security.title' | transloco }}</h2>
        <div class="switch">
          <span
            class="track"
            role="switch"
            aria-checked="true"
            aria-disabled="true"
            data-testid="two-factor"
            [attr.aria-label]="'app.profile.security.twoFactor' | transloco"
          ></span>
          <div>
            <strong>{{ 'app.profile.security.twoFactor' | transloco }}</strong>
            <p data-testid="two-factor-locked">{{ 'app.profile.security.twoFactorLocked' | transloco }}</p>
          </div>
        </div>

        <div class="sessions-head">
          <h3>{{ 'app.profile.sessions.title' | transloco }}</h3>
          <p class="note" data-testid="one-session">{{ 'app.profile.sessions.one' | transloco }}</p>
        </div>
        <table data-testid="sessions">
          <thead>
            <tr>
              <th scope="col">{{ 'app.profile.sessions.device' | transloco }}</th>
              <th scope="col">{{ 'app.profile.sessions.state' | transloco }}</th>
              <th scope="col">{{ 'app.profile.sessions.action' | transloco }}</th>
            </tr>
          </thead>
          <tbody>
            @for (session of me.sessions; track session.startedAt) {
              <tr data-testid="session-row" [attr.data-current]="session.current">
                <td>
                  <span dir="auto">{{ session.device }}</span>
                  <small><bdi>{{ session.address ?? '—' }}</bdi></small>
                </td>
                <td>
                  @if (session.current) {
                    <span class="now">{{ 'app.profile.sessions.activeNow' | transloco }}</span>
                  } @else {
                    <span class="muted">
                      {{ 'app.profile.sessions.ended' | transloco }} {{ ago(session.endedAt ?? session.startedAt) }}
                    </span>
                  }
                </td>
                <td class="muted">
                  {{ session.current ? ('app.profile.sessions.current' | transloco) : '—' }}
                </td>
              </tr>
            }
          </tbody>
        </table>

        <div class="everywhere">
          <button type="button" class="ops-danger" data-testid="sign-out-everywhere" (click)="everywhereOpen.set(true)">
            <scs-icon name="sign-out" [size]="16" />
            <span>{{ 'app.profile.everywhere.button' | transloco }}</span>
          </button>
        </div>
      </section>
    }

    <scs-dialog [closable]="false" [width]="480" [heading]="'app.profile.everywhere.title' | transloco" [(open)]="everywhereOpen">
      <div class="dialog" data-testid="everywhere-dialog">
        <p>{{ 'app.profile.everywhere.line' | transloco }}</p>
        <!-- The button that acts is at the right in both languages, as the design's dialogs. -->
        <div class="buttons even">
          @if (leftToRight()) {
            <button type="button" scsButton="secondary" data-testid="everywhere-cancel" (click)="everywhereOpen.set(false)">
              {{ 'app.profile.cancel' | transloco }}
            </button>
          }
          <button type="button" class="ops-danger" data-testid="everywhere-confirm" (click)="signOutEverywhere()">
            {{ 'app.profile.everywhere.button' | transloco }}
          </button>
          @if (!leftToRight()) {
            <button type="button" scsButton="secondary" data-testid="everywhere-cancel" (click)="everywhereOpen.set(false)">
              {{ 'app.profile.cancel' | transloco }}
            </button>
          }
        </div>
      </div>
    </scs-dialog>

    <scs-dialog
      [closable]="false"
      [width]="changeStep() === 'done' ? 440 : 480"
      [heading]="changeStep() === 'done' ? '' : ('app.profile.account.changePassword' | transloco)"
      [label]="'app.profile.account.changePassword' | transloco"
      [open]="changeOpen()"
      (openChange)="$event ? null : closeChange()"
    >
      <div class="dialog" data-testid="change-dialog" [class.done]="changeStep() === 'done'">
        @switch (changeStep()) {
          @case ('code') {
            <p class="centre">
              {{ 'app.profile.change.codeSentTo' | transloco }} <bdi class="ops-address" data-testid="change-code-email">{{ codeStep()?.email }}</bdi>
            </p>
            @if (codeStep(); as step) {
              <app-code-step
                [step]="step"
                [problem]="codeProblem()"
                [busy]="busy()"
                [confirmLabel]="'app.profile.change.confirm' | transloco"
                (submitted)="confirmChange($event)"
                (resend)="resendChange()"
              />
            }
            <div class="buttons whole">
              <button type="button" scsButton="secondary" data-testid="change-cancel" (click)="closeChange()">
                {{ 'app.profile.cancel' | transloco }}
              </button>
            </div>
          }
          @case ('done') {
            <span class="ops-tick" aria-hidden="true"><scs-icon name="check" [size]="32" /></span>
            <strong data-testid="change-done">{{ 'app.profile.change.doneTitle' | transloco }}</strong>
            <p>{{ 'app.profile.change.doneLine' | transloco }}</p>
            <button type="button" scsButton data-testid="change-done-close" (click)="closeChange()">
              {{ 'app.profile.change.done' | transloco }}
            </button>
          }
          @default {
            <p>{{ 'app.profile.change.lead' | transloco }}</p>
            <scs-text-field
          [lead]="false"
              kind="password"
              testId="current-password"
              autocomplete="current-password"
              [label]="'app.profile.change.current' | transloco"
              [error]="currentWrong() ? ('app.profile.change.currentWrong' | transloco) : ''"
              [(value)]="current"
            />
            <scs-text-field
          [lead]="false"
              kind="password"
              testId="new-password"
              autocomplete="new-password"
              [label]="'app.profile.change.new' | transloco"
              [error]="sameAsCurrent() ? ('app.profile.change.same' | transloco) : ''"
              [(value)]="next"
            />
            <p class="hint" data-testid="change-hint" [class.unmet]="rulesUnmet()">
              {{ 'app.profile.change.hint' | transloco }}
            </p>
            <scs-text-field
          [lead]="false"
              kind="password"
              testId="confirm-password"
              autocomplete="new-password"
              [label]="'app.profile.change.confirmNew' | transloco"
              [error]="mismatch() ? ('app.profile.change.mismatch' | transloco) : ''"
              [(value)]="confirmation"
            />
            @if (changeFailed()) {
              <p class="failed" role="alert" data-testid="change-failed">{{ 'app.profile.change.failed' | transloco }}</p>
            }
            <!-- "Save" is at the right in both languages (frames 1851:2003 and 1853:2115). -->
            <div class="buttons even">
              @if (leftToRight()) {
                <button type="button" scsButton="secondary" data-testid="change-cancel" (click)="closeChange()">
                  {{ 'app.profile.cancel' | transloco }}
                </button>
              }
              <button type="button" scsButton data-testid="change-save" [busy]="busy()" (click)="beginChange()">
                {{ 'app.profile.change.save' | transloco }}
              </button>
              @if (!leftToRight()) {
                <button type="button" scsButton="secondary" data-testid="change-cancel" (click)="closeChange()">
                  {{ 'app.profile.cancel' | transloco }}
                </button>
              }
            </div>
          }
        }
      </div>
    </scs-dialog>
  `,
})
export class ProfilePage {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(OpsAuthService);
  private readonly language = inject(LanguageService);
  private readonly words = inject(TranslocoService);

  protected readonly state = signal<'loading' | 'ready' | 'failed'>('loading');
  protected readonly profile = signal<Profile | null>(null);
  protected readonly initials = computed(() => initialsOf(this.profile()?.fullName ?? ''));
  private readonly loadedAt = signal(Date.now());

  protected readonly everywhereOpen = signal(false);

  protected readonly changeOpen = signal(false);
  protected readonly changeStep = signal<'form' | 'code' | 'done'>('form');
  protected readonly current = signal('');
  protected readonly next = signal('');
  protected readonly confirmation = signal('');
  protected readonly currentWrong = signal(false);
  protected readonly sameAsCurrent = signal(false);
  protected readonly mismatch = signal(false);
  protected readonly rulesUnmet = signal(false);
  protected readonly changeFailed = signal(false);
  protected readonly busy = signal(false);
  protected readonly codeStep = signal<OpsCodeStep | null>(null);
  protected readonly codeProblem = signal<CodeProblem>(null);

  constructor() {
    void this.load();
  }

  protected ago(from: string): string {
    return agoLabel(from, this.loadedAt(), this.language.language());
  }

  protected leftToRight(): boolean {
    return this.language.language() === 'en';
  }

  protected async load(): Promise<void> {
    this.state.set('loading');
    try {
      this.profile.set(await firstValueFrom(this.http.get<Profile>('/v1/ops/profile')));
      this.loadedAt.set(Date.now());
      this.state.set('ready');
    } catch {
      this.state.set('failed');
    }
  }

  protected async signOutEverywhere(): Promise<void> {
    this.everywhereOpen.set(false);
    await this.auth.signOutEverywhere();
  }

  // ---------------------------------------------------------------- changing the password

  protected openChange(): void {
    this.current.set('');
    this.next.set('');
    this.confirmation.set('');
    this.currentWrong.set(false);
    this.sameAsCurrent.set(false);
    this.mismatch.set(false);
    this.rulesUnmet.set(false);
    this.changeFailed.set(false);
    this.codeProblem.set(null);
    this.codeStep.set(null);
    this.changeStep.set('form');
    this.changeOpen.set(true);
  }

  /** Closing while the code is awaited abandons the change (FR-045a). */
  protected closeChange(): void {
    if (this.changeStep() === 'code') {
      void firstValueFrom(this.http.post('/v1/ops/profile/password/cancel', null)).catch(() => undefined);
    }
    this.changeOpen.set(false);
  }

  protected async beginChange(): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.currentWrong.set(false);
    this.sameAsCurrent.set(false);
    this.changeFailed.set(false);
    this.mismatch.set(this.next() !== this.confirmation());
    this.rulesUnmet.set(
      passwordRules(this.next(), (key) => this.words.translate(key)).some((rule) => !rule.met),
    );
    if (this.mismatch() || this.rulesUnmet() || this.current() === '') {
      this.currentWrong.set(this.current() === '');
      return;
    }

    this.busy.set(true);
    try {
      this.codeStep.set(
        await firstValueFrom(
          this.http.post<OpsCodeStep>('/v1/ops/profile/password', {
            current: this.current(),
            password: this.next(),
          }),
        ),
      );
      this.changeStep.set('code');
    } catch (error) {
      const errors = error instanceof ScsProblemError ? error.problem.errors : undefined;
      if (errors?.['current'] !== undefined) {
        this.currentWrong.set(true);
      } else if (errors?.['password']?.includes('SAME_AS_CURRENT')) {
        this.sameAsCurrent.set(true);
      } else if (errors?.['password'] !== undefined) {
        this.rulesUnmet.set(true);
      } else if (!(error instanceof ScsProblemError && error.problem.code === OpsCodes.sessionEnded)) {
        this.changeFailed.set(true);
      }
    } finally {
      this.busy.set(false);
    }
  }

  protected async confirmChange(code: string): Promise<void> {
    if (this.busy() || code.length !== 6) {
      return;
    }
    this.busy.set(true);
    this.codeProblem.set(null);
    try {
      await firstValueFrom(this.http.post('/v1/ops/profile/password/confirm', { code }));
      this.changeStep.set('done');
    } catch (error) {
      this.refusedCode(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async resendChange(): Promise<void> {
    this.codeProblem.set(null);
    try {
      this.codeStep.set(
        await firstValueFrom(this.http.post<OpsCodeStep>('/v1/ops/profile/password/resend', null)),
      );
    } catch (error) {
      this.refusedCode(error);
    }
  }

  private refusedCode(error: unknown): void {
    const code = error instanceof ScsProblemError ? error.problem.code : '';
    if (code === OpsCodes.codeInvalid) {
      this.codeProblem.set('wrong');
    } else if (code === OpsCodes.resendLimit) {
      this.codeProblem.set('resendLimit');
    } else if (code === OpsCodes.codeNotResent || code === OpsCodes.resendTooEarly) {
      this.codeProblem.set('notResent');
    } else if (code === OpsCodes.stepExpired) {
      // The change is abandoned: the form starts again and says so.
      this.changeStep.set('form');
      this.changeFailed.set(true);
    } else {
      this.codeProblem.set('unexpected');
    }
  }
}
