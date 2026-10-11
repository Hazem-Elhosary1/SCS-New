import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { AuthService, ClinicTraitsSource, InboxCountSource, PERMISSIONS, PreferencesService } from 'core';
import { DESTINATIONS } from './destinations';
import { SideBarComponent } from './side-bar.component';

const EVERYTHING = PERMISSIONS.map((permission) => permission.code);

class FakePreferences {
  readonly saved = signal<boolean | null>(null);
  readonly sidebarFolded = () => this.saved();
  setSidebarFolded(folded: boolean): void {
    this.saved.set(folded);
  }
}

async function sideBar(
  permissions: readonly string[],
  options: { dental?: boolean; unread?: number | null; folded?: boolean | null } = {},
): Promise<{ fixture: ComponentFixture<SideBarComponent>; preferences: FakePreferences }> {
  const preferences = new FakePreferences();
  preferences.saved.set(options.folded === undefined ? false : options.folded);

  await TestBed.configureTestingModule({
    imports: [SideBarComponent, TranslocoTestingModule.forRoot({ langs: { ar: {} } })],
    providers: [
      provideRouter([]),
      {
        provide: AuthService,
        useValue: { session: signal({ permissions, role: null }), status: signal('signed-out') },
      },
      { provide: PreferencesService, useValue: preferences },
      {
        provide: ClinicTraitsSource,
        useValue: { dental: signal(options.dental ?? false), linkedToDoctor: signal(false) },
      },
      { provide: InboxCountSource, useValue: { unread: signal(options.unread ?? null) } },
    ],
  }).compileComponents();

  const fixture = TestBed.createComponent(SideBarComponent);
  await fixture.whenStable();
  return { fixture, preferences };
}

function shown(fixture: ComponentFixture<SideBarComponent>): string[] {
  return [...(fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid^="nav-"]')]
    .map((element) => element.getAttribute('data-testid')!.slice(4))
    .filter((key) => key !== 'inbox-count');
}

function press(fixture: ComponentFixture<SideBarComponent>, testId: string): void {
  (fixture.nativeElement as HTMLElement)
    .querySelector<HTMLElement>(`[data-testid="${testId}"]`)!
    .click();
}

describe('SideBarComponent', () => {
  it('shows an owner every destination of a clinic that is not dental, in the one order', async () => {
    const { fixture } = await sideBar(EVERYTHING);

    expect(shown(fixture)).toEqual([
      'home',
      'bookings',
      'patients',
      'invoices',
      'inbox',
      'tickets',
      'reports',
      'administration',
      'data',
      'settings',
    ]);
  });

  it('shows the dental lab only in a dental clinic', async () => {
    const { fixture } = await sideBar(EVERYTHING, { dental: true });

    expect(shown(fixture).at(-1)).toBe('dental-lab');
  });

  it('leaves out what the person may not open, and a parent with no child left', async () => {
    const { fixture } = await sideBar(['bookings.view', 'admin.roles.view']);

    expect(shown(fixture)).toEqual(['home', 'bookings', 'administration']);

    press(fixture, 'nav-administration');
    await fixture.whenStable();

    expect(shown(fixture)).toEqual(['home', 'bookings', 'administration', 'administration.roles']);
  });

  it('shows home alone to a person who holds nothing', async () => {
    const { fixture } = await sideBar([]);

    expect(shown(fixture)).toEqual(['home']);
  });

  it('opens and closes a parent on press, and says which it is', async () => {
    const { fixture } = await sideBar(EVERYTHING);
    const parent = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="nav-reports"]')!;

    expect(parent.getAttribute('aria-expanded')).toBe('false');

    press(fixture, 'nav-reports');
    await fixture.whenStable();

    expect(parent.getAttribute('aria-expanded')).toBe('true');
    expect(shown(fixture)).toEqual(
      expect.arrayContaining(
        DESTINATIONS.filter((place) => place.parent === 'reports').map((place) => place.key),
      ),
    );

    press(fixture, 'nav-reports');
    await fixture.whenStable();

    expect(parent.getAttribute('aria-expanded')).toBe('false');
  });

  it('keeps the fold as the person chose it', async () => {
    const { fixture, preferences } = await sideBar(EVERYTHING);
    const host = fixture.nativeElement as HTMLElement;

    expect(host.getAttribute('data-folded')).toBe('false');

    press(fixture, 'side-bar-fold');
    await fixture.whenStable();

    expect(preferences.saved()).toBe(true);
    expect(host.getAttribute('data-folded')).toBe('true');
  });

  it('unfolds with the group open when a group is pressed on the folded bar', async () => {
    const { fixture, preferences } = await sideBar(EVERYTHING, { folded: true });

    press(fixture, 'nav-data');
    await fixture.whenStable();

    expect(preferences.saved()).toBe(false);
    expect(shown(fixture)).toContain('data.doctors');
  });

  it('shows the unread count beside the inbox when there is one, and not while folded', async () => {
    const { fixture, preferences } = await sideBar(EVERYTHING, { unread: 7 });
    const count = () =>
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="nav-inbox-count"]');

    expect(count()?.textContent?.trim()).toBe('٧');

    preferences.saved.set(true);
    await fixture.whenStable();

    expect(count() === null || getComputedStyle(count()!).display === 'none').toBe(true);
  });

  it('shows no count when the source has none', async () => {
    const { fixture } = await sideBar(EVERYTHING);

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="nav-inbox-count"]')).toBeNull();
  });
});

describe('SideBarComponent, one parent at a time', () => {
  it('closes the open parent when another is opened', async () => {
    const { fixture } = await sideBar(EVERYTHING);
    const expanded = (key: string) =>
      (fixture.nativeElement as HTMLElement)
        .querySelector(`[data-testid="nav-${key}"]`)!
        .getAttribute('aria-expanded');

    press(fixture, 'nav-reports');
    await fixture.whenStable();
    expect([expanded('reports'), expanded('administration'), expanded('data')]).toEqual(['true', 'false', 'false']);

    press(fixture, 'nav-data');
    await fixture.whenStable();
    expect([expanded('reports'), expanded('administration'), expanded('data')]).toEqual(['false', 'false', 'true']);
    expect(shown(fixture)).not.toContain('reports.financial');
    expect(shown(fixture)).toContain('data.doctors');
  });
});
