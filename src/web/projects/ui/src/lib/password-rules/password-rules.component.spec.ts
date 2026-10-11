import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { uiTranslations } from '../../testing/ui-testing';
import { ScsPasswordRule, ScsPasswordRulesComponent } from './password-rules.component';

@Component({
  imports: [ScsPasswordRulesComponent],
  template: `<scs-password-rules [rules]="rules()" [submitted]="submitted()" />`,
})
class Host {
  rules = signal<ScsPasswordRule[]>([
    { key: 'length', label: '١٠ أحرف على الأقل', met: false },
    { key: 'cases', label: 'حرف كبير وحرف صغير (A-z)', met: false },
    { key: 'digit', label: 'رقم واحد على الأقل (0-9)', met: false },
    { key: 'symbol', label: 'رمز خاص واحد على الأقل (!@#$%)', met: false },
  ]);
  submitted = signal(false);

  meet(...keys: string[]): void {
    this.rules.update((rules) => rules.map((rule) => ({ ...rule, met: keys.includes(rule.key) })));
  }
}

function render(language: 'ar' | 'en' = 'ar') {
  TestBed.configureTestingModule({ imports: [Host, uiTranslations(language)] });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  const element = fixture.nativeElement as HTMLElement;
  const states = () =>
    [...element.querySelectorAll('li')].map((line) => line.getAttribute('data-state'));
  return { fixture, element, host: fixture.componentInstance, states };
}

describe('ScsPasswordRulesComponent', () => {
  it('shows every rule, in order, neutral before anything is typed', () => {
    const { element, states } = render();

    const lines = [...element.querySelectorAll('li')].map((line) => line.textContent ?? '');
    expect(lines.length).toBe(4);
    expect(lines[0]).toContain('١٠ أحرف على الأقل');
    expect(lines[1]).toContain('حرف كبير وحرف صغير (A-z)');
    expect(lines[2]).toContain('رقم واحد على الأقل (0-9)');
    expect(lines[3]).toContain('رمز خاص واحد على الأقل (!@#$%)');
    expect(states()).toEqual(['pending', 'pending', 'pending', 'pending']);
    expect(
      element.querySelector('[data-testid="password-rules-summary"]')!.textContent!.trim(),
    ).toBe('');
  });

  it('turns a rule green as soon as it is met, and back when it no longer is', () => {
    const { fixture, host, states } = render();

    host.meet('length', 'digit');
    fixture.detectChanges();
    expect(states()).toEqual(['met', 'pending', 'met', 'pending']);

    host.meet('digit');
    fixture.detectChanges();
    expect(states()).toEqual(['pending', 'pending', 'met', 'pending']);
  });

  it('turns the rules still unmet red once the form is submitted, and says so', () => {
    const { fixture, element, host, states } = render();

    host.meet('length', 'cases');
    host.submitted.set(true);
    fixture.detectChanges();

    expect(states()).toEqual(['met', 'met', 'unmet', 'unmet']);
    const summary = element.querySelector('[data-testid="password-rules-summary"]')!;
    expect(summary.getAttribute('role')).toBe('status');
    expect(summary.textContent!.trim()).not.toBe('');

    host.meet('length', 'cases', 'digit', 'symbol');
    fixture.detectChanges();
    expect(states()).toEqual(['met', 'met', 'met', 'met']);
    expect(summary.textContent!.trim()).toBe('');
  });

  it('says each state in words, not by colour alone, in both languages', () => {
    for (const language of ['ar', 'en'] as const) {
      TestBed.resetTestingModule();
      const { fixture, element, host } = render(language);
      host.meet('length');
      host.submitted.set(true);
      fixture.detectChanges();

      const said = [...element.querySelectorAll('li .said')].map((s) => s.textContent!.trim());
      expect(said.every((text) => text !== '' && !text.startsWith('ui.'))).toBe(true);
      expect(new Set(said).size).toBe(2);
      expect(element.querySelector('ul')!.getAttribute('aria-label')).not.toContain('ui.');
    }
  });
});
