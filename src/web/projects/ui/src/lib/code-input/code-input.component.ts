import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  input,
  model,
  output,
  viewChildren,
} from '@angular/core';

export type ScsCodeState = 'idle' | 'error' | 'accepted';

/** Western and Arabic-Indic digits, read as the same six digits. */
function digitsOf(text: string): string {
  let out = '';
  for (const character of text) {
    const code = character.codePointAt(0) ?? 0;
    if (code >= 0x30 && code <= 0x39) {
      out += character;
    } else if (code >= 0x660 && code <= 0x669) {
      out += String(code - 0x660);
    } else if (code >= 0x6f0 && code <= 0x6f9) {
      out += String(code - 0x6f0);
    }
  }
  return out;
}

/**
 * Six boxes for a six-digit code. Always written left to right in Western digits, in both
 * languages (BRD CL-SH-086). Typing moves on, Backspace moves back, and pasting six digits
 * fills all six. The boxes are one labelled group. When `state` is `accepted` they are locked.
 */
@Component({
  selector: 'scs-code-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-state]': 'state()' },
  styles: `
    :host {
      display: block;
    }
    .boxes {
      display: flex;
      gap: var(--scs-spacing-sm);
      justify-content: center;
    }
    input {
      inline-size: 48px;
      block-size: 56px;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-sm);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-text-title-sm);
      font-weight: 600;
      text-align: center;
    }
    input:focus-visible {
      border-color: var(--scs-color-brand-default);
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    :host([data-state='error']) input {
      border-color: var(--scs-color-feedback-danger-icon);
      background: var(--scs-color-feedback-danger-bg);
    }
    :host([data-state='accepted']) input {
      border-color: var(--scs-color-feedback-success-icon);
      background: var(--scs-color-feedback-success-bg);
    }
  `,
  template: `
    <div class="boxes" dir="ltr" role="group" [attr.aria-label]="label()">
      @for (position of positions; track position) {
        <input
          #box
          type="text"
          inputmode="numeric"
          autocomplete="one-time-code"
          [value]="digits()[position] ?? ''"
          [disabled]="state() === 'accepted'"
          [attr.aria-label]="label() + ' ' + (position + 1)"
          [attr.aria-invalid]="state() === 'error' ? 'true' : null"
          [attr.data-testid]="'code-box-' + position"
          (input)="typed(position, box.value)"
          (keydown)="key(position, $event)"
          (paste)="pasted($event)"
          (focus)="box.select()"
        />
      }
    </div>
  `,
})
export class ScsCodeInputComponent {
  static readonly Length = 6;

  /** Already translated: what the group is, for assistive technology. */
  readonly label = input.required<string>();

  /** The digits typed so far, as a string of up to six. */
  readonly value = model('');

  readonly state = input<ScsCodeState>('idle');

  /** All six digits are in. */
  readonly completed = output<string>();

  protected readonly positions = Array.from({ length: ScsCodeInputComponent.Length }, (_, i) => i);

  protected readonly digits = computed(() => this.value().split(''));

  private readonly boxes = viewChildren<ElementRef<HTMLInputElement>>('box');

  focus(position = 0): void {
    this.boxes()[Math.min(position, ScsCodeInputComponent.Length - 1)]?.nativeElement.focus();
  }

  protected typed(position: number, raw: string): void {
    const typed = digitsOf(raw);
    const current = this.value().padEnd(ScsCodeInputComponent.Length, ' ').split('');

    if (typed.length <= 1 || (typed.length === 2 && typed[0] === current[position])) {
      // One key: the box keeps the newest digit.
      const digit = typed.slice(-1);
      current[position] = digit === '' ? ' ' : digit;
      this.set(current.join(''));
      if (digit !== '') {
        this.focus(position + 1);
      }
      return;
    }

    // Several digits at once (the browser offering the code from a message, or a paste the
    // browser delivered as typing): they fill the boxes from here on.
    const room = ScsCodeInputComponent.Length - position;
    const incoming = typed.slice(0, room).split('');
    incoming.forEach((digit, offset) => (current[position + offset] = digit));
    this.set(current.join(''));
    this.focus(position + incoming.length);
  }

  protected key(position: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && (this.digits()[position] ?? '') === '' && position > 0) {
      event.preventDefault();
      const current = this.value().split('');
      current.splice(position - 1, 1);
      this.set(current.join(''));
      this.focus(position - 1);
    } else if (event.key === 'ArrowLeft' && position > 0) {
      event.preventDefault();
      this.focus(position - 1);
    } else if (event.key === 'ArrowRight' && position < ScsCodeInputComponent.Length - 1) {
      event.preventDefault();
      this.focus(position + 1);
    }
  }

  protected pasted(event: ClipboardEvent): void {
    const digits = digitsOf(event.clipboardData?.getData('text') ?? '').slice(
      0,
      ScsCodeInputComponent.Length,
    );
    if (digits === '') {
      return;
    }
    event.preventDefault();
    this.set(digits);
    this.focus(digits.length);
  }

  private set(next: string): void {
    // Digits stay in their boxes; a gap ends the value at the gap.
    const gap = next.indexOf(' ');
    const value = (gap === -1 ? next : next.slice(0, gap)).slice(0, ScsCodeInputComponent.Length);
    this.value.set(value);

    // The boxes show what the value holds, even when the browser kept a refused character.
    this.boxes().forEach((box, index) => {
      box.nativeElement.value = value[index] ?? '';
    });

    if (value.length === ScsCodeInputComponent.Length) {
      this.completed.emit(value);
    }
  }
}
