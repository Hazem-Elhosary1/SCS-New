import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { uiTranslations } from '../../testing/ui-testing';
import { ScsBannerComponent } from '../controls/controls';
import { ScsNoAccessComponent } from './no-access.component';

@Component({
  imports: [ScsNoAccessComponent],
  template: `<scs-no-access [permissionName]="permission()" [roleName]="role()" />`,
})
class Host {
  permission = signal('عرض الأدوار والصلاحيات');
  role = signal('فريق العيادة');
}

function render(language: 'ar' | 'en' = 'ar') {
  TestBed.configureTestingModule({ imports: [Host, uiTranslations(language)] });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  const text = (id: string) =>
    element.querySelector(`[data-testid="${id}"]`)!.textContent!.replace(/\s+/g, ' ').trim();
  return { fixture, element, host: fixture.componentInstance, text };
}

describe('ScsNoAccessComponent', () => {
  it('says the page is not in the role, which permission opens it and which role the person holds', () => {
    const { element, text } = render();

    expect(text('no-access-title')).toBe('هذه الصفحة ليست في دورك');
    expect(text('no-access-text')).toBe(
      'تُفتح لمن يملك «عرض الأدوار والصلاحيات»، ودورك «فريق العيادة» لا يحملها. وما يلزم منها يُطلب ممن يدير الأدوار والصلاحيات في العيادة.',
    );
    expect(element.querySelector('scs-icon')!.getAttribute('data-icon')).toBe('lock');
  });

  it('reads the same in English, as the design writes it', () => {
    const { host, fixture, text } = render('en');
    host.permission.set('View roles & permissions');
    host.role.set('Clinic team');
    fixture.detectChanges();

    expect(text('no-access-title')).toBe('This page is not in your role');
    expect(text('no-access-text')).toBe(
      'It opens for holders of “View roles & permissions”, and your role “Clinic team” does not carry it. Whatever is needed from it is requested from whoever manages roles & permissions in the clinic.',
    );
  });

  it('says the account holds no role yet when there is none to name', () => {
    const { host, fixture, text } = render();
    host.role.set('');
    fixture.detectChanges();

    expect(text('no-access-text')).toBe(
      'تُفتح لمن يملك «عرض الأدوار والصلاحيات»، وحسابك لا يحمل دورًا بعد. وما يلزم منها يُطلب ممن يدير الأدوار والصلاحيات في العيادة.',
    );
  });

  it('keeps both names inside the sentence, where assistive technology reads them', () => {
    const { element } = render();
    const paragraph = element.querySelector('[data-testid="no-access-text"]')!;

    expect(paragraph.querySelector('[aria-hidden]')).toBeNull();
    expect(paragraph.textContent).toContain('عرض الأدوار والصلاحيات');
    expect(paragraph.textContent).toContain('فريق العيادة');
    // The mark is decoration; the heading carries the meaning.
    expect(element.querySelector('scs-icon')!.getAttribute('aria-hidden')).toBe('true');
    expect(element.querySelector('h2')).not.toBeNull();
  });
});

@Component({
  imports: [ScsBannerComponent],
  template: `<scs-banner tone="info" [title]="title()">التعديل يحتاج صلاحية «إدارة الأدوار والصلاحيات».</scs-banner>`,
})
class Notice {
  title = signal('للعرض فقط');
}

describe('ScsBannerComponent as the view-only notice', () => {
  it('is an information notice with a title line, announced politely', () => {
    TestBed.configureTestingModule({ imports: [Notice, uiTranslations()] });
    const fixture = TestBed.createComponent(Notice);
    fixture.detectChanges();
    const banner = (fixture.nativeElement as HTMLElement).querySelector('scs-banner')!;

    expect(banner.getAttribute('data-tone')).toBe('info');
    expect(banner.getAttribute('role')).toBe('status');
    expect(banner.querySelector('[data-testid="banner-title"]')!.textContent!.trim()).toBe('للعرض فقط');
    expect(banner.textContent).toContain('التعديل يحتاج صلاحية «إدارة الأدوار والصلاحيات».');
    expect(banner.querySelector('scs-icon')!.getAttribute('data-icon')).toBe('info');
    expect(banner.querySelector('button')).toBeNull();
  });

  it('has no title line when none is given, as every banner before it', () => {
    TestBed.configureTestingModule({ imports: [Notice, uiTranslations()] });
    const fixture = TestBed.createComponent(Notice);
    fixture.componentInstance.title.set('');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="banner-title"]')).toBeNull();
  });
});
