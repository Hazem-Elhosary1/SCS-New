import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

/** The letters standing for a name: the first of its first two words — "Ahmed Salem" → "AS", «أحمد حسن» → «أح». */
export function initialsOf(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => [...word][0] ?? '')
    .join('')
    .toLocaleUpperCase();
}

/**
 * A person's picture, or the letters of their name when there is none (design: Avatar — `294:131` /
 * `8:111`; the clinic's Avatar on `4228:7`). Round, in three sizes; it names the person for assistive
 * technology. A picture that fails to load gives way to the letters. Each look draws its own sizes and
 * letters through the `--scs-avatar-*` settings: the clinic's 32, 40 and 56 by default, the operators'
 * 28, 44 and 64.
 */
@Component({
  selector: 'scs-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    role: 'img',
    '[attr.aria-label]': 'name()',
    '[attr.data-size]': 'size()',
    '[attr.data-tone]': 'tone()',
  },
  styles: `
    :host {
      --side: var(--scs-avatar-md, 40px);
      display: inline-grid;
      place-items: center;
      flex: none;
      inline-size: var(--side);
      block-size: var(--side);
      overflow: hidden;
      border-radius: 50%;
      background: var(--scs-color-brand-subtle);
      color: var(--scs-avatar-text, var(--scs-color-brand-active));
      font-size: var(--scs-avatar-md-text, 14px);
      font-weight: var(--scs-avatar-weight, 600);
      line-height: 1;
      user-select: none;
    }
    :host([data-size='sm']) {
      --side: var(--scs-avatar-sm, 32px);
      font-size: var(--scs-avatar-sm-text, 12px);
    }
    :host([data-size='lg']) {
      --side: var(--scs-avatar-lg, 56px);
      font-size: var(--scs-avatar-lg-text, 18px);
    }
    :host([data-tone='neutral']) {
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-secondary);
    }
    img {
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
    }
  `,
  template: `
    @if (src() && !failed()) {
      <img [src]="src()" alt="" (error)="failed.set(true)" />
    } @else {
      <span aria-hidden="true">{{ letters() }}</span>
    }
  `,
})
export class ScsAvatarComponent {
  /** The person's name: read out, and its letters shown when there is no picture. */
  readonly name = input.required<string>();
  readonly src = input<string | null>(null);
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  /** The brand's colours, or grey (the clinic's "Neutral" track). */
  readonly tone = input<'brand' | 'neutral'>('brand');

  protected readonly failed = signal(false);
  protected readonly letters = computed(() => initialsOf(this.name()));
}
