import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DOCUMENT, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthService, StaffSession } from '../auth/auth.service';
import { OnlineService } from '../network/online.service';
import { NOTIFICATIONS_POLL_MS, PlatformNotifications } from './platform-notifications';
import { NotificationItem, NotificationsSource } from './sources';

const STATE = '/v1/notifications/state';
const LIST = '/v1/notifications';

function item(id: string, more: Partial<NotificationItem> = {}): NotificationItem {
  return {
    id,
    kind: 'ticket',
    title: 'تذكرة دعم جديدة',
    text: `التذكرة ${id}`,
    at: '2026-10-11T09:00:00+00:00',
    read: false,
    link: null,
    ...more,
  };
}

const person = (accountId = 'a-1', more: object = {}) =>
  ({ accountId, email: 'owner@noor.example', ...more }) as StaffSession;

function start(session: StaffSession | null = person()) {
  const held = signal<StaffSession | null>(session);
  const online = signal(true);
  const went: string[] = [];
  let visibility: DocumentVisibilityState = 'visible';

  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: AuthService, useValue: { session: held } },
      { provide: OnlineService, useValue: { online } },
      {
        provide: Router,
        useValue: {
          navigateByUrl: (url: string) => {
            went.push(url);
            return Promise.resolve(true);
          },
        },
      },
    ],
  });

  const document = TestBed.inject(DOCUMENT);
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => visibility });

  const source = TestBed.inject(PlatformNotifications);
  TestBed.tick();
  const http = TestBed.inject(HttpTestingController);

  return {
    source,
    http,
    held,
    online,
    went,
    show: (visible: boolean) => {
      visibility = visible ? 'visible' : 'hidden';
      document.dispatchEvent(new Event('visibilitychange'));
    },
    /** Answers the one waiting question about the state. */
    state: (unread: boolean, latestAt: string | null = null) =>
      http.expectOne(STATE).flush({ unread, latestAt }),
  };
}

const settle = async () => {
  await Promise.resolve();
  await Promise.resolve();
  TestBed.tick();
};

describe('the bell’s source, from the platform', () => {
  beforeEach(() => vi.useFakeTimers());

  afterEach(() => {
    try {
      TestBed.inject(HttpTestingController).verify();
    } finally {
      TestBed.inject(PlatformNotifications).ngOnDestroy();
      delete (TestBed.inject(DOCUMENT) as unknown as Record<string, unknown>)['visibilityState'];
      vi.useRealTimers();
      TestBed.resetTestingModule();
    }
  });

  it('is what the frame’s bell is given', () => {
    const world = start();
    world.state(false);

    expect(TestBed.inject(NotificationsSource)).toBe(world.source);
  });

  it('asks whether anything is unread when it starts, and shows the dot it is told', async () => {
    const world = start();

    expect(world.source.unread()).toBe(false);
    world.state(true, '2026-10-11T09:00:00+00:00');
    await settle();

    expect(world.source.unread()).toBe(true);
    expect(world.source.items()).toEqual([]);
  });

  it('asks again every thirty seconds while the tab is seen, and not while it is hidden', async () => {
    const world = start();
    world.state(false);

    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS - 1);
    world.http.expectNone(STATE);
    vi.advanceTimersByTime(1);
    world.state(true);
    await settle();
    expect(world.source.unread()).toBe(true);

    world.show(false);
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS * 3);
    world.http.expectNone(STATE);

    // Coming back asks at once, without waiting for the next turn.
    world.show(true);
    world.state(false);
    await settle();
    expect(world.source.unread()).toBe(false);
  });

  it('fetches the list when the bell is opened', async () => {
    const world = start();
    world.state(true, 't1');
    await settle();

    world.source.setOpen(true);
    expect(world.source.state()).toBe('loading');
    world.http.expectOne(LIST).flush({ items: [item('n-2'), item('n-1')], unread: true, next: 'c1' });
    await settle();

    expect(world.source.state()).toBe('ready');
    expect(world.source.items().map((each) => each.id)).toEqual(['n-2', 'n-1']);
    expect(world.source.hasMore()).toBe(true);
  });

  it('fetches the list again when something arrives while the bell is open, and only then', async () => {
    const world = start();
    world.state(true, 't1');
    await settle();
    world.source.setOpen(true);
    world.http.expectOne(LIST).flush({ items: [item('n-1', { read: true })], unread: false, next: null });
    await settle();

    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS);
    world.state(false, 't1');
    await settle();
    world.http.expectNone(LIST);

    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS);
    world.state(true, 't2');
    await settle();
    world.http
      .expectOne(LIST)
      .flush({ items: [item('n-2', { read: true }), item('n-1', { read: true })], unread: false, next: null });
    await settle();

    // No skeleton over a list that is already there.
    expect(world.source.items().map((each) => each.id)).toEqual(['n-2', 'n-1']);

    world.source.setOpen(false);
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS);
    world.state(true, 't3');
    await settle();
    world.http.expectNone(LIST);
  });

  it('an open panel shows what was read on the person’s other browser', async () => {
    const world = start();
    world.state(true, 't1');
    await settle();
    world.source.setOpen(true);
    world.http.expectOne(LIST).flush({ items: [item('n-2'), item('n-1')], unread: true, next: null });
    await settle();

    // Read elsewhere: nothing new arrived, and the open panel still asks.
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS);
    world.state(true, 't1');
    await settle();
    world.http
      .expectOne(LIST)
      .flush({ items: [item('n-2', { read: true }), item('n-1')], unread: true, next: null });
    await settle();
    expect(world.source.items().map((each) => each.read)).toEqual([true, false]);

    // A turn that fails leaves the list as it is, with no error over it.
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS);
    world.state(true, 't1');
    await settle();
    world.http.expectOne(LIST).flush(null, { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(world.source.state()).toBe('ready');
    expect(world.source.items()).toHaveLength(2);

    // A closed panel asks for no list.
    world.source.setOpen(false);
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS);
    world.state(true, 't1');
    await settle();
    world.http.expectNone(LIST);
  });

  it('a refresh keeps the older ones the person loaded, and the way to the ones after them', async () => {
    const world = start();
    world.state(true, 't1');
    await settle();
    world.source.setOpen(true);
    const first = [item('n-4', { at: '2026-10-11T09:04:00+00:00' }), item('n-3', { at: '2026-10-11T09:03:00+00:00' })];
    const second = [item('n-2', { at: '2026-10-11T09:02:00+00:00' }), item('n-1', { at: '2026-10-11T09:01:00+00:00' })];
    world.http.expectOne(LIST).flush({ items: first, unread: true, next: 'c1' });
    await settle();
    world.source.loadMore();
    world.http
      .expectOne((request) => request.url === LIST && request.params.has('before'))
      .flush({ items: second, unread: true, next: 'c2' });
    await settle();

    // A new one arrives, and one of the first page was read elsewhere.
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS);
    world.state(true, 't2');
    await settle();
    world.http.expectOne(LIST).flush({
      items: [item('n-5', { at: '2026-10-11T09:05:00+00:00' }), { ...first[0], read: true }],
      unread: true,
      next: 'c0',
    });
    await settle();

    // n-3 fell off the first page and is kept with the older ones; nothing is lost or doubled.
    expect(world.source.items().map((each) => each.id)).toEqual(['n-5', 'n-4', 'n-3', 'n-2', 'n-1']);
    expect(world.source.items()[1].read).toBe(true);
    expect(world.source.hasMore()).toBe(true);
    world.source.loadMore();
    const older = world.http.expectOne((request) => request.url === LIST && request.params.has('before'));
    expect(older.request.params.get('before')).toBe('c2');
    older.flush({ items: [], unread: true, next: null });
    await settle();

    // Everything read elsewhere: the older ones are read too.
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS);
    world.state(false, 't2');
    await settle();
    world.http.expectOne(LIST).flush({
      items: [item('n-5', { at: '2026-10-11T09:05:00+00:00', read: true }), { ...first[0], read: true }],
      unread: false,
      next: 'c0',
    });
    await settle();
    expect(world.source.items()).toHaveLength(5);
    expect(world.source.items().every((each) => each.read)).toBe(true);
  });

  it('marks everything read at once, and tells the platform', async () => {
    const world = start();
    world.state(true, 't1');
    world.source.setOpen(true);
    world.http.expectOne(LIST).flush({ items: [item('n-2'), item('n-1')], unread: true, next: 'c1' });
    await settle();

    world.source.markAllRead();

    expect(world.source.items().every((each) => each.read)).toBe(true);
    expect(world.source.unread()).toBe(false);
    const sent = world.http.expectOne(`${LIST}/read-all`);
    expect(sent.request.method).toBe('POST');
    sent.flush(null);
  });

  it('opening one marks it read and goes where it leads', async () => {
    const world = start();
    world.state(true, 't1');
    world.source.setOpen(true);
    const there = item('n-1', { link: 'tickets/7' });
    world.http.expectOne(LIST).flush({ items: [there, item('n-2', { read: true })], unread: true, next: null });
    await settle();

    world.source.open(there);

    expect(world.source.items()[0].read).toBe(true);
    expect(world.source.unread()).toBe(false);
    expect(world.went).toEqual(['/tickets/7']);
    const sent = world.http.expectOne(`${LIST}/n-1/read`);
    expect(sent.request.method).toBe('POST');
    sent.flush(null);
    await settle();
    world.state(false, 't1');
  });

  it('one that leads nowhere is marked read and the person stays where they are', async () => {
    const world = start();
    world.state(true, 't1');
    world.source.setOpen(true);
    const nowhere = item('n-1');
    world.http.expectOne(LIST).flush({ items: [nowhere, item('n-2')], unread: true, next: null });
    await settle();

    world.source.open(nowhere);

    expect(world.went).toEqual([]);
    expect(world.source.items().map((each) => each.read)).toEqual([true, false]);
    expect(world.source.unread()).toBe(true);
    world.http.expectOne(`${LIST}/n-1/read`).flush(null);
    await settle();
    world.state(true, 't1');
    await settle();

    // The panel is open and one is still unread: it is kept true.
    world.http
      .expectOne(LIST)
      .flush({ items: [item('n-1', { read: true }), item('n-2')], unread: true, next: null });
  });

  it('one already read is not marked again', async () => {
    const world = start();
    world.state(false, 't1');

    world.source.open(item('n-1', { read: true, link: '/roles' }));

    expect(world.went).toEqual(['/roles']);
    world.http.expectNone(`${LIST}/n-1/read`);
  });

  it('loads older ones after those shown', async () => {
    const world = start();
    world.state(true, 't1');
    world.source.setOpen(true);
    world.http.expectOne(LIST).flush({ items: [item('n-3'), item('n-2')], unread: true, next: 'c1' });
    await settle();

    world.source.loadMore();
    world.source.loadMore();

    const asked = world.http.expectOne((request) => request.url === LIST && request.params.has('before'));
    expect(asked.request.params.get('before')).toBe('c1');
    asked.flush({ items: [item('n-2'), item('n-1')], unread: true, next: null });
    await settle();

    expect(world.source.items().map((each) => each.id)).toEqual(['n-3', 'n-2', 'n-1']);
    expect(world.source.hasMore()).toBe(false);

    world.source.loadMore();
    world.http.expectNone((request) => request.url === LIST);
  });

  it('a list that could not be loaded says so, and trying again loads it', async () => {
    const world = start();
    world.state(true, 't1');
    world.source.setOpen(true);
    world.http.expectOne(LIST).flush(null, { status: 503, statusText: 'Unavailable' });
    await settle();

    expect(world.source.state()).toBe('failed');

    world.source.retry();
    expect(world.source.state()).toBe('loading');
    world.http.expectOne(LIST).flush({ items: [item('n-1')], unread: true, next: null });
    await settle();

    expect(world.source.state()).toBe('ready');
    expect(world.source.items()).toHaveLength(1);
  });

  it('asks nothing for nobody, and forgets what it held when the person signs out', async () => {
    const world = start(null);
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS * 2);
    world.http.expectNone(STATE);

    world.held.set(person());
    TestBed.tick();
    world.state(true, 't1');
    world.source.setOpen(true);
    world.http.expectOne(LIST).flush({ items: [item('n-1')], unread: true, next: 'c1' });
    await settle();

    world.held.set(null);
    TestBed.tick();

    expect(world.source.items()).toEqual([]);
    expect(world.source.unread()).toBe(false);
    expect(world.source.hasMore()).toBe(false);
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS * 2);
    world.http.expectNone(STATE);
    world.source.markAllRead();
    world.source.loadMore();
    world.source.retry();
    world.http.expectNone(() => true);
  });

  it('an answer meant for the person before is not shown to the next one', async () => {
    const world = start();
    world.state(false, 't1');
    world.source.setOpen(true);
    const slow = world.http.expectOne(LIST);

    world.held.set(person('a-2'));
    TestBed.tick();
    slow.flush({ items: [item('n-1')], unread: true, next: null });
    await settle();

    expect(world.source.items()).toEqual([]);
    expect(world.source.unread()).toBe(false);
    world.state(false);
  });

  it('asks nothing for a person who has yet to replace a temporary password', () => {
    const world = start(person('a-1', { mustChangePassword: true }));

    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS);
    world.http.expectNone(STATE);
  });

  it('with no connection it asks nothing and marks nothing, and asks again when it returns', async () => {
    const world = start();
    world.state(true, 't1');
    world.source.setOpen(true);
    const unread = item('n-1', { link: 'tickets/7' });
    world.http.expectOne(LIST).flush({ items: [unread], unread: true, next: 'c1' });
    await settle();

    world.online.set(false);
    TestBed.tick();
    vi.advanceTimersByTime(NOTIFICATIONS_POLL_MS * 2);
    world.source.markAllRead();
    world.source.open(unread);
    world.source.loadMore();
    world.source.retry();

    world.http.expectNone(() => true);
    expect(world.source.items()[0].read).toBe(false);
    expect(world.source.unread()).toBe(true);

    world.online.set(true);
    TestBed.tick();
    world.state(true, 't1');
    await settle();
    world.http.expectOne(LIST).flush({ items: [unread], unread: true, next: 'c1' });
  });
});
