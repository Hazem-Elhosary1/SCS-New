import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ScsButtonDirective, ScsInsightCardComponent, ScsInsightState, ScsMessageBubbleComponent } from 'ui';
import { AiBadgeComponent, AskAiButtonComponent, AskAiFieldComponent, ChatMessage, ChatWidgetComponent } from '../../components/ai/ai';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The AI page (399:2): the badge, the insight and suggestion cards and the suggestion card's states
 * (2421:88), the message bubbles, the Ask-AI button and field, and the chat widget with a picture in it.
 * Example words only: nothing is asked of any AI from here (Constitution V).
 */
@Component({
  selector: 'app-gallery-ai-section',
  imports: [
    ScsInsightCardComponent,
    ScsMessageBubbleComponent,
    ScsButtonDirective,
    AiBadgeComponent,
    AskAiButtonComponent,
    AskAiFieldComponent,
    ChatWidgetComponent,
    GalleryComponentComponent,
    GallerySpecimenDirective,
    GalleryLiveDirective,
    GalleryPartComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    app-gallery-component + app-gallery-component {
      display: block;
      margin-block-start: var(--scs-spacing-xl, 32px);
    }
    .cells {
      --gallery-cell: min(380px, 100%);
    }
    scs-insight-card,
    .bubbles {
      inline-size: 100%;
      max-inline-size: 360px;
    }
    .bubbles {
      display: grid;
      gap: 12px;
    }
    .states {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(min(300px, 100%), 1fr));
      gap: var(--scs-spacing-lg);
      inline-size: 100%;
    }
    .state-name {
      margin: 0 0 8px;
      font-size: 12px;
      color: var(--scs-color-text-tertiary);
    }
    app-ask-ai-field {
      inline-size: 100%;
      max-inline-size: 360px;
    }
    .dismiss {
      min-block-size: var(--scs-touch, 0px);
      min-inline-size: var(--scs-touch, 0px);
      margin-block: min(0px, calc((1lh - var(--scs-touch, 0px)) / 2));
      padding: 0;
      border: 0;
      background: none;
      color: var(--scs-color-text-tertiary);
      font: inherit;
      font-size: 12px;
      font-weight: 500;
      cursor: pointer;
    }
  `,
  template: `
    <app-gallery-component page="AI" set="AI Badge">
      <ng-template appGallerySpecimen let-values>
        <app-ai-badge [size]="values['Size'] === 'Medium' ? 'md' : 'sm'" />
      </ng-template>
    </app-gallery-component>

    <app-gallery-component class="cells" page="AI" set="AI Insight Card">
      <ng-template appGallerySpecimen let-values>
        @if (values['Type'] === 'Suggestion') {
          <scs-insight-card ai [heading]="words().suggestion" [text]="words().suggestionText">
            <button type="button" scsButton="primary" size="sm">{{ words().send }}</button>
            <button type="button" class="dismiss">{{ words().dismiss }}</button>
          </scs-insight-card>
        } @else {
          <scs-insight-card ai [heading]="words().insight" [text]="words().insightText" />
        }
      </ng-template>
      <ng-template appGalleryLive>
        <div class="states">
          @for (state of states(); track state.name) {
            <div>
              <p class="state-name">{{ state.name }}</p>
              <scs-insight-card [state]="state.state" [heading]="state.heading" [text]="state.text" [retryLabel]="words().retry">
                <button type="button" scsButton="primary" size="sm">{{ words().sendAll }}</button>
                <button type="button" class="dismiss">{{ words().dismiss }}</button>
              </scs-insight-card>
            </div>
          }
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-component class="cells" page="AI" set="Message Bubble">
      <ng-template appGallerySpecimen let-values>
        <div class="bubbles" role="list">
          @if (values['Sender'] === 'User') {
            <scs-message-bubble kind="user">{{ words().user }}</scs-message-bubble>
          } @else {
            <scs-message-bubble kind="bot">{{ words().bot }}</scs-message-bubble>
          }
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Composed Example — Ask AI · button" [node]="ar() ? '409:12' : '402:3'" testId="specimen-ai/parts/ask-ai-fab" [shared]="false">
      <app-ask-ai-button [label]="words().ask" [expanded]="open()" (pressed)="open.set(!open())" />
    </app-gallery-part>
    <app-gallery-part name="Composed Example — Ask AI · field" [node]="ar() ? '409:15' : '402:6'" testId="specimen-ai/parts/ask-ai-field" [shared]="false">
      <app-ask-ai-field [label]="words().field" [sendLabel]="words().sendMessage" (asked)="add($event)" />
    </app-gallery-part>
    <app-gallery-part name="Composed Example — Chat Widget (with media)" [node]="ar() ? '411:11' : '405:2'" testId="specimen-ai/parts/chat-widget-panel" [shared]="false">
      <app-chat-widget
        [title]="words().assistant"
        [subtitle]="words().help"
        [messages]="chat()"
        [placeholder]="words().type"
        [sendLabel]="words().sendMessage"
        [attachLabel]="words().attach"
        [closeLabel]="words().close"
        (sent)="add($event)"
      />
    </app-gallery-part>
  `,
})
export class AiSection {
  private readonly state = inject(GalleryState);
  protected readonly ar = computed(() => this.state.language() === 'ar');
  protected readonly open = signal(false);
  private readonly asked = signal<string[]>([]);

  /** The page's words (AI 399:2 and its Arabic twins 408:21 … 411:11, 2421:112). */
  protected readonly words = computed(() =>
    this.ar()
      ? {
          insight: 'رؤية ذكاء اصطناعي',
          insightText: 'حجم التذاكر ارتفع 24% هذا الأسبوع، معظمها من عيادة النور. فكر في إعادة توزيع فريق الدعم لتغطية أوقات الذروة.',
          suggestion: 'اقتراح ذكاء اصطناعي',
          suggestionText: '3 عيادات لم تجدد باقتها منذ أكثر من 30 يوم. هل تريد إرسال تذكير بالتجديد الآن؟',
          send: 'إرسال تذكير',
          sendAll: 'إرسال التذكيرات',
          dismiss: 'تجاهل',
          retry: 'إعادة المحاولة',
          bot: 'راجعت قائمة التذاكر — 3 تذاكر متأخرة أكثر من 24 ساعة. هل تريد تصعيدها؟',
          user: 'نعم، صعّدها كلها لفريق المناوبة.',
          picture: 'إليك لقطة الشاشة من العميل:',
          ask: 'اسأل الذكاء الاصطناعي',
          field: 'اسأل الذكاء الاصطناعي عن عياداتك...',
          sendMessage: 'إرسال',
          assistant: 'المساعد الذكي',
          help: 'دائماً هنا للمساعدة',
          type: 'اكتب رسالة...',
          attach: 'إرفاق ملف',
          close: 'إغلاق',
          example: 'هذا مثال فقط: لا يُرسَل شيء إلى أي ذكاء اصطناعي من هنا.',
          stateNames: ['تحميل', 'لا توجد اقتراحات حاليًا', 'الذكاء الاصطناعي متوقف — صياغة ثابتة بدون شارة', 'خطأ'],
          empty: ['لا يوجد ما نقترحه حاليًا', 'كل ما يحتاج إلى مشغّل موجود بالفعل في لوحة «بحاجة إلى انتباه».'],
          off: ['اقتراح', '3 عيادات لم تجدد اشتراكها منذ أكثر من 30 يومًا. هل تريد إرسال تذكير بالتجديد؟'],
          failed: ['تعذّر تحديث الاقتراحات', 'آخر تشغيل ناجح كان قبل 6 ساعات. نتائج الكواشف نفسها ما زالت معروضة.'],
        }
      : {
          insight: 'AI Insight',
          insightText: 'Ticket volume is up 24% this week, mostly from Al Noor Clinic. Consider reassigning support staff to cover peak hours.',
          suggestion: 'AI Suggestion',
          suggestionText: "3 clinics haven't renewed their plan in over 30 days. Send them a renewal reminder now?",
          send: 'Send Reminder',
          sendAll: 'Send reminders',
          dismiss: 'Dismiss',
          retry: 'Retry',
          bot: "I've checked the ticket queue — 3 tickets are overdue by more than 24 hours. Want me to escalate them?",
          user: 'Yes, please escalate all of them to the on-call team.',
          picture: "Here's the screenshot from the customer:",
          ask: 'Ask AI',
          field: 'Ask AI anything about your clinics...',
          sendMessage: 'Send',
          assistant: 'AI Assistant',
          help: 'Always here to help',
          type: 'Type a message...',
          attach: 'Attach a file',
          close: 'Close',
          example: 'This is an example: nothing is sent to any AI from here.',
          stateNames: ['Loading', 'No suggestions right now', 'AI off — fixed wording, no badge', 'Error'],
          empty: ['Nothing to suggest right now', 'Everything that needs an operator is already in the Needs-attention panel.'],
          off: ['Suggestion', '3 clinics haven’t renewed in over 30 days. Send them a renewal reminder?'],
          failed: ['Suggestions couldn’t be refreshed', 'The last successful run was 6 hours ago. The detectors’ own findings are still shown.'],
        },
  );

  /** The suggestion card's four states (2421:88). */
  protected readonly states = computed(() => {
    const words = this.words();
    const shapes: { state: ScsInsightState; heading: string; text: string }[] = [
      { state: 'loading', heading: '', text: '' },
      { state: 'empty', heading: words.empty[0], text: words.empty[1] },
      { state: 'ready', heading: words.off[0], text: words.off[1] },
      { state: 'error', heading: words.failed[0], text: words.failed[1] },
    ];
    return shapes.map((shape, index) => ({ ...shape, name: words.stateNames[index] }));
  });

  /** The page's conversation (405:2), the questions asked here, and a fixed answer: an example only. */
  protected readonly chat = computed<ChatMessage[]>(() => {
    const words = this.words();
    return [
      { from: 'bot', text: words.bot },
      { from: 'user', text: words.user },
      { from: 'user', text: words.picture, attachment: 'ticket_screenshot.png' },
      ...this.asked().flatMap((question) => [
        { from: 'user' as const, text: question },
        { from: 'bot' as const, text: words.example },
      ]),
    ];
  });

  protected add(question: string): void {
    this.asked.update((asked) => [...asked, question]);
  }
}
