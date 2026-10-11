import { Type } from '@angular/core';
import { ScsIconName } from 'ui';

/**
 * Every place the console's frame leads to, once (spec 006, FR-003 and FR-005; BRD Chapter 01
 * §A-2). The side bar is drawn from this list, the routes are generated from it, and the tests
 * walk it — so the three cannot disagree. The order here is the order everyone sees.
 */
export interface Destination {
  /** Stable name; a later spec registers its page under it. */
  readonly key: string;
  /** The address, without the leading slash; empty for the home page. */
  readonly path: string;
  /** Translation key of its name. */
  readonly labelKey: string;
  /** The view permission that opens it, from the catalogue; null when it needs none. */
  readonly permission: string | null;
  /** The parent it sits under in the side bar, if any. */
  readonly parent?: 'reports' | 'administration' | 'data';
  /** Every destination has one, a parent's child too (design: Sidebar, sub-item). */
  readonly icon: ScsIconName;
  /** Shown only in a clinic of this kind. */
  readonly needs?: 'dental';
  /** Where it is reached from. */
  readonly place: 'side-bar' | 'account-menu';
}

export interface NavParent {
  readonly key: 'reports' | 'administration' | 'data';
  readonly path: string;
  readonly labelKey: string;
  readonly icon: ScsIconName;
}

export type NavGroup = 'main' | 'management';

export const PARENTS: readonly NavParent[] = [
  { key: 'reports', path: 'reports', labelKey: 'app.nav.reports', icon: 'chart' },
  { key: 'administration', path: 'administration', labelKey: 'app.nav.administration', icon: 'shield' },
  { key: 'data', path: 'data', labelKey: 'app.nav.data', icon: 'database' },
];

export const DESTINATIONS: readonly Destination[] = [
  { key: 'home', path: '', labelKey: 'app.nav.home', permission: null, icon: 'home', place: 'side-bar' },
  { key: 'bookings', path: 'bookings', labelKey: 'app.nav.bookings', permission: 'bookings.view', icon: 'calendar', place: 'side-bar' },
  { key: 'patients', path: 'patients', labelKey: 'app.nav.patients', permission: 'patients.view', icon: 'people', place: 'side-bar' },
  { key: 'invoices', path: 'invoices', labelKey: 'app.nav.invoices', permission: 'money.view', icon: 'receipt', place: 'side-bar' },
  { key: 'inbox', path: 'inbox', labelKey: 'app.nav.inbox', permission: 'inbox.view', icon: 'inbox', place: 'side-bar' },
  { key: 'tickets', path: 'tickets', labelKey: 'app.nav.tickets', permission: 'tickets.view', icon: 'ticket', place: 'side-bar' },
  { key: 'reports.financial', path: 'reports/financial', labelKey: 'app.nav.reportsFinancial', permission: 'reports.financial', parent: 'reports', icon: 'card', place: 'side-bar' },
  { key: 'reports.clinical', path: 'reports/clinical', labelKey: 'app.nav.reportsClinical', permission: 'reports.clinical', parent: 'reports', icon: 'stethoscope', place: 'side-bar' },
  { key: 'reports.activity', path: 'reports/activity', labelKey: 'app.nav.reportsActivity', permission: 'reports.activity', parent: 'reports', icon: 'shield-check', place: 'side-bar' },
  { key: 'reports.weekly', path: 'reports/weekly', labelKey: 'app.nav.reportsWeekly', permission: 'reports.weekly', parent: 'reports', icon: 'list', place: 'side-bar' },
  { key: 'reports.feedback', path: 'reports/feedback', labelKey: 'app.nav.reportsFeedback', permission: 'reports.feedback', parent: 'reports', icon: 'star', place: 'side-bar' },
  { key: 'administration.users', path: 'administration/users', labelKey: 'app.nav.users', permission: 'admin.users.view', parent: 'administration', icon: 'people', place: 'side-bar' },
  { key: 'administration.roles', path: 'administration/roles', labelKey: 'app.nav.roles', permission: 'admin.roles.view', parent: 'administration', icon: 'key', place: 'side-bar' },
  { key: 'administration.licences', path: 'administration/licences', labelKey: 'app.nav.licences', permission: 'admin.licences.view', parent: 'administration', icon: 'shield-check', place: 'side-bar' },
  { key: 'data.doctors', path: 'data/doctors', labelKey: 'app.nav.doctors', permission: 'data.doctors.view', parent: 'data', icon: 'stethoscope', place: 'side-bar' },
  { key: 'data.branches', path: 'data/branches', labelKey: 'app.nav.branches', permission: 'data.branches.view', parent: 'data', icon: 'building', place: 'side-bar' },
  { key: 'data.services', path: 'data/services', labelKey: 'app.nav.services', permission: 'data.services.view', parent: 'data', icon: 'clipboard', place: 'side-bar' },
  { key: 'data.consents', path: 'data/consents', labelKey: 'app.nav.consents', permission: 'data.consents.view', parent: 'data', icon: 'file', place: 'side-bar' },
  { key: 'settings', path: 'settings', labelKey: 'app.nav.settings', permission: 'settings.view', icon: 'settings', place: 'side-bar' },
  { key: 'dental-lab', path: 'dental-lab', labelKey: 'app.nav.dentalLab', permission: 'patients.lab.view', icon: 'layers', needs: 'dental', place: 'side-bar' },
  { key: 'account.subscription', path: 'account/subscription', labelKey: 'app.nav.subscription', permission: null, icon: 'user', place: 'account-menu' },
  { key: 'account.doctor-profile', path: 'account/doctor-profile', labelKey: 'app.nav.doctorProfile', permission: null, icon: 'stethoscope', place: 'account-menu' },
];

/** The two labelled groups of the side bar, and what stands in each, in order. */
export const NAV: readonly { group: NavGroup; labelKey: string; entries: readonly string[] }[] = [
  {
    group: 'main',
    labelKey: 'app.nav.group.main',
    entries: ['home', 'bookings', 'patients', 'invoices', 'inbox', 'tickets', 'reports'],
  },
  {
    group: 'management',
    labelKey: 'app.nav.group.management',
    entries: ['administration', 'data', 'settings', 'dental-lab'],
  },
];

export function destination(key: string): Destination {
  const found = DESTINATIONS.find((candidate) => candidate.key === key);
  if (found === undefined) {
    throw new Error(`No destination is called "${key}".`);
  }
  return found;
}

export function childrenOf(parent: string): readonly Destination[] {
  return DESTINATIONS.filter((candidate) => candidate.parent === parent);
}

/** How a destination's page is loaded: it is downloaded when the destination is first opened. */
export type PageLoader = () => Promise<Type<unknown>>;

const PAGES = new Map<string, PageLoader>();

/**
 * Gives a destination its page, by saying how to load it. A later spec adds one line for its
 * destination's key in shell.routes.ts; nothing in the frame changes (SC-008), and the frame's
 * own download does not grow (spec 007, FR-041). Until a destination has a page, the frame
 * shows "not available yet" for it.
 */
export function registerDestinationPage(key: string, load: PageLoader): void {
  destination(key);
  PAGES.set(key, load);
}

export function pageOf(key: string): PageLoader | undefined {
  return PAGES.get(key);
}
