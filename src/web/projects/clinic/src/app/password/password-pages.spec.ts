import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, Type, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { AuthService, LinkAnswer, PasswordLinkKind, PasswordLinkView, ScsProblemError } from 'core';
import { SignInFlow } from '../sign-in/sign-in-flow';
import { ChangePasswordPage } from './change-password.page';
import { ForgotPasswordPage } from './forgot-password.page';
import { SetPasswordPage } from './set-password.page';

const NEW = 'Brand-New-Pass-7!';

function refusal(code: string, status: number, errors?: Record<string, string[]>, wait?: number) {
  return new ScsProblemError(
    { type: 'urn:scs:error:x', title: code, status, code, traceId: 'a'.repeat(32), errors },
    false,
    wait ?? null,
  );
}

const offline = new ScsProblemError(
  { type: 'x', title: '', status: 0, code: 'SERVICE_UNAVAILABLE', traceId: 'a'.repeat(32) },
  true,
);

/** Stands in for the platform: answers what each test tells it to, and remembers what was asked. */
class FakeAuth {
  readonly calls: string[] = [];
  readonly session = signal<{ email: string } | null>({ email: 'temp@noor.example' });

  forgot: () => Promise<void> = async () => undefined;
  inspect: () => Promise<LinkAnswer<PasswordLinkView>> = async () => ({
    live: true,
    value: { kind: 'reset', email: 'owner@noor.example' },
  });
  set: () => Promise<LinkAnswer<PasswordLinkKind>> = async () => ({ live: true, value: 'reset' });
  change: () => Promise<void> = async () => undefined;

  requestPasswordReset(email: string) {
    this.calls.push(`forgot ${email}`);
    return this.forgot();
  }
  inspectLink(token: string) {
    this.calls.push(`inspect ${token}`);
    return this.inspect();
  }
  setPasswordWithLink(token: string, password: string) {
    this.calls.push(`set ${token} ${password}`);
    return this.set();
  }
  changeTemporaryPassword(password: string) {
    this.calls.push(`change ${password}`);
    return this.change();
  }
  async signOut() {
    this.calls.push('sign-out');
  }
}

@Component({ template: '' })
class Blank {}

interface Rendered<T> {
  fixture: ComponentFixture<T>;
  element: HTMLElement;
  auth: FakeAuth;
  router: Router;
  flow: SignInFlow;
  settle: () => Promise<void>;
  find: (id: string) => HTMLElement | null;
  type: (id: string, text: string) => Promise<void>;
  submit: () => Promise<void>;
}

async function render<T>(page: Type<T>, fragment: string | null = null): Promise<Rendered<T>> {
  const auth = new FakeAuth();
  TestBed.configureTestingModule({
    imports: [
      page,
      // No texts are loaded: a screen shows its keys, which is all these tests need.
      TranslocoTestingModule.forRoot({
        langs: { ar: {}, en: {} },
        translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: 'ar' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideRouter([{ path: '**', component: Blank }]),
      { provide: AuthService, useValue: auth },
      { provide: ActivatedRoute, useValue: { snapshot: { fragment } } },
    ],
  });
  return mount(page, auth);
}

async function mount<T>(page: Type<T>, auth: FakeAuth): Promise<Rendered<T>> {
  const fixture = TestBed.createComponent(page);
  const element = fixture.nativeElement as HTMLElement;
  const settle = async () => {
    for (let round = 0; round < 3; round++) {
      await new Promise((resolve) => setTimeout(resolve));
      fixture.detectChanges();
      await fixture.whenStable();
    }
  };
  const find = (id: string) => element.querySelector<HTMLElement>(`[data-testid="${id}"]`);
  await settle();

  return {
    fixture,
    element,
    auth,
    router: TestBed.inject(Router),
    flow: TestBed.inject(SignInFlow),
    settle,
    find,
    type: async (id, text) => {
      const input = find(id) as HTMLInputElement;
      input.value = text;
      input.dispatchEvent(new Event('input'));
      await settle();
    },
    submit: async () => {
      element.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
      await settle();
    },
  };
}

/** Spec 004: what each password page shows for each answer of the platform. */
describe('ForgotPasswordPage', () => {
  it('checks the address itself and asks nothing while it is missing or malformed', async () => {
    const page = await render(ForgotPasswordPage);

    await page.submit();
    expect(page.find('email-error')!.textContent).toContain('app.signIn.email.required');

    await page.type('email', 'not-an-email');
    await page.submit();
    expect(page.find('email-error')!.textContent).toContain('app.signIn.email.invalid');

    expect(page.auth.calls).toEqual([]);
    expect(page.element.querySelector('scs-banner')).toBeNull();
  });

  it('replaces the form with the result, naming the address, whatever the platform did', async () => {
    const page = await render(ForgotPasswordPage);

    await page.type('email', '  owner@noor.example ');
    await page.submit();

    expect(page.auth.calls).toEqual(['forgot owner@noor.example']);
    expect(page.find('forgot-sent-title')).not.toBeNull();
    expect(page.find('forgot-sent-email')!.textContent).toBe('owner@noor.example');
    expect(page.element.querySelector('form')).toBeNull();
  });

  it('says too many requests with the wait, and an outage, on the form as typed', async () => {
    const page = await render(ForgotPasswordPage);
    await page.type('email', 'owner@noor.example');

    page.auth.forgot = () => Promise.reject(refusal('RATE_LIMITED', 429, undefined, 37));
    await page.submit();
    expect(page.find('banner-rateLimited')).not.toBeNull();
    expect(page.find('retry-after')!.textContent).toBe('37');

    page.auth.forgot = () => Promise.reject(offline);
    await page.submit();
    expect(page.find('banner-rateLimited')).toBeNull();
    expect(page.find('banner-unreachable')).not.toBeNull();
    expect((page.find('email') as HTMLInputElement).value).toBe('owner@noor.example');
    expect(page.find('forgot-sent-title')).toBeNull();
  });
});

describe('SetPasswordPage', () => {
  it('shows the dead reset-link screen without asking when the address carries no token', async () => {
    for (const fragment of [null, '', 'x=1', 't=has spaces']) {
      TestBed.resetTestingModule();
      const page = await render(SetPasswordPage, fragment);

      expect(page.find('dead-link-title')).not.toBeNull();
      expect(page.find('ask-again')).not.toBeNull();
      expect(page.auth.calls).toEqual([]);
    }
  });

  it('shows the reset form for a live reset link, with the account and no clinic', async () => {
    const page = await render(SetPasswordPage, 't=tok_en-1');

    expect(page.auth.calls).toEqual(['inspect tok_en-1']);
    expect(page.find('reset-title')).not.toBeNull();
    expect(page.find('link-email')!.textContent).toBe('owner@noor.example');
    expect(page.find('clinic-name')).toBeNull();
    expect(page.find('link-life')).toBeNull();
    expect(page.element.querySelectorAll('input').length).toBe(1);
  });

  it('shows the new-account form with the clinic, and for a dead one no way to ask again', async () => {
    const auth = new FakeAuth();
    auth.inspect = async () => ({
      live: true,
      value: {
        kind: 'new-account',
        email: 'new@shifa.example',
        clinicNameAr: 'عيادة الشفاء',
        clinicNameEn: 'Al Shifa Clinic',
      },
    });
    let page = await renderWith(auth, 't=tok');
    expect(page.find('new-account-title')).not.toBeNull();
    expect(page.find('clinic-name')!.textContent).toBe('عيادة الشفاء');
    expect(page.find('link-life')).not.toBeNull();

    TestBed.resetTestingModule();
    const dead = new FakeAuth();
    dead.inspect = async () => ({ live: false, kind: 'new-account' });
    page = await renderWith(dead, 't=tok');
    expect(page.find('dead-link-text')!.textContent).toContain('app.password.dead.newAccount');
    expect(page.find('ask-again')).toBeNull();
    expect(page.element.querySelector('input')).toBeNull();
  });

  it('keeps the link when the platform cannot be asked, and tries again on request', async () => {
    const auth = new FakeAuth();
    auth.inspect = () => Promise.reject(offline);
    const page = await renderWith(auth, 't=tok');

    expect(page.find('banner-unreachable')).not.toBeNull();
    expect(page.find('dead-link-title')).toBeNull();

    auth.inspect = async () => ({ live: true, value: { kind: 'reset', email: 'a@b.example' } });
    page.find('link-retry')!.click();
    await page.settle();

    expect(page.find('reset-title')).not.toBeNull();
    expect(auth.calls).toEqual(['inspect tok', 'inspect tok']);
  });

  it('sends nothing while a rule is unmet, then hands the good news to the sign-in form', async () => {
    const page = await render(SetPasswordPage, 't=tok');

    await page.type('new-password', 'short');
    await page.submit();
    expect(page.find('rule-length')!.getAttribute('data-state')).toBe('unmet');
    expect(page.auth.calls).toEqual(['inspect tok']);

    await page.type('new-password', NEW);
    await page.submit();

    expect(page.auth.calls).toEqual(['inspect tok', `set tok ${NEW}`]);
    expect(page.router.url).toBe('/sign-in');
    expect(page.flow.takeNotice()).toEqual({
      kind: 'passwordChanged',
      email: 'owner@noor.example',
    });
    expect(page.flow.takeNotice()).toBeNull();
  });

  it('shows a refused password on the form and a link that ran out as the dead screen', async () => {
    const page = await render(SetPasswordPage, 't=tok');
    await page.type('new-password', NEW);

    page.auth.set = () =>
      Promise.reject(refusal('VALIDATION_FAILED', 400, { password: ['SAME_AS_CURRENT'] }));
    await page.submit();
    expect(page.find('new-password-error')!.textContent).toContain('app.password.sameAsCurrent');
    expect(page.element.querySelector('scs-banner')).toBeNull();

    page.auth.set = () => Promise.reject(refusal('RATE_LIMITED', 429, undefined, 12));
    await page.submit();
    expect(page.find('banner-rateLimited')).not.toBeNull();
    expect(page.find('new-password-error')).toBeNull();

    page.auth.set = async () => ({ live: false, kind: 'reset' });
    await page.submit();
    expect(page.find('dead-link-title')).not.toBeNull();
    expect(page.element.querySelector('input')).toBeNull();
    expect(page.flow.takeNotice()).toBeNull();
  });

  async function renderWith(auth: FakeAuth, fragment: string) {
    TestBed.configureTestingModule({
      imports: [
        SetPasswordPage,
        TranslocoTestingModule.forRoot({
          langs: { ar: {}, en: {} },
          translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: 'ar' },
          preloadLangs: true,
        }),
      ],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: Blank }]),
        { provide: AuthService, useValue: auth },
        { provide: ActivatedRoute, useValue: { snapshot: { fragment } } },
      ],
    });
    return mount(SetPasswordPage, auth);
  }
});

describe('ChangePasswordPage', () => {
  it('names the account and refuses the temporary password itself without leaving', async () => {
    const page = await render(ChangePasswordPage);
    await page.router.navigateByUrl('/change-password');
    expect(page.find('account-email')!.textContent).toBe('temp@noor.example');
    expect(page.element.querySelectorAll('input').length).toBe(1);

    page.auth.change = () =>
      Promise.reject(refusal('VALIDATION_FAILED', 400, { password: ['SAME_AS_CURRENT'] }));
    await page.type('new-password', NEW);
    await page.submit();

    expect(page.find('new-password-error')!.textContent).toContain('app.password.sameAsCurrent');
    expect(page.router.url).toBe('/change-password');
  });

  it('sends nothing while a rule is unmet, says an outage, and goes inside on success', async () => {
    const page = await render(ChangePasswordPage);
    await page.router.navigateByUrl('/change-password');

    await page.type('new-password', 'alllowercase1!');
    await page.submit();
    expect(page.find('rule-cases')!.getAttribute('data-state')).toBe('unmet');
    expect(page.auth.calls).toEqual([]);

    await page.type('new-password', NEW);
    page.auth.change = () => Promise.reject(offline);
    await page.submit();
    expect(page.find('banner-unreachable')).not.toBeNull();
    expect(page.router.url).toBe('/change-password');

    page.auth.change = async () => undefined;
    await page.submit();
    expect(page.auth.calls).toEqual([`change ${NEW}`, `change ${NEW}`]);
    expect(page.router.url).toBe('/');
  });

  it('lets the person sign out instead', async () => {
    const page = await render(ChangePasswordPage);

    page.find('sign-out')!.click();
    await page.settle();

    expect(page.auth.calls).toEqual(['sign-out']);
  });
});
