import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsIconComponent, ScsMessageBubbleComponent } from 'ui';

/**
 * The AI mark, the operators console's own (design: AI — AI Badge, `399:13` / `408:21`): a pale purple
 * pill with a sparkle and "AI", small or medium. It is shown only where AI is on.
 */
@Component({
  selector: 'app-ai-badge',
  imports: [ScsIconComponent, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-size]': 'size()' },
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      box-sizing: border-box;
      block-size: 22px;
      padding-inline: 8px 10px;
      border-radius: 999px;
      background: var(--scs-ai-tint);
      color: var(--scs-ai-ink);
      font-size: 11px;
      font-weight: 700;
    }
    :host([data-size='md']) {
      block-size: 26px;
      padding-inline: 10px 12px;
      font-size: 12px;
    }
    scs-icon {
      color: var(--scs-color-brand-default);
    }
  `,
  template: `<scs-icon name="sparkle" [size]="size() === 'md' ? 13 : 11" /><bdi dir="ltr">{{ 'app.ai.badge' | transloco }}</bdi>`,
})
export class AiBadgeComponent {
  readonly size = input<'sm' | 'md'>('sm');
}

/**
 * The round Ask-AI button, the operators console's own (design: AI — FAB, `402:3` / `409:12`): a
 * purple gradient circle with a sparkle, named by `label`; it opens the chat.
 */
@Component({
  selector: 'app-ask-ai-button',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    button {
      display: grid;
      place-items: center;
      inline-size: 56px;
      block-size: 56px;
      border: 0;
      border-radius: 50%;
      background: var(--scs-ai-gradient);
      box-shadow: 0 8px 20px rgb(94 39 206 / 30%);
      color: #ffffff;
      cursor: pointer;
    }
    button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 3px;
    }
  `,
  template: `
    <button type="button" data-testid="ask-ai-button" [attr.aria-label]="label()" [attr.aria-expanded]="expanded()" (click)="pressed.emit()">
      <scs-icon name="sparkle" [size]="22" />
    </button>
  `,
})
export class AskAiButtonComponent {
  /** Already translated: "Ask AI". */
  readonly label = input.required<string>();
  readonly expanded = input(false);
  readonly pressed = output<void>();
}

/**
 * The floating Ask-AI field, the operators console's own (design: AI — Floating Input, `402:6` /
 * `409:15`): a round field with a sparkle at its start and a send button at its end. Enter or the
 * button hands the question to the page (`asked`); nothing is sent anywhere from here.
 */
@Component({
  selector: 'app-ask-ai-field',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    form {
      display: flex;
      align-items: center;
      gap: 10px;
      box-sizing: border-box;
      block-size: 52px;
      padding: 10px;
      border: 1px solid var(--scs-color-border-control, var(--scs-color-border-default));
      border-radius: 26px;
      background: var(--scs-color-bg-surface);
      box-shadow: 0 6px 16px rgb(26 32 36 / 8%);
    }
    form:focus-within {
      border-color: var(--scs-color-brand-default);
    }
    .mark {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 32px;
      block-size: 32px;
      border-radius: 50%;
      background: var(--scs-ai-tint);
      color: var(--scs-color-brand-default);
    }
    input {
      flex: 1;
      min-inline-size: 0;
      border: 0;
      background: none;
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: 14px;
      outline: none;
    }
    input::placeholder {
      color: var(--scs-color-text-placeholder);
    }
    button {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 32px;
      block-size: 32px;
      border: 0;
      border-radius: 50%;
      background: var(--scs-color-brand-default);
      color: #ffffff;
      cursor: pointer;
    }
    button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
  `,
  template: `
    <form (submit)="$event.preventDefault(); ask()">
      <span class="mark" aria-hidden="true"><scs-icon name="sparkle" [size]="16" /></span>
      <input type="text" data-testid="ask-ai-input" [attr.aria-label]="label()" [placeholder]="label()" [value]="question()" (input)="question.set($any($event.target).value)" />
      <button type="submit" data-testid="ask-ai-send" [attr.aria-label]="sendLabel()"><scs-icon name="send" [size]="16" /></button>
    </form>
  `,
})
export class AskAiFieldComponent {
  /** Already translated: "Ask AI anything about your clinics...". */
  readonly label = input.required<string>();
  readonly sendLabel = input.required<string>();
  readonly asked = output<string>();

  protected readonly question = signal('');

  protected ask(): void {
    const question = this.question().trim();
    if (!question) return;
    this.asked.emit(question);
    this.question.set('');
  }
}

/** One message of the chat. */
export interface ChatMessage {
  readonly from: 'bot' | 'user';
  readonly text: string;
  readonly attachment?: string;
}

/**
 * The AI assistant's chat, the operators console's own (design: AI — chat-widget-panel, `405:2` /
 * `411:11`): a purple header with its name and a close button, the conversation (a list, said
 * politely as it grows), and a field with an attach button and a send button. What is typed is handed
 * to the page (`sent`); nothing is sent anywhere from here (Constitution V).
 */
@Component({
  selector: 'app-chat-widget',
  imports: [ScsIconComponent, ScsMessageBubbleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'dialog', '[attr.aria-label]': 'title()' },
  styles: `
    :host {
      display: grid;
      grid-template-rows: auto 1fr auto;
      box-sizing: border-box;
      inline-size: min(360px, 100%);
      block-size: 620px;
      max-block-size: 80vh;
      overflow: hidden;
      border: 1px solid var(--scs-color-border-control, var(--scs-color-border-default));
      border-radius: 16px;
      background: var(--scs-color-bg-surface);
      box-shadow: 0 12px 32px rgb(26 32 36 / 16%);
    }
    header {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 14px 16px;
      background: var(--scs-ai-gradient);
      color: #ffffff;
    }
    .mark {
      display: grid;
      place-items: center;
      inline-size: 32px;
      block-size: 32px;
      border-radius: 50%;
      background: rgb(255 255 255 / 20%);
    }
    .names {
      flex: 1;
      display: grid;
    }
    .names strong {
      font-size: 14px;
    }
    .names span {
      font-size: 11px;
      opacity: 0.8;
    }
    .close,
    .attach,
    .send {
      display: grid;
      place-items: center;
      border: 0;
      background: none;
      color: inherit;
      cursor: pointer;
    }
    .close:focus-visible,
    .attach:focus-visible,
    .send:focus-visible {
      outline: 2px solid currentColor;
      outline-offset: 2px;
    }
    .talk {
      display: grid;
      align-content: start;
      gap: 12px;
      padding: 16px;
      overflow-y: auto;
    }
    form {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px;
      border-block-start: 1px solid var(--scs-color-border-control, var(--scs-color-border-default));
    }
    .attach {
      color: var(--scs-color-icon-default);
    }
    input {
      flex: 1;
      min-inline-size: 0;
      block-size: 40px;
      padding-inline: 14px;
      border: 0;
      border-radius: 20px;
      background: var(--scs-color-bg-hover, var(--scs-color-bg-surface-secondary));
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: 13px;
    }
    input:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
    }
    .send {
      inline-size: 40px;
      block-size: 40px;
      border-radius: 50%;
      background: var(--scs-color-brand-default);
      color: #ffffff;
    }
  `,
  template: `
    <header>
      <span class="mark" aria-hidden="true"><scs-icon name="sparkle" [size]="16" /></span>
      <span class="names"><strong>{{ title() }}</strong><span>{{ subtitle() }}</span></span>
      <button type="button" class="close" data-testid="chat-close" [attr.aria-label]="closeLabel()" (click)="closed.emit()"><scs-icon name="close" [size]="14" /></button>
    </header>
    <div class="talk" role="list" aria-live="polite" data-testid="chat-messages">
      @for (message of messages(); track $index) {
        <scs-message-bubble [kind]="message.from" [attachment]="message.attachment ?? ''">{{ message.text }}</scs-message-bubble>
      }
    </div>
    <form (submit)="$event.preventDefault(); send()">
      <button type="button" class="attach" [attr.aria-label]="attachLabel()"><scs-icon name="paperclip" [size]="20" /></button>
      <input type="text" data-testid="chat-input" [attr.aria-label]="placeholder()" [placeholder]="placeholder()" [value]="draft()" (input)="draft.set($any($event.target).value)" />
      <button type="submit" class="send" data-testid="chat-send" [attr.aria-label]="sendLabel()"><scs-icon name="send" [size]="16" /></button>
    </form>
  `,
})
export class ChatWidgetComponent {
  readonly title = input.required<string>();
  readonly subtitle = input('');
  readonly messages = input.required<readonly ChatMessage[]>();
  readonly placeholder = input('');
  readonly sendLabel = input.required<string>();
  readonly attachLabel = input.required<string>();
  readonly closeLabel = input.required<string>();

  readonly sent = output<string>();
  readonly closed = output<void>();

  protected readonly draft = signal('');

  protected send(): void {
    const text = this.draft().trim();
    if (!text) return;
    this.sent.emit(text);
    this.draft.set('');
  }
}
