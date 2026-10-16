import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FileUploads, LanguageService, OnlineService, createFileUploads } from 'core';
import { uiTranslations } from '../../testing/ui-testing';
import { ScsFileAction, ScsFileCardComponent, ScsFileCardState, ScsFileCardType } from './file-card.component';
import { ScsFileFieldComponent, fileCardType } from './file-field.component';
import { ScsFileUploadComponent } from './file-upload.component';

function file(name: string, size = 2000): File {
  return new File([new Uint8Array(size)], name);
}

function setUp(language: 'ar' | 'en', host: unknown) {
  TestBed.configureTestingModule({
    imports: [host as never, uiTranslations(language)],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: OnlineService, useValue: { online: signal(true) } },
    ],
  });
  TestBed.inject(LanguageService).use(language);
}

const text = (element: Element, id: string) =>
  element.querySelector(`[data-testid="${id}"]`)?.textContent?.trim() ?? null;

describe('ScsFileUploadComponent', () => {
  @Component({
    imports: [ScsFileUploadComponent],
    template: `<scs-file-upload
      [kind]="kind"
      [uploading]="uploading"
      [error]="error"
      [disabled]="disabled"
      [full]="full"
      (chosen)="chosen = $event"
    />`,
  })
  class Host {
    kind = 'ticket.attachment';
    uploading: number | null = null;
    error = '';
    disabled = false;
    full = false;
    chosen: File[] = [];
  }

  function render(language: 'ar' | 'en' = 'ar', changes: Partial<Host> = {}) {
    setUp(language, Host);
    const fixture = TestBed.createComponent(Host);
    Object.assign(fixture.componentInstance, changes);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return {
      fixture,
      element,
      host: fixture.componentInstance,
      zone: element.querySelector<HTMLElement>('[data-testid="file-zone"]')!,
      upload: element.querySelector<HTMLElement>('scs-file-upload')!,
    };
  }

  it('says before anything is chosen what it takes, from the kind, in each language', () => {
    const arabic = render('ar');
    expect(text(arabic.element, 'file-zone-title')).toBe('إفلات الملف هنا أو النقر للاختيار');
    expect(text(arabic.element, 'file-zone-hint')).toBe('ملف PDF أو صورة · حتى ١٠ ميجابايت');
    expect(arabic.upload.getAttribute('data-state')).toBe('default');

    TestBed.resetTestingModule();
    const english = render('en');
    expect(text(english.element, 'file-zone-title')).toBe('Drag a file here or click to browse');
    expect(text(english.element, 'file-zone-hint')).toBe('PDF or image · up to 10 MB');

    TestBed.resetTestingModule();
    const photo = render('en', { kind: 'doctor.photo' });
    expect(text(photo.element, 'file-zone-hint')).toBe('Image · up to 2 MB');
    const chooser = photo.element.querySelector<HTMLInputElement>('[data-testid="file-chooser"]')!;
    expect(chooser.accept).toBe('.jpg,.jpeg,image/jpeg,.png,image/png');
    expect(chooser.multiple).toBe(false);
  });

  it('is highlighted while a file is dragged over it, and gives what is dropped', () => {
    const { fixture, zone, upload, host } = render();
    const dropped = file('scan.pdf');

    zone.dispatchEvent(new Event('dragenter', { cancelable: true }));
    fixture.detectChanges();
    expect(upload.getAttribute('data-state')).toBe('drag');

    zone.dispatchEvent(new Event('dragleave', { cancelable: true }));
    fixture.detectChanges();
    expect(upload.getAttribute('data-state')).toBe('default');

    const drop = new Event('drop', { cancelable: true });
    Object.defineProperty(drop, 'dataTransfer', { value: { files: [dropped] } });
    zone.dispatchEvent(drop);
    fixture.detectChanges();

    expect(drop.defaultPrevented).toBe(true);
    expect(host.chosen).toEqual([dropped]);
  });

  it('opens the chooser on a press, on Enter and on Space, and gives what is chosen', () => {
    const { element, zone, fixture, host } = render();
    const chooser = element.querySelector<HTMLInputElement>('[data-testid="file-chooser"]')!;
    const opened = vi.spyOn(chooser, 'click').mockImplementation(() => undefined);

    zone.click();
    zone.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    zone.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', cancelable: true }));
    expect(opened).toHaveBeenCalledTimes(3);
    expect(zone.getAttribute('role')).toBe('button');
    expect(zone.getAttribute('tabindex')).toBe('0');

    const picked = file('photo.png');
    Object.defineProperty(chooser, 'files', { value: [picked], configurable: true });
    chooser.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(host.chosen).toEqual([picked]);
  });

  it('shows a file on its way with its progress', () => {
    const { element, upload } = render('en', { uploading: 64 });

    expect(upload.getAttribute('data-state')).toBe('uploading');
    expect(text(element, 'file-zone-title')).toBe('Uploading the file…');
    const bar = element.querySelector('[data-testid="file-zone-bar"]')!;
    expect(bar.getAttribute('role')).toBe('progressbar');
    expect(bar.getAttribute('aria-valuenow')).toBe('64');
  });

  it('says why a file was not taken, and stays ready for another', () => {
    const { element, upload, zone } = render('en', { error: 'Larger than 10 MB — choose a smaller file' });

    expect(upload.getAttribute('data-state')).toBe('error');
    expect(text(element, 'file-zone-title')).toBe('The file could not be uploaded');
    expect(text(element, 'file-zone-hint')).toBe('Larger than 10 MB — choose a smaller file');
    expect(zone.getAttribute('aria-disabled')).toBe('false');
    expect(zone.getAttribute('tabindex')).toBe('0');
  });

  it('takes nothing while disabled or full, and says the limit when full', () => {
    const disabled = render('ar', { disabled: true });
    const chooser = disabled.element.querySelector<HTMLInputElement>('[data-testid="file-chooser"]')!;
    const opened = vi.spyOn(chooser, 'click').mockImplementation(() => undefined);
    disabled.zone.click();
    const drop = new Event('drop', { cancelable: true });
    Object.defineProperty(drop, 'dataTransfer', { value: { files: [file('a.pdf')] } });
    disabled.zone.dispatchEvent(drop);

    expect(disabled.upload.getAttribute('data-state')).toBe('disabled');
    expect(disabled.zone.getAttribute('aria-disabled')).toBe('true');
    expect(disabled.zone.getAttribute('tabindex')).toBe('-1');
    expect(opened).not.toHaveBeenCalled();
    expect(disabled.host.chosen).toEqual([]);

    TestBed.resetTestingModule();
    const full = render('ar', { full: true });
    expect(full.upload.getAttribute('data-state')).toBe('full');
    expect(text(full.element, 'file-zone-hint')).toBe('بلغ العدد حدَّه: ٣');
  });
});

describe('ScsFileCardComponent', () => {
  @Component({
    imports: [ScsFileCardComponent],
    template: `<scs-file-card
      [name]="name"
      [type]="type"
      [state]="state"
      [meta]="meta"
      [percent]="percent"
      [reason]="reason"
      [canRetry]="canRetry"
      [actions]="actions"
      (download)="events.push('download')"
      (retry)="events.push('retry')"
      (remove)="events.push('remove')"
      (action)="events.push('action ' + $event)"
    />`,
  })
  class Host {
    name = 'ملف-الأشعة.pdf';
    type: ScsFileCardType = 'pdf';
    state: ScsFileCardState = 'default';
    meta = '٢٫٤ ميجابايت · ٢١ أغسطس ٢٠٢٦';
    percent: number | string = '٦٤';
    reason = '';
    canRetry = true;
    actions: ScsFileAction[] = [];
    events: string[] = [];
  }

  function render(changes: Partial<Host> = {}, language: 'ar' | 'en' = 'ar') {
    setUp(language, Host);
    const fixture = TestBed.createComponent(Host);
    Object.assign(fixture.componentInstance, changes);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element, host: fixture.componentInstance, card: element.querySelector('scs-file-card')! };
  }

  const press = (element: Element, id: string) =>
    element.querySelector<HTMLButtonElement>(`[data-testid="${id}"]`)!.click();

  it.each([
    ['image', 'image'],
    ['pdf', 'file'],
    ['other', 'paperclip'],
  ] as const)('draws the mark of a %s', (type, icon) => {
    const { element, card } = render({ type });

    expect(card.getAttribute('data-type')).toBe(type);
    expect(element.querySelector('.mark scs-icon')!.getAttribute('data-icon')).toBe(icon);
  });

  it('at rest shows the name as written, its size and date, and a download', () => {
    const { element, host } = render();
    const name = element.querySelector('[data-testid="file-name"]')!;

    expect(name.textContent!.trim()).toBe('ملف-الأشعة.pdf');
    expect(name.getAttribute('dir')).toBe('auto');
    expect(name.getAttribute('title')).toBe('ملف-الأشعة.pdf');
    expect(text(element, 'file-line')).toBe('٢٫٤ ميجابايت · ٢١ أغسطس ٢٠٢٦');
    expect(element.querySelector('[data-testid="file-download"]')!.getAttribute('aria-label')).toBe('تنزيل ملف-الأشعة.pdf');
    expect(element.querySelector('[data-testid="file-more"]')).toBeNull();

    press(element, 'file-download');
    expect(host.events).toEqual(['download']);
  });

  it('while uploading shows the progress and a spinner, and can be taken out', () => {
    const { element, host } = render({ state: 'uploading' });

    expect(text(element, 'file-line')).toBe('جارٍ الرفع… ٦٤٪');
    expect(element.querySelector('[data-testid="file-spinner"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="file-download"]')).toBeNull();

    press(element, 'file-remove');
    expect(host.events).toEqual(['remove']);
  });

  it('a failed upload says so and offers another try and removal', () => {
    const { element, host, card } = render({ state: 'error' }, 'en');

    expect(card.getAttribute('data-state')).toBe('error');
    expect(text(element, 'file-line')).toBe('Upload failed — try again');
    press(element, 'file-retry');
    press(element, 'file-remove');
    expect(host.events).toEqual(['retry', 'remove']);
  });

  it('a refused file gives its reason and no second try', () => {
    const { element } = render({ state: 'error', reason: 'Larger than 10 MB — choose a smaller file', canRetry: false }, 'en');

    expect(text(element, 'file-line')).toBe('Larger than 10 MB — choose a smaller file');
    expect(element.querySelector('[data-testid="file-retry"]')).toBeNull();
    expect(element.querySelector('[data-testid="file-remove"]')).not.toBeNull();
  });

  it.each([
    ['being-checked', 'قيد الفحص', 'Being checked'],
    ['refused', 'مرفوض: لم يجتز الفحص الأمني', 'Refused: it did not pass the security check'],
    ['not-kept', 'لم يعد محفوظًا', 'No longer kept'],
  ] as const)('a saved file that is %s says so in place of the download', (state, arabic, english) => {
    const ar = render({ state });
    expect(text(ar.element, 'file-line')).toBe(arabic);
    expect(ar.element.querySelector('[data-testid="file-download"]')).toBeNull();

    TestBed.resetTestingModule();
    expect(text(render({ state }, 'en').element, 'file-line')).toBe(english);
  });

  it('uploaded and not yet saved, it can only be taken out', () => {
    const { element } = render({ state: 'ready', meta: '٢٫٤ ميجابايت' });

    expect(text(element, 'file-line')).toBe('٢٫٤ ميجابايت');
    expect(element.querySelector('[data-testid="file-download"]')).toBeNull();
    expect(element.querySelector('[data-testid="file-remove"]')).not.toBeNull();
  });

  it('"more" holds what the page offers, and is absent when the page offers nothing', () => {
    const { element, fixture, host } = render({
      actions: [
        { key: 'remove', label: 'إزالة' },
        { key: 'hold', label: 'تعليق المحو' },
      ],
    });
    const more = element.querySelector<HTMLButtonElement>('[data-testid="file-more"]')!;
    expect(more.getAttribute('aria-expanded')).toBe('false');
    expect(more.getAttribute('aria-label')).toBe('إجراءات ملف-الأشعة.pdf');

    more.click();
    fixture.detectChanges();
    expect(more.getAttribute('aria-expanded')).toBe('true');
    const items = [...element.querySelectorAll('[role="menuitem"]')].map((item) => item.textContent!.trim());
    expect(items).toEqual(['إزالة', 'تعليق المحو']);

    press(element, 'file-action-hold');
    fixture.detectChanges();
    expect(host.events).toEqual(['action hold']);
    expect(element.querySelector('[data-testid="file-menu"]')).toBeNull();

    // A press elsewhere, and Escape, close it.
    more.click();
    fixture.detectChanges();
    document.body.click();
    fixture.detectChanges();
    expect(element.querySelector('[data-testid="file-menu"]')).toBeNull();
  });

  it('every control has a name a screen reader says', () => {
    for (const state of ['default', 'uploading', 'error', 'ready'] as const) {
      const { element } = render({ state, actions: [{ key: 'remove', label: 'إزالة' }] });
      for (const button of element.querySelectorAll('button.act')) {
        expect(button.getAttribute('aria-label'), state).toContain('ملف-الأشعة.pdf');
      }
      TestBed.resetTestingModule();
    }
  });
});

describe('ScsFileFieldComponent', () => {
  @Component({
    imports: [ScsFileFieldComponent],
    template: `<scs-file-field [uploads]="uploads" />`,
  })
  class Host {
    uploads!: FileUploads;
  }

  function render(language: 'ar' | 'en' = 'ar', kind = 'dev.sample') {
    setUp(language, Host);
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.uploads = TestBed.runInInjectionContext(() => createFileUploads(kind));
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element, uploads: fixture.componentInstance.uploads, http: TestBed.inject(HttpTestingController) };
  }

  const cards = (element: Element) => [...element.querySelectorAll('scs-file-card')];
  const taken = (id: string, name: string) => ({ id, name, sizeBytes: 2000, type: 'pdf', state: 'being-checked' });

  it('gives each chosen file its card, under the zone, and tells the page when it is busy', () => {
    const { fixture, element, uploads, http } = render('en');
    expect(element.querySelector('scs-file-upload')).not.toBeNull();
    expect(cards(element)).toHaveLength(0);

    uploads.add([file('scan.pdf', 2 * 1024 * 1024)]);
    fixture.detectChanges();

    const card = cards(element)[0];
    expect(card.getAttribute('data-state')).toBe('uploading');
    expect(card.getAttribute('data-type')).toBe('pdf');
    expect(element.querySelector('scs-file-upload')!.getAttribute('data-state')).toBe('uploading');
    expect(uploads.busy()).toBe(true);

    http.expectOne((request) => request.url === '/v1/files').flush(taken('f-1', 'scan.pdf'));
    fixture.detectChanges();

    expect(cards(element)[0].getAttribute('data-state')).toBe('ready');
    expect(text(element, 'file-line')).toBe('2 KB');
    expect(text(element, 'file-said')).toBe('scan.pdf was uploaded');
    expect(element.querySelector('scs-file-upload')!.getAttribute('data-state')).toBe('default');
    expect(uploads.busy()).toBe(false);
  });

  it('names the refused file and its reason, in the card, in the zone and aloud', () => {
    const { fixture, element, uploads, http } = render('ar');

    uploads.add([file('big.pdf', 11 * 1024 * 1024)]);
    fixture.detectChanges();

    http.expectNone('/v1/files');
    expect(cards(element)[0].getAttribute('data-state')).toBe('error');
    expect(text(element, 'file-line')).toBe('الحجم يتجاوز ١٠ ميجابايت — اختر ملفًا أصغر');
    expect(text(element, 'file-zone-hint')).toBe('الحجم يتجاوز ١٠ ميجابايت — اختر ملفًا أصغر');
    expect(text(element, 'file-said')).toBe('لم يُرفع big.pdf: الحجم يتجاوز ١٠ ميجابايت — اختر ملفًا أصغر');
    expect(element.querySelector('[data-testid="file-retry"]')).toBeNull();
    expect(element.querySelector('[data-testid="file-said"]')!.getAttribute('aria-live')).toBe('polite');

    // Another file chosen: the zone is ready, and the other card stays.
    uploads.add([file('ok.pdf')]);
    fixture.detectChanges();
    expect(element.querySelector('scs-file-upload')!.getAttribute('data-state')).toBe('uploading');
    expect(cards(element)).toHaveLength(2);
    http.expectOne((request) => request.url === '/v1/files').flush(taken('f-2', 'ok.pdf'));
  });

  it('says the limit when the record is full, and takes a file out again', () => {
    const { fixture, element, uploads, http } = render('en');
    uploads.add([file('a.pdf'), file('b.pdf'), file('c.pdf')]);
    http.match((request) => request.url === '/v1/files').forEach((sent, index) => sent.flush(taken(`f-${index}`, 'x.pdf')));
    fixture.detectChanges();

    expect(element.querySelector('scs-file-upload')!.getAttribute('data-state')).toBe('full');
    expect(text(element, 'file-zone-hint')).toBe('The limit is reached: 3');

    element.querySelector<HTMLButtonElement>('[data-testid="file-remove"]')!.click();
    fixture.detectChanges();
    http.expectOne('/v1/files/f-0').flush(null);

    expect(cards(element)).toHaveLength(2);
    expect(element.querySelector('scs-file-upload')!.getAttribute('data-state')).toBe('default');
    expect(text(element, 'file-said')).toBe('x.pdf was removed');
  });

  it('draws the mark by what the file is', () => {
    expect(fileCardType('pdf')).toBe('pdf');
    expect(fileCardType('png')).toBe('image');
    expect(fileCardType('jpeg')).toBe('image');
    expect(fileCardType(null)).toBe('other');
  });
});
