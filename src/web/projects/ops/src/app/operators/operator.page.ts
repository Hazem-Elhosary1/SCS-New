import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LanguageService } from 'core';
import {
  ScsBannerComponent,
  ScsBreadcrumbComponent,
  ScsButtonDirective,
  ScsCardComponent,
  ScsDialogComponent,
  ScsEmptyStateComponent,
  ScsLinkDirective,
  ScsSkeletonComponent,
  ScsStatusMarkComponent,
  ScsTextareaComponent,
  ToastService,
} from 'ui';
import { OpsAccess } from '../access/ops-access.service';
import { opsDate, opsNumber, opsRelative } from '../i18n/ops-format';
import { activitySentence } from './activity-sentences';
import { STATE_TONES } from './operators-list.component';
import { ActivityLine, OperatorDetail, OperatorsService, TeamCodes, codeOf, initials } from './operators.service';

type Working = 'reactivate' | 'unlock' | 'suspend' | 'link' | null;

/**
 * One operator (spec 011, FR-025, FR-026, FR-041 to FR-046; frames `1658:70`/`1663:70`, the
 * suspend dialog `1708:70`/`1713:70` and the reset dialog `1720:112`/`1720:304`): who they are,
 * what their role holds, what they did lately, and the actions the platform says this viewer
 * may take — each shown only when it applies.
 */
@Component({
  selector: 'app-operator-page',
  imports: [
    RouterLink,
    TranslocoPipe,
    ScsBreadcrumbComponent,
    ScsCardComponent,
    ScsButtonDirective,
    ScsLinkDirective,
    ScsStatusMarkComponent,
    ScsSkeletonComponent,
    ScsEmptyStateComponent,
    ScsDialogComponent,
    ScsTextareaComponent,
    ScsBannerComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      margin-block-end: var(--scs-spacing-lg);
    }
    .who {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-md);
      min-inline-size: 0;
    }
    .initials {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 48px;
      block-size: 48px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
      font-size: var(--scs-text-body-lg);
      font-weight: 600;
    }
    h1 {
      margin: 0;
      font-size: var(--scs-text-title-sm);
      font-weight: 700;
      line-height: 1.4;
      overflow-wrap: anywhere;
    }
    .under {
      margin: 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
      overflow-wrap: anywhere;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-sm);
    }
    .actions button {
      min-block-size: 40px;
      padding-block: 0;
      padding-inline: var(--scs-spacing-md);
      font-size: var(--scs-text-body);
    }
    .danger-outline {
      border-color: var(--scs-color-feedback-danger-icon);
      color: var(--scs-color-feedback-danger-text);
    }
    scs-card {
      display: block;
      margin-block-end: var(--scs-spacing-md);
    }
    scs-banner {
      margin-block: var(--scs-spacing-md);
    }
    dl {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      margin: 0;
    }
    dl > div {
      padding-block: var(--scs-spacing-12);
      border-block-end: 1px solid var(--scs-color-border-subtle);
    }
    dt {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    dd {
      margin: var(--scs-spacing-2) 0 0;
      font-size: var(--scs-text-body);
      font-weight: 500;
      overflow-wrap: anywhere;
    }
    h3 {
      margin: var(--scs-spacing-md) 0 var(--scs-spacing-sm);
      font-size: var(--scs-text-label);
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
    .numbers {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: var(--scs-spacing-12);
    }
    .number {
      padding: var(--scs-spacing-12);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
    }
    .number strong {
      display: block;
      font-size: var(--scs-text-heading-sm);
    }
    .number span {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .activity {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .activity li {
      display: flex;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding-block: var(--scs-spacing-12);
      border-block-end: 1px solid var(--scs-color-border-subtle);
      font-size: var(--scs-text-body-sm);
    }
    .activity li:last-child {
      border-block-end: 0;
    }
    .activity time {
      flex: none;
      color: var(--scs-color-text-secondary);
    }
    .quiet {
      margin: 0;
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body-sm);
    }
    .dialog p {
      margin: 0 0 var(--scs-spacing-md);
      color: var(--scs-color-text-secondary);
      line-height: 1.7;
    }
    .kept {
      padding: var(--scs-spacing-12);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-feedback-warning-bg);
      color: var(--scs-color-feedback-warning-text);
    }
    .buttons {
      display: flex;
      justify-content: flex-end;
      gap: var(--scs-spacing-sm);
      margin-block-start: var(--scs-spacing-md);
    }
    .danger {
      border-color: var(--scs-color-feedback-danger-icon);
      background: var(--scs-color-feedback-danger-icon);
    }
    @media (max-width: 640px) {
      dl {
        grid-template-columns: minmax(0, 1fr);
      }
      .numbers {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .actions,
      .actions button {
        inline-size: 100%;
      }
    }
  `,
  template: `
    <scs-breadcrumb
      [label]="'app.operators.crumbs' | transloco"
      [crumbs]="[{ label: 'app.operators.title' | transloco, link: '/operators' }, { label: 'app.operators.detail.crumb' | transloco }]"
    />

    @switch (state()) {
      @case ('loading') {
        <scs-card><scs-skeleton data-testid="operator-loading" [rows]="6" [label]="'app.operators.detail.loading' | transloco" /></scs-card>
      }
      @case ('missing') {
        <scs-card>
          <scs-empty-state
            data-testid="operator-missing"
            mark="user"
            [title]="'app.operators.detail.notFoundTitle' | transloco"
            [text]="'app.operators.detail.notFoundText' | transloco"
          >
            <a scsLink routerLink="/operators">{{ 'app.operators.detail.back' | transloco }}</a>
          </scs-empty-state>
        </scs-card>
      }
      @case ('failed') {
        <scs-card>
          <scs-empty-state context="error" data-testid="operator-failed" [title]="'app.operators.detail.failed' | transloco">
            <button type="button" scsButton="secondary" data-testid="operator-retry" (click)="load()">
              {{ 'app.operators.retry' | transloco }}
            </button>
          </scs-empty-state>
        </scs-card>
      }
      @default {
        @if (operator(); as who) {
          <div class="head">
            <div class="who">
              <span class="initials" aria-hidden="true">{{ initialsOf(who.fullName) }}</span>
              <div>
                <h1 data-testid="operator-title"><bdi>{{ who.fullName }}</bdi></h1>
                <p class="under" data-testid="operator-under">
                  <bdi>{{ access.nameOfRole(who.role) }}</bdi> · <bdi dir="ltr">{{ who.email }}</bdi>
                </p>
              </div>
            </div>
            <div class="actions" data-testid="operator-actions">
              @if (who.can.edit) {
                <button type="button" scsButton="secondary" data-testid="operator-edit" (click)="edit(who)">
                  {{ 'app.operators.detail.edit' | transloco }}
                </button>
              }
              @if (who.can.sendLink) {
                <button type="button" scsButton="secondary" data-testid="operator-reset" (click)="open('link')">
                  {{ 'app.operators.detail.reset' | transloco }}
                </button>
              }
              @if (who.can.unlock) {
                <button type="button" scsButton="secondary" data-testid="operator-unlock" [busy]="working() === 'unlock'" (click)="unlock(who)">
                  {{ (working() === 'unlock' ? 'app.operators.detail.unlocking' : 'app.operators.detail.unlock') | transloco }}
                </button>
              }
              @if (who.can.reactivate) {
                <button type="button" scsButton data-testid="operator-reactivate" [busy]="working() === 'reactivate'" (click)="reactivate(who)">
                  {{ (working() === 'reactivate' ? 'app.operators.detail.reactivating' : 'app.operators.detail.reactivate') | transloco }}
                </button>
              }
              @if (who.can.suspend) {
                <button type="button" scsButton="secondary" class="danger-outline" data-testid="operator-suspend" (click)="open('suspend')">
                  {{ 'app.operators.detail.suspend' | transloco }}
                </button>
              }
            </div>
          </div>

          @if (who.locked) {
            <scs-banner tone="warning" data-testid="operator-locked">{{ 'app.operators.detail.locked' | transloco }}</scs-banner>
          }

          <scs-card data-testid="operator-info" [heading]="'app.operators.detail.info' | transloco">
            <dl>
              <div>
                <dt>{{ 'app.operators.detail.role' | transloco }}</dt>
                <dd data-testid="info-role"><bdi>{{ access.nameOfRole(who.role) }}</bdi></dd>
              </div>
              <div>
                <dt>{{ 'app.operators.detail.joined' | transloco }}</dt>
                <dd data-testid="info-joined">{{ date(who.createdAtUtc) }}</dd>
              </div>
              <div>
                <dt>{{ 'app.operators.detail.lastSignIn' | transloco }}</dt>
                <dd data-testid="info-last">
                  {{ who.lastSignInAtUtc === null ? ('app.operators.never' | transloco) : relative(who.lastSignInAtUtc) }}
                </dd>
              </div>
              <div>
                <dt>{{ 'app.operators.detail.twoStep' | transloco }}</dt>
                <dd data-testid="info-two-step">
                  {{ (who.twoStep ? 'app.operators.twoStep.on' : 'app.operators.twoStep.off') | transloco }}
                </dd>
              </div>
              <div>
                <dt>{{ 'app.operators.detail.mobile' | transloco }}</dt>
                <dd data-testid="info-mobile"><bdi dir="ltr">{{ who.mobile ?? '—' }}</bdi></dd>
              </div>
              <div>
                <dt>{{ 'app.operators.detail.department' | transloco }}</dt>
                <dd data-testid="info-department">
                  {{ who.department ? ('app.operators.department.' + who.department | transloco) : '—' }}
                </dd>
              </div>
              <div>
                <dt>{{ 'app.operators.detail.jobTitle' | transloco }}</dt>
                <dd data-testid="info-job"><bdi>{{ who.jobTitle ?? '—' }}</bdi></dd>
              </div>
              <div>
                <dt>{{ 'app.operators.detail.addedBy' | transloco }}</dt>
                <dd data-testid="info-added-by">
                  @if (who.invitedBy) {
                    <bdi>{{ who.invitedBy.name }}</bdi>
                  } @else {
                    {{ (who.firstOperator ? 'app.operators.detail.systemFirst' : 'app.operators.detail.system') | transloco }}
                  }
                </dd>
              </div>
            </dl>

            <h3>{{ 'app.operators.detail.permissions' | transloco }}</h3>
            <ul class="marks" data-testid="operator-permissions">
              @for (code of who.permissions; track code) {
                <li><scs-status-mark tone="brand" [dot]="false">{{ access.nameOfPermission(code) }}</scs-status-mark></li>
              }
            </ul>
          </scs-card>

          <scs-card data-testid="operator-performance" [heading]="'app.operators.detail.performance' | transloco">
            <div class="numbers">
              @for (card of who.performance; track card.key) {
                <div class="number" [attr.data-testid]="'performance-' + card.key">
                  <strong><bdi dir="ltr">{{ number(card.value) }}</bdi></strong>
                  <span>{{ 'app.operators.detail.' + card.key | transloco }}</span>
                </div>
              }
            </div>
          </scs-card>

          <scs-card data-testid="operator-activity" [heading]="'app.operators.detail.activity' | transloco">
            @if (who.activity.length === 0) {
              <p class="quiet" data-testid="activity-none">{{ 'app.operators.detail.noActivity' | transloco }}</p>
            } @else {
              <ul class="activity">
                @for (line of who.activity; track $index) {
                  <li data-testid="activity-line">
                    <span><bdi>{{ sentence(line) }}</bdi></span>
                    <time [attr.datetime]="line.atUtc">{{ relative(line.atUtc) }}</time>
                  </li>
                }
              </ul>
            }
          </scs-card>

          <scs-dialog
            class="dialog"
            [heading]="'app.operators.suspend.title' | transloco"
            [width]="440"
            [open]="dialog() === 'suspend'"
            (openChange)="$event || close()"
          >
            <div class="dialog">
              <p data-testid="suspend-text">{{ 'app.operators.suspend.text' | transloco: { name: who.fullName } }}</p>
              <p class="kept" data-testid="suspend-kept">{{ 'app.operators.suspend.kept' | transloco }}</p>
              <scs-textarea
                testId="suspend-reason"
                [label]="'app.operators.suspend.reason' | transloco"
                [placeholder]="'app.operators.suspend.reasonPlaceholder' | transloco"
                [max]="500"
                [rows]="2"
                [(value)]="reason"
              />
              @if (problem(); as words) {
                <scs-banner tone="danger" data-testid="suspend-problem">{{ words | transloco }}</scs-banner>
              }
              <div class="buttons">
                <button type="button" scsButton="secondary" data-testid="suspend-cancel" (click)="close()">
                  {{ 'app.operators.cancel' | transloco }}
                </button>
                <button
                  type="button"
                  scsButton
                  class="danger"
                  data-testid="suspend-confirm"
                  [busy]="working() === 'suspend'"
                  [disabled]="working() === 'suspend'"
                  (click)="suspend(who)"
                >
                  {{ (working() === 'suspend' ? 'app.operators.suspend.working' : 'app.operators.suspend.action') | transloco }}
                </button>
              </div>
            </div>
          </scs-dialog>

          <scs-dialog
            [heading]="'app.operators.link.title' | transloco"
            [width]="440"
            [open]="dialog() === 'link'"
            (openChange)="$event || close()"
          >
            <div class="dialog">
              <p data-testid="link-text">{{ 'app.operators.link.text' | transloco: { name: who.fullName, email: who.email } }}</p>
              @if (problem(); as words) {
                <scs-banner tone="danger" data-testid="link-problem">{{ words | transloco }}</scs-banner>
              }
              <div class="buttons">
                <button type="button" scsButton="secondary" data-testid="link-cancel" (click)="close()">
                  {{ 'app.operators.cancel' | transloco }}
                </button>
                <button
                  type="button"
                  scsButton
                  data-testid="link-confirm"
                  [busy]="working() === 'link'"
                  [disabled]="working() === 'link'"
                  (click)="sendLink(who)"
                >
                  {{ (working() === 'link' ? 'app.operators.link.working' : 'app.operators.link.action') | transloco }}
                </button>
              </div>
            </div>
          </scs-dialog>
        }
      }
    }
  `,
})
export class OperatorPage {
  private readonly team = inject(OperatorsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly words = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  protected readonly access = inject(OpsAccess);

  protected readonly tones = STATE_TONES;
  protected readonly state = signal<'loading' | 'missing' | 'failed' | 'ready'>('loading');
  protected readonly operator = signal<OperatorDetail | null>(null);
  protected readonly dialog = signal<'suspend' | 'link' | null>(null);
  protected readonly working = signal<Working>(null);
  protected readonly problem = signal('');
  protected readonly reason = signal('');

  private readonly id = computed(() => this.route.snapshot.paramMap.get('id') ?? '');
  private now = Date.now();

  constructor() {
    void this.load();
  }

  protected async load(): Promise<void> {
    if (this.operator() === null) {
      this.state.set('loading');
    }
    try {
      this.operator.set(await this.team.read(this.id()));
      this.now = Date.now();
      this.state.set('ready');
    } catch (error) {
      this.operator.set(null);
      this.state.set(codeOf(error) === TeamCodes.notFound ? 'missing' : 'failed');
    }
  }

  protected initialsOf(name: string): string {
    return initials(name);
  }

  protected date(value: string): string {
    return opsDate(value, this.language.language());
  }

  protected relative(value: string): string {
    return opsRelative(value, Math.max(this.now, Date.parse(value)), this.language.language());
  }

  protected number(value: number): string {
    return opsNumber(value, this.language.language());
  }

  protected sentence(line: ActivityLine): string {
    return activitySentence(
      line,
      (key, values) => this.words.translate(key, values),
      (role) => this.access.nameOfRole(role),
    );
  }

  protected edit(who: OperatorDetail): void {
    void this.router.navigate(['/operators', who.id, 'edit']);
  }

  protected open(dialog: 'suspend' | 'link'): void {
    this.problem.set('');
    this.reason.set('');
    this.dialog.set(dialog);
  }

  protected close(): void {
    if (this.working() === null) {
      this.dialog.set(null);
    }
  }

  protected async suspend(who: OperatorDetail): Promise<void> {
    await this.act('suspend', () => this.team.suspend(who.id, this.reason()), 'app.operators.suspend.done', (code) =>
      code === TeamCodes.lastSuperAdmin ? 'app.operators.suspend.last' : 'app.operators.suspend.failed',
    );
  }

  protected async sendLink(who: OperatorDetail): Promise<void> {
    await this.act('link', () => this.team.sendLink(who.id), 'app.operators.link.done', (code) =>
      code === TeamCodes.linkTooSoon ? 'app.operators.link.tooSoon' : 'app.operators.link.failed',
    );
  }

  protected async reactivate(who: OperatorDetail): Promise<void> {
    await this.act('reactivate', () => this.team.reactivate(who.id), 'app.operators.detail.reactivated', () =>
      'app.operators.detail.reactivateFailed',
    );
  }

  protected async unlock(who: OperatorDetail): Promise<void> {
    await this.act('unlock', () => this.team.unlock(who.id), 'app.operators.detail.unlocked', () => 'app.operators.detail.unlockFailed');
  }

  /**
   * One action on the account: its button works, the page is read again whatever happened —
   * a refusal usually means the account is not what this page showed — and the outcome is said
   * in the dialog that asked, or as a passing notice when there was none.
   */
  private async act(kind: Exclude<Working, null>, run: () => Promise<void>, done: string, refusal: (code: string) => string): Promise<void> {
    this.working.set(kind);
    this.problem.set('');
    try {
      await run();
      this.dialog.set(null);
      this.toasts.show(this.words.translate(done));
      await this.load();
    } catch (error) {
      const words = refusal(codeOf(error));
      if (this.dialog() === null) {
        this.toasts.show(this.words.translate(words), '', 'danger');
        await this.load();
      } else {
        this.problem.set(words);
      }
    } finally {
      this.working.set(null);
    }
  }
}
