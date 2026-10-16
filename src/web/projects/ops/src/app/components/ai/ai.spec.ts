import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AskAiFieldComponent, ChatWidgetComponent } from './ai';

@Component({
  imports: [AskAiFieldComponent, ChatWidgetComponent],
  template: `
    <app-ask-ai-field label="Ask AI anything" sendLabel="Send" (asked)="asked.set($event)" />
    <app-chat-widget title="AI Assistant" sendLabel="Send" attachLabel="Attach" closeLabel="Close" [messages]="[{ from: 'bot', text: 'Hello' }]" (sent)="sent.set($event)" (closed)="closed.set(true)" />
  `,
})
class Host {
  readonly asked = signal('');
  readonly sent = signal('');
  readonly closed = signal(false);
}

/** The operators' AI parts hand what is typed to the page and send nothing themselves (spec 095). */
describe('AI parts of the operators console', () => {
  it('the field and the chat hand their words to the page, and the chat closes', () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const type = (id: string, words: string) => {
      const input = fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLInputElement;
      input.value = words;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    type('ask-ai-input', 'Which clinics are overdue?');
    (fixture.nativeElement.querySelector('[data-testid="ask-ai-send"]') as HTMLButtonElement).click();
    expect(fixture.componentInstance.asked()).toBe('Which clinics are overdue?');
    type('chat-input', 'Escalate them');
    (fixture.nativeElement.querySelector('[data-testid="chat-send"]') as HTMLButtonElement).click();
    expect(fixture.componentInstance.sent()).toBe('Escalate them');
    expect(fixture.nativeElement.querySelector('[data-testid="chat-messages"]').getAttribute('role')).toBe('list');
    (fixture.nativeElement.querySelector('[data-testid="chat-close"]') as HTMLButtonElement).click();
    expect(fixture.componentInstance.closed()).toBe(true);
  });
});
