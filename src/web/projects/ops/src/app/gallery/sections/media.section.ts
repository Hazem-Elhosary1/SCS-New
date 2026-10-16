import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { AvatarGroupComponent, CarouselComponent, GalleryTileComponent, ImageComponent, VideoPlayerComponent } from '../../components/media/media';
import { EXAMPLE_PICTURES, EXAMPLE_POSTER } from '../examples/media-examples';
import { GalleryComponentComponent, GalleryLiveDirective, GallerySpecimenDirective } from '../gallery-component.component';
import { GalleryPartComponent } from '../gallery-part.component';
import { GalleryState } from '../gallery-state';

/**
 * The Media page (82:2), the operators console's own: the image empty, loading and failed; the avatar
 * group, the video player, the carousel and the gallery grid; working: a picture that loads and one
 * that fails.
 */
@Component({
  selector: 'app-gallery-media-section',
  imports: [
    ImageComponent,
    AvatarGroupComponent,
    VideoPlayerComponent,
    CarouselComponent,
    GalleryTileComponent,
    GalleryComponentComponent,
    GallerySpecimenDirective,
    GalleryLiveDirective,
    GalleryPartComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    app-image {
      inline-size: 100%;
      max-inline-size: 240px;
    }
    app-video-player,
    app-carousel {
      inline-size: 100%;
      max-inline-size: 280px;
    }
    .live {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-md);
      inline-size: 100%;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 90px));
      gap: 8px;
      inline-size: 100%;
      max-inline-size: 384px;
    }
  `,
  template: `
    <app-gallery-component page="Media" set="Image">
      <ng-template appGallerySpecimen let-values>
        <app-image [still]="stillOf(values['State'])" [alt]="words().picture" [failedLabel]="words().failed" />
      </ng-template>
      <ng-template appGalleryLive>
        <div class="live">
          <app-image [src]="pictures[0]" [alt]="words().picture" [failedLabel]="words().failed" />
          <app-image src="/missing-picture.png" [alt]="words().picture" [failedLabel]="words().failed" />
        </div>
      </ng-template>
    </app-gallery-component>

    <app-gallery-part name="Avatar Group" node="84:3" testId="specimen-media/parts/avatar-group">
      <app-avatar-group [names]="words().people" />
    </app-gallery-part>
    <app-gallery-part name="Video Player" node="84:15" testId="specimen-media/parts/video-player">
      <app-video-player [poster]="poster" length="12:34" [stillShare]="35" [playLabel]="words().play" [pauseLabel]="words().pause" />
    </app-gallery-part>
    <app-gallery-part name="Carousel" node="84:26" testId="specimen-media/parts/carousel">
      <app-carousel [label]="words().carousel" [slides]="slides()" [previousLabel]="words().previous" [nextLabel]="words().next" />
    </app-gallery-part>
    <app-gallery-part name="Gallery Grid" node="84:39" testId="specimen-media/parts/gallery-grid">
      <div class="grid">
        @for (tile of tiles; track $index) {
          <app-gallery-tile [src]="tile" [alt]="words().picture" [more]="$last ? 12 : 0" [moreLabel]="$last ? words().more : ''" />
        }
      </div>
    </app-gallery-part>
  `,
})
export class MediaSection {
  private readonly state = inject(GalleryState);
  protected readonly pictures = EXAMPLE_PICTURES;
  protected readonly poster = EXAMPLE_POSTER;
  /** The page's grid of eight: four pictures, four still empty, the last saying 12 more. */
  protected readonly tiles = [EXAMPLE_PICTURES[0], EXAMPLE_PICTURES[1], null, EXAMPLE_PICTURES[2], null, EXAMPLE_PICTURES[3], null, null];

  /** The page's words (Media 82:2 and its Arabic twin 314:19). */
  protected readonly words = computed(() =>
    this.state.language() === 'ar'
      ? {
          picture: 'صورة العيادة',
          failed: 'تعذّر تحميل الصورة',
          people: ['جمال داود', 'منى عادل', 'سارة خالد', 'رامي طارق', 'أ ب', 'ج د', 'ه و', 'ز ح', 'ط ي'],
          play: 'تشغيل الفيديو',
          pause: 'إيقاف الفيديو مؤقتاً',
          carousel: 'صور العيادة',
          previous: 'الصورة السابقة',
          next: 'الصورة التالية',
          slides: ['تلال بنفسجية', 'تلال زرقاء', 'تلال خضراء'],
          more: '12 صورة أخرى',
        }
      : {
          picture: 'Clinic photo',
          failed: 'Failed to load image',
          people: ['John Doe', 'Mona Adel', 'Sara Khaled', 'Rami Tarek', 'A B', 'C D', 'E F', 'G H', 'I J'],
          play: 'Play the video',
          pause: 'Pause the video',
          carousel: 'Clinic photos',
          previous: 'Previous photo',
          next: 'Next photo',
          slides: ['Purple hills', 'Blue hills', 'Green hills'],
          more: '12 more photos',
        },
  );
  protected readonly slides = computed(() => this.words().slides.map((alt, index) => ({ src: EXAMPLE_PICTURES[index], alt })));

  protected stillOf(state: string): 'default' | 'loading' | 'error' {
    return state === 'Loading' ? 'loading' : state === 'Error' ? 'error' : 'default';
  }
}
