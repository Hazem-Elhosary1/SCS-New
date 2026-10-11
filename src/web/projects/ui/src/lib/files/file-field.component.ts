import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import {
  FileRow,
  FileType,
  FileUploads,
  LanguageService,
  acceptedLabel,
  sizeLabel,
} from 'core';
import { ScsFileCardComponent, ScsFileCardState, ScsFileCardType } from './file-card.component';
import { ScsFileUploadComponent } from './file-upload.component';

/** The card's mark for a type the platform read, or the name suggests. */
export function fileCardType(type: FileType | null): ScsFileCardType {
  if (type === 'pdf') {
    return 'pdf';
  }
  return type === null ? 'other' : 'image';
}

/**
 * The zone over the files chosen in it (spec 009, FR-040 to FR-043): what a page places where
 * a record takes files. Each chosen file has its card — its progress while it uploads, its
 * reason when it is refused, and a way to take it out or to try again. One refused file does
 * not stop the others. What happened to each file is also said aloud, for a person who does
 * not see the cards. The page sends `uploads().ids()` with its save and keeps the save
 * disabled while `uploads().busy()`.
 */
@Component({
  selector: 'scs-file-field',
  imports: [ScsFileUploadComponent, ScsFileCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'data-testid': 'file-field' },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-sm);
      inline-size: 100%;
      max-inline-size: 320px;
    }
    ul {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-sm);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .said {
      position: absolute;
      inline-size: 1px;
      block-size: 1px;
      overflow: hidden;
      clip-path: inset(50%);
      white-space: nowrap;
    }
  `,
  template: `
    <scs-file-upload
      [kind]="uploads().kind.key"
      [disabled]="disabled()"
      [full]="uploads().full()"
      [uploading]="sending()"
      [error]="lastRefusal()"
      (chosen)="uploads().add($event)"
    />
    @if (uploads().rows().length > 0) {
      <ul data-testid="file-rows">
        @for (row of uploads().rows(); track row.key) {
          <li>
            <scs-file-card
              [name]="row.name"
              [type]="cardType(row.type)"
              [state]="cardState(row)"
              [meta]="size(row.sizeBytes)"
              [percent]="digits(row.percent)"
              [reason]="reason(row)"
              [canRetry]="row.state === 'failed'"
              (retry)="uploads().retry(row)"
              (remove)="remove(row)"
            />
          </li>
        }
      </ul>
    }
    <p class="said" role="status" aria-live="polite" data-testid="file-said">{{ said() }}</p>
  `,
})
export class ScsFileFieldComponent {
  private readonly language = inject(LanguageService);
  private readonly words = inject(TranslocoService);

  /** The state of this control, made by the page with `createFileUploads(kind)`. */
  readonly uploads = input.required<FileUploads>();

  readonly disabled = input(false);

  protected readonly cardType = fileCardType;
  protected readonly said = signal('');
  private readonly told = new Map<number, string>();

  /** The zone shows the file that is furthest along its way. */
  protected readonly sending = computed(() => {
    const going = this.uploads()
      .rows()
      .filter((row) => row.state === 'uploading');
    return going.length === 0 ? null : Math.max(...going.map((row) => row.percent));
  });

  /** The zone says why the newest chosen file was not taken, until another is chosen. */
  protected readonly lastRefusal = computed(() => {
    const rows = this.uploads().rows();
    const last = rows.at(-1);
    return last !== undefined && (last.state === 'refused' || last.state === 'failed')
      ? this.reason(last)
      : '';
  });

  constructor() {
    // Each file is announced once when it settles: taken, or not and why.
    effect(() => {
      // The words follow the language.
      this.language.language();
      for (const row of this.uploads().rows()) {
        if (row.state === 'uploading' || this.told.get(row.key) === row.state) {
          continue;
        }
        this.told.set(row.key, row.state);
        this.said.set(
          row.state === 'uploaded'
            ? this.words.translate('ui.files.said.uploaded', { name: row.name })
            : this.words.translate('ui.files.said.refused', {
                name: row.name,
                reason: this.reason(row),
              }),
        );
      }
    });
  }

  protected remove(row: FileRow): void {
    this.uploads().remove(row);
    this.told.delete(row.key);
    this.said.set(this.words.translate('ui.files.said.removed', { name: row.name }));
  }

  protected cardState(row: FileRow): ScsFileCardState {
    if (row.state === 'uploading') {
      return 'uploading';
    }
    return row.state === 'uploaded' ? 'ready' : 'error';
  }

  protected size(bytes: number): string {
    return sizeLabel(bytes, this.language.language());
  }

  protected digits(value: number): string {
    return new Intl.NumberFormat(
      this.language.language() === 'ar' ? 'ar-EG-u-nu-arab' : 'en-GB',
    ).format(value);
  }

  /** Why a file was not taken, in the words of the language. */
  protected reason(row: FileRow): string {
    const reason = row.reason;
    if (reason === null) {
      return '';
    }
    const language = this.language.language();
    const kind = this.uploads().kind;

    switch (reason.code) {
      case 'FILE_TOO_LARGE':
        return this.words.translate('ui.files.reason.tooLarge', {
          max: sizeLabel(reason.maxBytes, language),
        });
      case 'FILE_TYPE_NOT_ACCEPTED':
        return this.words.translate('ui.files.reason.wrongType', {
          types: acceptedLabel(kind, language),
        });
      case 'FILE_EMPTY':
        return this.words.translate('ui.files.reason.empty');
      case 'FILE_COUNT_REACHED':
        return this.words.translate('ui.files.reason.countReached', {
          max: this.digits(kind.maxPerRecord ?? 0),
        });
      case 'FILE_LIMIT_REACHED':
        return this.words.translate('ui.files.reason.limitReached');
      case 'PERMISSION_REQUIRED':
        return this.words.translate('ui.files.reason.notAllowed');
      case 'OFFLINE':
        return this.words.translate('ui.files.reason.offline');
      default:
        return this.words.translate('ui.files.reason.failed');
    }
  }
}
