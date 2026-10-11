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
      // Loading, empty and error are the shared parts of lists: one look per state (spec 006, FR-025).
      const shown =
        state === 'loading'
          ? element.querySelector('scs-skeleton')?.getAttribute('aria-label')
          : element.querySelector('.title, [data-testid="empty-title"], [data-testid="offline-alert"]')?.textContent;
      expect(shown?.trim().length, state).toBeGreaterThan(0);
      TestBed.resetTestingModule();
    }
  });

  it('shows an error with its message, its reference and a retry action', () => {
    const { fixture, element } = render('error', {
      message: 'الخدمة غير متاحة مؤقتًا',
      reference: '4bf92f3577b34da6a3ce929d0e0e4736',
    });

    expect(element.querySelector('[role="alert"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="empty-title"]')?.textContent).toContain(
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

    expect(element.querySelector('[data-testid="empty-title"]')?.textContent?.trim()).toBe(
      'تعذّر إتمام الطلب',
    );
    expect(element.querySelector('[data-testid="state-reference"]')).toBeNull();
  });

  it('shows the plan notice in the BRD wording with the upgrade path', () => {
    const { element } = render('not-in-plan');

    expect(element.querySelector('[data-testid="empty-title"]')?.textContent?.trim()).toBe(
      'غير مشمولة في باقتك',
    );
    expect(element.querySelector('scs-empty-state')?.getAttribute('data-context')).toBe('not-in-plan');
    expect(element.querySelector('[data-testid="upgrade"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="content"]')).toBeNull();
  });

  it('keeps the content visible under the view-only notice', () => {
    const { element } = render('view-only');

    expect(element.querySelector('.title')?.textContent?.trim()).toBe('للعرض فقط');
    expect(element.querySelector('[data-testid="content"]')).not.toBeNull();
    expect(element.querySelector('button')).toBeNull();
  });

  it('draws the view-only notice as the design does: the information banner, its title, and what changing needs', () => {
    // Spec 005, FR-030: one view-only notice in the library, the one of frame "Roles List Read Only".
    const { element } = render('view-only', {
      message: 'التعديل يحتاج صلاحية «إدارة الأدوار والصلاحيات».',
    });
    const notice = element.querySelector('[data-testid="view-only"]')!;

    expect(notice.tagName.toLowerCase()).toBe('scs-banner');
    expect(notice.getAttribute('data-tone')).toBe('info');
    expect(notice.getAttribute('role')).toBe('status');
    expect(notice.querySelector('scs-icon')?.getAttribute('data-icon')).toBe('info');
    expect(notice.querySelector('[data-testid="banner-title"]')?.textContent?.trim()).toBe('للعرض فقط');
    expect(notice.textContent).toContain('التعديل يحتاج صلاحية «إدارة الأدوار والصلاحيات».');
    expect(element.querySelector('.panel')).toBeNull();
  });

  it('reads "View only" in English, with the title alone when no line is given', () => {
    const { element } = render('view-only', {}, 'en');
    const notice = element.querySelector('[data-testid="view-only"]')!;

    expect(notice.querySelector('[data-testid="banner-title"]')?.textContent?.trim()).toBe('View only');
    expect(notice.querySelector('.line')).toBeNull();
  });

  it('says that work resumes when offline', () => {
    const { element } = render('offline');

    expect(element.querySelector('scs-banner')?.getAttribute('data-tone')).toBe('warning');
    expect(element.querySelector('[data-testid="offline-alert"]')?.textContent?.trim()).toBe(
      'انقطع الاتصال بالإنترنت — ما تراه آخر نسخة محمَّلة، وما يغيّر البيانات معطَّل حتى يعود الاتصال.',
    );
  });

  it('renders in English when English is active', () => {
    const { element } = render('empty', {}, 'en');

    expect(element.querySelector('[data-testid="empty-title"]')?.textContent?.trim()).toBe(
      'Nothing here yet',
    );
    expect(element.querySelector('scs-empty-state')).not.toBeNull();
  });
});
