import { ChangeDetectionStrategy, Component, ElementRef, computed, input, signal, viewChild } from '@angular/core';
import { ScsAvatarComponent, ScsIconComponent } from 'ui';

/**
 * A picture with its three states, the operators console's own (design: Media — Image, `82:20` /
 * `314:19`): a grey box with a picture mark until it is given, a shimmer while it loads, and a pale red
 * box with a broken mark and "Failed to load image" when it cannot be shown. `alt` names it.
 */
@Component({
  selector: 'app-image',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[attr.data-state]': 'state()' },
  styles: `
    :host {
      display: grid;
      gap: 6px;
    }
    .box {
      position: relative;
      display: grid;
      place-items: center;
      overflow: hidden;
      aspect-ratio: 3 / 2;
      border: 1px solid var(--scs-color-border-control, var(--scs-color-border-default));
      border-radius: 8px;
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-disabled, var(--scs-color-text-placeholder));
    }
    :host([data-state='loading']) .box {
      border-color: transparent;
      background: #eceef1;
    }
    :host([data-state='loading']) .box::after {
      content: '';
      position: absolute;
      inset-block: 0;
      inline-size: 40%;
      background: rgb(224 227 231 / 60%);
      animation: shimmer 1.4s ease-in-out infinite;
    }
    :host([data-state='error']) .box {
      background: var(--scs-color-feedback-danger-bg);
      color: var(--scs-color-feedback-danger-icon);
    }
    img {
      position: absolute;
      inset: 0;
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
    }
    :host(:not([data-state='shown'])) img {
      opacity: 0;
    }
    .failed {
      font-size: 11px;
      color: var(--scs-color-feedback-danger-icon);
    }
    @keyframes shimmer {
      from {
        inset-inline-start: -40%;
      }
      to {
        inset-inline-start: 100%;
      }
    }
    @media (prefers-reduced-motion: reduce) {
      :host([data-state='loading']) .box::after {
        inset-inline-start: 30%;
        animation: none;
      }
    }
  `,
  template: `
    <div class="box" [attr.role]="src() ? null : 'img'" [attr.aria-label]="src() ? null : alt()">
      @if (state() !== 'loading') {
        <scs-icon [name]="'image'" [size]="state() === 'error' ? 32 : 40" aria-hidden="true" />
      }
      @if (src()) {
        <img [src]="src()" [alt]="alt()" (load)="loaded.set('shown')" (error)="loaded.set('error')" />
      }
    </div>
    @if (state() === 'error' && failedLabel()) {
      <span class="failed" data-testid="image-failed">{{ failedLabel() }}</span>
    }
  `,
})
export class ImageComponent {
  readonly src = input<string | null>(null);
  /** Already translated: what the picture shows. */
  readonly alt = input('');
  /** Already translated: "Failed to load image". */
  readonly failedLabel = input('');
  /** Draws a state still, whatever the picture does (the gallery's specimens). */
  readonly still = input<'default' | 'loading' | 'error' | null>(null);

  protected readonly loaded = signal<'shown' | 'error' | null>(null);
  protected readonly state = computed(() => this.still() ?? (this.src() ? (this.loaded() ?? 'loading') : 'default'));
}

/**
 * People, overlapping, the operators console's own (design: Media — avatar-group, `84:3`): the first
 * few as avatars ringed in white, the rest as "+5". The group names everyone for assistive technology.
 */
@Component({
  selector: 'app-avatar-group',
  imports: [ScsAvatarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'img', '[attr.aria-label]': 'names().join(", ")' },
  styles: `
    :host {
      display: inline-flex;
    }
    scs-avatar,
    .more {
      box-shadow: 0 0 0 2px var(--scs-color-bg-surface);
    }
    scs-avatar + scs-avatar,
    .more {
      margin-inline-start: -10px;
    }
    .more {
      display: grid;
      place-items: center;
      inline-size: var(--scs-avatar-md, 40px);
      block-size: var(--scs-avatar-md, 40px);
      border-radius: 50%;
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-tertiary);
      font-size: 13px;
      font-weight: 700;
    }
  `,
  template: `
    @for (name of shown(); track $index) {
      <scs-avatar [name]="name" aria-hidden="true" />
    }
    @if (rest() > 0) {
      <span class="more" aria-hidden="true"><bdi dir="ltr">+{{ rest() }}</bdi></span>
    }
  `,
})
export class AvatarGroupComponent {
  readonly names = input.required<readonly string[]>();
  /** How many are drawn before "+N". */
  readonly max = input(4);

  protected readonly shown = computed(() => this.names().slice(0, this.max()));
  protected readonly rest = computed(() => Math.max(0, this.names().length - this.max()));
}

/**
 * A video on the browser's own player with the console's drawn controls (design: Media — video-player,
 * `84:15`): a round play button in the middle, the length in a dark chip, a thin purple bar of what has
 * played. The play button is named and says whether it plays or pauses.
 */
@Component({
  selector: 'app-video-player',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      position: relative;
      display: block;
      overflow: hidden;
      aspect-ratio: 7 / 4;
      border-radius: 8px;
      background: #1c1c21;
    }
    video {
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
    }
    .play {
      position: absolute;
      inset: 50% auto auto 50%;
      display: grid;
      place-items: center;
      inline-size: 48px;
      block-size: 48px;
      border: 0;
      border-radius: 50%;
      background: rgb(255 255 255 / 92%);
      color: #1c1c21;
      cursor: pointer;
      transform: translate(-50%, -50%);
    }
    .play:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    .time {
      position: absolute;
      inset-block-end: 8px;
      inset-inline-end: 8px;
      padding: 2px 8px;
      border-radius: 4px;
      background: rgb(0 0 0 / 60%);
      color: #ffffff;
      font-size: 10px;
      font-weight: 500;
    }
    .track {
      position: absolute;
      inset-inline: 0;
      inset-block-end: 0;
      block-size: 3px;
      background: rgb(255 255 255 / 25%);
    }
    .played {
      block-size: 100%;
      background: var(--scs-color-brand-default);
    }
  `,
  template: `
    <video #video [src]="src()" [poster]="poster()" preload="metadata" playsinline (timeupdate)="tick()" (play)="playing.set(true)" (pause)="playing.set(false)" (ended)="playing.set(false)"></video>
    <button type="button" class="play" data-testid="video-play" [attr.aria-label]="playing() ? pauseLabel() : playLabel()" (click)="toggle()">
      <scs-icon [name]="playing() ? 'pause' : 'play'" [size]="16" />
    </button>
    <bdi class="time" dir="ltr">{{ length() }}</bdi>
    <div class="track" aria-hidden="true"><div class="played" [style.inline-size.%]="share()"></div></div>
  `,
})
export class VideoPlayerComponent {
  readonly src = input<string | null>(null);
  readonly poster = input<string | null>(null);
  /** Already written: "12:34". */
  readonly length = input('');
  /** Already translated: the play button's names. */
  readonly playLabel = input('Play');
  readonly pauseLabel = input('Pause');
  /** The share played, drawn still (the gallery's specimen). */
  readonly stillShare = input<number | null>(null);

  protected readonly playing = signal(false);
  private readonly played = signal(0);
  private readonly video = viewChild.required<ElementRef<HTMLVideoElement>>('video');

  protected readonly share = computed(() => this.stillShare() ?? this.played());

  protected toggle(): void {
    const video = this.video().nativeElement;
    if (video.paused) {
      video.play()?.catch(() => this.playing.set(false));
    } else {
      video.pause();
    }
  }

  protected tick(): void {
    const video = this.video().nativeElement;
    this.played.set(video.duration ? (video.currentTime / video.duration) * 100 : 0);
  }
}

/** One picture of a carousel. */
export interface CarouselSlide {
  readonly src: string;
  readonly alt: string;
}

/**
 * Pictures one at a time, the operators console's own (design: Media — carousel, `84:26`): round
 * previous and next buttons at its sides, a dot for each picture under it. The arrows of the keyboard
 * move too, in the page's direction; the dots say which picture is shown.
 */
@Component({
  selector: 'app-carousel',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'region', 'aria-roledescription': 'carousel', '[attr.aria-label]': 'label()' },
  styles: `
    :host {
      position: relative;
      display: block;
      overflow: hidden;
      aspect-ratio: 7 / 4;
      border-radius: 8px;
      background: var(--scs-color-bg-surface-secondary);
    }
    img {
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
    }
    .step {
      position: absolute;
      inset-block-start: 50%;
      display: grid;
      place-items: center;
      inline-size: 28px;
      block-size: 28px;
      border: 0;
      border-radius: 50%;
      background: rgb(255 255 255 / 90%);
      color: var(--scs-color-text-primary);
      cursor: pointer;
      transform: translateY(-50%);
    }
    .back {
      inset-inline-start: 10px;
    }
    .on {
      inset-inline-end: 10px;
    }
    .on scs-icon {
      transform: scaleX(-1);
    }
    .step:focus-visible,
    .dots button:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    .dots {
      position: absolute;
      inset-block-end: 10px;
      inset-inline: 0;
      display: flex;
      justify-content: center;
      gap: 8px;
    }
    .dots button {
      inline-size: 6px;
      block-size: 6px;
      padding: 0;
      border: 0;
      border-radius: 50%;
      background: rgb(255 255 255 / 60%);
      cursor: pointer;
    }
    .dots button[aria-current='true'] {
      background: var(--scs-color-brand-default);
    }
  `,
  template: `
    <div #stage tabindex="0" aria-live="polite" (keydown)="key($event)">
      @if (slides()[at()]; as slide) {
        <img [src]="slide.src" [alt]="slide.alt" />
      }
    </div>
    <button type="button" class="step back" [attr.aria-label]="previousLabel()" (click)="move(-1)"><scs-icon name="chevron-start" [size]="14" /></button>
    <button type="button" class="step on" [attr.aria-label]="nextLabel()" (click)="move(1)"><scs-icon name="chevron-start" [size]="14" /></button>
    <div class="dots">
      @for (slide of slides(); track $index; let index = $index) {
        <button type="button" [attr.aria-label]="slide.alt" [attr.aria-current]="index === at()" (click)="at.set(index)"></button>
      }
    </div>
  `,
})
export class CarouselComponent {
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('stage');
  readonly slides = input.required<readonly CarouselSlide[]>();
  /** Already translated. */
  readonly label = input('');
  readonly previousLabel = input('Previous');
  readonly nextLabel = input('Next');

  protected readonly at = signal(0);

  protected move(step: number): void {
    const count = this.slides().length;
    if (count) this.at.set((this.at() + step + count) % count);
  }

  protected key(event: KeyboardEvent): void {
    const rtl = getComputedStyle(this.host().nativeElement).direction === 'rtl';
    if (event.key === 'ArrowRight') this.move(rtl ? -1 : 1);
    else if (event.key === 'ArrowLeft') this.move(rtl ? 1 : -1);
  }
}

/**
 * A square picture in a grid, the operators console's own (design: Media — gallery-tile, `84:39` …
 * `84:67`): the picture, or a grey tile with a picture mark while there is none; the grid's last
 * tile may say how many more pictures there are ("+12" over a dark veil).
 */
@Component({
  selector: 'app-gallery-tile',
  imports: [ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.has-more]': 'more() > 0' },
  styles: `
    :host {
      display: grid;
      place-items: center;
      overflow: hidden;
      aspect-ratio: 1;
      border-radius: 6px;
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-disabled, var(--scs-color-text-placeholder));
    }
    img {
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
    }
    :host(.has-more) {
      position: relative;
    }
    .more {
      position: absolute;
      inset: 0;
      display: grid;
      align-content: end;
      justify-content: center;
      padding-block-end: 6px;
      background: rgb(26 32 36 / 60%);
      color: #fff;
      font-size: 13px;
      font-weight: 700;
    }
  `,
  template: `
    @if (src()) {
      <img [src]="src()" [alt]="alt()" />
    } @else {
      <scs-icon name="image" [size]="24" aria-hidden="true" />
    }
    @if (more() > 0) {
      <span class="more" role="img" [attr.aria-label]="moreLabel() || null"><bdi dir="ltr" aria-hidden="true">+{{ more() }}</bdi></span>
    }
  `,
})
export class GalleryTileComponent {
  readonly src = input<string | null>(null);
  readonly alt = input('');
  /** How many more pictures there are than the grid shows (0: none). */
  readonly more = input(0);
  /** Already translated: says that count, for example «١٢ صورة أخرى» · "12 more pictures". */
  readonly moreLabel = input('');
}
