import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AvatarGroupComponent, CarouselComponent, GalleryTileComponent, ImageComponent, VideoPlayerComponent } from './media';

const PIXEL = 'data:image/gif;base64,R0lGODlhAQABAAAAACw=';

@Component({
  imports: [ImageComponent, AvatarGroupComponent, VideoPlayerComponent, CarouselComponent, GalleryTileComponent],
  template: `
    <app-image data-testid="empty" alt="Clinic photo" />
    <app-image data-testid="photo" [src]="pixel" alt="Clinic photo" failedLabel="Failed to load image" />
    <app-avatar-group data-testid="group" [names]="['A B', 'C D', 'E F', 'G H', 'I J', 'K L']" />
    <app-video-player data-testid="video" length="12:34" playLabel="Play" pauseLabel="Pause" />
    <app-gallery-tile data-testid="tile" [src]="pixel" alt="Clinic photo" />
    <app-gallery-tile data-testid="last-tile" [src]="pixel" alt="Clinic photo" [more]="12" moreLabel="12 more photos" />
    <app-carousel data-testid="carousel" label="Photos" [slides]="[{ src: pixel, alt: 'First' }, { src: pixel, alt: 'Second' }]" previousLabel="Previous" nextLabel="Next" />
  `,
})
class Host {
  readonly pixel = PIXEL;
}

/** The operators' media parts (spec 095, `contracts/components.md`). */
describe('media', () => {
  function draw() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const find = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement;
    return { fixture, find };
  }

  it('an image is empty, loading, shown or failed, and says so when it fails', () => {
    const { fixture, find } = draw();
    expect(find('empty').getAttribute('data-state')).toBe('default');
    expect(find('photo').getAttribute('data-state')).toBe('loading');
    find('photo').querySelector('img')!.dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(find('photo').getAttribute('data-state')).toBe('error');
    expect(find('photo').querySelector('[data-testid="image-failed"]')?.textContent).toBe('Failed to load image');
  });

  it('an avatar group draws four and counts the rest, and names everyone', () => {
    const { find } = draw();
    expect(find('group').querySelectorAll('scs-avatar').length).toBe(4);
    expect(find('group').textContent).toContain('+2');
    expect(find('group').getAttribute('aria-label')).toBe('A B, C D, E F, G H, I J, K L');
  });

  it('a gallery tile may say how many more pictures there are, and says it to a screen reader (spec 095)', () => {
    const { find } = draw();
    expect(find('tile').querySelector('.more')).toBeNull();
    expect(find('tile').classList.contains('has-more')).toBe(false);
    const more = find('last-tile').querySelector('.more') as HTMLElement;
    expect(more.textContent?.trim()).toBe('+12');
    expect(more.getAttribute('role')).toBe('img');
    expect(more.getAttribute('aria-label')).toBe('12 more photos');
    expect(find('last-tile').classList.contains('has-more')).toBe(true);
  });

  it('a video player has a named play button and its length; a carousel moves and marks its dot', () => {
    const { fixture, find } = draw();
    expect(find('video').querySelector('[data-testid="video-play"]')?.getAttribute('aria-label')).toBe('Play');
    expect(find('video').textContent).toContain('12:34');
    const carousel = find('carousel');
    expect(carousel.getAttribute('aria-roledescription')).toBe('carousel');
    (carousel.querySelector('.on') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(carousel.querySelector('img')?.getAttribute('alt')).toBe('Second');
    expect(carousel.querySelector('[aria-current="true"]')?.getAttribute('aria-label')).toBe('Second');
  });
});
