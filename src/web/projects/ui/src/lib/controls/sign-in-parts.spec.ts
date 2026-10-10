import { Component, signal, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { uiTranslations } from '../../testing/ui-testing';
import { ScsAuthFrameComponent } from '../auth-frame/auth-frame.component';
import { ScsCodeInputComponent, ScsCodeState } from '../code-input/code-input.component';
import { ScsDialogComponent } from '../dialog/dialog.component';
import { ScsTextFieldComponent } from '../field/text-field.component';
import { ScsBannerComponent, ScsBannerTone, ScsButtonDirective } from './controls';

function render<T>(host: new () => T, language: 'ar' | 'en' = 'ar') {
  TestBed.configureTestingModule({ imports: [host, uiTranslations(language)] });
  const fixture = TestBed.createComponent(host);
  fixture.detectChanges();
  return {
    fixture,
    element: fixture.nativeElement as HTMLElement,
    host: fixture.componentInstance,
  };
}

describe('ScsBannerComponent', () => {
  @Component({
    imports: [ScsBannerComponent],
    template: `<scs-banner [tone]="tone()" [actionLabel]="label()" (action)="pressed = pressed + 1"
      >text</scs-banner
    >`,
  })
  class Host {
    tone = signal<ScsBannerTone>('neutral');
    label = signal('');
    pressed = 0;
  }

  it('announces refusals and warnings at once and the rest politely', () => {
    const { fixture, element, host } = render(Host);
    const banner = element.querySelector('scs-banner')!;

    for (const [tone, role] of [
      ['danger', 'alert'],
      ['warning', 'alert'],
      ['neutral', 'status'],
      ['success', 'status'],
    ] as const) {
      host.tone.set(tone);
      fixture.detectChanges();
      expect(banner.getAttribute('role')).toBe(role);
      expect(banner.getAttribute('data-tone')).toBe(tone);
    }
  });

  it('offers its one action only when given a label', () => {
    const { fixture, element, host } = render(Host);
    expect(element.querySelector('[data-testid="banner-action"]')).toBeNull();

    host.label.set('التواصل مع الدعم');
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('[data-testid="banner-action"]')!.click();

    expect(host.pressed).toBe(1);
  });
});

describe('ScsButtonDirective', () => {
  @Component({
    imports: [ScsButtonDirective],
    template: `<button scsButton [busy]="busy()">go</button
      ><button scsButton="secondary">other</button>`,
  })
  class Host {
    busy = signal(false);
  }

  it('is locked and says so while busy', () => {
    const { fixture, element, host } = render(Host);
    const [primary, secondary] = Array.from(element.querySelectorAll('button'));

    expect(primary.disabled).toBe(false);
    expect(primary.getAttribute('data-variant')).toBe('primary');
    expect(secondary.getAttribute('data-variant')).toBe('secondary');

    host.busy.set(true);
    fixture.detectChanges();

    expect(primary.disabled).toBe(true);
    expect(primary.getAttribute('aria-busy')).toBe('true');
  });
});

describe('ScsTextFieldComponent', () => {
  @Component({
    imports: [ScsTextFieldComponent],
    template: `<scs-text-field
      label="كلمة المرور"
      [kind]="kind()"
      testId="f"
      [error]="error()"
      [(value)]="value"
    />`,
  })
  class Host {
    kind = signal<'text' | 'email' | 'password'>('password');
    error = signal('');
    value = signal('');
  }

  it('ties its label and its error to the input', () => {
    const { fixture, element, host } = render(Host);
    const input = element.querySelector('input')!;

    expect(element.querySelector('label')!.getAttribute('for')).toBe(input.id);
    expect(input.getAttribute('aria-invalid')).toBeNull();

    host.error.set('كلمة المرور مطلوبة');
    fixture.detectChanges();

    const error = element.querySelector('[data-testid="f-error"]')!;
    expect(error.textContent).toContain('كلمة المرور مطلوبة');
    expect(input.getAttribute('aria-describedby')).toBe(error.id);
    expect(input.getAttribute('aria-invalid')).toBe('true');
  });

  it('keeps what is typed and reads an email or a password left to right', () => {
    const { fixture, element, host } = render(Host);
    const input = element.querySelector('input')!;

    input.value = 'secret';
    input.dispatchEvent(new Event('input'));
    expect(host.value()).toBe('secret');
    expect(input.getAttribute('dir')).toBe('ltr');

    host.kind.set('text');
    fixture.detectChanges();
    expect(input.getAttribute('dir')).toBeNull();
  });

  it('shows and hides a password with the eye, sending nothing', () => {
    const { fixture, element } = render(Host);
    const input = element.querySelector('input')!;
    const eye = element.querySelector<HTMLButtonElement>('[data-testid="password-eye"]')!;

    expect(input.type).toBe('password');
    expect(eye.getAttribute('aria-label')).toBe('إظهار كلمة المرور');
    expect(eye.type).toBe('button');

    eye.click();
    fixture.detectChanges();
    expect(input.type).toBe('text');
    expect(eye.getAttribute('aria-pressed')).toBe('true');
    expect(element.querySelector('[data-testid="eye-crossed"]')).not.toBeNull();

    eye.click();
    fixture.detectChanges();
    expect(input.type).toBe('password');
  });

  it('has no eye on a field that is not a password', () => {
    const { fixture, element, host } = render(Host);
    host.kind.set('email');
    fixture.detectChanges();

    expect(element.querySelector('[data-testid="password-eye"]')).toBeNull();
    expect(element.querySelector('input')!.getAttribute('inputmode')).toBe('email');
  });
});

describe('ScsCodeInputComponent', () => {
  @Component({
    imports: [ScsCodeInputComponent],
    template: `<scs-code-input
      label="رمز التحقق"
      [state]="state()"
      [(value)]="value"
      (completed)="done = $event"
    />`,
  })
  class Host {
    state = signal<ScsCodeState>('idle');
    value = signal('');
    done = '';
    boxes = viewChild.required(ScsCodeInputComponent);
  }

  function boxes(element: HTMLElement): HTMLInputElement[] {
    return Array.from(element.querySelectorAll('input'));
  }

  function type(box: HTMLInputElement, text: string): void {
    box.value = text;
    box.dispatchEvent(new Event('input'));
  }

  for (const language of ['ar', 'en'] as const) {
    it(`is six boxes written left to right as one labelled group (${language})`, () => {
      const { element } = render(Host, language);
      const group = element.querySelector('[role="group"]')!;

      expect(boxes(element)).toHaveLength(6);
      expect(group.getAttribute('dir')).toBe('ltr');
      expect(group.getAttribute('aria-label')).toBe('رمز التحقق');
      expect(boxes(element)[0].getAttribute('inputmode')).toBe('numeric');
    });
  }

  it('moves on as digits are typed and reports the code when all six are in', () => {
    const { fixture, element, host } = render(Host);
    document.body.appendChild(element);

    '123456'.split('').forEach((digit, index) => {
      type(boxes(element)[index], digit);
      fixture.detectChanges();
      if (index < 5) {
        expect(document.activeElement).toBe(boxes(element)[index + 1]);
      }
    });

    expect(host.value()).toBe('123456');
    expect(host.done).toBe('123456');
    element.remove();
  });

  it('takes Arabic-Indic digits as the same digits and refuses anything else', () => {
    const { fixture, element, host } = render(Host);

    type(boxes(element)[0], '٧');
    fixture.detectChanges();
    type(boxes(element)[1], 'x');
    fixture.detectChanges();

    expect(host.value()).toBe('7');
    expect(boxes(element)[0].value).toBe('7');
    expect(boxes(element)[1].value).toBe('');
  });

  it('fills every box from a pasted code, or from a code delivered in one go', () => {
    const { fixture, element, host } = render(Host);

    const paste = new Event('paste', { cancelable: true }) as ClipboardEvent;
    Object.defineProperty(paste, 'clipboardData', { value: { getData: () => ' 12 34 56 ' } });
    boxes(element)[0].dispatchEvent(paste);
    fixture.detectChanges();
    expect(host.value()).toBe('123456');
    expect(paste.defaultPrevented).toBe(true);

    host.value.set('');
    fixture.detectChanges();
    type(boxes(element)[0], '908940');
    fixture.detectChanges();
    expect(host.value()).toBe('908940');
    expect(
      boxes(element)
        .map((box) => box.value)
        .join(''),
    ).toBe('908940');
  });

  it('moves back with Backspace on an empty box', () => {
    const { fixture, element, host } = render(Host);
    document.body.appendChild(element);
    type(boxes(element)[0], '1');
    type(boxes(element)[1], '2');
    fixture.detectChanges();

    boxes(element)[2].dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Backspace', cancelable: true }),
    );
    fixture.detectChanges();

    expect(host.value()).toBe('1');
    expect(document.activeElement).toBe(boxes(element)[1]);
    element.remove();
  });

  it('shows the error and accepted states and locks when accepted', () => {
    const { fixture, element, host } = render(Host);

    host.state.set('error');
    fixture.detectChanges();
    expect(element.querySelector('scs-code-input')!.getAttribute('data-state')).toBe('error');
    expect(boxes(element)[0].getAttribute('aria-invalid')).toBe('true');
    expect(boxes(element)[0].disabled).toBe(false);

    host.state.set('accepted');
    fixture.detectChanges();
    expect(boxes(element).every((box) => box.disabled)).toBe(true);
  });
});

describe('ScsAuthFrameComponent', () => {
  @Component({
    imports: [ScsAuthFrameComponent],
    template: `<scs-auth-frame (help)="asked = asked + 1"
      ><p data-testid="inside">form</p></scs-auth-frame
    >`,
  })
  class Host {
    asked = 0;
  }

  for (const [language, name, help] of [
    ['ar', 'نظام تشغيل العيادة الذكي', 'تحتاج مساعدة؟ تواصل مع الدعم الفني'],
    ['en', 'The smart clinic operating system', 'Need help? Contact support'],
  ] as const) {
    it(`has the introduction panel, the form, the language switch and the footer (${language})`, () => {
      const { element, host } = render(Host, language);

      expect(element.querySelector('[data-testid="auth-intro"]')!.textContent).toContain(name);
      expect(
        element.querySelector('[data-testid="auth-intro"] button, [data-testid="auth-intro"] a'),
      ).toBeNull();
      expect(element.querySelector('[data-testid="inside"]')).not.toBeNull();
      expect(element.querySelector('[data-testid="language-switch"]')).not.toBeNull();
      expect(element.querySelector('[data-testid="privacy-link"]')!.getAttribute('href')).toBe(
        '/privacy',
      );
      expect(element.querySelector('[data-testid="terms-link"]')!.getAttribute('href')).toBe(
        '/terms',
      );

      const link = element.querySelector<HTMLButtonElement>('[data-testid="help-link"]')!;
      expect(link.textContent!.trim()).toBe(help);
      link.click();
      expect(host.asked).toBe(1);
    });
  }
});

describe('ScsDialogComponent', () => {
  @Component({
    imports: [ScsDialogComponent],
    template: `<scs-dialog heading="تحتاج مساعدة؟" [(open)]="open"
      ><a href="#" data-testid="row">row</a></scs-dialog
    >`,
  })
  class Host {
    open = signal(false);
  }

  beforeAll(() => {
    // The test DOM has the element but not its two methods.
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

  it('opens as a labelled modal window and closes from the × button', () => {
    const { fixture, element, host } = render(Host);
    const dialog = element.querySelector('dialog')!;
    expect(dialog.hasAttribute('open')).toBe(false);

    host.open.set(true);
    fixture.detectChanges();
    TestBed.tick();

    expect(dialog.hasAttribute('open')).toBe(true);
    expect(document.getElementById(dialog.getAttribute('aria-labelledby')!)!.textContent).toBe(
      'تحتاج مساعدة؟',
    );
    const close = element.querySelector<HTMLButtonElement>('[data-testid="dialog-close"]')!;
    expect(close.getAttribute('aria-label')).toBe('إغلاق');

    close.click();
    fixture.detectChanges();
    TestBed.tick();

    expect(host.open()).toBe(false);
    expect(dialog.hasAttribute('open')).toBe(false);
  });

  function opened() {
    const rendered = render(Host);
    rendered.host.open.set(true);
    rendered.fixture.detectChanges();
    TestBed.tick();
    const dialog = rendered.element.querySelector('dialog')!;
    expect(dialog.hasAttribute('open')).toBe(true);
    return { ...rendered, dialog };
  }

  it('follows the browser closing it, as Escape does', () => {
    const { host, dialog } = opened();

    dialog.close();
    dialog.dispatchEvent(new Event('close'));

    expect(host.open()).toBe(false);
  });

  it('closes on a click on the dimmed area around it', () => {
    const { host, dialog } = opened();

    dialog.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(host.open()).toBe(false);
  });

  it('stays open on a click inside it', () => {
    const { host, element } = opened();

    element
      .querySelector<HTMLElement>('[data-testid="row"]')!
      .dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(host.open()).toBe(true);
  });
});
