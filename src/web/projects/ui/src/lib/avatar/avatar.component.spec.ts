import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsAvatarComponent, initialsOf } from './avatar.component';

@Component({
  imports: [ScsAvatarComponent],
  template: `
    <scs-avatar name="Ahmed Salem" size="lg" data-testid="letters" />
    <scs-avatar name="Mona Adel" [src]="src()" data-testid="picture" />
  `,
})
class Host {
  readonly src = signal<string | null>('data:image/gif;base64,R0lGODlhAQABAAAAACw=');
}

/** The avatar (spec 095, `contracts/components.md`). */
describe('ScsAvatarComponent', () => {
  it('takes the first letter of the first two words, in either language', () => {
    expect(initialsOf('Ahmed Salem')).toBe('AS');
    expect(initialsOf('أحمد حسن علي')).toBe('أح');
    expect(initialsOf('  mona ')).toBe('M');
  });

  it('names the person, shows the letters without a picture, and the picture with one', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const letters = fixture.nativeElement.querySelector('[data-testid="letters"]') as HTMLElement;
    expect(letters.getAttribute('role')).toBe('img');
    expect(letters.getAttribute('aria-label')).toBe('Ahmed Salem');
    expect(letters.getAttribute('data-size')).toBe('lg');
    expect(letters.textContent?.trim()).toBe('AS');
    const picture = fixture.nativeElement.querySelector('[data-testid="picture"]') as HTMLElement;
    expect(picture.querySelector('img')?.getAttribute('alt')).toBe('');
  });

  it('gives way to the letters when the picture does not load', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const picture = fixture.nativeElement.querySelector('[data-testid="picture"]') as HTMLElement;
    picture.querySelector('img')!.dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(picture.querySelector('img')).toBeNull();
    expect(picture.textContent?.trim()).toBe('MA');
  });
});
