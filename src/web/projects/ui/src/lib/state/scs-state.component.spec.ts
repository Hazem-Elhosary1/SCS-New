import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { uiTranslations } from '../../testing/ui-testing';
import { SCS_STATES, ScsState, ScsStateComponent } from './scs-state.component';

@Component({
  imports: [ScsStateComponent],
  template: `
    <scs-state
      [state]="state"
      [message]="message"
      [reference]="reference"
      (retry)="retries = retries + 1"
    >
      <p data-testid="content">content</p>
      <a scsUpgrade data-testid="upgrade" href="#">upgrade</a>
    </scs-state>
  `,
})
class Host {
  state: ScsState = 'loading';
  message = '';
  reference = '';
  retries = 0;
}

describe('ScsStateComponent', () => {
  function render(state: ScsState, changes: Partial<Host> = {}, language: 'ar' | 'en' = 'ar') {
    TestBed.configureTestingModule({ imports: [Host, uiTranslations(language)] });
    const fixture = TestBed.createComponent(Host);
    Object.assign(fixture.componentInstance, { state, ...changes });
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('gives each of the six states a defined appearance', () => {
    expect(SCS_STATES).toEqual([
      'loading',
      'empty',
      'error',
      'offline',
      'view-only',
      'not-in-plan',
    ]);

    for (const state of SCS_STATES) {
      const { element } = render(state);
      expect(element.querySelector('scs-state')?.getAttribute('data-state')).toBe(state);
      expect(element.querySelector('.panel .title')?.textContent?.trim().length).toBeGreaterThan(0);
      TestBed.resetTestingModule();
    }
  });

  it('shows an error with its message, its reference and a retry action', () => {
    const { fixture, element } = render('error', {
      message: 'الخدمة غير متاحة مؤقتًا',
      reference: '4bf92f3577b34da6a3ce929d0e0e4736',
    });

    expect(element.querySelector('[role="alert"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="state-message"]')?.textContent).toContain(
      'الخدمة غير متاحة مؤقتًا',
    );
    expect(element.querySelector('[data-testid="state-reference"]')?.textContent?.trim()).toBe(
      '4bf92f3577b34da6a3ce929d0e0e4736',
    );

    element.querySelector<HTMLButtonElement>('[data-testid="state-retry"]')!.click();
    expect(fixture.componentInstance.retries).toBe(1);
  });

  it('falls back to the default error text and omits the reference when there is none', () => {
    const { element } = render('error');

    expect(element.querySelector('[data-testid="state-message"]')?.textContent?.trim()).toBe(
      'تعذّر إتمام الطلب',
    );
    expect(element.querySelector('[data-testid="state-reference"]')).toBeNull();
  });

  it('shows the plan notice in the BRD wording with the upgrade path', () => {
    const { element } = render('not-in-plan');

    expect(element.querySelector('.title')?.textContent?.trim()).toBe('غير مشمولة في باقتك');
    expect(element.querySelector('[data-testid="upgrade"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="content"]')).toBeNull();
  });

  it('keeps the content visible under the view-only notice', () => {
    const { element } = render('view-only');

    expect(element.querySelector('.title')?.textContent?.trim()).toBe('للعرض فقط');
    expect(element.querySelector('[data-testid="content"]')).not.toBeNull();
    expect(element.querySelector('button')).toBeNull();
  });

  it('says that work resumes when offline', () => {
    const { element } = render('offline');

    expect(element.querySelector('.detail')?.textContent?.trim()).toBe(
      'يُستأنف العمل عند عودة الاتصال',
    );
  });

  it('renders in English when English is active', () => {
    const { element } = render('empty', {}, 'en');

    expect(element.querySelector('.title')?.textContent?.trim()).toBe('Nothing here yet');
  });
});
