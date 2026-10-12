import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { uiTranslations } from '../../testing/ui-testing';
import { ScsAuthFrameComponent } from '../auth-frame/auth-frame.component';
import { ScsDialogComponent } from '../dialog/dialog.component';
import { ScsTextFieldComponent } from '../field/text-field.component';
import { ScsBannerComponent } from './controls';

/**
 * What the operators console asked of the shared parts (spec 010): each is an option that is
 * off unless asked for, so the clinic's screens keep the look their own design draws.
 */
function render<T>(host: new () => T) {
  TestBed.configureTestingModule({ imports: [host, uiTranslations('ar')] });
  const fixture = TestBed.createComponent(host);
  fixture.detectChanges();
  return { fixture, element: fixture.nativeElement as HTMLElement, host: fixture.componentInstance };
}

describe('a field without its leading mark', () => {
  @Component({
    imports: [ScsTextFieldComponent],
    template: `<scs-text-field kind="email" label="البريد" [lead]="lead()" />
      <scs-text-field kind="password" label="كلمة المرور" [lead]="lead()" />`,
  })
  class Host {
    lead = signal(true);
  }

  it('draws the envelope and the lock unless told not to, and keeps the eye either way', () => {
    const { fixture, element, host } = render(Host);
    expect(element.querySelectorAll('scs-icon.lead')).toHaveLength(2);
    expect(element.querySelectorAll('.box.plain')).toHaveLength(0);

    host.lead.set(false);
    fixture.detectChanges();
    expect(element.querySelectorAll('scs-icon.lead')).toHaveLength(0);
    expect(element.querySelectorAll('.box.plain')).toHaveLength(2);
    expect(element.querySelectorAll('[data-testid="password-eye"]')).toHaveLength(1);
  });
});

describe('a banner that can be closed', () => {
  @Component({
    imports: [ScsBannerComponent],
    template: `<scs-banner tone="danger" [closeLabel]="label()" (closed)="closed = closed + 1">text</scs-banner>`,
  })
  class Host {
    label = signal('');
    closed = 0;
  }

  it('has no ✕ unless given its name, and says when it was pressed', () => {
    const { fixture, element, host } = render(Host);
    expect(element.querySelector('[data-testid="banner-close"]')).toBeNull();

    host.label.set('إغلاق التنبيه');
    fixture.detectChanges();
    const close = element.querySelector<HTMLButtonElement>('[data-testid="banner-close"]')!;
    expect(close.getAttribute('aria-label')).toBe('إغلاق التنبيه');
    expect(close.type).toBe('button');

    close.click();
    expect(host.closed).toBe(1);
  });
});

describe('a dialog as the operators console draws it', () => {
  @Component({
    imports: [ScsDialogComponent],
    template: `<scs-dialog [heading]="heading()" [closable]="closable()" [width]="width()" [label]="label()" [open]="false"
      >body</scs-dialog
    >`,
  })
  class Host {
    heading = signal('تغيير كلمة المرور');
    closable = signal(true);
    width = signal(400);
    label = signal('');
  }

  it('keeps its × and its width of 400 unless asked otherwise', () => {
    const { element } = render(Host);
    const dialog = element.querySelector('dialog')!;

    expect(dialog.style.getPropertyValue('--scs-dialog-width')).toBe('400px');
    expect(element.querySelector('[data-testid="dialog-close"]')).not.toBeNull();
    expect(dialog.getAttribute('aria-labelledby')).toBe(element.querySelector('h2')!.id);
    expect(dialog.hasAttribute('aria-label')).toBe(false);
  });

  it('not closable: no ×, the heading still there, and the width it was given', () => {
    const { fixture, element, host } = render(Host);
    host.closable.set(false);
    host.width.set(480);
    fixture.detectChanges();
    const dialog = element.querySelector('dialog')!;

    expect(dialog.style.getPropertyValue('--scs-dialog-width')).toBe('480px');
    expect(element.querySelector('[data-testid="dialog-close"]')).toBeNull();
    expect(element.querySelector('[data-testid="dialog-title"]')!.textContent).toBe('تغيير كلمة المرور');
  });

  it('with no heading it draws none, and is named by its label instead', () => {
    const { fixture, element, host } = render(Host);
    host.heading.set('');
    host.label.set('تغيير كلمة المرور');
    fixture.detectChanges();
    const dialog = element.querySelector('dialog')!;

    expect(element.querySelector('header')).toBeNull();
    expect(dialog.hasAttribute('aria-labelledby')).toBe(false);
    expect(dialog.getAttribute('aria-label')).toBe('تغيير كلمة المرور');
  });
});

describe('the frame before sign-in, with a console’s panel', () => {
  @Component({
    imports: [ScsAuthFrameComponent],
    template: `<scs-auth-frame [panelTitle]="title()" [panelLine]="line()" [panelMark]="mark()">form</scs-auth-frame>`,
  })
  class Host {
    title = signal('');
    line = signal('');
    mark = signal('S');
  }

  it('is the clinic’s frame, with its help and terms, when no panel title is given', () => {
    const { fixture, element } = render(Host);
    const frame = fixture.nativeElement.querySelector('scs-auth-frame') as HTMLElement;

    expect(frame.classList.contains('console')).toBe(false);
    expect(element.querySelector('[data-testid="auth-panel-title"]')).toBeNull();
    expect(element.querySelector('[data-testid="help-link"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="terms-link"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="privacy-link"]')).not.toBeNull();
  });

  it('given a title it shows the console’s mark, title and line, and only the privacy link', () => {
    const { fixture, element, host } = render(Host);
    host.title.set('لوحة تشغيل SCS');
    host.line.set('إدارة كل عيادات المنصة من مكان واحد.');
    host.mark.set('S');
    fixture.detectChanges();
    const frame = fixture.nativeElement.querySelector('scs-auth-frame') as HTMLElement;

    expect(frame.classList.contains('console')).toBe(true);
    expect(element.querySelector('[data-testid="auth-panel-title"]')!.textContent).toBe('لوحة تشغيل SCS');
    expect(element.querySelector('[data-testid="auth-panel-line"]')!.textContent).toBe(
      'إدارة كل عيادات المنصة من مكان واحد.',
    );
    expect(element.querySelector('.logo.plain')!.textContent).toBe('S');
    expect(element.querySelector('[data-testid="help-link"]')).toBeNull();
    expect(element.querySelector('[data-testid="terms-link"]')).toBeNull();
    expect(element.querySelector('[data-testid="privacy-link"]')).not.toBeNull();
  });
});
