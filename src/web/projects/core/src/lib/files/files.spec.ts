import { HttpEventType, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsProblemError } from '../http/problem';
import { OnlineService } from '../network/online.service';
import { UnsavedWork } from '../network/unsaved-work';
import {
  acceptAttribute,
  acceptedLabel,
  fileDateLabel,
  fileKind,
  sizeLabel,
  typeByName,
} from './file-kinds';
import { FileUploads, createFileUploads } from './file-uploads';
import { FilesService, toFileRefusal } from './files.service';

const MB = 1024 * 1024;

function file(name: string, size = 2000): File {
  return new File([new Uint8Array(size)], name);
}

function refused(code: string, more: object = {}, status = 400): ScsProblemError {
  return new ScsProblemError(
    { type: 'urn:scs:error:x', title: '', status, code, traceId: 't', ...more },
    false,
  );
}

describe('the kinds of file, as the console reads them', () => {
  it('knows a kind by its key, and not a stranger', () => {
    expect(fileKind('ticket.attachment')).toEqual({
      key: 'ticket.attachment',
      types: ['pdf', 'png', 'jpeg'],
      maxBytes: 10 * MB,
      maxPerRecord: 3,
      public: false,
    });
    expect(fileKind('doctor.photo')?.public).toBe(true);
    expect(fileKind('no.such-kind')).toBeUndefined();
  });

  it('writes a size as people read it, in the digits of the language', () => {
    expect(sizeLabel(2.4 * MB, 'ar')).toBe('٢٫٤ ميجابايت');
    expect(sizeLabel(2.4 * MB, 'en')).toBe('2.4 MB');
    expect(sizeLabel(10 * MB, 'ar')).toBe('١٠ ميجابايت');
    expect(sizeLabel(10 * MB, 'en')).toBe('10 MB');
    expect(sizeLabel(210 * 1024, 'ar')).toBe('٢١٠ كيلوبايت');
    expect(sizeLabel(210 * 1024, 'en')).toBe('210 KB');
    expect(sizeLabel(12, 'en')).toBe('1 KB');
  });

  it('writes the day a file was uploaded as the design does, in Cairo time', () => {
    expect(fileDateLabel('2026-08-21T09:00:00Z', 'ar')).toBe('٢١ أغسطس ٢٠٢٦');
    expect(fileDateLabel('2026-08-21T09:00:00Z', 'en')).toBe('21 Aug 2026');
    expect(fileDateLabel('2026-08-21T22:30:00Z', 'en')).toBe('22 Aug 2026');
  });

  it('says what a kind takes, in words, and what the chooser offers', () => {
    expect(acceptedLabel(fileKind('ticket.attachment')!, 'ar')).toBe('PDF أو صورة');
    expect(acceptedLabel(fileKind('ticket.attachment')!, 'en')).toBe('PDF or image');
    expect(acceptedLabel(fileKind('doctor.photo')!, 'ar')).toBe('صورة');
    expect(acceptedLabel(fileKind('doctor.photo')!, 'en')).toBe('Image');
    expect(acceptAttribute(fileKind('doctor.photo')!)).toBe('.jpg,.jpeg,image/jpeg,.png,image/png');
  });

  it('reads a type from a name only as a first, friendly check', () => {
    expect(typeByName('scan.PDF')).toBe('pdf');
    expect(typeByName('photo.jpeg')).toBe('jpeg');
    expect(typeByName('photo.jpg')).toBe('jpeg');
    expect(typeByName('shot.png')).toBe('png');
    expect(typeByName('program.exe')).toBeNull();
    expect(typeByName('no-extension')).toBeNull();
  });
});

describe('a refusal of the platform, read for a page', () => {
  it('keeps the figures and reasons the platform gave', () => {
    expect(toFileRefusal(refused('FILE_TOO_LARGE', { sizeBytes: 12, maxBytes: 10 }, 413))).toEqual({
      code: 'FILE_TOO_LARGE',
      sizeBytes: 12,
      maxBytes: 10,
    });
    expect(toFileRefusal(refused('FILE_TYPE_NOT_ACCEPTED', { accepted: ['pdf'] }))).toEqual({
      code: 'FILE_TYPE_NOT_ACCEPTED',
      accepted: ['pdf'],
    });
    expect(toFileRefusal(refused('FILE_REFUSED', { reason: 'unreadable' }, 410))).toEqual({
      code: 'FILE_REFUSED',
      reason: 'unreadable',
    });
    expect(toFileRefusal(refused('FILE_REFUSED', { reason: 'infected' }, 410))).toEqual({
      code: 'FILE_REFUSED',
      reason: 'infected',
    });
    for (const code of ['FILE_BEING_CHECKED', 'FILE_NOT_KEPT', 'FILE_LIMIT_REACHED'] as const) {
      expect(toFileRefusal(refused(code))).toEqual({ code });
    }
  });

  it('is "failed, try again" for anything it does not know', () => {
    expect(toFileRefusal(refused('SERVICE_UNAVAILABLE', {}, 503))).toEqual({ code: 'FAILED' });
    expect(toFileRefusal(new Error('boom'))).toEqual({ code: 'FAILED' });
  });
});

describe('the state of one upload control', () => {
  function start(kind = 'dev.sample', already = 0) {
    const online = signal(true);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: OnlineService, useValue: { online } },
      ],
    });
    const uploads: FileUploads = TestBed.runInInjectionContext(() =>
      createFileUploads(kind, () => already),
    );
    return { uploads, online, http: TestBed.inject(HttpTestingController) };
  }

  const upload = (http: HttpTestingController) =>
    http.expectOne((request) => request.method === 'POST' && request.url === '/v1/files');

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('refuses a kind the table does not hold', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });

    expect(() => TestBed.runInInjectionContext(() => createFileUploads('no.such-kind'))).toThrow();
  });

  it('sends a file as it is chosen, shows its progress, and holds its id when taken', () => {
    const { uploads, http } = start();

    uploads.add([file('scan.pdf', 4000)]);

    const sent = upload(http);
    expect(sent.request.params.get('kind')).toBe('dev.sample');
    expect((sent.request.body as FormData).get('file')).toBeInstanceOf(File);
    expect(uploads.rows()[0]).toMatchObject({ name: 'scan.pdf', state: 'uploading', percent: 0 });
    expect(uploads.busy()).toBe(true);
    expect(uploads.ids()).toEqual([]);

    sent.event({ type: HttpEventType.UploadProgress, loaded: 1600, total: 4000 });
    expect(uploads.rows()[0].percent).toBe(40);

    sent.flush({ id: 'f-1', name: 'scan.pdf', sizeBytes: 4000, type: 'pdf', state: 'being-checked' });

    expect(uploads.rows()[0]).toMatchObject({ state: 'uploaded', percent: 100, id: 'f-1', type: 'pdf' });
    expect(uploads.busy()).toBe(false);
    expect(uploads.ids()).toEqual(['f-1']);
  });

  it('says at once what the browser can already see, and sends nothing for it', () => {
    const { uploads, http } = start('doctor.photo');

    uploads.add([file('huge.png', 3 * MB), file('notes.pdf'), file('empty.png', 0)]);

    http.expectNone('/v1/files');
    expect(uploads.rows().map((row) => [row.state, row.reason?.code])).toEqual([
      ['refused', 'FILE_TOO_LARGE'],
      ['refused', 'FILE_TYPE_NOT_ACCEPTED'],
      ['refused', 'FILE_EMPTY'],
    ]);
    expect(uploads.busy()).toBe(false);
  });

  it('one refused file does not stop the others', () => {
    const { uploads, http } = start();

    uploads.add([file('program.exe'), file('a.pdf'), file('b.png')]);

    const sent = http.match((request) => request.url === '/v1/files');
    expect(sent).toHaveLength(2);
    sent[0].flush({ id: 'f-a', name: 'a.pdf', sizeBytes: 2000, type: 'pdf', state: 'being-checked' });
    sent[1].flush(
      { type: 'urn:x', title: '', status: 400, code: 'FILE_TYPE_NOT_ACCEPTED', traceId: 't', accepted: ['pdf'] },
      { status: 400, statusText: 'Bad Request' },
    );

    // Here the plain client gives no platform problem, so the second refusal reads as "failed".
    expect(uploads.rows().map((row) => row.state)).toEqual(['refused', 'uploaded', 'failed']);
    expect(uploads.ids()).toEqual(['f-a']);
  });

  it('takes no more than the record may hold, counting what it already holds', () => {
    const { uploads, http } = start('dev.sample', 1);

    uploads.add([file('a.pdf'), file('b.pdf'), file('c.pdf')]);

    expect(http.match((request) => request.url === '/v1/files')).toHaveLength(2);
    expect(uploads.rows()[2]).toMatchObject({ state: 'refused', reason: { code: 'FILE_COUNT_REACHED' } });
    expect(uploads.full()).toBe(true);
  });

  it('a failed upload can be tried again, and what the platform refused cannot', () => {
    const { uploads, http } = start();
    uploads.add([file('a.pdf')]);
    upload(http).flush(null, { status: 503, statusText: 'Unavailable' });

    expect(uploads.rows()[0]).toMatchObject({ state: 'failed', reason: { code: 'FAILED' } });

    uploads.retry(uploads.rows()[0]);
    expect(uploads.rows()[0].state).toBe('uploading');
    upload(http).flush({ id: 'f-1', name: 'a.pdf', sizeBytes: 2000, type: 'pdf', state: 'being-checked' });
    expect(uploads.ids()).toEqual(['f-1']);

    // Trying a file again that is not failed does nothing.
    uploads.retry(uploads.rows()[0]);
    http.expectNone('/v1/files');
  });

  it('with no connection a file is not lost: it reads as not uploaded and can be tried again', () => {
    const { uploads, http, online } = start();
    online.set(false);

    uploads.add([file('a.pdf')]);

    http.expectNone('/v1/files');
    expect(uploads.rows()[0]).toMatchObject({ state: 'failed', reason: { code: 'OFFLINE' } });

    online.set(true);
    uploads.retry(uploads.rows()[0]);
    upload(http).flush({ id: 'f-1', name: 'a.pdf', sizeBytes: 2000, type: 'pdf', state: 'being-checked' });
    expect(uploads.ids()).toEqual(['f-1']);
  });

  it('tells the console it holds work not saved, so the page is not drawn anew over it', () => {
    const { uploads, http, online } = start();
    const unsaved = TestBed.inject(UnsavedWork);
    expect(unsaved.any()).toBe(false);

    // A refused file is not kept, and holds nothing.
    uploads.add([file('a.exe')]);
    expect(unsaved.any()).toBe(false);

    // One waiting to be tried again is.
    online.set(false);
    uploads.add([file('a.pdf')]);
    expect(unsaved.any()).toBe(true);

    // And one ready to be saved, until the page saves or the person takes it out.
    online.set(true);
    uploads.retry(uploads.rows()[1]);
    upload(http).flush({ id: 'f-1', name: 'a.pdf', sizeBytes: 2000, type: 'pdf', state: 'being-checked' });
    expect(unsaved.any()).toBe(true);
    uploads.clear();
    expect(unsaved.any()).toBe(false);

    // A page that is gone holds nothing: what it registered is taken out.
    const release = unsaved.hold(() => true);
    expect(unsaved.any()).toBe(true);
    release();
    expect(unsaved.any()).toBe(false);
  });

  it('removing an uploaded file discards it on the platform; removing one on its way stops it', () => {
    const { uploads, http } = start();
    uploads.add([file('a.pdf'), file('b.pdf')]);
    const [first, second] = http.match((request) => request.url === '/v1/files');
    first.flush({ id: 'f-1', name: 'a.pdf', sizeBytes: 2000, type: 'pdf', state: 'being-checked' });

    uploads.remove(uploads.rows()[1]);
    expect(second.cancelled).toBe(true);

    uploads.remove(uploads.rows()[0]);
    const discarded = http.expectOne('/v1/files/f-1');
    expect(discarded.request.method).toBe('DELETE');
    discarded.flush(null);

    expect(uploads.rows()).toEqual([]);
    expect(uploads.ids()).toEqual([]);
  });

  it('starts empty again after the page saved', () => {
    const { uploads, http } = start();
    uploads.add([file('a.pdf')]);
    upload(http).flush({ id: 'f-1', name: 'a.pdf', sizeBytes: 2000, type: 'pdf', state: 'being-checked' });

    uploads.clear();

    expect(uploads.rows()).toEqual([]);
    http.expectNone('/v1/files/f-1');
  });
});

describe('downloading a file', () => {
  it('asks for a way in, fetches the content through it, and hands it to the browser under its name', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const files = TestBed.inject(FilesService);
    const http = TestBed.inject(HttpTestingController);
    const clicked: { href: string; download: string }[] = [];
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(function (this: HTMLAnchorElement) {
        clicked.push({ href: this.href, download: this.download });
      });
    const made = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:made');

    const done = files.download('f-1');
    const asked = http.expectOne('/v1/files/f-1/access');
    expect(asked.request.method).toBe('POST');
    asked.flush({ url: '/v1/files/content/tok', name: 'أشعة.pdf', sizeBytes: 3, type: 'pdf', expiresAt: 'x' });
    await Promise.resolve();
    await Promise.resolve();
    const content = http.expectOne('/v1/files/content/tok');
    expect(content.request.responseType).toBe('blob');
    content.flush(new Blob(['abc']));
    await done;

    expect(clicked).toEqual([{ href: 'blob:made', download: 'أشعة.pdf' }]);
    click.mockRestore();
    made.mockRestore();
    http.verify();
  });

  it('rejects with what the platform said when the file is not available', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const files = TestBed.inject(FilesService);
    const http = TestBed.inject(HttpTestingController);

    const done = files.download('f-1').catch((error: unknown) => error);
    http
      .expectOne('/v1/files/f-1/access')
      .flush({ code: 'FILE_BEING_CHECKED' }, { status: 409, statusText: 'Conflict' });

    expect(await done).toBeTruthy();
    http.verify();
  });
});
