import { Provider, computed, inject, signal } from '@angular/core';
import {
  BranchChoice,
  BranchesSource,
  ClinicTraitsSource,
  InboxCountSource,
  NotificationItem,
  NotificationsSource,
  PlanSource,
  PlatformNotifications,
  RuntimeConfigService,
  SubscriptionNotice,
  SubscriptionSource,
} from 'core';

/**
 * What a browser test, or a person building the product, puts on `window.__scsSample` before
 * the console starts, to see the parts of the frame whose data belongs to a later spec
 * (spec 006, research R5). Outside production only: in production the name is never read.
 */
export interface ShellSample {
  notifications?: NotificationItem[];
  notificationsState?: 'ready' | 'loading' | 'failed';
  branches?: BranchChoice[];
  multiBranch?: boolean;
  subscription?: SubscriptionNotice;
  outsidePlan?: string[];
  unread?: number;
  dental?: boolean;
  linkedToDoctor?: boolean;
}

/** What the sample sources were asked to do, for a test to read back. */
interface SampleLog {
  calls: string[];
}

function sample(): (ShellSample & SampleLog) | null {
  if (inject(RuntimeConfigService).value.environment === 'production') {
    return null;
  }
  const given = (globalThis as { __scsSample?: ShellSample & Partial<SampleLog> }).__scsSample;
  if (given === undefined) {
    return null;
  }
  given.calls ??= [];
  return given as ShellSample & SampleLog;
}

function sampleNotifications(given: ShellSample & SampleLog): NotificationsSource {
  const items = signal<readonly NotificationItem[]>(given.notifications ?? []);
  const state = signal(given.notificationsState ?? 'ready');
  const read = (item: NotificationItem) => ({ ...item, read: true });
  return {
    items: items.asReadonly(),
    state: state.asReadonly(),
    unread: computed(() => items().some((item) => !item.read)),
    hasMore: () => false,
    setOpen: () => undefined,
    loadMore: () => undefined,
    markAllRead: () => {
      given.calls.push('markAllRead');
      items.update((all) => all.map(read));
    },
    open: (item) => {
      given.calls.push(`open ${item.id}`);
      items.update((all) => all.map((each) => (each.id === item.id ? read(each) : each)));
    },
    retry: () => {
      given.calls.push('retry');
      state.set('ready');
    },
  };
}

function sampleBranches(given: ShellSample & SampleLog): BranchesSource {
  const choice = signal('all');
  const branches = given.branches ?? [];
  return {
    branches: signal(branches).asReadonly(),
    multiBranch: signal(given.multiBranch ?? branches.length > 0).asReadonly(),
    choice: choice.asReadonly(),
    choose: (chosen) => {
      given.calls.push(`choose ${chosen}`);
      choice.set(chosen);
    },
  };
}

function sampled<T>(source: new () => T, make: (given: ShellSample & SampleLog) => T): Provider {
  return {
    provide: source,
    useFactory: () => {
      const given = sample();
      return given === null ? new source() : make(given);
    },
  };
}

/** The bell's source is the platform's unless a sample was given. */
function sampledBell(): Provider {
  return {
    provide: NotificationsSource,
    useFactory: () => {
      const given = sample();
      return given === null ? inject(PlatformNotifications) : sampleNotifications(given);
    },
  };
}

/** The frame's sources, answering from `window.__scsSample` when it is there. */
export function provideSampleSources(): Provider[] {
  return [
    sampledBell(),
    sampled(BranchesSource, sampleBranches),
    sampled(SubscriptionSource, (given) => ({ notice: signal(given.subscription ?? null) })),
    sampled(PlanSource, (given) => ({
      excludes: (key: string) => (given.outsidePlan ?? []).includes(key),
    })),
    sampled(InboxCountSource, (given) => ({ unread: signal(given.unread ?? null) })),
    sampled(ClinicTraitsSource, (given) => ({
      dental: signal(given.dental ?? false),
      linkedToDoctor: signal(given.linkedToDoctor ?? false),
    })),
  ];
}
