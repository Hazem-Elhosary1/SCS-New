import { DestroyRef, Signal, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { OnlineService } from '../network/online.service';
import { UnsavedWork } from '../network/unsaved-work';
import { FileKind, FileType, fileKind, typeByName } from './file-kinds';
import { FileRefusal, FilesService, toFileRefusal } from './files.service';

/** Where one chosen file stands. */
export type FileRowState = 'uploading' | 'uploaded' | 'refused' | 'failed';

/** One file a person chose in an upload control, from choosing to being ready to save. */
export interface FileRow {
  /** Stable for the life of the row; not the platform's id. */
  readonly key: number;
  readonly name: string;
  readonly sizeBytes: number;
  /** What the name says; the platform's own reading replaces it once uploaded. */
  readonly type: FileType | null;
  readonly state: FileRowState;
  readonly percent: number;
  /** Why it was refused or failed. */
  readonly reason: FileRefusal | { readonly code: 'OFFLINE' } | null;
  /** The platform's id, once uploaded. */
  readonly id: string | null;
}

/**
 * The state of one upload control (spec 009, FR-037 to FR-043): the files a person chose for
 * one record of one kind, each uploading as it is chosen. A page sends `ids()` with its save
 * and keeps the save disabled while `busy()`. Made with `createFileUploads` in an injection
 * context.
 */
export class FileUploads {
  private readonly held = signal<readonly FileRow[]>([]);
  private readonly sending = new Map<number, { file: File; work: Subscription | null }>();
  private next = 1;

  readonly kind: FileKind;

  /** Every chosen file, in the order chosen. */
  readonly rows: Signal<readonly FileRow[]> = this.held.asReadonly();

  /** True while anything is still on its way: the page's save waits. */
  readonly busy = computed(() => this.held().some((row) => row.state === 'uploading'));

  /** The platform's ids of what is ready to be saved with the record. */
  readonly ids = computed(() =>
    this.held()
      .filter((row) => row.state === 'uploaded' && row.id !== null)
      .map((row) => row.id as string),
  );

  /**
   * True while a chosen file would be lost if the page were drawn anew: on its way, ready to
   * save, or waiting to be tried again. A refused file is not kept.
   */
  readonly unsaved = computed(() => this.held().some((row) => row.state !== 'refused'));

  /** True when the record can take no more: the control stops taking files and says so. */
  readonly full = computed(() => {
    const max = this.kind.maxPerRecord;
    return max !== null && this.counted() + this.already() >= max;
  });

  constructor(
    kindKey: string,
    private readonly files: FilesService,
    private readonly connection: OnlineService,
    /** How many files the record already holds, from the page. */
    private readonly already: () => number = () => 0,
  ) {
    const kind = fileKind(kindKey);
    if (kind === undefined) {
      throw new Error(`'${kindKey}' is not a kind of file.`);
    }
    this.kind = kind;
  }

  /** Takes the files a person chose or dropped. One refused file does not stop the others. */
  add(files: readonly File[]): void {
    for (const file of files) {
      const key = this.next++;
      const row: FileRow = {
        key,
        name: file.name,
        sizeBytes: file.size,
        type: typeByName(file.name),
        state: 'uploading',
        percent: 0,
        reason: null,
        id: null,
      };

      // What the browser can already see is said at once; the platform decides in any case.
      const early = this.early(file);
      if (early !== null) {
        this.held.update((rows) => [...rows, { ...row, state: 'refused', reason: early }]);
        continue;
      }

      this.held.update((rows) => [...rows, row]);
      this.sending.set(key, { file, work: null });
      this.send(key);
    }
  }

  /** Sends a failed file again. */
  retry(row: FileRow): void {
    if (row.state !== 'failed' || !this.sending.has(row.key)) {
      return;
    }
    this.change(row.key, { state: 'uploading', percent: 0, reason: null });
    this.send(row.key);
  }

  /** Takes a file out of the list; one already uploaded is discarded on the platform too. */
  remove(row: FileRow): void {
    this.sending.get(row.key)?.work?.unsubscribe();
    this.sending.delete(row.key);
    this.held.update((rows) => rows.filter((each) => each.key !== row.key));
    if (row.id !== null) {
      void this.files.discard(row.id).catch(() => {
        // Left behind, it is removed by the platform within a day.
      });
    }
  }

  /** After the page saved: the files are the record's now, and the control starts empty. */
  clear(): void {
    for (const each of this.sending.values()) {
      each.work?.unsubscribe();
    }
    this.sending.clear();
    this.held.set([]);
  }

  /** Files that count towards the record: on their way, or ready. */
  private counted(): number {
    return this.held().filter((row) => row.state === 'uploading' || row.state === 'uploaded')
      .length;
  }

  private early(file: File): FileRefusal | null {
    const max = this.kind.maxPerRecord;
    if (max !== null && this.counted() + this.already() >= max) {
      return { code: 'FILE_COUNT_REACHED' };
    }
    if (file.size === 0) {
      return { code: 'FILE_EMPTY' };
    }
    if (file.size > this.kind.maxBytes) {
      return { code: 'FILE_TOO_LARGE', sizeBytes: file.size, maxBytes: this.kind.maxBytes };
    }
    const type = typeByName(file.name);
    if (type === null || !this.kind.types.includes(type)) {
      return { code: 'FILE_TYPE_NOT_ACCEPTED', accepted: this.kind.types };
    }
    return null;
  }

  private send(key: number): void {
    const sending = this.sending.get(key);
    if (sending === undefined) {
      return;
    }
    if (!this.connection.online()) {
      this.change(key, { state: 'failed', reason: { code: 'OFFLINE' } });
      return;
    }

    sending.work = this.files.upload(this.kind.key, sending.file).subscribe({
      next: (step) => {
        if (step.kind === 'progress') {
          this.change(key, { percent: step.percent });
        } else {
          this.sending.delete(key);
          this.change(key, {
            state: 'uploaded',
            percent: 100,
            id: step.file.id,
            name: step.file.name,
            sizeBytes: step.file.sizeBytes,
            type: step.file.type,
          });
        }
      },
      error: (error: unknown) => {
        const reason = toFileRefusal(error);
        // What can be tried again keeps its file; what the platform refused does not.
        const again = reason.code === 'FAILED' || reason.code === 'FILE_NOT_STORED';
        if (!again) {
          this.sending.delete(key);
        }
        this.change(key, { state: again ? 'failed' : 'refused', reason });
      },
    });
  }

  private change(key: number, change: Partial<FileRow>): void {
    this.held.update((rows) => rows.map((row) => (row.key === key ? { ...row, ...change } : row)));
  }
}

/** Makes the state of one upload control. Call where `inject` works. */
export function createFileUploads(kind: string, already: () => number = () => 0): FileUploads {
  const uploads = new FileUploads(kind, inject(FilesService), inject(OnlineService), already);

  // The shell does not draw the page anew over files a person has not saved (US4, scenario 8).
  inject(DestroyRef).onDestroy(inject(UnsavedWork).hold(uploads.unsaved));
  return uploads;
}
