import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  LanguageService,
  OnlineService,
  PlatformStatusService,
  RuntimeConfigService,
  ScsDatePipe,
  ScsMoneyPipe,
  VersionCheckService,
} from 'core';
import { SCS_STATES, ScsState, ScsStateComponent } from '../state/scs-state.component';

/**
 * The one page each front end ships in spec 001. It shows that the app started and whether
 * the platform can serve it (FR-003), and it demonstrates the conventions later screens
 * inherit: language and direction, Cairo time and Egyptian pounds, and the six screen states.
 * Real screens replace it in later specs.
 */
@Component({
  selector: 'scs-placeholder-panel',
  imports: [TranslocoPipe, ScsDatePipe, ScsMoneyPipe, ScsStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    button.plain {
      padding: var(--scs-spacing-xs) var(--scs-spacing-12);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-sm);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-link);
      font: inherit;
      cursor: pointer;
    }
    button.plain[aria-pressed='true'] {
      border-color: var(--scs-color-brand-default);
      font-weight: 600;
    }
    dt {
      color: var(--scs-color-text-secondary);
    }
  `,
  template: `
    <main class="mx-auto max-w-xl p-6" data-testid="placeholder">
      <header class="flex items-start justify-between gap-4">
        <div>
          <h1 class="text-2xl font-semibold" data-testid="app-name">
            {{ appNameKey() | transloco }}
          </h1>
          <p class="mt-1 text-sm opacity-70" data-testid="environment">
            <bdi>{{ environment }}</bdi>
          </p>
          @if (appVersion(); as appVersion) {
            <p class="text-sm opacity-70">
              {{ 'ui.platform.detail.appVersion' | transloco }}:
              <bdi data-testid="app-version">{{ appVersion }}</bdi>
            </p>
          }
        </div>
        <button
          type="button"
          class="plain"
          data-testid="language-switch"
          [attr.aria-label]="'ui.language.switch.label' | transloco"
          (click)="language.toggle()"
        >
          {{ 'ui.language.switch.target' | transloco }}
        </button>
      </header>

      <section class="mt-6" aria-live="polite">
        @if (!online()) {
          <scs-state state="offline" />
        } @else if (checking()) {
          <scs-state state="loading" [message]="'ui.platform.status.checking' | transloco" />
        } @else if (problem(); as problem) {
          <scs-state
            state="error"
            data-testid="platform-unreachable"
            [message]="problem.title || ('ui.platform.status.unreachable' | transloco)"
            [reference]="problem.traceId"
            (retry)="check()"
          />
        } @else if (platform(); as platform) {
          <p data-testid="platform-reachable">{{ 'ui.platform.status.reachable' | transloco }}</p>
          <dl class="mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <dt>{{ 'ui.platform.detail.version' | transloco }}</dt>
            <dd data-testid="platform-version">
              <bdi>{{ platform.version }}</bdi>
            </dd>
            <dt>{{ 'ui.platform.detail.environment' | transloco }}</dt>
            <dd data-testid="platform-environment">
              <bdi>{{ platform.environment }}</bdi>
            </dd>
            <dt>{{ 'ui.sample.format.date' | transloco }}</dt>
            <dd data-testid="sample-date">{{ platform.builtAt | scsDate }}</dd>
            <dt>{{ 'ui.sample.format.amount' | transloco }}</dt>
            <dd data-testid="sample-amount">{{ sampleAmount | scsMoney }}</dd>
          </dl>
        }
      </section>

      @if (showDemo) {
        <section class="mt-8" data-testid="state-demo">
          <h2 class="text-base font-semibold">{{ 'ui.demo.states.title' | transloco }}</h2>
          <div class="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              class="plain"
              data-testid="force-none"
              [attr.aria-pressed]="forced() === null"
              (click)="forced.set(null)"
            >
              {{ 'ui.demo.states.content' | transloco }}
            </button>
            @for (option of demoStates; track option.state) {
              <button
                type="button"
                class="plain"
                [attr.data-testid]="'force-' + option.state"
                [attr.aria-pressed]="forced() === option.state"
                (click)="forced.set(option.state)"
              >
                {{ option.label | transloco }}
              </button>
            }
          </div>
          @if (forced(); as forced) {
            <scs-state class="mt-4" data-testid="forced-state" [state]="forced" />
          }
        </section>
      }
    </main>
  `,
})
export class PlaceholderPanelComponent implements OnInit {
  private readonly status = inject(PlatformStatusService);
  protected readonly language = inject(LanguageService);
  protected readonly online = inject(OnlineService).online;

  /** Translation key of the app's name, from the app's own translation file. */
  readonly appNameKey = input.required<string>();

  readonly environment = inject(RuntimeConfigService).value.environment;

  /** The state demonstration is never shown to real users. */
  protected readonly showDemo = this.environment !== 'production';
  protected readonly forced = signal<ScsState | null>(null);
  protected readonly demoStates = SCS_STATES.map((state) => ({
    state,
    label: `ui.demo.states.${DEMO_LABELS[state]}`,
  }));

  protected readonly sampleAmount = '1250.50';

  /** This front end's own release, from its `version.json`; absent on a development server. */
  private readonly versions = inject(VersionCheckService);
  protected readonly appVersion = computed(() => this.versions.current()?.version ?? null);

  protected readonly checking = computed(() => this.status.status().state === 'checking');

  protected readonly platform = computed(() => {
    const current = this.status.status();
    return current.state === 'reachable' ? current.platform : null;
  });

  protected readonly problem = computed(() => {
    const current = this.status.status();
    return current.state === 'unreachable' ? current.problem : null;
  });

  constructor() {
    // A check that failed while offline is tried again by itself when the connection returns.
    effect(() => {
      if (this.online() && untracked(this.problem) !== null) {
        this.check();
      }
    });
  }

  ngOnInit(): void {
    this.check();
  }

  protected check(): void {
    void this.status.check();
  }
}

const DEMO_LABELS: Record<ScsState, string> = {
  loading: 'loading',
  empty: 'empty',
  error: 'error',
  offline: 'offline',
  'view-only': 'viewOnly',
  'not-in-plan': 'notInPlan',
};
