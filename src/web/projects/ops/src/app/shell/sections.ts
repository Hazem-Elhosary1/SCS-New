import { ScsIconName } from 'ui';

/** One section of the console in the side bar. */
export interface OpsSection {
  /** Names its words: `app.shell.nav.<key>`. */
  readonly key: string;
  readonly path: string;
  readonly icon: ScsIconName;
  /** The permission that shows it (spec 011, FR-006); none while a section is open to every operator. */
  readonly permission?: string;
}

/**
 * The sections of the operators console that exist (spec 010, FR-049). A spec that builds a
 * section adds its line here, with its route, and nothing of the frame changes. The design's
 * side bar lists eight; each appears when it is built.
 */
export const OPS_SECTIONS: readonly OpsSection[] = [
  // Open to every operator until the dashboard is built (spec 022), which puts it behind its own permission.
  { key: 'home', path: '/', icon: 'home' },
  { key: 'operators', path: '/operators', icon: 'people', permission: 'operators.view' },
];
