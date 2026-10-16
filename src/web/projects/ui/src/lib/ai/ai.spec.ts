import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ScsInsightCardComponent, ScsInsightState, ScsMessageBubbleComponent } from './ai';

@Component({
  imports: [ScsInsightCardComponent, ScsMessageBubbleComponent],
  template: `
    <scs-insight-card data-testid="card" ai [state]="state()" heading="AI Suggestion" text="Send a reminder?" retryLabel="Retry" (retry)="retried.set(true)">
      <button type="button" data-testid="act">Send Reminder</button>
    </scs-insight-card>
    <div role="list">
      <scs-message-bubble data-testid="mine" kind="user" time="10:32">Yes, please.</scs-message-bubble>
      <scs-message-bubble data-testid="failed" kind="outgoing" failedLabel="Not sent · Try again">Hello</scs-message-bubble>
    </div>
  `,
})
class Host {
  readonly state = signal<ScsInsightState>('ready');
  readonly retried = signal(false);
}

/** The shared AI parts (spec 095, `contracts/components.md`). */
describe('AI parts', () => {
  it('an insight card shows its AI mark and actions when ready, shapes while loading, Retry when it failed', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const card = fixture.nativeElement.querySelector('[data-testid="card"]') as HTMLElement;
    expect(card.querySelector('.mark scs-icon')).not.toBeNull();
    expect(card.querySelector('[data-testid="act"]')).not.toBeNull();
    fixture.componentInstance.state.set('loading');
    fixture.detectChanges();
    expect(card.getAttribute('aria-busy')).toBe('true');
    expect(card.querySelector('[data-testid="insight-heading"]')).toBeNull();
    fixture.componentInstance.state.set('error');
    fixture.detectChanges();
    expect(card.querySelector('.mark')).toBeNull();
    expect(card.querySelector('[data-testid="act"]')).toBeNull();
    (card.querySelector('[data-testid="insight-retry"]') as HTMLButtonElement).click();
    expect(fixture.componentInstance.retried()).toBe(true);
  });

  it('a message is a list item, the person’s own at the end, with its time and a failure said at once', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const mine = fixture.nativeElement.querySelector('[data-testid="mine"]') as HTMLElement;
    expect(mine.getAttribute('role')).toBe('listitem');
    expect(mine.getAttribute('data-kind')).toBe('user');
    expect(mine.textContent).toContain('10:32');
    const failed = fixture.nativeElement.querySelector('[data-testid="failed"] [role="alert"]') as HTMLElement;
    expect(failed.textContent).toBe('Not sent · Try again');
  });
});
