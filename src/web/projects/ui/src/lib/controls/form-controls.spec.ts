import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ScsBreadcrumbComponent, ScsKpiCardComponent } from '../list/page-parts';
import { ScsCheckboxComponent, ScsStatusMarkComponent, ScsSwitchComponent, ScsTextareaComponent } from './form-controls';
import { ScsSelectComponent } from './select.component';

@Component({
  imports: [
    ScsCheckboxComponent,
    ScsSelectComponent,
    ScsSwitchComponent,
    ScsTextareaComponent,
    ScsStatusMarkComponent,
    ScsKpiCardComponent,
    ScsBreadcrumbComponent,
  ],
  template: `
    <scs-checkbox label="عرض العيادات" testId="box" [(checked)]="ticked" [disabled]="locked()" [reason]="reason()" />
    <scs-select
      label="الدور"
      testId="role"
      placeholder="اختر الدور"
      required
      [options]="roles"
      [(value)]="role"
      [error]="error()"
    />
    <scs-switch label="إرسال دعوة" testId="invite" [(on)]="invite" [disabled]="locked()" [reason]="reason()" />
    <scs-textarea label="الوصف" testId="words" [(value)]="words" [max]="500" hint="اختياري" />
    <scs-status-mark tone="success">نشط</scs-status-mark>
    <scs-kpi-card label="إجمالي المشغلين" value="12" note="جميع الأدوار" icon="people" />
    <scs-breadcrumb label="المسار" [crumbs]="[{ label: 'المشغلون', link: '/operators' }, { label: 'تفاصيل المشغل' }]" />
  `,
})
class Host {
  readonly ticked = signal(false);
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
    expect(find('scs-status-mark').getAttribute('data-tone')).toBe('success');
    expect(find('[data-testid="kpi-value"]').textContent).toBe('12');
    expect(find('[data-testid="kpi-note"]').textContent).toBe('جميع الأدوار');
    const trail = find('[data-testid="breadcrumb"]');
    expect(trail.querySelector('a')?.getAttribute('href')).toBe('/operators');
    expect(trail.querySelector('[aria-current="page"]')?.textContent).toBe('تفاصيل المشغل');
  });
});
