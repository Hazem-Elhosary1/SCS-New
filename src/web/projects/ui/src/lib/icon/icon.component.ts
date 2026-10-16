import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SCS_ICONS, ScsIconDrawing, ScsIconName, ScsIconShape } from './icon-registry';

export type { ScsIconDrawing, ScsIconName, ScsIconShape } from './icon-registry';
export { SCS_ICONS } from './icon-registry';

/**
 * The line icons of the design system, drawn inline from the registry so they take the colour of
 * the text around them and need no icon font or image request. Decorative by default: the words
 * beside an icon say what it means, so assistive technology skips it.
 *
 * An icon that points (`points` in the registry — an arrow, a chevron, "back", sign-out) is drawn
 * for one direction and mirrored on a page of the other.
 */
@Component({
  selector: 'scs-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[attr.data-icon]': 'name()',
    '[attr.data-points]': 'drawing().points ?? null',
    'aria-hidden': 'true',
  },
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      line-height: 0;
    }
    /* The nearest direction decides, not any ancestor's: an arrow inside a left-to-right island of an
       Arabic page (an address, a code) points as that island reads. */
    :host(:dir(rtl)[data-points='ltr']) svg,
    :host(:dir(ltr)[data-points='rtl']) svg {
      transform: scaleX(-1);
    }
  `,
  template: `
    <svg
      [attr.width]="size()"
      [attr.height]="size()"
      [attr.viewBox]="drawing().viewBox"
      fill="none"
      stroke="currentColor"
      [attr.stroke-width]="drawing().strokeWidth ?? null"
      stroke-linecap="round"
      stroke-linejoin="round"
      focusable="false"
    >
      @for (shape of drawing().shapes; track $index) {
        @switch (shape.tag) {
          @case ('path') {
            <path
              [attr.d]="$any(shape).d"
              [attr.fill]="fill(shape)"
              [attr.stroke]="stroke(shape)"
              [attr.stroke-width]="shape.strokeWidth ?? null"
              [attr.fill-rule]="$any(shape).rule ?? null"
              [attr.clip-rule]="$any(shape).rule ?? null"
            />
          }
          @case ('circle') {
            <circle
              [attr.cx]="$any(shape).cx"
              [attr.cy]="$any(shape).cy"
              [attr.r]="$any(shape).r"
              [attr.fill]="fill(shape)"
              [attr.stroke]="stroke(shape)"
              [attr.stroke-width]="shape.strokeWidth ?? null"
            />
          }
          @case ('ellipse') {
            <ellipse
              [attr.cx]="$any(shape).cx"
              [attr.cy]="$any(shape).cy"
              [attr.rx]="$any(shape).rx"
              [attr.ry]="$any(shape).ry"
              [attr.fill]="fill(shape)"
              [attr.stroke]="stroke(shape)"
              [attr.stroke-width]="shape.strokeWidth ?? null"
            />
          }
          @case ('rect') {
            <rect
              [attr.x]="$any(shape).x"
              [attr.y]="$any(shape).y"
              [attr.width]="$any(shape).width"
              [attr.height]="$any(shape).height"
              [attr.rx]="$any(shape).rx ?? null"
              [attr.ry]="$any(shape).ry ?? null"
              [attr.fill]="fill(shape)"
              [attr.stroke]="stroke(shape)"
              [attr.stroke-width]="shape.strokeWidth ?? null"
            />
          }
          @case ('line') {
            <line
              [attr.x1]="$any(shape).x1"
              [attr.y1]="$any(shape).y1"
              [attr.x2]="$any(shape).x2"
              [attr.y2]="$any(shape).y2"
              [attr.stroke-width]="shape.strokeWidth ?? null"
            />
          }
          @case ('polyline') {
            <polyline [attr.points]="$any(shape).points" [attr.fill]="fill(shape)" [attr.stroke]="stroke(shape)" />
          }
          @case ('polygon') {
            <polygon [attr.points]="$any(shape).points" [attr.fill]="fill(shape)" [attr.stroke]="stroke(shape)" />
          }
        }
      }
    </svg>
  `,
})
export class ScsIconComponent {
  readonly name = input.required<ScsIconName>();

  /** Width and height in pixels: the design uses 16, 18, 20 and 24. */
  readonly size = input(20);

  protected readonly drawing = computed<ScsIconDrawing>(() => SCS_ICONS[this.name()]);

  /** A filled shape takes the colour of the text; a stroked one keeps the svg's `fill="none"`. */
  protected fill(shape: ScsIconShape): string | null {
    return this.paintOf(shape) === 'fill' ? 'currentColor' : null;
  }

  protected stroke(shape: ScsIconShape): string | null {
    return this.paintOf(shape) === 'fill' ? 'none' : null;
  }

  private paintOf(shape: ScsIconShape): 'stroke' | 'fill' {
    return ('paint' in shape && shape.paint) || this.drawing().paint;
  }
}
