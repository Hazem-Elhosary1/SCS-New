import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import {
  FileType,
  FilesService,
  LanguageService,
  createFileUploads,
  fileDateLabel,
  sizeLabel,
  toFileRefusal,
} from 'core';
import {
  ScsButtonDirective,
  ScsCardComponent,
  ScsFileAction,
  ScsFileCardComponent,
  ScsFileCardState,
  ScsFileFieldComponent,
  ScsFileUploadComponent,
  ScsPageHeaderComponent,
  fileCardType,
} from 'ui';

interface SavedFile {
  readonly id: string;
  readonly name: string;
  readonly sizeBytes: number;
  readonly type: FileType;
  readonly state: 'available' | 'being-checked' | 'refused' | 'removed' | 'not-kept';
  readonly refusedFor: 'infected' | 'unreadable' | null;
  readonly uploadedAt: string;
  readonly restorableUntil: string | null;
}

const SAMPLE = '/v1/dev/files/sample';

/**
 * For people building the product (spec 009, FR-046): every state of the upload zone and the
 * file card, to lay beside the design, and one live field on the builders' kind of file — the
 * whole way from choosing a file to downloading, removing and restoring it. No page of the
 * product holds files yet; this address does not exist in production.
 */
@Component({
  selector: 'app-dev-files-page',
  imports: [
    TranslocoPipe,
    ScsPageHeaderComponent,
    ScsCardComponent,
    ScsButtonDirective,
    ScsFileUploadComponent,
    ScsFileCardComponent,
    ScsFileFieldComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, 320px);
      gap: var(--scs-spacing-lg);
      align-items: start;
    }
    .live {
      display: grid;
      grid-template-columns: 320px 320px;
      gap: var(--scs-spacing-xl, 32px);
      align-items: start;
    }
    .column {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-sm);
    }
    h3 {
      margin: 0 0 var(--scs-spacing-sm);
      font-size: var(--scs-text-body);
      font-weight: 600;
    }
    p {
      margin: 0;
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body-sm);
    }
    scs-card + scs-card {
      display: block;
      margin-block-start: var(--scs-spacing-lg);
    }
  `,
  template: `
    <scs-page-header
      [title]="'app.devFiles.title' | transloco"
      [note]="'app.devFiles.note' | transloco"
    />

    <scs-card data-testid="files-live" [heading]="'app.devFiles.live' | transloco">
      <div class="live">
        <div class="column">
          <scs-file-field data-testid="live-field" [uploads]="uploads" />
          <button
            type="button"
            scsButton
            data-testid="files-save"
            [disabled]="uploads.busy() || uploads.ids().length === 0 || saving()"
            (click)="save()"
          >
            {{ 'app.devFiles.save' | transloco }}
          </button>
          @if (problem() !== '') {
            <p role="alert" data-testid="files-problem">{{ problem() }}</p>
          }
        </div>
        <div class="column" data-testid="files-saved">
          <h3>{{ 'app.devFiles.saved' | transloco }}</h3>
          @for (file of saved(); track file.id) {
            <scs-file-card
              [name]="file.name"
              [type]="cardType(file.type)"
              [state]="cardState(file)"
              [meta]="meta(file)"
              [reason]="reason(file)"
              [actions]="actions(file)"
              (download)="download(file)"
              (action)="act(file, $event)"
            />
          } @empty {
            <p data-testid="files-none">{{ 'app.devFiles.none' | transloco }}</p>
          }
        </div>
      </div>
    </scs-card>

    <scs-card data-testid="files-zones" [heading]="'app.devFiles.zones' | transloco">
      <div class="grid">
        <scs-file-upload data-testid="zone-default" kind="dev.sample" />
        <scs-file-upload data-testid="zone-uploading" kind="dev.sample" [uploading]="64" />
        <scs-file-upload data-testid="zone-error" kind="dev.sample" [error]="tooLarge()" />
        <scs-file-upload data-testid="zone-disabled" kind="dev.sample" [disabled]="true" />
        <scs-file-upload data-testid="zone-full" kind="dev.sample" [full]="true" />
        <scs-file-upload data-testid="zone-photo" kind="doctor.photo" />
      </div>
    </scs-card>

    <scs-card data-testid="files-cards" [heading]="'app.devFiles.cards' | transloco">
      <div class="grid">
        @for (type of types; track type) {
          <scs-file-card [name]="sampleName" [type]="type" state="default" [meta]="sampleMeta()" />
          <scs-file-card [name]="sampleName" [type]="type" state="uploading" [percent]="percent()" />
          <scs-file-card [name]="sampleName" [type]="type" state="error" />
        }
        <scs-file-card [name]="sampleName" type="pdf" state="ready" [meta]="sampleSize()" />
        <scs-file-card [name]="sampleName" type="pdf" state="being-checked" />
        <scs-file-card [name]="sampleName" type="pdf" state="refused" />
        <scs-file-card [name]="sampleName" type="pdf" state="not-kept" />
        <scs-file-card [name]="sampleName" type="pdf" state="removed" />
        <scs-file-card
          [name]="longName"
          type="image"
          state="default"
          [meta]="sampleMeta()"
          [actions]="[{ key: 'remove', label: 'app.devFiles.remove' | transloco }]"
        />
      </div>
    </scs-card>
  `,
})
export class DevFilesPage {
  private readonly http = inject(HttpClient);
  private readonly files = inject(FilesService);
  private readonly language = inject(LanguageService);
  private readonly words = inject(TranslocoService);

  protected readonly cardType = fileCardType;
  protected readonly types = ['image', 'pdf', 'other'] as const;
  protected readonly sampleName = 'ملف-الأشعة.pdf';
  protected readonly longName =
    'صورة أشعة بانورامية للمريض قبل العلاج وبعده — النسخة الأصلية من جهاز الأشعة.png';

  protected readonly saved = signal<readonly SavedFile[]>([]);
  protected readonly saving = signal(false);
  protected readonly problem = signal('');

  /** The record holds what is saved and not removed; the control counts it against the limit. */
  protected readonly uploads = createFileUploads(
    'dev.sample',
    () => this.saved().filter((file) => file.state === 'available' || file.state === 'being-checked').length,
  );

  protected readonly sampleSize = computed(() => sizeLabel(2.4 * 1024 * 1024, this.language.language()));
  protected readonly sampleMeta = computed(
    () => `${this.sampleSize()} · ${fileDateLabel('2026-08-21T09:00:00Z', this.language.language())}`,
  );
  protected readonly percent = computed(() =>
    new Intl.NumberFormat(this.language.language() === 'ar' ? 'ar-EG-u-nu-arab' : 'en-GB').format(64),
  );
  protected readonly tooLarge = computed(() => {
    // Read so that the words follow the language.
    const language = this.language.language();
    return this.words.translate('ui.files.reason.tooLarge', {
      max: sizeLabel(10 * 1024 * 1024, language),
    });
  });

  constructor() {
    void this.load();
  }

  protected async save(): Promise<void> {
    this.saving.set(true);
    this.problem.set('');
    try {
      await firstValueFrom(this.http.post(SAMPLE, { fileIds: this.uploads.ids() }));
      this.uploads.clear();
      await this.load();
    } catch (error) {
      this.problem.set(this.say(error));
    } finally {
      this.saving.set(false);
    }
  }

  protected async download(file: SavedFile): Promise<void> {
    this.problem.set('');
    try {
      await this.files.download(file.id);
    } catch (error) {
      this.problem.set(this.say(error));
      await this.load();
    }
  }

  protected async act(file: SavedFile, action: string): Promise<void> {
    this.problem.set('');
    try {
      await firstValueFrom(this.http.post(`${SAMPLE}/${file.id}/${action}`, null));
    } catch (error) {
      this.problem.set(this.say(error));
    }
    await this.load();
  }

  protected cardState(file: SavedFile): ScsFileCardState {
    return file.state === 'available' ? 'default' : file.state;
  }

  protected meta(file: SavedFile): string {
    const language = this.language.language();
    return `${sizeLabel(file.sizeBytes, language)} · ${fileDateLabel(file.uploadedAt, language)}`;
  }

  protected reason(file: SavedFile): string {
    if (file.state === 'refused' && file.refusedFor === 'unreadable') {
      return this.words.translate('ui.files.card.unreadable');
    }
    if (file.state === 'removed' && file.restorableUntil !== null) {
      return this.words.translate('ui.files.card.removed', {
        date: fileDateLabel(file.restorableUntil, this.language.language()),
      });
    }
    return '';
  }

  /** What this page offers for a file: taking it off the record, or putting it back. */
  protected actions(file: SavedFile): ScsFileAction[] {
    if (file.state === 'removed') {
      return [{ key: 'restore', label: this.words.translate('app.devFiles.restore') }];
    }
    return file.state === 'not-kept'
      ? []
      : [{ key: 'remove', label: this.words.translate('app.devFiles.remove') }];
  }

  private async load(): Promise<void> {
    try {
      const answer = await firstValueFrom(this.http.get<{ files: SavedFile[] }>(SAMPLE));
      this.saved.set(answer.files);
    } catch {
      this.saved.set([]);
    }
  }

  private say(error: unknown): string {
    const refusal = toFileRefusal(error);
    switch (refusal.code) {
      case 'FILE_BEING_CHECKED':
        return this.words.translate('ui.files.card.beingChecked');
      case 'FILE_REFUSED':
        return this.words.translate(
          refusal.reason === 'unreadable' ? 'ui.files.card.unreadable' : 'ui.files.card.refused',
        );
      case 'FILE_NOT_KEPT':
        return this.words.translate('ui.files.card.notKept');
      case 'FILE_COUNT_REACHED':
        return this.words.translate('ui.files.reason.countReached', { max: this.percentOf(3) });
      default:
        return this.words.translate('ui.files.reason.failed');
    }
  }

  private percentOf(value: number): string {
    return new Intl.NumberFormat(this.language.language() === 'ar' ? 'ar-EG-u-nu-arab' : 'en-GB').format(value);
  }
}
