import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OnlineService } from 'core';
import { uiTranslations } from '../../testing/ui-testing';
import { ScsToastOutletComponent, ToastService } from '../overlay/overlay';
import {
  ScsAppliedChipsComponent,
  ScsEmptyStateComponent,
  ScsLanguageToggleComponent,
  ScsListToolbarComponent,
  ScsPageHeaderComponent,
  ScsTableFooterComponent,
  ScsTabsComponent,
  ScsWriteDirective,
} from './list';

@Component({
  imports: [
    ScsPageHeaderComponent,
    ScsTabsComponent,
    ScsListToolbarComponent,
    ScsAppliedChipsComponent,
    ScsTableFooterComponent,
    ScsEmptyStateComponent,
    ScsWriteDirective,
    ScsLanguageToggleComponent,
    ScsToastOutletComponent,
  ],
  template: `
    <scs-page-header title="المستخدمون" [parent]="parent()" [note]="note()">
      <button type="button" data-testid="save" [scsWrite]="online()" scsWriteReason="معطَّل" [disabled]="busy()">
        حفظ
      </button>
    </scs-page-header>
    <scs-tabs [tabs]="tabs" [(active)]="tab" />
    <scs-list-toolbar [(search)]="search" searchPlaceholder="بالاسم" clearLabel="مسح" [filterLabel]="filter()" />
    <scs-applied-chips
      [chips]="chips()"
      clearAllLabel="مسح الكل"
      removeLabel="إزالة"
      (remove)="removed.set($event)"
      (clearAll)="chips.set([])"
    />
    <scs-table-footer count="١–١٠ من ٢٥" [(page)]="page" [pages]="pages()" previousLabel="السابق" nextLabel="التالي" />
    <scs-empty-state [context]="context()" title="لا نتائج" text="غيّر البحث" />
    <scs-language-toggle [current]="language()" (chosen)="language.set($event)" />
    <scs-toast-outlet closeLabel="إغلاق" />
  `,
})
class Host {
  parent = signal('الإدارة');
  note = signal('');
  online = signal(true);
  busy = signal(false);
  tabs = [
    { key: 'all', label: 'الكل', count: '٢٥' },
    { key: 'active', label: 'النشطون' },
  ];
  tab = signal('all');
  search = signal('');
  filter = signal('تصفية');
  chips = signal([{ key: 'role', label: 'الدور: طبيب' }]);
  removed = signal('');
  page = signal(1);
  pages = signal(3);
  context = signal<'no-results' | 'error'>('no-results');
  language = signal<'ar' | 'en'>('ar');
}

const connection = signal(true);

function render() {
  connection.set(true);
  TestBed.configureTestingModule({
    imports: [Host, uiTranslations('ar')],
    providers: [{ provide: OnlineService, useValue: { online: connection } }],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const one = <T extends HTMLElement = HTMLElement>(id: string) =>
    root.querySelector<T>(`[data-testid="${id}"]`);
  return { fixture, host: fixture.componentInstance, root, one };
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.detectChanges();
  await fixture.whenStable();
}

describe('the parts of a list', () => {
  it('heads a page with where it sits, its title, and its buttons at the end', async () => {
    const { fixture, host, root, one } = render();

    expect(root.querySelector('scs-page-header nav')!.textContent).toContain('الإدارة');
    expect(one('page-title')!.textContent).toBe('المستخدمون');
    expect(one('page-note')).toBeNull();
    expect(one('page-actions')!.contains(one('save'))).toBe(true);

    host.parent.set('');
    host.note.set('٢٥ مستخدمًا');
    await settle(fixture);

    expect(root.querySelector('scs-page-header nav')).toBeNull();
    expect(one('page-note')!.textContent).toBe('٢٥ مستخدمًا');
  });

  it('shows the frame’s warning under the header while there is no connection, and takes it away', async () => {
    const { fixture, root, one } = render();
    expect(one('offline-alert')).toBeNull();

    connection.set(false);
    await settle(fixture);

    const alert = one('offline-alert')!;
    expect(alert.textContent!.trim()).toBe(
      'انقطع الاتصال بالإنترنت — ما تراه آخر نسخة محمَّلة، وما يغيّر البيانات معطَّل حتى يعود الاتصال.',
    );
    expect(alert.getAttribute('data-tone')).toBe('warning');
    expect(root.querySelector('scs-page-header')!.contains(alert)).toBe(true);

    connection.set(true);
    await settle(fixture);
    expect(one('offline-alert')).toBeNull();
  });

  it('marks the active tab and changes it on press', async () => {
    const { fixture, host, one } = render();

    expect(one('tab-all')!.getAttribute('aria-selected')).toBe('true');
    expect(one('tab-all')!.textContent).toContain('٢٥');

    one('tab-active')!.click();
    await settle(fixture);

    expect(host.tab()).toBe('active');
    expect(one('tab-active')!.getAttribute('aria-selected')).toBe('true');
  });

  it('searches as the person types, and offers the × only when there is something to clear', async () => {
    const { fixture, host, one } = render();
    const box = one<HTMLInputElement>('list-search')!;

    expect(box.getAttribute('placeholder')).toBe('بالاسم');
    expect(one('list-search-clear')).toBeNull();

    box.value = 'منى';
    box.dispatchEvent(new Event('input'));
    await settle(fixture);

    expect(host.search()).toBe('منى');

    one('list-search-clear')!.click();
    await settle(fixture);

    expect(host.search()).toBe('');
    expect(one('list-search-clear')).toBeNull();
  });

  it('draws the filter button pressed while its panel is open, with no count on it', async () => {
    const { fixture, host, one } = render();
    const button = one('list-filter')!;

    expect(button.textContent!.trim()).toBe('تصفية');
    expect(button.getAttribute('aria-expanded')).toBe('false');

    button.click();
    await settle(fixture);

    expect(button.getAttribute('aria-expanded')).toBe('true');

    host.filter.set('');
    await settle(fixture);

    expect(one('list-filter')).toBeNull();
  });

  it('shows what is applied as chips, and no row of chips when nothing is', async () => {
    const { fixture, host, root, one } = render();
    const row = root.querySelector<HTMLElement>('scs-applied-chips')!;

    expect(one('applied-chip')!.textContent).toContain('الدور: طبيب');
    expect(row.hidden).toBe(false);

    one('applied-chip')!.querySelector('button')!.click();
    expect(host.removed()).toBe('role');

    one('clear-all')!.click();
    await settle(fixture);

    expect(row.hidden).toBe(true);
    expect(one('clear-all')).toBeNull();
  });

  it('disables «السابق» on the first page and «التالي» on the last', async () => {
    const { fixture, host, one } = render();
    const previous = one<HTMLButtonElement>('page-previous')!;
    const next = one<HTMLButtonElement>('page-next')!;

    expect(one('table-count')!.textContent).toBe('١–١٠ من ٢٥');
    expect(previous.disabled).toBe(true);
    expect(next.disabled).toBe(false);
    expect(one('page-1')!.getAttribute('aria-current')).toBe('page');

    next.click();
    await settle(fixture);
    one('page-3')!.click();
    await settle(fixture);

    expect(host.page()).toBe(3);
    expect(previous.disabled).toBe(false);
    expect(next.disabled).toBe(true);
  });

  it('shows the count alone when the rows fit one page', async () => {
    const { fixture, host, one } = render();

    host.pages.set(1);
    await settle(fixture);

    expect(one('table-count')).not.toBeNull();
    expect(one('table-paging')).toBeNull();
  });

  it('announces a failure and not an empty result', async () => {
    const { fixture, host, root, one } = render();
    const state = root.querySelector('scs-empty-state')!;

    expect(one('empty-title')!.textContent).toBe('لا نتائج');
    expect(one('empty-text')!.textContent).toBe('غيّر البحث');
    expect(state.getAttribute('role')).toBeNull();

    host.context.set('error');
    await settle(fixture);

    expect(state.getAttribute('role')).toBe('alert');
  });

  it('names each language in its own words and offers only the other one', async () => {
    const { fixture, host, root, one } = render();

    expect(root.querySelector('scs-language-toggle [aria-current]')!.textContent).toBe('عربي');
    expect(one('language-switch')!.textContent!.trim()).toBe('EN');

    one('language-switch')!.click();
    await settle(fixture);

    expect(host.language()).toBe('en');
    expect(one('language-switch')!.textContent!.trim()).toBe('عربي');
  });
});

describe('ScsWriteDirective', () => {
  it('disables a writing control while there is no connection, says why, and gives it back', async () => {
    const { fixture, host, one } = render();
    const save = one<HTMLButtonElement>('save')!;

    expect(save.disabled).toBe(false);
    expect(save.getAttribute('data-blocked')).toBeNull();

    host.online.set(false);
    await settle(fixture);

    expect(save.disabled).toBe(true);
    expect(save.getAttribute('data-blocked')).toBe('offline');
    expect(save.title).toBe('معطَّل');

    host.online.set(true);
    await settle(fixture);

    expect(save.disabled).toBe(false);
    expect(save.hasAttribute('title')).toBe(false);
  });

  it('keeps the control’s own reason to be disabled when the connection is there', async () => {
    const { fixture, host, one } = render();

    host.busy.set(true);
    await settle(fixture);

    expect(one<HTMLButtonElement>('save')!.disabled).toBe(true);
    expect(one('save')!.getAttribute('data-blocked')).toBeNull();
  });
});

describe('ToastService', () => {
  afterEach(() => vi.useRealTimers());

  it('shows a notice where the frame holds them, and removes it by itself', async () => {
    vi.useFakeTimers();
    const { fixture, one } = render();

    TestBed.inject(ToastService).show('حُفظت التغييرات', 'على دور «طبيب»');
    fixture.detectChanges();

    expect(one('toast')!.textContent).toContain('حُفظت التغييرات');
    expect(one('toast')!.textContent).toContain('على دور «طبيب»');

    vi.advanceTimersByTime(ToastService.life);
    fixture.detectChanges();

    expect(one('toast')).toBeNull();
  });

  it('lets the person close a notice early', () => {
    const { fixture, one } = render();
    const toasts = TestBed.inject(ToastService);

    toasts.show('أُرشف السجل');
    fixture.detectChanges();
    one('toast')!.querySelector('button')!.click();
    fixture.detectChanges();

    expect(toasts.toasts()).toEqual([]);
  });
});
