import { ScsIconName } from 'ui';

/** One section of the console in the side bar. */
export interface OpsSection {
  /** Names its words: `app.shell.nav.<key>`. */
  readonly key: string;
  readonly path: string;
  readonly icon: ScsIconName;
}

/**
 * The sections of the operators console that exist (spec 010, FR-049). A spec that builds a
 * section adds its line here, with its route, and nothing of the frame changes. The design's
 * side bar lists eight; each appears when it is built.
 */
export const OPS_SECTIONS: readonly OpsSection[] = [{ key: 'home', path: '/', icon: 'home' }];
