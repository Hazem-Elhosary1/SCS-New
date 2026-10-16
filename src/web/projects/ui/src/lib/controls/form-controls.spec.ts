import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ScsBreadcrumbComponent, ScsKpiCardComponent } from '../list/page-parts';
import { ScsBadgeComponent } from '../badge/badge.component';
import { ScsCheckboxComponent, ScsSwitchComponent, ScsTextareaComponent } from './form-controls';
import { ScsSelectComponent } from './select.component';

@Component({
  imports: [
    ScsCheckboxComponent,
    ScsSelectComponent,
    ScsSwitchComponent,
    ScsTextareaComponent,
    ScsBadgeComponent,
    ScsKpiCardComponent,
    ScsBreadcrumbComponent,
  ],
  template: `
    <scs-checkbox
      label="عرض العيادات"
      testId="box"
      [(checked)]="ticked"
      [(indeterminate)]="partly"
      [disabled]="locked()"
      [reason]="reason()"
      [error]="boxError()"
    />
    <scs-select
      label="الدور"
      testId="role"
      placeholder="اختر الدور"
      required
      [options]="roles"
      [(value)]="role"
      [error]="error()"
    />
    <scs-switch label="إرسال دعوة" testId="invite" [(on)]="invite" [disabled]="locked()" [busy]="saving()" [reason]="reason()" />
    <scs-textarea label="الوصف" testId="words" [(value)]="words" [max]="500" hint="اختياري" />
    <scs-badge intent="success">نشط</scs-badge>
    <scs-kpi-card label="إجمالي المشغلين" value="12" note="جميع الأدوار" icon="people" />
    <scs-breadcrumb label="المسار" [crumbs]="[{ label: 'المشغلون', link: '/operators' }, { label: 'تفاصيل المشغل' }]" />
  `,
})
class Host {
  readonly ticked = signal(false);
  readonly partly = signal(false);
  readonly boxError = signal('');
  readonly saving = signal(false);
  readonly locked = signal(false);
  readonly reason = signal('');
  readonly role = signal('');
  readonly error = signal('');
  readonly invite = signal(true);
  readonly words = signal('');
  readonly roles = [
    { value: 'viewer', label: 'مشاهد فقط' },
    { value: 'admin', label: 'مسؤول عام', disabled: true, reason: 'يمنحه مسؤول عام فقط' },
  ];
}

/** Spec 011, research R16: the controls the operators' forms needed and `ui` did not have. */
describe('the form controls of the ui library', () => {
  function draw() {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = <T extends HTMLElement>(selector: string) => fixture.nativeElement.querySelector(selector) as T;
    return { fixture, host: fixture.componentInstance, find };
  }

  it('ticks a box, and a locked one names its reason as its description', () => {
    const { fixture, host, find } = draw();
    const box = find<HTMLInputElement>('[data-testid="box"]');
    box.click();
    expect(host.ticked()).toBe(true);

    host.locked.set(true);
    host.reason.set('مطلوبة لـتعديل بيانات العيادات');
    fixture.detectChanges();
    expect(box.disabled).toBe(true);
    const reason = find('[data-testid="box-reason"]');
    expect(reason.textContent).toContain('مطلوبة');
    expect(box.getAttribute('aria-describedby')).toBe(reason.id);
  });

  it('opens its own list, chooses by pointer and by keys, and keeps a refused choice out of reach', async () => {
    const { fixture, host, find } = draw();
    const trigger = find<HTMLButtonElement>('[data-testid="role"]');
    expect(trigger.getAttribute('role')).toBe('combobox');
    expect(trigger.textContent).toContain('اختر الدور');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');

    trigger.click();
    fixture.detectChanges();
    await fixture.whenStable();
    const panel = () => document.querySelector('[data-testid="role-panel"]');
    const rows = () => [...document.querySelectorAll<HTMLElement>('[data-testid="role-panel"] [role="option"]')];
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    // Required: no "nothing" row; the refused one is marked and says why.
    expect(rows().map((row) => row.getAttribute('aria-disabled'))).toEqual([null, 'true']);
    expect(rows()[1].textContent).toContain('يمنحه مسؤول عام فقط');

    rows()[1].click();
    fixture.detectChanges();
    expect(host.role()).toBe('');
    expect(panel()).not.toBeNull();

    rows()[0].click();
    fixture.detectChanges();
    await fixture.whenStable();
    expect(host.role()).toBe('viewer');
    expect(panel()).toBeNull();
    expect(trigger.textContent).toContain('مشاهد فقط');
    expect(trigger.getAttribute('aria-expanded')).toBe('false');

    // By keys: the arrow opens it on the chosen row, Escape closes it.
    trigger.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }));
    fixture.detectChanges();
    await fixture.whenStable();
    expect(rows()[0].getAttribute('aria-selected')).toBe('true');
    expect(rows()[0].getAttribute('data-active')).toBe('true');
    document.querySelector('[data-testid="role-panel"] [role="listbox"]')!.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }),
    );
    fixture.detectChanges();
    expect(panel()).toBeNull();

    host.error.set('هذا الحقل مطلوب.');
    fixture.detectChanges();
    expect(trigger.getAttribute('aria-invalid')).toBe('true');
    expect(find('[data-testid="role-error"]').id).toBe(trigger.getAttribute('aria-describedby'));
  });

  it('switches on and off, and a locked switch says why', () => {
    const { fixture, host, find } = draw();
    const button = find<HTMLButtonElement>('[data-testid="invite"]');
    expect(button.getAttribute('role')).toBe('switch');
    expect(button.getAttribute('aria-checked')).toBe('true');
    button.click();
    expect(host.invite()).toBe(false);

    host.locked.set(true);
    host.reason.set('مطلوب لكل المشغلين');
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(find('[data-testid="invite-reason"]').textContent).toContain('مطلوب لكل المشغلين');
  });

  it('draws a box partly chosen, clears that when it is chosen, and reads an error in place of the reason (spec 095)', () => {
    const { fixture, host, find } = draw();
    host.partly.set(true);
    fixture.detectChanges();
    const box = find<HTMLInputElement>('[data-testid="box"]');
    expect(box.indeterminate).toBe(true);
    box.click();
    fixture.detectChanges();
    expect(host.partly()).toBe(false);
    expect(host.ticked()).toBe(true);

    host.reason.set('مطلوبة لـتعديل بيانات العيادات');
    host.boxError.set('هذا الحقل مطلوب');
    fixture.detectChanges();
    expect(box.getAttribute('aria-invalid')).toBe('true');
    expect(find('[data-testid="box-error"]').id).toBe(box.getAttribute('aria-describedby'));
    expect(fixture.nativeElement.querySelector('[data-testid="box-reason"]')).toBeNull();
  });

  it('locks a busy switch and says so (spec 095)', () => {
    const { fixture, host, find } = draw();
    host.saving.set(true);
    fixture.detectChanges();
    const button = find<HTMLButtonElement>('[data-testid="invite"]');
    expect(button.disabled).toBe(true);
    expect(button.getAttribute('aria-busy')).toBe('true');
    button.click();
    expect(host.invite()).toBe(true);
  });

  it('takes free text up to its limit', () => {
    const { host, find } = draw();
    const area = find<HTMLTextAreaElement>('[data-testid="words"]');
    expect(area.maxLength).toBe(500);
    area.value = 'يرد على التذاكر';
    area.dispatchEvent(new Event('input'));
    expect(host.words()).toBe('يرد على التذاكر');
  });

  it('draws a state, a number and a trail with their words', () => {
    const { find } = draw();
    expect(find('scs-badge').getAttribute('data-intent')).toBe('success');
    expect(find('[data-testid="kpi-value"]').textContent).toBe('12');
    expect(find('[data-testid="kpi-note"]').textContent).toBe('جميع الأدوار');
    const trail = find('[data-testid="breadcrumb"]');
    expect(trail.querySelector('a')?.getAttribute('href')).toBe('/operators');
    expect(trail.querySelector('[aria-current="page"]')?.textContent).toBe('تفاصيل المشغل');
  });

  it('a searchable list may start with words in its search box, and shows only what they find (spec 095)', () => {
    const fixture = TestBed.createComponent(ScsSelectComponent);
    fixture.componentRef.setInput('label', 'العيادة');
    fixture.componentRef.setInput('testId', 'clinic');
    fixture.componentRef.setInput('searchable', true);
    fixture.componentRef.setInput('listInline', true);
    fixture.componentRef.setInput('searchLabel', 'بحث...');
    fixture.componentRef.setInput('options', [
      { value: 'noor', label: 'عيادة النور' },
      { value: 'new-noor', label: 'عيادة النور الجديدة' },
      { value: 'shifa', label: 'عيادة الشفاء' },
    ]);
    fixture.componentRef.setInput('searchText', 'النور');
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const search = element.querySelector('[data-testid="clinic-search"]') as HTMLInputElement;
    expect(search.value).toBe('النور');
    const rows = [...element.querySelectorAll('[data-testid="clinic-panel"] [role="option"]')].map((row) => row.textContent?.trim());
    expect(rows).toEqual(['عيادة النور', 'عيادة النور الجديدة']);
    // Without words, every row is there.
    fixture.componentRef.setInput('searchText', '');
    fixture.detectChanges();
    expect(element.querySelectorAll('[data-testid="clinic-panel"] [role="option"]').length).toBe(3);
  });
});
