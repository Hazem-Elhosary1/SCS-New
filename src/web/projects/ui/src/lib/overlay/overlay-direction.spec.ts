import { Directionality } from '@angular/cdk/bidi';
import { TestBed } from '@angular/core/testing';
import { provideOverlayDirection } from './overlay-direction';

/** Lets the mutation observer deliver what it saw. */
const settle = () => new Promise((resolve) => setTimeout(resolve));

describe('the direction panels open in', () => {
  const root = document.documentElement;
  let before: string;

  beforeEach(() => {
    before = root.dir;
  });

  afterEach(() => {
    root.dir = before;
    document.querySelector('.cdk-overlay-container')?.remove();
  });

  it('is the page’s, however early the CDK looked', async () => {
    root.dir = 'ltr';
    TestBed.configureTestingModule({ providers: [provideOverlayDirection()] });
    const cdk = TestBed.inject(Directionality);
    expect(cdk.value).toBe('ltr');

    // The remembered language is applied a moment later.
    root.dir = 'rtl';
    await settle();

    expect(cdk.value).toBe('rtl');
  });

  it('follows every switch of the language without a reload, and says so', async () => {
    root.dir = 'rtl';
    TestBed.configureTestingModule({ providers: [provideOverlayDirection()] });
    const cdk = TestBed.inject(Directionality);
    const said: string[] = [];
    cdk.change.subscribe((direction) => said.push(direction));

    root.dir = 'ltr';
    await settle();
    root.dir = 'rtl';
    await settle();

    expect(cdk.value).toBe('rtl');
    expect(said).toEqual(['ltr', 'rtl']);
  });

  it('turns a panel that is open and has its place worked out again', async () => {
    root.dir = 'rtl';
    TestBed.configureTestingModule({ providers: [provideOverlayDirection()] });
    TestBed.inject(Directionality);

    const container = document.createElement('div');
    container.className = 'cdk-overlay-container';
    const open = document.createElement('div');
    open.setAttribute('dir', 'rtl');
    container.append(open);
    document.body.append(container);
    let resized = 0;
    const count = () => (resized += 1);
    window.addEventListener('resize', count);

    root.dir = 'ltr';
    await settle();
    window.removeEventListener('resize', count);

    expect(open.getAttribute('dir')).toBe('ltr');
    expect(resized).toBe(1);
  });

  it('does nothing when the direction is what it was', async () => {
    root.dir = 'rtl';
    TestBed.configureTestingModule({ providers: [provideOverlayDirection()] });
    const cdk = TestBed.inject(Directionality);
    const said: string[] = [];
    cdk.change.subscribe((direction) => said.push(direction));

    root.dir = 'rtl';
    root.lang = 'ar';
    await settle();

    expect(said).toEqual([]);
  });
});
