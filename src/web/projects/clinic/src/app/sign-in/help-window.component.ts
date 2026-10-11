import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  model,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService, ScsProblemError, SupportChannels, SupportService } from 'core';
import { ScsDialogComponent, ScsIconComponent, ScsStateComponent } from 'ui';

/**
 * The help window of every sign-in screen (BRD B-5-9): the support hours and three channels,
 * each row a link that leaves the application — a call, a WhatsApp conversation, an email. It
 * has no form and sends nothing. The values are platform settings.
 */
@Component({
  selector: 'app-help-window',
  imports: [TranslocoPipe, ScsDialogComponent, ScsIconComponent, ScsStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .hours {
      margin: 0 0 var(--scs-spacing-md);
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body-sm);
      line-height: 1.7;
    }
    ul {
      margin: 0;
      padding: 0;
      list-style: none;
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-sm);
    }
    a {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      padding: var(--scs-spacing-12) var(--scs-spacing-md);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-primary);
      text-decoration: none;
    }
    a:hover {
      background: var(--scs-color-border-default);
    }
    a:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
    }
    .text {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-2);
      min-inline-size: 0;
    }
    .value {
      font-size: var(--scs-text-body-lg);
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .label {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .mark {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 40px;
      block-size: 40px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-brand-default);
    }
    .mark.chat {
      color: var(--scs-color-feedback-success-icon);
    }
  `,
  template: `
    <scs-dialog [heading]="'app.help.title' | transloco" [(open)]="open">
      @if (channels(); as channels) {
        <p class="hours" data-testid="support-hours">{{ hours() }}</p>
        <ul>
          <li>
            <a data-testid="support-hotline" [href]="'tel:' + channels.hotline">
              <span class="text">
                <bdi class="value">{{ channels.hotline }}</bdi>
                <span class="label">{{ 'app.help.hotline' | transloco }}</span>
              </span>
              <span class="mark"><scs-icon name="phone" [size]="20" /></span>
            </a>
          </li>
          <li>
            <a
              data-testid="support-whatsapp"
              target="_blank"
              rel="noopener noreferrer"
              [href]="'https://wa.me/' + channels.whatsapp"
            >
              <span class="text">
                <bdi class="value">+{{ channels.whatsapp }}</bdi>
                <span class="label">{{ 'app.help.whatsapp' | transloco }}</span>
              </span>
              <span class="mark chat"><scs-icon name="chat" [size]="20" /></span>
            </a>
          </li>
          <li>
            <a data-testid="support-email" [href]="'mailto:' + channels.email">
              <span class="text">
                <bdi class="value">{{ channels.email }}</bdi>
                <span class="label">{{ 'app.help.email' | transloco }}</span>
              </span>
              <span class="mark"><scs-icon name="mail" [size]="20" /></span>
            </a>
          </li>
        </ul>
      } @else if (failure(); as failure) {
        <scs-state
          state="error"
          [message]="failure.title"
          [reference]="failure.traceId"
          (retry)="load()"
        />
      } @else {
        <scs-state state="loading" />
      }
    </scs-dialog>
  `,
})
export class HelpWindowComponent {
  private readonly support = inject(SupportService);
  private readonly language = inject(LanguageService);

  readonly open = model(false);

  protected readonly channels = signal<SupportChannels | null>(null);
  protected readonly failure = signal<{ title: string; traceId: string } | null>(null);

  protected readonly hours = computed(() => {
    const channels = this.channels();
    return channels === null
      ? ''
      : this.language.language() === 'ar'
        ? channels.hoursAr
        : channels.hoursEn;
  });

  constructor() {
    // Asked for when the window is first opened, not when the page loads.
    effect(() => {
      if (this.open() && this.channels() === null) {
        this.load();
      }
    });
  }

  protected load(): void {
    this.failure.set(null);
    this.support.load().then(
      (channels) => this.channels.set(channels),
      (error: unknown) =>
        this.failure.set(
          error instanceof ScsProblemError
            ? { title: error.problem.title, traceId: error.problem.traceId }
            : { title: '', traceId: '' },
        ),
    );
  }
}
