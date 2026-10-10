import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type ScsIconName =
  | 'mail'
  | 'lock'
  | 'eye'
  | 'eye-off'
  | 'alert-triangle'
  | 'alert-circle'
  | 'check-circle'
  | 'info'
  | 'help-circle'
  | 'circle'
  | 'refresh'
  | 'phone'
  | 'chat'
  | 'close'
  | 'back';

/**
 * The line icons of the design system, drawn inline so they take the colour of the text around
 * them and need no icon font or image request. Decorative by default: the words beside an icon
 * say what it means, so assistive technology skips it.
 *
 * `back` is an arrow that points to where the person came from — toward the start of the line —
 * so it turns with the direction of the page.
 */
@Component({
  selector: 'scs-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-icon]': 'name()', 'aria-hidden': 'true' },
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      line-height: 0;
    }
    :host-context([dir='ltr'])[data-icon='back'] svg {
      transform: scaleX(-1);
    }
  `,
  template: `
    <svg
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.75"
      stroke-linecap="round"
      stroke-linejoin="round"
      focusable="false"
    >
      @switch (name()) {
        @case ('mail') {
          <rect x="2" y="4" width="20" height="16" rx="2" />
          <path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7" />
        }
        @case ('lock') {
          <rect x="3" y="11" width="18" height="11" rx="2" />
          <path d="M7 11V7a5 5 0 0 1 10 0v4" />
        }
        @case ('eye') {
          <path
            d="M2.06 12.35a1 1 0 0 1 0-.7 10.75 10.75 0 0 1 19.88 0 1 1 0 0 1 0 .7 10.75 10.75 0 0 1-19.88 0"
          />
          <circle cx="12" cy="12" r="3" />
        }
        @case ('eye-off') {
          <path
            d="M2.06 12.35a1 1 0 0 1 0-.7 10.75 10.75 0 0 1 19.88 0 1 1 0 0 1 0 .7 10.75 10.75 0 0 1-19.88 0"
          />
          <circle cx="12" cy="12" r="3" />
          <path d="m3 3 18 18" />
        }
        @case ('alert-triangle') {
          <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3" />
          <path d="M12 9v4" />
          <path d="M12 17h.01" />
        }
        @case ('alert-circle') {
          <circle cx="12" cy="12" r="10" />
          <path d="M12 8v4" />
          <path d="M12 16h.01" />
        }
        @case ('check-circle') {
          <path d="M21.8 10A10 10 0 1 1 17 3.34" />
          <path d="m9 11 3 3L22 4" />
        }
        @case ('info') {
          <circle cx="12" cy="12" r="10" />
          <path d="M12 16v-4" />
          <path d="M12 8h.01" />
        }
        @case ('help-circle') {
          <circle cx="12" cy="12" r="10" />
          <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3" />
          <path d="M12 17h.01" />
        }
        @case ('circle') {
          <circle cx="12" cy="12" r="10" />
        }
        @case ('refresh') {
          <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8" />
          <path d="M21 3v5h-5" />
          <path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16" />
          <path d="M8 16H3v5" />
        }
        @case ('phone') {
          <path
            d="M13.83 16.57a1 1 0 0 0 1.21-.3l.36-.47A2 2 0 0 1 17 15h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2A18 18 0 0 1 2 4a2 2 0 0 1 2-2h3a2 2 0 0 1 2 2v3a2 2 0 0 1-.8 1.6l-.47.35a1 1 0 0 0-.29 1.23 14 14 0 0 0 6.39 6.39"
          />
        }
        @case ('chat') {
          <path d="M7.9 20A9 9 0 1 0 4 16.1L2 22Z" />
        }
        @case ('close') {
          <path d="M18 6 6 18" />
          <path d="m6 6 12 12" />
        }
        @case ('back') {
          <path d="M5 12h14" />
          <path d="m12 5 7 7-7 7" />
        }
      }
    </svg>
  `,
})
export class ScsIconComponent {
  readonly name = input.required<ScsIconName>();

  /** Width and height in pixels. */
  readonly size = input(20);
}
