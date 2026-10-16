import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { SCS_ICONS, ScsIconComponent, ScsIconName } from 'ui';
import { GalleryState } from '../gallery-state';

/** One registered icon as the section lists it. */
interface IconRow {
  readonly name: ScsIconName;
  /** Its name in the design file's Icons page, or "drawn" for the icons drawn by hand before spec 095. */
  readonly from: string;
  readonly points: 'ltr' | 'rtl' | null;
}

/**
 * The gallery's Icons (spec 095, US5; design: Icons — `738:7380`): every entry of the registry, at
 * 16, 18, 20 and 24, with its short name, its name in the design file and whether it points. A
 * pointing icon is shown in both directions, so its mirroring on a page of the other direction is
 * seen; one that does not point is the same either way. Adding an icon is `node tools/icons.mjs add`
 * (quickstart §4); it appears here at once.
 */
@Component({
  selector: 'app-gallery-icons-section',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
    }
    .lead {
      margin: 0 0 var(--scs-spacing-md);
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-body-sm);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(min(240px, 100%), 1fr));
      gap: var(--scs-spacing-sm);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .icon {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-sm);
      padding: var(--scs-spacing-12);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
    }
    .sizes {
      display: flex;
      align-items: end;
      gap: var(--scs-spacing-12);
      color: var(--scs-color-icon-default);
    }
    .both {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-md);
    }
    .way {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-xs);
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .name {
      font-family: ui-monospace, monospace;
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
      color: var(--scs-color-text-primary);
      overflow-wrap: anywhere;
    }
    .from,
    .points {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
      overflow-wrap: anywhere;
    }
    .points {
      color: var(--scs-color-brand-active);
    }
  `,
  template: `
    <p class="lead" data-testid="icons-count">{{ words().count }}</p>
    <ul class="grid">
      @for (icon of icons; track icon.name) {
        <li class="icon" [attr.data-testid]="'icon-' + icon.name" [attr.data-points]="icon.points">
          @if (icon.points) {
            <div class="both">
              @for (way of ways; track way) {
                <span class="way" [attr.dir]="way" [attr.data-testid]="'icon-' + icon.name + '-' + way">
                  <span class="sizes">
                    @for (size of sizes; track size) {
                      <scs-icon [name]="icon.name" [size]="size" />
                    }
                  </span>
                  <bdi>{{ way === 'rtl' ? words().rtl : words().ltr }}</bdi>
                </span>
              }
            </div>
          } @else {
            <span class="sizes">
              @for (size of sizes; track size) {
                <scs-icon [name]="icon.name" [size]="size" />
              }
            </span>
          }
          <span class="name" dir="ltr">{{ icon.name }}</span>
          <span class="from" dir="ltr">{{ icon.from === 'drawn' ? words().drawn : icon.from }}</span>
          @if (icon.points) {
            <span class="points">{{ words().points }}</span>
          }
        </li>
      }
    </ul>
  `,
})
export class IconsSection {
  private readonly state = inject(GalleryState);

  protected readonly sizes = [16, 18, 20, 24] as const;
  protected readonly ways = ['ltr', 'rtl'] as const;

  protected readonly icons: readonly IconRow[] = (Object.keys(SCS_ICONS) as ScsIconName[])
    .sort((one, two) => one.localeCompare(two))
    .map((name) => {
      const drawing = SCS_ICONS[name] as { from: string; points?: 'ltr' | 'rtl' };
      return { name, from: drawing.from, points: drawing.points ?? null };
    });

  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          count: `${this.icons.length} أيقونة مسجّلة، كلٌّ منها بالأحجام 16 و18 و20 و24.`,
          drawn: 'مرسومة يدويًا قبل المواصفة 095',
          points: 'تشير — تنعكس في الاتجاه الآخر',
          ltr: 'من اليسار',
          rtl: 'من اليمين',
        }
      : {
          count: `${this.icons.length} registered icons, each at 16, 18, 20 and 24.`,
          drawn: 'Drawn by hand before spec 095',
          points: 'Points — mirrored in the other direction',
          ltr: 'Left to right',
          rtl: 'Right to left',
        },
  );
}
