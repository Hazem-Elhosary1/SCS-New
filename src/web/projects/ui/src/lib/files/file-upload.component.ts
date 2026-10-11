import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { LanguageService, acceptAttribute, acceptedLabel, fileKind, sizeLabel } from 'core';
import { ScsIconComponent } from '../icon/icon.component';

/** What the zone shows: the design's six states, and the full one it does not draw. */
export type ScsFileUploadState =
  | 'default'
  | 'drag'
  | 'uploading'
  | 'error'
  | 'disabled'
  | 'full';

/**
 * The zone a person drops a file on, or presses to choose one (design: "File Upload",
 * `4260:814`). It says, before anything is chosen, which types and what size it takes — read
 * from the kind, not told twice. Dragging a file over it highlights it; while a file is on its
 * way it shows the progress; when a file is refused it says so and stays ready for another
 * try. It takes no part in the upload itself: it gives the chosen files to its page
 * (spec 009, FR-037 to FR-039).
 */
@Component({
  selector: 'scs-file-upload',
  imports: [TranslocoPipe, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-state]': 'shown()', 'data-testid': 'file-upload' },
  styles: `
    :host {
      display: block;
      inline-size: 100%;
      max-inline-size: 320px;
    }
    .zone {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: var(--scs-spacing-sm);
      box-sizing: border-box;
      inline-size: 100%;
      min-block-size: 162px;
      padding: var(--scs-spacing-lg) var(--scs-spacing-md);
      border: 1px dashed var(--scs-color-border-strong, var(--scs-color-border-default));
      border-radius: var(--scs-radius-lg, 12px);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      font: inherit;
      text-align: center;
      cursor: pointer;
    }
    .zone:hover {
      background: var(--scs-color-bg-surface-secondary);
    }
    .zone:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    :host([data-state='drag']) .zone {
      border-color: var(--scs-color-brand-default);
      background: var(--scs-color-brand-subtle);
    }
    :host([data-state='uploading']) .zone {
      min-block-size: 178px;
      border-style: solid;
      cursor: progress;
    }
    :host([data-state='error']) .zone {
      border-color: var(--scs-color-feedback-danger-icon);
    }
    :host([data-state='disabled']) .zone,
    :host([data-state='full']) .zone {
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-disabled, var(--scs-color-text-tertiary));
      cursor: not-allowed;
    }
    .mark {
      display: grid;
      place-items: center;
      inline-size: 40px;
      block-size: 40px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-icon-default);
    }
    :host([data-state='drag']) .mark {
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-brand-default);
    }
    :host([data-state='error']) .mark {
      background: var(--scs-color-feedback-danger-bg, var(--scs-color-bg-surface-secondary));
      color: var(--scs-color-feedback-danger-icon);
    }
    .title {
      font-size: var(--scs-text-body);
      font-weight: 500;
    }
    :host([data-state='error']) .title,
    :host([data-state='error']) .hint {
      color: var(--scs-color-feedback-danger-text, var(--scs-color-feedback-danger-icon));
    }
    .hint {
      font-size: var(--scs-text-caption);
      color: var(--scs-color-text-tertiary);
    }
    :host([data-state='disabled']) .hint,
    :host([data-state='full']) .hint {
      color: inherit;
    }
    .bar {
      inline-size: 100%;
      block-size: 4px;
      margin-block-start: var(--scs-spacing-xs);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface-secondary);
      overflow: hidden;
    }
    .bar > span {
      display: block;
      block-size: 100%;
      border-radius: inherit;
      background: var(--scs-color-brand-default);
      transition: inline-size 0.2s ease;
    }
    input {
      display: none;
    }
  `,
  template: `
    <div
      #zone
      class="zone"
      role="button"
      data-testid="file-zone"
      [attr.tabindex]="inert() ? -1 : 0"
      [attr.aria-disabled]="inert()"
      [attr.aria-describedby]="hintId"
      (click)="browse()"
      (keydown.enter)="browse()"
      (keydown.space)="$event.preventDefault(); browse()"
      (dragenter)="dragged($event, true)"
      (dragover)="dragged($event, true)"
      (dragleave)="dragged($event, false)"
      (drop)="dropped($event)"
    >
      <span class="mark">
        <scs-icon
          [name]="
            shown() === 'error' ? 'alert-circle' : shown() === 'uploading' ? 'file' : 'upload'
          "
          [size]="20"
        />
      </span>
      <span class="title" data-testid="file-zone-title">
        @switch (shown()) {
          @case ('uploading') {
            {{ 'ui.files.zone.uploading' | transloco }}
          }
          @case ('error') {
            {{ 'ui.files.zone.failed' | transloco }}
          }
          @default {
            {{ 'ui.files.zone.title' | transloco }}
          }
        }
      </span>
      <span class="hint" data-testid="file-zone-hint" [id]="hintId">
        @switch (shown()) {
          @case ('error') {
            {{ error() }}
          }
          @case ('full') {
            {{ 'ui.files.zone.full' | transloco: { max: maxWords() } }}
          }
          @default {
            {{ 'ui.files.zone.hint' | transloco: { types: types(), size: size() } }}
          }
        }
      </span>
      @if (shown() === 'uploading') {
        <span
          class="bar"
          role="progressbar"
          aria-valuemin="0"
          aria-valuemax="100"
          [attr.aria-valuenow]="percent()"
          data-testid="file-zone-bar"
          ><span [style.inline-size.%]="percent()"></span
        ></span>
      }
    </div>
    <input
      #chooser
      type="file"
      tabindex="-1"
      aria-hidden="true"
      data-testid="file-chooser"
      [attr.accept]="accept()"
      [multiple]="multiple()"
      (change)="chose($event)"
    />
  `,
})
export class ScsFileUploadComponent {
  private static next = 0;

  private readonly language = inject(LanguageService);
  private readonly chooser = viewChild.required<ElementRef<HTMLInputElement>>('chooser');

  protected readonly hintId = `scs-file-hint-${ScsFileUploadComponent.next++}`;
  private readonly over = signal(false);

  /** A key of the platform's table of kinds: it decides the hint and what the chooser offers. */
  readonly kind = input.required<string>();

  /** Per cent of the file on its way; `null` when nothing is. */
  readonly uploading = input<number | null>(null);

  /** The reason the last file was not taken, already in words; empty when none. */
  readonly error = input('');

  readonly disabled = input(false);

  /** The record holds as many files as its kind allows. */
  readonly full = input(false);

  /** The files a person dropped or chose. */
  readonly chosen = output<File[]>();

  private readonly known = computed(() => {
    const kind = fileKind(this.kind());
    if (kind === undefined) {
      throw new Error(`'${this.kind()}' is not a kind of file.`);
    }
    return kind;
  });

  protected readonly types = computed(() =>
    acceptedLabel(this.known(), this.language.language()),
  );
  protected readonly size = computed(() =>
    sizeLabel(this.known().maxBytes, this.language.language()),
  );
  protected readonly accept = computed(() => acceptAttribute(this.known()));
  protected readonly multiple = computed(() => this.known().maxPerRecord !== 1);
  protected readonly maxWords = computed(() =>
    new Intl.NumberFormat(this.language.language() === 'ar' ? 'ar-EG-u-nu-arab' : 'en-GB').format(
      this.known().maxPerRecord ?? 0,
    ),
  );
  protected readonly percent = computed(() => this.uploading() ?? 0);
  protected readonly inert = computed(() => this.disabled() || this.full());

  /** One state at a time, the most pressing first. */
  protected readonly shown = computed<ScsFileUploadState>(() => {
    if (this.disabled()) {
      return 'disabled';
    }
    if (this.full()) {
      return 'full';
    }
    if (this.over()) {
      return 'drag';
    }
    if (this.uploading() !== null) {
      return 'uploading';
    }
    return this.error() !== '' ? 'error' : 'default';
  });

  protected browse(): void {
    if (!this.inert()) {
      this.chooser().nativeElement.click();
    }
  }

  protected chose(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = [...(input.files ?? [])];
    // The same file can be chosen again after it was refused.
    input.value = '';
    this.give(files);
  }

  protected dragged(event: DragEvent, over: boolean): void {
    event.preventDefault();
    this.over.set(over && !this.inert());
  }

  protected dropped(event: DragEvent): void {
    event.preventDefault();
    this.over.set(false);
    this.give([...(event.dataTransfer?.files ?? [])]);
  }

  private give(files: File[]): void {
    if (files.length > 0 && !this.inert()) {
      this.chosen.emit(files);
    }
  }
}
