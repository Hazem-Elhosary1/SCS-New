import { CdkMenuTrigger } from '@angular/cdk/menu';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { ScsConfirmComponent, ScsFilterPopoverComponent } from '../list/list';
import { ScsMenuDirective, ScsMenuItemDirective, ScsTooltipDirective } from './overlay';

@Component({
  imports: [
    CdkMenuTrigger,
    ScsMenuDirective,
    ScsMenuItemDirective,
    ScsTooltipDirective,
    ScsFilterPopoverComponent,
    ScsConfirmComponent,
  ],
  template: `
    <button type="button" data-testid="opener" [cdkMenuTriggerFor]="menu" [scsTooltip]="tip()">⋯</button>
    <ng-template #menu>
      <div scsMenu data-testid="menu">
        <button type="button" scsMenuItem data-testid="archive" [active]="true" (chosen)="chosen.set('archive')">
          أرشفة
        </button>
        <button type="button" scsMenuItem danger data-testid="delete" (chosen)="chosen.set('delete')">حذف</button>
      </div>
    </ng-template>

    <scs-filter-popover heading="تصفية المواعيد" closeLabel="إغلاق" (closed)="filter.set('closed')" applyLabel="تطبيق" clearLabel="مسح الكل" (apply)="filter.set('applied')" (clear)="filter.set('cleared')">
      <input data-testid="facet" />
    </scs-filter-popover>

    <scs-confirm
      danger
      heading="حذف السجل"
      actionLabel="حذف السجل"
      cancelLabel="إلغاء"
      [ready]="ready()"
      [(open)]="confirming"
      (confirmed)="confirmed.set(true)"
    >
      <p data-testid="what">يُحذف نهائيًّا</p>
    </scs-confirm>
  `,
})
class Host {
  tip = signal('إجراءات السجل');
  chosen = signal('');
  filter = signal('');
  ready = signal(false);
  confirming = signal(true);
  confirmed = signal(false);
}

beforeAll(() => {
  // The test DOM has the dialog element but not its two methods.
  const proto = HTMLDialogElement.prototype as HTMLDialogElement & {
    showModal?: unknown;
    close?: unknown;
  };
  proto.showModal ??= function (this: HTMLDialogElement) {
    this.setAttribute('open', '');
  };
  proto.close ??= function (this: HTMLDialogElement) {
    this.removeAttribute('open');
    this.dispatchEvent(new Event('close'));
  };
});

function render() {
  TestBed.configureTestingModule({
    imports: [Host, TranslocoTestingModule.forRoot({ langs: { ar: {} } })],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const one = <T extends HTMLElement = HTMLElement>(id: string) =>
    document.querySelector<T>(`[data-testid="${id}"]`);
  const settle = async () => {
    fixture.detectChanges();
    await fixture.whenStable();
  };
  return { fixture, host: fixture.componentInstance, one, settle };
}

describe('a menu', () => {
  it('opens under its button, marks the active item, draws the dangerous one, and says what was chosen', async () => {
    const { host, one, settle } = render();

    one('opener')!.click();
    await settle();

    const menu = one('menu')!;
    expect(menu.getAttribute('role')).toBe('menu');
    expect(menu.classList).toContain('scs-menu');
    expect(one('archive')!.getAttribute('role')).toBe('menuitem');
    expect(one('archive')!.getAttribute('data-active')).toBe('true');
    expect(one('delete')!.getAttribute('data-danger')).toBe('true');
    expect(one('opener')!.getAttribute('aria-expanded')).toBe('true');

    one('delete')!.click();
    await settle();

    expect(host.chosen()).toBe('delete');
    expect(one('menu')).toBeNull();
    expect(one('opener')!.getAttribute('aria-expanded')).toBe('false');
  });
});

describe('ScsTooltipDirective', () => {
  const bubble = () => document.querySelector('.scs-tooltip');

  it('shows the name on hover and on focus, and takes it away on leave', async () => {
    const { one, settle } = render();
    const opener = one('opener')!;

    opener.dispatchEvent(new MouseEvent('mouseenter'));
    await settle();
    expect(bubble()?.textContent?.trim()).toBe('إجراءات السجل');
    expect(bubble()?.getAttribute('role')).toBe('tooltip');

    opener.dispatchEvent(new MouseEvent('mouseleave'));
    await settle();
    expect(bubble()).toBeNull();

    opener.dispatchEvent(new FocusEvent('focus'));
    await settle();
    expect(bubble()).not.toBeNull();

    opener.dispatchEvent(new FocusEvent('blur'));
    await settle();
    expect(bubble()).toBeNull();
  });

  it('shows nothing when there is nothing to say', async () => {
    const { host, one, settle } = render();
    host.tip.set('');
    await settle();

    one('opener')!.dispatchEvent(new MouseEvent('mouseenter'));
    await settle();

    expect(bubble()).toBeNull();
  });
});

describe('ScsFilterPopoverComponent', () => {
  it('applies on «تطبيق» and on Enter, and clears on «مسح الكل» — nothing before', async () => {
    const { host, one, settle } = render();

    expect(host.filter()).toBe('');
    expect(one('filter-apply')!.textContent!.trim()).toBe('تطبيق');
    expect(one('filter-clear')!.textContent!.trim()).toBe('مسح الكل');
    expect(one('filter-apply')!.closest('form')!.contains(one('facet'))).toBe(true);
    expect(one('filter-title')!.textContent).toBe('تصفية المواعيد');

    one('filter-close')!.click();
    await settle();
    expect(host.filter()).toBe('closed');

    one('filter-clear')!.click();
    await settle();
    expect(host.filter()).toBe('cleared');

    one<HTMLButtonElement>('filter-apply')!.click();
    await settle();
    expect(host.filter()).toBe('applied');
  });
});

describe('ScsConfirmComponent', () => {
  it('names the action on its button and keeps it disabled until what it needs is chosen', async () => {
    const { host, one, settle } = render();
    await settle();
    const action = one<HTMLButtonElement>('confirm-action')!;

    expect(one('dialog-title')!.textContent).toBe('حذف السجل');
    expect(action.textContent!.trim()).toBe('حذف السجل');
    expect(action.disabled).toBe(true);
    expect(action.classList).toContain('danger');
    expect(one('what')).not.toBeNull();

    action.click();
    expect(host.confirmed()).toBe(false);

    host.ready.set(true);
    await settle();
    expect(action.disabled).toBe(false);

    action.click();
    expect(host.confirmed()).toBe(true);
  });

  it('goes back without doing anything', async () => {
    const { host, one, settle } = render();
    await settle();

    one('confirm-cancel')!.click();
    await settle();

    expect(host.confirming()).toBe(false);
    expect(host.confirmed()).toBe(false);
  });
});
