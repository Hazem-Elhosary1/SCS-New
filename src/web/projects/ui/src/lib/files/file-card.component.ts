import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ScsIconComponent, ScsIconName } from '../icon/icon.component';

/** What the card's mark shows. The type changes the icon and its tint, nothing else. */
export type ScsFileCardType = 'image' | 'pdf' | 'other';

/**
 * - `default`: the file is there to download.
 * - `ready`: uploaded and not yet saved with its record: it can be taken out, not downloaded.
 * - `uploading`, `error`: on its way, or not taken.
 * - `being-checked`, `refused`, `not-kept`, `removed`: a saved file that is not available, and why.
 */
export type ScsFileCardState =
  | 'default'
  | 'ready'
  | 'uploading'
  | 'error'
  | 'being-checked'
  | 'refused'
  | 'not-kept'
  | 'removed';

/** Something the owning page offers for a file under the card's "more" control. */
export interface ScsFileAction {
  readonly key: string;
  readonly label: string;
}

const MARKS: Record<ScsFileCardType, ScsIconName> = {
  image: 'image',
  pdf: 'file',
  other: 'paperclip',
};

/**
 * One file, in a patient's record, on a ticket or under an upload zone (design: "File
 * Attachment Card", `4252:63169`): a mark for its type, its name, a second line, then what
 * can be done with it. While uploading the second line is the progress and the actions are a
 * spinner; a failed upload offers another try and removal. The states the design does not
 * draw — being checked, refused, no longer kept, removed — use the same card with their words
 * in place of the download (spec 009, FR-044).
 */
@Component({
  selector: 'scs-file-card',
  imports: [TranslocoPipe, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-state]': 'state()',
    '[attr.data-type]': 'type()',
    'data-testid': 'file-card',
  },
  styles: `
    :host {
      position: relative;
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      box-sizing: border-box;
      inline-size: 100%;
      max-inline-size: 320px;
      min-block-size: 73px;
      padding: var(--scs-spacing-12) var(--scs-spacing-md);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-lg, 12px);
      background: var(--scs-color-bg-surface);
    }
    :host([data-state='error']),
    :host([data-state='refused']) {
      border-color: var(--scs-color-feedback-danger-icon);
    }
    .mark {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 40px;
      block-size: 40px;
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-icon-default);
    }
    :host([data-type='image']) .mark {
      background: var(--scs-color-feedback-info-bg, var(--scs-color-brand-subtle));
      color: var(--scs-color-feedback-info-icon, var(--scs-color-brand-default));
    }
    :host([data-type='pdf']) .mark {
      background: var(--scs-color-feedback-danger-bg, var(--scs-color-bg-surface-secondary));
      color: var(--scs-color-feedback-danger-icon);
    }
    .words {
      flex: 1;
      min-inline-size: 0;
    }
    .name {
      display: block;
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
      font-size: var(--scs-text-body);
      font-weight: 500;
      color: var(--scs-color-text-primary);
    }
    .line {
      display: block;
      margin-block-start: var(--scs-spacing-xs);
      font-size: var(--scs-text-caption);
      color: var(--scs-color-text-tertiary);
    }
    :host([data-state='error']) .line,
    :host([data-state='refused']) .line {
      color: var(--scs-color-feedback-danger-text, var(--scs-color-feedback-danger-icon));
    }
    /* A removed file is marked in red: it is no longer one of the record's files, and can
       still be put back. Dashed, so it is not taken for a refused one. */
    :host([data-state='removed']) {
      border-style: dashed;
      border-color: var(--scs-color-feedback-danger-icon);
      background: var(--scs-color-feedback-danger-bg, var(--scs-color-bg-surface));
    }
    :host([data-state='removed']) .name {
      color: var(--scs-color-feedback-danger-text, var(--scs-color-feedback-danger-icon));
      text-decoration: line-through;
    }
    :host([data-state='removed']) .line {
      color: var(--scs-color-feedback-danger-text, var(--scs-color-feedback-danger-icon));
    }
    :host([data-state='removed']) .mark {
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-feedback-danger-icon);
    }
    .actions {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-xs);
      flex: none;
    }
    button.act {
      display: grid;
      place-items: center;
      inline-size: 32px;
      block-size: 32px;
      padding: 0;
      border: 0;
      border-radius: var(--scs-radius-md);
      background: none;
      color: var(--scs-color-icon-default);
      cursor: pointer;
    }
    button.act:hover {
      background: var(--scs-color-bg-surface-secondary);
    }
    button.act:focus-visible,
    .menu button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    .spinner {
      inline-size: 18px;
      block-size: 18px;
      margin: 7px;
      border: 2px solid var(--scs-color-brand-subtle);
      border-block-start-color: var(--scs-color-brand-default);
      border-radius: var(--scs-radius-full);
      animation: scs-file-spin 0.9s linear infinite;
    }
    @keyframes scs-file-spin {
      to {
        transform: rotate(360deg);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .spinner {
        animation: none;
      }
    }
    .menu {
      position: absolute;
      inset-block-start: calc(100% - 12px);
      inset-inline-end: var(--scs-spacing-md);
      z-index: 2;
      min-inline-size: 160px;
      margin: 0;
      padding: var(--scs-spacing-xs);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      box-shadow: var(--scs-shadow-md, 0 8px 24px rgb(0 0 0 / 12%));
      list-style: none;
    }
    .menu button {
      display: block;
      inline-size: 100%;
      padding: var(--scs-spacing-sm) var(--scs-spacing-12);
      border: 0;
      border-radius: var(--scs-radius-sm, 6px);
      background: none;
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-text-body-sm);
      text-align: start;
      cursor: pointer;
    }
    .menu button:hover {
      background: var(--scs-color-bg-surface-secondary);
    }
  `,
  template: `
    <span class="mark"><scs-icon [name]="mark()" [size]="20" /></span>
    <span class="words">
      <span class="name" dir="auto" data-testid="file-name" [attr.title]="name()">{{
        name()
      }}</span>
      <span class="line" data-testid="file-line">
        @switch (state()) {
          @case ('uploading') {
            {{ 'ui.files.card.uploading' | transloco: { percent: percentWords() } }}
          }
          @case ('error') {
            {{ reason() || ('ui.files.card.failed' | transloco) }}
          }
          @case ('being-checked') {
            {{ 'ui.files.card.beingChecked' | transloco }}
          }
          @case ('refused') {
            {{ reason() || ('ui.files.card.refused' | transloco) }}
          }
          @case ('not-kept') {
            {{ 'ui.files.card.notKept' | transloco }}
          }
          @case ('removed') {
            {{ reason() || meta() }}
          }
          @default {
            {{ meta() }}
          }
        }
      </span>
    </span>
    <span class="actions">
      @switch (state()) {
        @case ('uploading') {
          <span class="spinner" data-testid="file-spinner"></span>
          <button
            type="button"
            class="act"
            data-testid="file-remove"
            [attr.aria-label]="'ui.files.action.remove' | transloco: { name: name() }"
            (click)="remove.emit()"
          >
            <scs-icon name="close" [size]="18" />
          </button>
        }
        @case ('error') {
          @if (canRetry()) {
            <button
              type="button"
              class="act"
              data-testid="file-retry"
              [attr.aria-label]="'ui.files.action.retry' | transloco: { name: name() }"
              (click)="retry.emit()"
            >
              <scs-icon name="refresh" [size]="18" />
            </button>
          }
          <button
            type="button"
            class="act"
            data-testid="file-remove"
            [attr.aria-label]="'ui.files.action.remove' | transloco: { name: name() }"
            (click)="remove.emit()"
          >
            <scs-icon name="trash" [size]="18" />
          </button>
        }
        @case ('ready') {
          <button
            type="button"
            class="act"
            data-testid="file-remove"
            [attr.aria-label]="'ui.files.action.remove' | transloco: { name: name() }"
            (click)="remove.emit()"
          >
            <scs-icon name="close" [size]="18" />
          </button>
        }
        @case ('default') {
          <button
            type="button"
            class="act"
            data-testid="file-download"
            [attr.aria-label]="'ui.files.action.download' | transloco: { name: name() }"
            (click)="download.emit()"
          >
            <scs-icon name="download" [size]="18" />
          </button>
        }
      }
      @if (showsMore()) {
        <button
          type="button"
          class="act"
          data-testid="file-more"
          aria-haspopup="menu"
          [attr.aria-expanded]="open()"
          [attr.aria-label]="'ui.files.action.more' | transloco: { name: name() }"
          (click)="open.set(!open())"
        >
          <scs-icon name="more" [size]="18" />
        </button>
      }
    </span>
    @if (open()) {
      <ul class="menu" role="menu" data-testid="file-menu">
        @for (action of actions(); track action.key) {
          <li role="none">
            <button
              type="button"
              role="menuitem"
              [attr.data-testid]="'file-action-' + action.key"
              (click)="choose(action.key)"
            >
              {{ action.label }}
            </button>
          </li>
        }
      </ul>
    }
  `,
})
export class ScsFileCardComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly name = input.required<string>();
  readonly type = input<ScsFileCardType>('other');
  readonly state = input<ScsFileCardState>('default');

  /** The second line at rest: size and date, already in the words of the language. */
  readonly meta = input('');

  /** Per cent sent, while uploading; written with the digits of the language by the page. */
  readonly percent = input<number | string>(0);

  /** Why, for `error`, `refused` and `removed`; the card's own words when not given. */
  readonly reason = input('');

  /** Whether a failed upload can be sent again. */
  readonly canRetry = input(true);

  /** What the owning page offers for this file; with none the "more" control is absent. */
  readonly actions = input<readonly ScsFileAction[]>([]);

  readonly download = output<void>();
  readonly retry = output<void>();
  readonly remove = output<void>();
  readonly action = output<string>();

  protected readonly open = signal(false);
  protected readonly mark = computed(() => MARKS[this.type()]);
  protected readonly percentWords = computed(() => String(this.percent()));

  /** Offered for a saved file only: nothing is done to a file still on its way. */
  protected readonly showsMore = computed(
    () =>
      this.actions().length > 0 &&
      this.state() !== 'uploading' &&
      this.state() !== 'error' &&
      this.state() !== 'ready',
  );

  protected choose(key: string): void {
    this.open.set(false);
    this.action.emit(key);
  }

  @HostListener('document:click', ['$event'])
  protected pressedElsewhere(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  @HostListener('keydown.escape')
  protected escaped(): void {
    this.open.set(false);
  }
}
