import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsSortDirection, ScsSortHeaderComponent } from './sort-header.component';

@Component({
  imports: [ScsSortHeaderComponent],
  template: `
    <table>
      <thead>
        <tr>
          <th data-testid="cell">
            <button scsSortHeader data-testid="sort" [direction]="direction()" (sorted)="direction.set($event)">Ends</button>
          </th>
        </tr>
      </thead>
    </table>
  `,
})
class Host {
  readonly direction = signal<ScsSortDirection>('none');
}

/** The sortable column (spec 015, the clinics list's «ينتهي في ▾»). */
describe('ScsSortHeaderComponent', () => {
  it('names the order on its column and asks for ascending, then descending, then ascending', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const cell = fixture.nativeElement.querySelector('[data-testid="cell"]') as HTMLElement;
    const button = fixture.nativeElement.querySelector('[data-testid="sort"]') as HTMLButtonElement;

    expect(cell.getAttribute('aria-sort')).toBe('none');
    expect(button.getAttribute('type')).toBe('button');
    expect(button.textContent?.trim()).toBe('Ends');

    button.click();
    fixture.detectChanges();
    expect(cell.getAttribute('aria-sort')).toBe('ascending');
    expect(button.getAttribute('data-direction')).toBe('ascending');

    button.click();
    fixture.detectChanges();
    expect(cell.getAttribute('aria-sort')).toBe('descending');

    button.click();
    fixture.detectChanges();
    expect(cell.getAttribute('aria-sort')).toBe('ascending');
  });
});
