import { ChangeDetectionStrategy, Component, booleanAttribute, input, output } from '@angular/core';
import { ScsIconComponent } from '../icon/icon.component';

/** What an insight card is showing. */
export type ScsInsightState = 'ready' | 'loading' | 'empty' | 'error';

/**
 * A finding to read and, perhaps, act on (design: AI — AI Insight Card, `401:19` / `408:39`, and the
 * suggestion card's states `2421:88`; the clinic's Insight Card `4309:491`): its heading — with the AI
 * mark when the finding is the AI's (`ai`), plain when AI is off — a big number when there is one
 * (the clinic's), its words, a line of what it costs (`impact`, red when a loss), and its actions,
 * projected. While loading it is grey shapes; with nothing to say it says so; when it failed, its red
 * heading and "Retry". Each look draws it through `--scs-insight-*`.
 */
@Component({
  selector: 'scs-insight-card',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-state]': 'state()', '[class.ai]': 'ai()', '[attr.aria-busy]': 'state() === "loading" ? "true" : null' },
  styles: `
    :host {
      display: grid;
      align-content: start;
      gap: 10px;
      box-sizing: border-box;
      padding: var(--scs-insight-pad, 20px);
      border: 1px solid var(--scs-insight-border, var(--scs-color-border-subtle, var(--scs-color-border-default)));
      border-radius: var(--scs-insight-radius, 16px);
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-insight-shadow, none);
    }
    .head {
      display: flex;
      align-items: center;
      gap: 10px;
      margin: 0;
      font-size: 13px;
      font-weight: 700;
      color: var(--scs-color-text-primary);
    }
    :host(.ai) .head {
      color: var(--scs-insight-head, var(--scs-color-text-primary));
    }
    .mark {
      display: grid;
      place-items: center;
      inline-size: 24px;
      block-size: 24px;
      border-radius: 50%;
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
    }
    :host([data-state='error']) .head {
      color: var(--scs-color-feedback-danger-icon);
      font-size: 14px;
      font-weight: 600;
    }
    :host([data-state='empty']) .head {
      color: var(--scs-color-text-primary);
      font-size: 14px;
      font-weight: 600;
    }
    .value {
      margin: 0;
      font-size: 34px;
      font-weight: 700;
      line-height: 1.2;
      color: var(--scs-color-text-primary);
    }
    .text {
      margin: 0;
      font-size: 13px;
      line-height: 1.6;
      color: var(--scs-insight-text, var(--scs-color-text-primary));
    }
    :host([data-state='empty']) .text,
    :host([data-state='error']) .text {
      font-size: 12px;
      color: var(--scs-color-text-tertiary);
    }
    .impact {
      margin: 0;
      font-size: 13px;
      font-weight: 600;
      color: var(--scs-color-text-secondary);
    }
    .impact.loss {
      color: var(--scs-color-feedback-danger-text);
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
    }
    .actions:empty {
      display: none;
    }
    .shape {
      border-radius: 6px;
      background: var(--scs-insight-shape, var(--scs-color-bg-surface-secondary));
    }
  `,
  template: `
    @if (state() === 'loading') {
      <div class="head"><span class="shape" style="inline-size: 24px; block-size: 24px"></span><span class="shape" style="inline-size: 120px; block-size: 14px"></span></div>
      <span class="shape" style="block-size: 12px"></span>
      <span class="shape" style="inline-size: 80%; block-size: 12px"></span>
      <span class="shape" style="inline-size: 130px; block-size: 32px; margin-block-start: 8px"></span>
    } @else {
      <p class="head" data-testid="insight-heading">
        @if (ai() && state() === 'ready') {
          <span class="mark" aria-hidden="true"><scs-icon name="sparkle" [size]="13" /></span>
        }
        {{ heading() }}
      </p>
      @if (value() && state() === 'ready') {
        <p class="value"><bdi dir="ltr">{{ value() }}</bdi></p>
      }
      @if (text()) {
        <p class="text" data-testid="insight-text">{{ text() }}</p>
      }
      @if (impact() && state() === 'ready') {
        <p class="impact" [class.loss]="loss()">{{ impact() }}</p>
      }
      <div class="actions">
        @if (state() === 'error' && retryLabel()) {
          <button type="button" class="scs-link" data-testid="insight-retry" (click)="retry.emit()">{{ retryLabel() }}</button>
        } @else if (state() === 'ready') {
          <ng-content />
        }
      </div>
    }
  `,
})
export class ScsInsightCardComponent {
  readonly state = input<ScsInsightState>('ready');
  /** Already translated: "AI Insight", "Suggestion", "Nothing to suggest right now". */
  readonly heading = input('');
  /** The finding is the AI's: its mark before the heading. Off when AI is turned off. */
  readonly ai = input(false, { transform: booleanAttribute });
  /** A big number (the clinic's insight). Already written in the page's digits. */
  readonly value = input('');
  readonly text = input('');
  /** What it costs or brings: "EGP 1,200 at their service". */
  readonly impact = input('');
  readonly loss = input(false, { transform: booleanAttribute });
  readonly retryLabel = input('');

  readonly retry = output<void>();
}

/** Who wrote a message. */
export type ScsMessageKind = 'bot' | 'user' | 'incoming' | 'outgoing' | 'system';

/**
 * One message of a conversation (design: AI — Message Bubble, `404:10` / `409:29`; the clinic's Chat
 * Bubble `4252:63119`): a message to the person (the assistant's or someone's) at the start, the
 * person's own at the end, a note of the conversation in the middle. It can carry the sender's name,
 * the assistant's tag, its time and whether it was sent, and an attached picture's name. Each look
 * draws it through `--scs-bubble-*`: the clinic's pale bubbles by default, the operators' purple one
 * for what the person wrote.
 */
@Component({
  selector: 'scs-message-bubble',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-kind]': 'kind()', role: 'listitem' },
  styles: `
    :host {
      display: grid;
      justify-items: start;
      gap: 4px;
    }
    :host([data-kind='user']),
    :host([data-kind='outgoing']) {
      justify-items: end;
    }
    :host([data-kind='system']) {
      justify-items: center;
    }
    .sender {
      font-size: 12px;
      font-weight: 500;
      color: var(--scs-color-text-secondary);
    }
    .bubble {
      display: grid;
      gap: 4px;
      box-sizing: border-box;
      max-inline-size: var(--scs-bubble-width, 75%);
      padding: var(--scs-bubble-pad, 8px 12px);
      border-radius: var(--scs-bubble-radius, 10px);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-primary);
      font-size: 13px;
      line-height: 1.55;
    }
    :host([data-kind='bot']) .bubble {
      border-end-start-radius: var(--scs-bubble-tail, var(--scs-bubble-radius, 10px));
      background: var(--scs-bubble-bot-bg, var(--scs-color-brand-subtle));
    }
    :host([data-kind='incoming']) .bubble {
      border-end-start-radius: var(--scs-bubble-tail, var(--scs-bubble-radius, 10px));
    }
    :host([data-kind='user']) .bubble,
    :host([data-kind='outgoing']) .bubble {
      border-end-end-radius: var(--scs-bubble-tail, var(--scs-bubble-radius, 10px));
      background: var(--scs-bubble-mine-bg, var(--scs-color-brand-subtle));
      color: var(--scs-bubble-mine-text, var(--scs-color-text-primary));
    }
    :host([data-kind='system']) .bubble {
      max-inline-size: 90%;
      padding: 4px 12px;
      color: var(--scs-color-text-tertiary);
      font-size: 11px;
      font-weight: 500;
    }
    .tag {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 10px;
      font-weight: 600;
      color: var(--scs-color-brand-active);
    }
    .picture {
      display: grid;
      place-items: center;
      gap: 8px;
      box-sizing: border-box;
      inline-size: 180px;
      max-inline-size: 100%;
      padding: 24px 8px 8px;
      border-radius: 8px;
      background: var(--scs-color-feedback-info-bg);
      color: var(--scs-color-feedback-info-icon);
      font-size: 10px;
      font-weight: 500;
    }
    .meta {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 10px;
      color: var(--scs-color-text-tertiary);
    }
    .failed {
      font-size: 12px;
      font-weight: 500;
      color: var(--scs-color-feedback-danger-text);
    }
  `,
  template: `
    @if (sender()) {
      <span class="sender">{{ sender() }}</span>
    }
    <div class="bubble">
      @if (tag()) {
        <span class="tag"><scs-icon name="sparkle" [size]="12" />{{ tag() }}</span>
      }
      @if (attachment()) {
        <span class="picture"><scs-icon name="image" [size]="32" aria-hidden="true" />{{ attachment() }}</span>
      }
      <span><ng-content /></span>
    </div>
    @if (time()) {
      <span class="meta"><bdi dir="ltr">{{ time() }}</bdi></span>
    }
    @if (failedLabel()) {
      <span class="failed" role="alert">{{ failedLabel() }}</span>
    }
  `,
})
export class ScsMessageBubbleComponent {
  readonly kind = input<ScsMessageKind>('incoming');
  /** Already translated: who wrote it. */
  readonly sender = input('');
  /** The assistant's tag: "Booking assistant". */
  readonly tag = input('');
  /** Already written: "10:32". */
  readonly time = input('');
  /** An attached picture's name. */
  readonly attachment = input('');
  /** Already translated: "Not sent · Try again". */
  readonly failedLabel = input('');
}
