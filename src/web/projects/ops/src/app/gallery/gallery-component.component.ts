import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Directive,
  TemplateRef,
  computed,
  contentChild,
  inject,
  input,
} from '@angular/core';
import { CATALOGUE, STILL_STATES, kebab, setOf } from './catalogue';
import { GalleryState } from './gallery-state';

/** What a specimen's template receives: the drawn option values by property, and the still state. */
export interface SpecimenContext {
  /** Values by the design's property names: `{ Type: 'Primary', Size: 'Small', State: 'Hover' }`. */
  readonly $implicit: Readonly<Record<string, string>>;
  readonly variant: string;
  readonly still: 'hover' | 'focus' | 'pressed' | null;
}

/** Marks the template that draws one specimen from its option values. */
@Directive({ selector: 'ng-template[appGallerySpecimen]' })
export class GallerySpecimenDirective {
  readonly template = inject<TemplateRef<SpecimenContext>>(TemplateRef);
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- the guard names the context only to type it
  static ngTemplateContextGuard(_directive: GallerySpecimenDirective, _context: unknown): _context is SpecimenContext {
    return true;
  }
}

/** Marks the template of the working example. */
@Directive({ selector: 'ng-template[appGalleryLive]' })
export class GalleryLiveDirective {
  readonly template = inject(TemplateRef);
}

/**
 * One component of the design file on the gallery (spec 095, `contracts/gallery.md`): its name,
 * whether it is shared, its nodes, every drawn variant as a still, labelled specimen, and a working
 * example. In the clinic look an operators-only component is not shown.
 *
 * A specimen of a pointer or keyboard state carries `data-scs-state` (hover, focus, pressed), which
 * the components style like the real state (research R5), and is inert: it cannot be focused or
 * pressed. Labels and nodes are the design's own names and carry `data-scs-label`.
 */
@Component({
  selector: 'app-gallery-component',
  imports: [NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    :host + :host {
      margin-block-start: var(--scs-spacing-xl);
      padding-block-start: var(--scs-spacing-lg);
      border-block-start: 1px solid var(--scs-color-border-default);
    }
    header {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: var(--scs-spacing-sm) var(--scs-spacing-md);
      margin-block-end: var(--scs-spacing-md);
    }
    h3 {
      margin: 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 600;
    }
    .kind {
      padding: 2px var(--scs-spacing-sm);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-muted);
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .node {
      font-family: ui-monospace, Consolas, monospace;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(var(--gallery-cell, 220px), 1fr));
      gap: var(--scs-spacing-md);
    }
    figure {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-sm);
      margin: 0;
      min-inline-size: 0;
    }
    figcaption {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: var(--scs-spacing-xs);
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-secondary);
    }
    .specimen {
      display: flex;
      align-items: flex-start;
      min-inline-size: 0;
    }
    .live {
      margin-block-start: var(--scs-spacing-lg);
    }
    .live h4 {
      margin: 0 0 var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
      color: var(--scs-color-text-secondary);
    }
    @media (max-width: 720px) {
      .grid {
        grid-template-columns: 1fr;
      }
    }
  `,
  template: `
    @if (visible()) {
      <header [attr.data-testid]="'component-' + pageId() + '-' + setId()">
        <h3 data-scs-label>{{ name() }}</h3>
        <span class="kind">{{ entry().kind === 'shared' ? words().shared : words().own }}</span>
        <span class="node" data-scs-label>{{ nodes() }}</span>
      </header>
      @if (specimen(); as draw) {
        <div class="grid">
          @for (variant of variants(); track variant.key) {
            <figure>
              <figcaption>
                <span data-scs-label>{{ variant.label }}</span>
                <span class="node" data-scs-label>{{ variant.node }}</span>
              </figcaption>
              <div
                class="specimen"
                inert
                [attr.data-testid]="'specimen-' + variant.key"
                [attr.data-scs-state]="variant.still"
              >
                <ng-container
                  [ngTemplateOutlet]="draw.template"
                  [ngTemplateOutletContext]="{ $implicit: variant.values, variant: variant.raw, still: variant.still }"
                />
              </div>
            </figure>
          }
        </div>
      }
      @if (live(); as example) {
        <div class="live" [attr.data-testid]="'live-' + pageId() + '-' + setId()">
          <h4>{{ words().live }}</h4>
          <ng-container [ngTemplateOutlet]="example.template" />
        </div>
      }
    }
  `,
})
export class GalleryComponentComponent {
  private readonly state = inject(GalleryState);
  protected readonly words = this.state.words;

  /** The design page and component set, as the inventory names them. */
  readonly page = input.required<string>();
  readonly set = input.required<string>();

  protected readonly specimen = contentChild(GallerySpecimenDirective);
  protected readonly live = contentChild(GalleryLiveDirective);

  protected readonly entry = computed(() => CATALOGUE[`${this.page()}/${this.set()}`]);
  protected readonly visible = computed(() => this.state.look() === 'ops' || this.entry().kind === 'shared');
  protected readonly name = computed(() => this.set());
  protected readonly pageId = computed(() => kebab(this.page()));
  protected readonly setId = computed(() => kebab(this.set()));

  protected readonly nodes = computed(() => {
    const { set } = setOf(this.page(), this.set());
    const clinic = this.state.look() === 'clinic' ? ` · clinic ${this.entry().clinicNode ?? '—'}` : '';
    return `EN ${set.en} · AR ${set.ar}${clinic}`;
  });

  protected readonly variants = computed(() => {
    const { set } = setOf(this.page(), this.set());
    const arabic = this.state.language() === 'ar';
    return set.variants.map(([raw, en, ar]) => {
      const parts = raw.split('|');
      const values = Object.fromEntries(set.props.map((prop, index) => [prop, parts[index]]));
      const still = parts.map((part) => STILL_STATES[part]).find((state) => !!state) ?? null;
      return {
        raw,
        values,
        still,
        key: `${this.pageId()}/${this.setId()}/${kebab(raw)}`,
        label: set.props.map((prop, index) => `${prop}: ${parts[index]}`).join(' · '),
        node: arabic ? ar : en,
      };
    });
  });
}
