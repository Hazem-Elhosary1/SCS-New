import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ToastService } from '../overlay/overlay';
import { ScsCopyButtonComponent } from './copy-button.component';

@Component({
  imports: [ScsCopyButtonComponent],
  template: `<scs-copy-button value="https://app.scs-heta.com" label="Copy" copiedText="Copied." failedText="Copying failed." />`,
})
class Host {}

/** The copy button (spec 015, the sign-in address row). */
describe('ScsCopyButtonComponent', () => {
  async function press(writeText: (text: string) => Promise<void>): Promise<{ text: string; tone: string }[]> {
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toBe('Copy');
    button.click();
    await new Promise((resolve) => setTimeout(resolve));
    return TestBed.inject(ToastService).toasts().map((toast) => ({ text: toast.title, tone: toast.tone }));
  }

  it('copies the value and says so', async () => {
    const copied: string[] = [];
    const toasts = await press(async (text) => {
      copied.push(text);
    });
    expect(copied).toEqual(['https://app.scs-heta.com']);
    expect(toasts).toEqual([{ text: 'Copied.', tone: 'success' }]);
  });

  it('says it could not copy when the browser refuses', async () => {
    const toasts = await press(() => Promise.reject(new Error('denied')));
    expect(toasts).toEqual([{ text: 'Copying failed.', tone: 'danger' }]);
  });
});
