import { HttpClient, HttpEventType } from '@angular/common/http';
import { DOCUMENT, Injectable, inject } from '@angular/core';
import { Observable, filter, firstValueFrom, map } from 'rxjs';
import { ScsProblemError } from '../http/problem';
import { FileType } from './file-kinds';

/** A file the platform took: unattached, and being checked. */
export interface UploadedFile {
  readonly id: string;
  readonly name: string;
  readonly sizeBytes: number;
  readonly type: FileType;
  readonly state: 'being-checked' | 'available' | 'refused';
}

/** Progress of one upload: per cent sent, then the file. */
export type UploadStep =
  | { readonly kind: 'progress'; readonly percent: number }
  | { readonly kind: 'done'; readonly file: UploadedFile };

/** Why a file was not taken or not given, as the platform said it. */
export type FileRefusal =
  | { readonly code: 'FILE_TOO_LARGE'; readonly sizeBytes: number; readonly maxBytes: number }
  | { readonly code: 'FILE_TYPE_NOT_ACCEPTED'; readonly accepted: readonly FileType[] }
  | { readonly code: 'FILE_REFUSED'; readonly reason: 'infected' | 'unreadable' }
  | {
      readonly code:
        | 'FILE_EMPTY'
        | 'FILE_LIMIT_REACHED'
        | 'FILE_COUNT_REACHED'
        | 'FILE_NOT_STORED'
        | 'FILE_BEING_CHECKED'
        | 'FILE_NOT_KEPT'
        | 'PERMISSION_REQUIRED'
        | 'FAILED';
    };

/** Reads a failed call as a refusal a page can word. Anything unknown is "failed, try again". */
export function toFileRefusal(error: unknown): FileRefusal {
  if (!(error instanceof ScsProblemError)) {
    return { code: 'FAILED' };
  }
  const problem = error.problem as unknown as Record<string, unknown>;
  switch (error.problem.code) {
    case 'FILE_TOO_LARGE':
      return {
        code: 'FILE_TOO_LARGE',
        sizeBytes: Number(problem['sizeBytes']),
        maxBytes: Number(problem['maxBytes']),
      };
    case 'FILE_TYPE_NOT_ACCEPTED':
      return {
        code: 'FILE_TYPE_NOT_ACCEPTED',
        accepted: (problem['accepted'] as FileType[] | undefined) ?? [],
      };
    case 'FILE_REFUSED':
      return {
        code: 'FILE_REFUSED',
        reason: problem['reason'] === 'unreadable' ? 'unreadable' : 'infected',
      };
    case 'FILE_EMPTY':
    case 'FILE_LIMIT_REACHED':
    case 'FILE_COUNT_REACHED':
    case 'FILE_NOT_STORED':
    case 'FILE_BEING_CHECKED':
    case 'FILE_NOT_KEPT':
    case 'PERMISSION_REQUIRED':
      return { code: error.problem.code };
    default:
      return { code: 'FAILED' };
  }
}

interface WayIn {
  readonly url: string;
  readonly name: string;
}

/**
 * The platform's addresses for files (spec 009): take a file in as it is chosen, discard one
 * not yet saved, and download one — by asking for a way in, fetching the content with the
 * person's session, and handing it to the browser to save under its own name.
 */
@Injectable({ providedIn: 'root' })
export class FilesService {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);

  /** Sends one file. Emits per cent as it goes, then the file as the platform holds it. */
  upload(kind: string, file: File): Observable<UploadStep> {
    const form = new FormData();
    form.append('file', file, file.name);

    return this.http
      .post<UploadedFile>('/v1/files', form, {
        params: { kind },
        reportProgress: true,
        observe: 'events',
      })
      .pipe(
        filter(
          (event) =>
            event.type === HttpEventType.UploadProgress || event.type === HttpEventType.Response,
        ),
        map((event): UploadStep => {
          if (event.type === HttpEventType.UploadProgress) {
            const total = event.total ?? file.size;
            return {
              kind: 'progress',
              percent: total > 0 ? Math.min(99, Math.floor((event.loaded / total) * 100)) : 0,
            };
          }
          return { kind: 'done', file: (event as { body: UploadedFile }).body };
        }),
      );
  }

  /** Removes a file the person uploaded and has not saved with anything. */
  discard(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/v1/files/${id}`));
  }

  /**
   * Downloads one file. Rejects with what `toFileRefusal` reads: still being checked, refused,
   * no longer kept, not this person's to see.
   */
  async download(id: string): Promise<void> {
    const wayIn = await firstValueFrom(this.http.post<WayIn>(`/v1/files/${id}/access`, null));
    const content = await firstValueFrom(this.http.get(wayIn.url, { responseType: 'blob' }));
    this.save(content, wayIn.name);
  }

  private save(content: Blob, name: string): void {
    const view = this.document.defaultView;
    if (view === null) {
      return;
    }
    const address = view.URL.createObjectURL(content);
    const link = this.document.createElement('a');
    link.href = address;
    link.download = name;
    link.rel = 'noopener';
    link.click();
    // The browser has taken the address by now; holding it longer only holds the bytes.
    view.setTimeout(() => view.URL.revokeObjectURL(address), 60_000);
  }
}
