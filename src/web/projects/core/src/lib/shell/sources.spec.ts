import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService, StaffSession } from '../auth/auth.service';
import { LanguageService } from '../i18n/language.service';
import {
  BranchesSource,
  ClinicTraitsSource,
  InboxCountSource,
  NotificationsSource,
  PlanSource,
  PreferencesService,
  SubscriptionSource,
} from './sources';

const PUT = '/v1/account/preferences';

class FakeLanguage {
  readonly language = signal<'ar' | 'en'>('ar');
  use(language: 'ar' | 'en'): void {
    this.language.set(language);
  }
}

function person(preferences?: object, more: object = {}): StaffSession {
  return { accountId: 'a-1', email: 'owner@noor.example', preferences, ...more } as StaffSession;
}

function start(session: StaffSession | null) {
  const held = signal<StaffSession | null>(session);
  const language = new FakeLanguage();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: AuthService, useValue: { session: held } },
      { provide: LanguageService, useValue: language },
    ],
  });
  const preferences = TestBed.inject(PreferencesService);
  TestBed.tick();
  return { preferences, language, held, http: TestBed.inject(HttpTestingController) };
}

const settle = () => new Promise((resolve) => setTimeout(resolve));

describe('the frame’s sources before their specs arrive', () => {
  it('say "nothing yet"', () => {
    TestBed.configureTestingModule({});

    expect(TestBed.inject(NotificationsSource).items()).toEqual([]);
    expect(TestBed.inject(NotificationsSource).state()).toBe('ready');
    expect(TestBed.inject(BranchesSource).multiBranch()).toBe(false);
    expect(TestBed.inject(BranchesSource).choice()).toBe('all');
    expect(TestBed.inject(SubscriptionSource).notice()).toBeNull();
    expect(TestBed.inject(PlanSource).excludes('bookings')).toBe(false);
    expect(TestBed.inject(InboxCountSource).unread()).toBeNull();
    expect(TestBed.inject(ClinicTraitsSource).dental()).toBe(false);
    expect(TestBed.inject(ClinicTraitsSource).linkedToDoctor()).toBe(false);
  });
});

describe('PreferencesService', () => {
  it('turns the console to the language the person saved, and sends nothing', () => {
    const { language, preferences, http } = start(
      person({ language: 'en', sidebarFolded: true, dismissedBanner: 'trial' }),
    );

    expect(language.language()).toBe('en');
    expect(preferences.sidebarFolded()).toBe(true);
    expect(preferences.dismissedBanner()).toBe('trial');
    http.expectNone(PUT);
  });

  it('keeps the browser’s language as the first choice of a person who never chose', () => {
    const { language, http } = start(
      person({ language: null, sidebarFolded: null, dismissedBanner: null }),
    );

    expect(language.language()).toBe('ar');
    expect(http.expectOne(PUT).request.body).toEqual({ language: 'ar' });
  });

  it('neither adopts nor saves anything while a temporary password is being replaced', () => {
    const { language, http } = start(
      person({ language: 'en', sidebarFolded: null, dismissedBanner: null }, { mustChangePassword: true }),
    );

    expect(language.language()).toBe('ar');
    http.expectNone(PUT);
  });

  it('applies a choice at once and then saves it', () => {
    const { preferences, language, http } = start(person({ language: 'ar', sidebarFolded: null, dismissedBanner: null }));

    preferences.setLanguage('en');
    preferences.setSidebarFolded(true);
    preferences.dismissBanner('grace');

    expect(language.language()).toBe('en');
    expect(preferences.sidebarFolded()).toBe(true);
    expect(preferences.dismissedBanner()).toBe('grace');
    expect(http.match(PUT).map((sent) => sent.request.body)).toEqual([
      { language: 'en' },
      { language: 'en', sidebarFolded: true },
      { language: 'en', sidebarFolded: true, dismissedBanner: 'grace' },
    ]);
  });

  it('sends a choice that failed to save with the next one, and shows no error', async () => {
    const { preferences, http } = start(person({ language: 'ar', sidebarFolded: null, dismissedBanner: null }));

    preferences.setSidebarFolded(true);
    http.expectOne(PUT).flush(null, { status: 503, statusText: 'Unavailable' });
    await settle();

    expect(preferences.sidebarFolded()).toBe(true);

    preferences.setLanguage('en');
    const second = http.expectOne(PUT);
    expect(second.request.body).toEqual({ sidebarFolded: true, language: 'en' });
    second.flush(null);
    await settle();

    preferences.dismissBanner('trial');
    expect(http.expectOne(PUT).request.body).toEqual({ dismissedBanner: 'trial' });
  });

  it('keeps what was chosen on this screen when the session is read again', () => {
    const { preferences, held, http } = start(person({ language: 'ar', sidebarFolded: null, dismissedBanner: null }));

    preferences.setSidebarFolded(true);
    held.set(person({ language: 'ar', sidebarFolded: null, dismissedBanner: null }));
    TestBed.tick();

    expect(preferences.sidebarFolded()).toBe(true);
    http.match(PUT);
  });

  it('forgets everything when the person signs out', () => {
    const { preferences, held } = start(person({ language: 'ar', sidebarFolded: true, dismissedBanner: 'trial' }));

    held.set(null);
    TestBed.tick();

    expect(preferences.sidebarFolded()).toBeNull();
    expect(preferences.dismissedBanner()).toBeNull();
  });
});
