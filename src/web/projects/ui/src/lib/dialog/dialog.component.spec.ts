import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { uiTranslations } from '../../testing/ui-testing';
import { ScsDialogComponent } from './dialog.component';

@Component({
  imports: [ScsDialogComponent],
  template: `
    <scs-dialog data-testid="small" heading="Modal Title" size="sm" still>Body</scs-dialog>
    <scs-dialog data-testid="full" heading="Modal Title" size="full" [open]="false">Body</scs-dialog>
    <scs-dialog data-testid="own" heading="Modal Title" [width]="440" [open]="false">Body</scs-dialog>
  `,
})
class Host {}

/** The dialog's sizes (spec 095, `contracts/components.md`). */
describe('ScsDialogComponent sizes', () => {
  it('takes the page sizes, or its own width, and stands still in place when asked', async () => {
    TestBed.configureTestingModule({ imports: [uiTranslations('en')] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await fixture.whenStable();
    const dialog = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"] dialog`) as HTMLDialogElement;
    expect(dialog('small').style.getPropertyValue('--scs-dialog-width')).toBe('360px');
    expect(dialog('full').style.getPropertyValue('--scs-dialog-width')).toBe('900px');
    expect(dialog('own').style.getPropertyValue('--scs-dialog-width')).toBe('440px');
    expect(dialog('small').open).toBe(true);
    expect(dialog('small').classList).toContain('still');
    expect(dialog('full').open).toBe(false);
  });
});
