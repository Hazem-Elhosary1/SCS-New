/**
 * The icons the apps can show (spec 095, research R6, `contracts/icons.md`). Each is a drawing as
 * data — its box, its paint and its shapes — rendered by `scs-icon`. The type `ScsIconName` is the
 * set of keys, so a name that is not here does not compile.
 *
 * `from: 'drawn'` marks the icons drawn by hand before spec 095 (they follow the clinic file's Icon
 * Grid and were compared with the frames of specs 003–011). Every other entry is copied from the
 * design file's Icons page by `node tools/icons.mjs add`, which writes between the markers below.
 */

export type ScsIconShape =
  | { tag: 'path'; d: string; paint?: 'stroke' | 'fill'; rule?: 'evenodd'; strokeWidth?: number }
  | { tag: 'circle'; cx: number; cy: number; r: number; paint?: 'stroke' | 'fill'; strokeWidth?: number }
  | { tag: 'ellipse'; cx: number; cy: number; rx: number; ry: number; paint?: 'stroke' | 'fill'; strokeWidth?: number }
  | {
      tag: 'rect';
      x: number;
      y: number;
      width: number;
      height: number;
      rx?: number;
      ry?: number;
      paint?: 'stroke' | 'fill';
      strokeWidth?: number;
    }
  | { tag: 'line'; x1: number; y1: number; x2: number; y2: number; strokeWidth?: number }
  | { tag: 'polyline' | 'polygon'; points: string; paint?: 'stroke' | 'fill'; strokeWidth?: number };

export interface ScsIconDrawing {
  /** `drawn` (by hand, before spec 095) or the icon's name in the design file. */
  readonly from: string;
  readonly viewBox: string;
  /** How its shapes are painted unless a shape says otherwise. */
  readonly paint: 'stroke' | 'fill';
  readonly strokeWidth?: number;
  /**
   * An icon that points (an arrow, a chevron, "back", "next", sign-out) and the direction it is drawn
   * for: it is mirrored on a page of the other direction. Icons copied from the design file are drawn
   * left to right.
   */
  readonly points?: 'ltr' | 'rtl';
  readonly shapes: readonly ScsIconShape[];
}

export const SCS_ICONS = {
  'mail': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'rect', x: 2, y: 4, width: 20, height: 16, rx: 2 },
      { tag: 'path', d: 'm22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7' },
    ],
  },
  'lock': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'rect', x: 3, y: 11, width: 18, height: 11, rx: 2 },
      { tag: 'path', d: 'M7 11V7a5 5 0 0 1 10 0v4' },
    ],
  },
  'eye': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M2.06 12.35a1 1 0 0 1 0-.7 10.75 10.75 0 0 1 19.88 0 1 1 0 0 1 0 .7 10.75 10.75 0 0 1-19.88 0' },
      { tag: 'circle', cx: 12, cy: 12, r: 3 },
    ],
  },
  'eye-off': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M2.06 12.35a1 1 0 0 1 0-.7 10.75 10.75 0 0 1 19.88 0 1 1 0 0 1 0 .7 10.75 10.75 0 0 1-19.88 0' },
      { tag: 'circle', cx: 12, cy: 12, r: 3 },
      { tag: 'path', d: 'm3 3 18 18' },
    ],
  },
  'alert-triangle': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'm21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3' },
      { tag: 'path', d: 'M12 9v4' },
      { tag: 'path', d: 'M12 17h.01' },
    ],
  },
  'alert-circle': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'circle', cx: 12, cy: 12, r: 10 },
      { tag: 'path', d: 'M12 8v4' },
      { tag: 'path', d: 'M12 16h.01' },
    ],
  },
  'check-circle': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M21.8 10A10 10 0 1 1 17 3.34' },
      { tag: 'path', d: 'm9 11 3 3L22 4' },
    ],
  },
  'info': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'circle', cx: 12, cy: 12, r: 10 },
      { tag: 'path', d: 'M12 16v-4' },
      { tag: 'path', d: 'M12 8h.01' },
    ],
  },
  'help-circle': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'circle', cx: 12, cy: 12, r: 10 },
      { tag: 'path', d: 'M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3' },
      { tag: 'path', d: 'M12 17h.01' },
    ],
  },
  'circle': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'circle', cx: 12, cy: 12, r: 10 },
    ],
  },
  'refresh': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8' },
      { tag: 'path', d: 'M21 3v5h-5' },
      { tag: 'path', d: 'M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16' },
      { tag: 'path', d: 'M8 16H3v5' },
    ],
  },
  'phone': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M13.83 16.57a1 1 0 0 0 1.21-.3l.36-.47A2 2 0 0 1 17 15h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2A18 18 0 0 1 2 4a2 2 0 0 1 2-2h3a2 2 0 0 1 2 2v3a2 2 0 0 1-.8 1.6l-.47.35a1 1 0 0 0-.29 1.23 14 14 0 0 0 6.39 6.39' },
    ],
  },
  'chat': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M7.9 20A9 9 0 1 0 4 16.1L2 22Z' },
    ],
  },
  'close': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M18 6 6 18' },
      { tag: 'path', d: 'm6 6 12 12' },
    ],
  },
  'back': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    points: 'rtl',
    shapes: [
      { tag: 'path', d: 'M5 12h14' },
      { tag: 'path', d: 'm12 5 7 7-7 7' },
    ],
  },
  'home': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8' },
      { tag: 'path', d: 'M3 10a2 2 0 0 1 .709-1.528l7-5.999a2 2 0 0 1 2.582 0l7 5.999A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z' },
    ],
  },
  'calendar': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M8 2v4' },
      { tag: 'path', d: 'M16 2v4' },
      { tag: 'rect', width: 18, height: 18, x: 3, y: 4, rx: 2 },
      { tag: 'path', d: 'M3 10h18' },
    ],
  },
  'people': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2' },
      { tag: 'circle', cx: 9, cy: 7, r: 4 },
      { tag: 'path', d: 'M22 21v-2a4 4 0 0 0-3-3.87' },
      { tag: 'path', d: 'M16 3.13a4 4 0 0 1 0 7.75' },
    ],
  },
  'receipt': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1Z' },
      { tag: 'path', d: 'M14 8H8' },
      { tag: 'path', d: 'M16 12H8' },
      { tag: 'path', d: 'M13 16H8' },
    ],
  },
  'inbox': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M22 12h-6l-2 3h-4l-2-3H2' },
      { tag: 'path', d: 'M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z' },
    ],
  },
  'ticket': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z' },
      { tag: 'path', d: 'M13 5v2' },
      { tag: 'path', d: 'M13 17v2' },
      { tag: 'path', d: 'M13 11v2' },
    ],
  },
  'chart': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M3 3v16a2 2 0 0 0 2 2h16' },
      { tag: 'path', d: 'm19 9-5 5-4-4-3 3' },
    ],
  },
  'shield': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z' },
    ],
  },
  'database': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'ellipse', cx: 12, cy: 5, rx: 9, ry: 3 },
      { tag: 'path', d: 'M3 5v14a9 3 0 0 0 18 0V5' },
      { tag: 'path', d: 'M3 12a9 3 0 0 0 18 0' },
    ],
  },
  'settings': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z' },
      { tag: 'circle', cx: 12, cy: 12, r: 3 },
    ],
  },
  'building': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'rect', width: 16, height: 20, x: 4, y: 2, rx: 2 },
      { tag: 'path', d: 'M9 22v-4h6v4' },
      { tag: 'path', d: 'M8 6h.01' },
      { tag: 'path', d: 'M16 6h.01' },
      { tag: 'path', d: 'M12 6h.01' },
      { tag: 'path', d: 'M12 10h.01' },
      { tag: 'path', d: 'M12 14h.01' },
      { tag: 'path', d: 'M16 10h.01' },
      { tag: 'path', d: 'M16 14h.01' },
      { tag: 'path', d: 'M8 10h.01' },
      { tag: 'path', d: 'M8 14h.01' },
    ],
  },
  'layers': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83z' },
      { tag: 'path', d: 'M2 12a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 12' },
      { tag: 'path', d: 'M2 17a1 1 0 0 0 .58.91l8.6 3.91a2 2 0 0 0 1.65 0l8.58-3.9A1 1 0 0 0 22 17' },
    ],
  },
  'bell': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M10.268 21a2 2 0 0 0 3.464 0' },
      { tag: 'path', d: 'M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326' },
    ],
  },
  'chevron-down': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'm6 9 6 6 6-6' },
    ],
  },
  'chevron-start': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    points: 'ltr',
    shapes: [
      { tag: 'path', d: 'm15 18-6-6 6-6' },
    ],
  },
  'search': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'm21 21-4.34-4.34' },
      { tag: 'circle', cx: 11, cy: 11, r: 8 },
    ],
  },
  'filter': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M10 20a1 1 0 0 0 .553.895l2 1A1 1 0 0 0 14 21v-7a2 2 0 0 1 .517-1.341L21.74 4.67A1 1 0 0 0 21 3H3a1 1 0 0 0-.742 1.67l7.225 7.989A2 2 0 0 1 10 14z' },
    ],
  },
  'more': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'circle', cx: 12, cy: 12, r: 1 },
      { tag: 'circle', cx: 19, cy: 12, r: 1 },
      { tag: 'circle', cx: 5, cy: 12, r: 1 },
    ],
  },
  'sign-out': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    points: 'ltr',
    shapes: [
      { tag: 'path', d: 'm16 17 5-5-5-5' },
      { tag: 'path', d: 'M21 12H9' },
      { tag: 'path', d: 'M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4' },
    ],
  },
  'user': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2' },
      { tag: 'circle', cx: 12, cy: 7, r: 4 },
    ],
  },
  'key': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M2.586 17.414A2 2 0 0 0 2 18.828V21a1 1 0 0 0 1 1h3a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h1a1 1 0 0 0 1-1v-1a1 1 0 0 1 1-1h.172a2 2 0 0 0 1.414-.586l.814-.814a6.5 6.5 0 1 0-4-4z' },
      { tag: 'path', d: 'M16.5 7.5h.01' },
    ],
  },
  'card': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'rect', width: 20, height: 14, x: 2, y: 5, rx: 2 },
      { tag: 'path', d: 'M2 10h20' },
    ],
  },
  'check': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M20 6 9 17l-5-5' },
    ],
  },
  'plus': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M5 12h14' },
      { tag: 'path', d: 'M12 5v14' },
    ],
  },
  'pencil': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M12 20h9' },
      { tag: 'path', d: 'M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z' },
    ],
  },
  'trash': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M3 6h18' },
      { tag: 'path', d: 'M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6' },
      { tag: 'path', d: 'M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2' },
    ],
  },
  'stethoscope': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M11 2v2' },
      { tag: 'path', d: 'M5 2v2' },
      { tag: 'path', d: 'M5 3H4a2 2 0 0 0-2 2v4a6 6 0 0 0 12 0V5a2 2 0 0 0-2-2h-1' },
      { tag: 'path', d: 'M8 15a6 6 0 0 0 12 0v-3' },
      { tag: 'circle', cx: 20, cy: 10, r: 2 },
    ],
  },
  'shield-check': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z' },
      { tag: 'path', d: 'm9 12 2 2 4-4' },
    ],
  },
  'list': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M3 12h.01' },
      { tag: 'path', d: 'M3 18h.01' },
      { tag: 'path', d: 'M3 6h.01' },
      { tag: 'path', d: 'M8 12h13' },
      { tag: 'path', d: 'M8 18h13' },
      { tag: 'path', d: 'M8 6h13' },
    ],
  },
  'star': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z' },
    ],
  },
  'clipboard': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'rect', width: 8, height: 4, x: 8, y: 2, rx: 1, ry: 1 },
      { tag: 'path', d: 'M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2' },
    ],
  },
  'menu': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M4 6h16' },
      { tag: 'path', d: 'M4 12h16' },
      { tag: 'path', d: 'M4 18h16' },
    ],
  },
  'clock': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'circle', cx: 12, cy: 12, r: 10 },
      { tag: 'path', d: 'M12 6v6l4 2' },
    ],
  },
  'upload': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4' },
      { tag: 'path', d: 'm17 8-5-5-5 5' },
      { tag: 'path', d: 'M12 3v12' },
    ],
  },
  'download': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4' },
      { tag: 'path', d: 'm7 10 5 5 5-5' },
      { tag: 'path', d: 'M12 15V3' },
    ],
  },
  'image': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'rect', x: 3, y: 3, width: 18, height: 18, rx: 2 },
      { tag: 'circle', cx: 9, cy: 9, r: 2 },
      { tag: 'path', d: 'm21 15-3.09-3.09a2 2 0 0 0-2.82 0L6 21' },
    ],
  },
  'paperclip': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'm21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48' },
    ],
  },
  'file': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    strokeWidth: 1.75,
    shapes: [
      { tag: 'path', d: 'M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z' },
      { tag: 'path', d: 'M14 2v4a2 2 0 0 0 2 2h4' },
      { tag: 'path', d: 'M10 9H8' },
      { tag: 'path', d: 'M16 13H8' },
      { tag: 'path', d: 'M16 17H8' },
    ],
  },
  // ── Added from the design file by `node tools/icons.mjs add` — keep this marker ──
  'menu-more': {
    from: 'Menu/menu-icon/more',
    viewBox: '0 0 16 16',
    paint: 'fill',
    shapes: [
      { tag: 'path', d: 'M3.25 9.35C4 9.35 4.6 8.75 4.6 8C4.6 7.25 4 6.65 3.25 6.65C2.5 6.65 1.9 7.25 1.9 8C1.9 8.75 2.5 9.35 3.25 9.35Z' },
      { tag: 'path', d: 'M8 9.35C8.75 9.35 9.35 8.75 9.35 8C9.35 7.25 8.75 6.65 8 6.65C7.25 6.65 6.65 7.25 6.65 8C6.65 8.75 7.25 9.35 8 9.35Z' },
      { tag: 'path', d: 'M12.75 9.35C13.5 9.35 14.1 8.75 14.1 8C14.1 7.25 13.5 6.65 12.75 6.65C12 6.65 11.4 7.25 11.4 8C11.4 8.75 12 9.35 12.75 9.35Z' },
    ],
  },
  'menu-view': {
    from: 'Menu/menu-icon/view',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M1.75 8C3.25 5 5.4 3.5 8 3.5C10.6 3.5 12.75 5 14.25 8C12.75 11 10.6 12.5 8 12.5C5.4 12.5 3.25 11 1.75 8Z' },
      { tag: 'path', d: 'M8 10.1C9.16 10.1 10.1 9.16 10.1 8C10.1 6.84 9.16 5.9 8 5.9C6.84 5.9 5.9 6.84 5.9 8C5.9 9.16 6.84 10.1 8 10.1Z' },
    ],
  },
  'menu-edit': {
    from: 'Menu/menu-icon/edit',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M2.75 13.25L3.35 10.55L11.25 2.65C11.37 2.53 11.52 2.43 11.67 2.36C11.83 2.3 12 2.26 12.18 2.26C12.35 2.26 12.52 2.3 12.68 2.36C12.84 2.43 12.98 2.53 13.1 2.65L13.35 2.9C13.47 3.02 13.57 3.16 13.64 3.32C13.7 3.48 13.74 3.65 13.74 3.82C13.74 4 13.7 4.17 13.64 4.33C13.57 4.49 13.47 4.63 13.35 4.75L5.45 12.65L2.75 13.25Z' },
      { tag: 'path', d: 'M9.6 4.3L11.7 6.4' },
    ],
  },
  'menu-renew': {
    from: 'Menu/menu-icon/renew',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M2.75 8C2.75 6.91 3.1 5.84 3.74 4.95C4.37 4.06 5.27 3.4 6.31 3.05C7.34 2.69 8.46 2.67 9.51 2.99C10.56 3.3 11.48 3.94 12.15 4.8' },
      { tag: 'path', d: 'M12.75 2.25V5.25H9.75' },
      { tag: 'path', d: 'M13.25 8C13.25 9.09 12.9 10.16 12.26 11.05C11.63 11.94 10.73 12.6 9.69 12.95C8.66 13.31 7.54 13.33 6.49 13.01C5.44 12.7 4.52 12.06 3.85 11.2' },
      { tag: 'path', d: 'M3.25 13.75V10.75H6.25' },
    ],
  },
  'menu-suspend': {
    from: 'Menu/menu-icon/suspend',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M5.5 2.75H4.75C4.2 2.75 3.75 3.2 3.75 3.75V12.25C3.75 12.8 4.2 13.25 4.75 13.25H5.5C6.05 13.25 6.5 12.8 6.5 12.25V3.75C6.5 3.2 6.05 2.75 5.5 2.75Z' },
      { tag: 'path', d: 'M11.25 2.75H10.5C9.95 2.75 9.5 3.2 9.5 3.75V12.25C9.5 12.8 9.95 13.25 10.5 13.25H11.25C11.8 13.25 12.25 12.8 12.25 12.25V3.75C12.25 3.2 11.8 2.75 11.25 2.75Z' },
    ],
  },
  'menu-delete': {
    from: 'Menu/menu-icon/delete',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M2.75 4.25H13.25' },
      { tag: 'path', d: 'M6.25 4.25V3C6.25 2.8 6.33 2.61 6.47 2.47C6.61 2.33 6.8 2.25 7 2.25H9C9.2 2.25 9.39 2.33 9.53 2.47C9.67 2.61 9.75 2.8 9.75 3V4.25' },
      { tag: 'path', d: 'M4 4.25L4.65 12.65C4.69 12.95 4.83 13.24 5.07 13.44C5.3 13.64 5.59 13.75 5.9 13.75H10.1C10.41 13.75 10.7 13.64 10.93 13.44C11.17 13.24 11.31 12.95 11.35 12.65L12 4.25' },
      { tag: 'path', d: 'M6.75 6.75V11.25M9.25 6.75V11.25' },
    ],
  },
  'menu-disable': {
    from: 'Menu/menu-icon/disable',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M8 14.25C11.45 14.25 14.25 11.45 14.25 8C14.25 4.55 11.45 1.75 8 1.75C4.55 1.75 1.75 4.55 1.75 8C1.75 11.45 4.55 14.25 8 14.25Z' },
      { tag: 'path', d: 'M3.6 3.6L12.4 12.4' },
    ],
  },
  'menu-enable': {
    from: 'Menu/menu-icon/enable',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M8 14.25C11.45 14.25 14.25 11.45 14.25 8C14.25 4.55 11.45 1.75 8 1.75C4.55 1.75 1.75 4.55 1.75 8C1.75 11.45 4.55 14.25 8 14.25Z' },
      { tag: 'path', d: 'M5.4 8.15L7.15 9.9L10.6 6.3' },
    ],
  },
  'menu-copy': {
    from: 'Menu/menu-icon/copy',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M12 5.5H7C6.17 5.5 5.5 6.17 5.5 7V12C5.5 12.83 6.17 13.5 7 13.5H12C12.83 13.5 13.5 12.83 13.5 12V7C13.5 6.17 12.83 5.5 12 5.5Z' },
      { tag: 'path', d: 'M10.5 5.5V4C10.5 3.6 10.34 3.22 10.06 2.94C9.78 2.66 9.4 2.5 9 2.5H4C3.6 2.5 3.22 2.66 2.94 2.94C2.66 3.22 2.5 3.6 2.5 4V9C2.5 9.4 2.66 9.78 2.94 10.06C3.22 10.34 3.6 10.5 4 10.5H5.5' },
    ],
  },
  'menu-send': {
    from: 'Menu/menu-icon/send',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    points: 'ltr',
    shapes: [
      { tag: 'path', d: 'M13.75 2.25L7 9' },
      { tag: 'path', d: 'M13.75 2.25L9.5 13.75L7 9L2.25 6.5L13.75 2.25Z' },
    ],
  },
  'menu-invoice': {
    from: 'Menu/menu-icon/invoice',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M3.5 13.75V2.25H12.5V13.75L11 12.65L9.5 13.75L8 12.65L6.5 13.75L5 12.65L3.5 13.75Z' },
      { tag: 'path', d: 'M6 5.5H10M6 8H10M6 10.5H8' },
    ],
  },
  'menu-print': {
    from: 'Menu/menu-icon/print',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M4.5 5.75V2.5H11.5V5.75' },
      { tag: 'path', d: 'M4.5 11.5H3.25C2.98 11.5 2.73 11.39 2.54 11.21C2.36 11.02 2.25 10.77 2.25 10.5V6.75C2.25 6.48 2.36 6.23 2.54 6.04C2.73 5.86 2.98 5.75 3.25 5.75H12.75C13.02 5.75 13.27 5.86 13.46 6.04C13.64 6.23 13.75 6.48 13.75 6.75V10.5C13.75 10.77 13.64 11.02 13.46 11.21C13.27 11.39 13.02 11.5 12.75 11.5H11.5' },
      { tag: 'path', d: 'M4.5 9.25H11.5V13.5H4.5V9.25Z' },
    ],
  },
  'menu-void': {
    from: 'Menu/menu-icon/void',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M3.5 13.75V2.25H12.5V13.75L11 12.65L9.5 13.75L8 12.65L6.5 13.75L5 12.65L3.5 13.75Z' },
      { tag: 'path', d: 'M6.5 5.75L9.5 8.75M9.5 5.75L6.5 8.75' },
    ],
  },
  'menu-refund': {
    from: 'Menu/menu-icon/refund',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    points: 'ltr',
    shapes: [
      { tag: 'path', d: 'M5.5 3L2.5 6L5.5 9' },
      { tag: 'path', d: 'M2.5 6H10C10.93 6 11.82 6.37 12.47 7.03C13.13 7.68 13.5 8.57 13.5 9.5C13.5 10.43 13.13 11.32 12.47 11.97C11.82 12.63 10.93 13 10 13H6' },
    ],
  },
  'menu-configure': {
    from: 'Menu/menu-icon/configure',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M2.5 5H7.75M11.25 5H13.5M2.5 11H4.75M8.25 11H13.5' },
      { tag: 'path', d: 'M9.5 6.75C10.47 6.75 11.25 5.97 11.25 5C11.25 4.03 10.47 3.25 9.5 3.25C8.53 3.25 7.75 4.03 7.75 5C7.75 5.97 8.53 6.75 9.5 6.75Z' },
      { tag: 'path', d: 'M6.5 12.75C7.47 12.75 8.25 11.97 8.25 11C8.25 10.03 7.47 9.25 6.5 9.25C5.53 9.25 4.75 10.03 4.75 11C4.75 11.97 5.53 12.75 6.5 12.75Z' },
    ],
  },
  'menu-key': {
    from: 'Menu/menu-icon/key',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'circle', cx: 5.5, cy: 10.4, r: 2.6 },
      { tag: 'path', d: 'M8.1 7.9L14 2' },
      { tag: 'path', d: 'M11.2 4.8L12.9 6.5' },
    ],
  },
  'menu-unlock': {
    from: 'Menu/menu-icon/unlock',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'rect', x: 3.9, y: 8.1, width: 8.2, height: 5, rx: 0.9 },
      { tag: 'path', d: 'M6 7.4V5.2C6 3.5 7.35 2.2 9 2.2C10.3 2.2 11.4 3 11.8 4.2' },
      { tag: 'path', d: 'M8 9.9V11.7' },
    ],
  },
  'menu-audit': {
    from: 'Menu/menu-icon/audit',
    viewBox: '0 0 16 16',
    paint: 'stroke',
    strokeWidth: 1.4,
    shapes: [
      { tag: 'path', d: 'M1.14 14.86H5.14' },
      { tag: 'path', d: 'M1.14 10.29H5.14' },
      { tag: 'path', d: 'M1.14 1.14H14.86' },
      { tag: 'path', d: 'M14.86 5.71H1.14' },
      { tag: 'path', d: 'M14.86 9.14L10.29 14.86L8 13.14' },
    ],
  },
  'play': {
    from: 'Entertainment/button-play',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    shapes: [
      { tag: 'path', d: 'M1.44 12.33C1.44 12.54 1.5 12.74 1.61 12.92C1.72 13.09 1.88 13.24 2.07 13.33C2.25 13.44 2.46 13.49 2.68 13.49C2.89 13.49 3.1 13.44 3.29 13.33L11.94 7.98C12.12 7.89 12.28 7.75 12.39 7.57C12.51 7.39 12.56 7.19 12.56 6.98C12.56 6.77 12.51 6.57 12.39 6.39C12.28 6.21 12.12 6.07 11.94 5.98L3.29 0.67C3.1 0.56 2.89 0.51 2.68 0.51C2.46 0.51 2.25 0.56 2.07 0.67C1.88 0.76 1.72 0.91 1.61 1.08C1.5 1.26 1.44 1.46 1.44 1.67V12.33Z' },
    ],
  },
  'pause': {
    from: 'Entertainment/button-pause-2',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    shapes: [
      { tag: 'path', d: 'M4 0.5H1.5C0.95 0.5 0.5 0.95 0.5 1.5V12.5C0.5 13.05 0.95 13.5 1.5 13.5H4C4.55 13.5 5 13.05 5 12.5V1.5C5 0.95 4.55 0.5 4 0.5Z' },
      { tag: 'path', d: 'M12.5 0.5H10C9.45 0.5 9 0.95 9 1.5V12.5C9 13.05 9.45 13.5 10 13.5H12.5C13.05 13.5 13.5 13.05 13.5 12.5V1.5C13.5 0.95 13.05 0.5 12.5 0.5Z' },
    ],
  },
  'sparkle': {
    from: 'Interface-Essential/magic-wand-2',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    shapes: [
      { tag: 'path', d: 'M12.64 1.87L11.8 4.35C11.77 4.41 11.76 4.47 11.76 4.53C11.76 4.6 11.77 4.66 11.8 4.72L13.37 6.82C13.42 6.88 13.44 6.95 13.45 7.03C13.46 7.1 13.44 7.18 13.41 7.24C13.37 7.31 13.32 7.37 13.25 7.4C13.19 7.44 13.12 7.46 13.04 7.46H10.42C10.36 7.46 10.29 7.48 10.24 7.51C10.18 7.54 10.13 7.58 10.09 7.63L8.63 9.73C8.59 9.79 8.53 9.84 8.46 9.87C8.39 9.9 8.31 9.91 8.24 9.9C8.17 9.88 8.1 9.85 8.04 9.8C7.98 9.76 7.94 9.69 7.92 9.62L7.14 7.12C7.12 7.06 7.09 7 7.04 6.96C7 6.91 6.94 6.88 6.88 6.86L4.38 6.08C4.31 6.06 4.24 6.02 4.2 5.96C4.15 5.9 4.12 5.83 4.1 5.76C4.09 5.69 4.1 5.61 4.13 5.54C4.16 5.47 4.21 5.41 4.27 5.37L6.41 3.86C6.46 3.82 6.5 3.77 6.53 3.71C6.56 3.66 6.58 3.59 6.58 3.53V0.91C6.58 0.84 6.6 0.77 6.64 0.72C6.68 0.66 6.73 0.61 6.79 0.58C6.85 0.54 6.92 0.53 6.98 0.53C7.05 0.53 7.12 0.55 7.18 0.58L9.28 2.15C9.33 2.19 9.39 2.21 9.46 2.22C9.52 2.23 9.59 2.22 9.65 2.2L12.13 1.36C12.2 1.33 12.28 1.33 12.35 1.35C12.42 1.36 12.49 1.4 12.55 1.45C12.6 1.51 12.64 1.58 12.65 1.65C12.67 1.72 12.67 1.8 12.64 1.87Z' },
      { tag: 'path', d: 'M7.04 6.96L0.5 13.5' },
    ],
  },
  'send': {
    from: 'Mail/send',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    points: 'rtl',
    shapes: [
      { tag: 'path', d: 'M11.29 3.86L1.59 0.85C1.38 0.79 1.15 0.85 1 1C0.85 1.15 0.79 1.38 0.85 1.59L3.86 11.29C3.94 11.56 4.19 11.75 4.48 11.75C4.78 11.75 5.05 11.54 5.12 11.24L6.11 6.86C6.2 6.49 6.49 6.2 6.86 6.11L11.24 5.12C11.54 5.05 11.75 4.78 11.75 4.48C11.75 4.19 11.56 3.94 11.29 3.86Z' },
      { tag: 'path', d: 'M8.5 8.5L13 13' },
      { tag: 'path', d: 'M11.25 7.25L13 9' },
      { tag: 'path', d: 'M7.25 11.25L9 13' },
    ],
  },
  'wifi-off': {
    from: 'Computer-Devices/wifi-disabled',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    shapes: [
      { tag: 'path', d: 'M0.5 0.5L13.5 13.5' },
      { tag: 'path', d: 'M7 13.25C7.72 13.25 8.31 12.66 8.31 11.94C8.31 11.22 7.72 10.63 7 10.63C6.28 10.63 5.69 11.22 5.69 11.94C5.69 12.66 6.28 13.25 7 13.25Z' },
      { tag: 'path', d: 'M4.53 9C4.86 8.67 5.24 8.4 5.67 8.22' },
      { tag: 'path', d: 'M2.36 7.31C2.97 6.7 3.62 6.32 3.62 6.32' },
      { tag: 'path', d: 'M5.55 5.54C5.55 5.54 6.14 5.38 7.01 5.38C7.87 5.38 8.72 5.55 9.52 5.88C10.32 6.21 11.04 6.7 11.65 7.31' },
      { tag: 'path', d: 'M3.48 3.46C4.6 2.99 5.79 2.76 7 2.76C8.21 2.76 9.4 2.99 10.52 3.46C11.63 3.92 12.65 4.6 13.5 5.45' },
      { tag: 'path', d: 'M0.5 5.45C0.86 5.09 1.23 4.79 1.68 4.46' },
    ],
  },
  'dashboard': {
    from: 'Interface-Essential/dashboard-3',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    shapes: [
      { tag: 'path', d: 'M13 6.5H9C8.72 6.5 8.5 6.72 8.5 7V13C8.5 13.28 8.72 13.5 9 13.5H13C13.28 13.5 13.5 13.28 13.5 13V7C13.5 6.72 13.28 6.5 13 6.5Z' },
      { tag: 'path', d: 'M13 0.5H9C8.72 0.5 8.5 0.72 8.5 1V3.01C8.5 3.29 8.72 3.51 9 3.51H13C13.28 3.51 13.5 3.29 13.5 3.01V1C13.5 0.72 13.28 0.5 13 0.5Z' },
      { tag: 'path', d: 'M5 0.5H1C0.72 0.5 0.5 0.72 0.5 1V7C0.5 7.28 0.72 7.5 1 7.5H5C5.28 7.5 5.5 7.28 5.5 7V1C5.5 0.72 5.28 0.5 5 0.5Z' },
      { tag: 'path', d: 'M5 10.49H1C0.72 10.49 0.5 10.71 0.5 10.99V13C0.5 13.28 0.72 13.5 1 13.5H5C5.28 13.5 5.5 13.28 5.5 13V10.99C5.5 10.71 5.28 10.49 5 10.49Z' },
    ],
  },
  'gem': {
    from: 'fi-rr-gem',
    viewBox: '0 0 24 24',
    paint: 'fill',
    shapes: [
      { tag: 'path', d: 'M23.15 9.47L22.36 8.4C21.81 7.66 21.1 7.05 20.28 6.64C19.46 6.22 18.55 6 17.63 6H6.38C5.48 6 4.59 6.21 3.78 6.61C2.97 7.01 2.27 7.59 1.72 8.31L0.9 9.38C0.23 10.26 -0.08 11.36 0.02 12.46C0.12 13.56 0.63 14.58 1.45 15.32L9.44 23.03C10.14 23.65 11.05 24 11.99 24C12.95 23.99 13.87 23.64 14.59 23L22.52 15.36C23.34 14.64 23.86 13.63 23.98 12.54C24.09 11.45 23.8 10.35 23.15 9.47ZM20.75 9.58L21.53 10.65C21.61 10.76 21.68 10.88 21.74 11H16.95C16.88 10.52 16.75 10.05 16.58 9.6L15.94 8H17.63C18.24 8 18.84 8.14 19.38 8.42C19.92 8.69 20.39 9.09 20.75 9.58ZM11.99 20.09L9.22 13.82C9.12 13.55 9.05 13.28 8.99 13H14.95C14.9 13.22 14.85 13.44 14.77 13.65L11.99 20.09ZM9.1 11C9.14 10.82 9.19 10.64 9.26 10.47L10.24 8H13.78L14.72 10.34C14.8 10.56 14.87 10.78 14.92 11H9.1ZM3.31 9.52C3.67 9.05 4.14 8.67 4.67 8.4C5.2 8.14 5.79 8 6.38 8H8.08L7.4 9.73C7.24 10.14 7.11 10.57 7.03 11H2.24C2.31 10.86 2.4 10.72 2.49 10.6L3.31 9.52ZM2.81 13.85C2.55 13.62 2.35 13.33 2.21 13H6.98C7.05 13.54 7.18 14.07 7.37 14.58L10.2 20.98L2.81 13.85ZM13.77 21.01L16.62 14.4C16.79 13.95 16.91 13.48 16.97 13H21.8C21.66 13.34 21.44 13.65 21.17 13.9L13.77 21.01Z' },
      { tag: 'path', d: 'M12 4C12.27 4 12.52 3.89 12.71 3.71C12.89 3.52 13 3.27 13 3V1C13 0.73 12.89 0.48 12.71 0.29C12.52 0.11 12.27 0 12 0C11.73 0 11.48 0.11 11.29 0.29C11.11 0.48 11 0.73 11 1V3C11 3.27 11.11 3.52 11.29 3.71C11.48 3.89 11.73 4 12 4Z' },
      { tag: 'path', d: 'M16.55 3.9C16.67 3.96 16.8 3.99 16.93 4C17.06 4.01 17.2 4 17.32 3.95C17.45 3.91 17.56 3.85 17.66 3.76C17.76 3.67 17.84 3.57 17.9 3.45L18.9 1.45C19.02 1.21 19.04 0.93 18.95 0.68C18.87 0.43 18.69 0.22 18.45 0.1C18.21 -0.01 17.94 -0.03 17.69 0.05C17.44 0.13 17.23 0.32 17.11 0.55L16.11 2.55C16.05 2.67 16.01 2.8 16 2.93C15.99 3.06 16.01 3.19 16.05 3.32C16.09 3.44 16.16 3.56 16.24 3.66C16.33 3.76 16.43 3.84 16.55 3.9Z' },
      { tag: 'path', d: 'M6.11 3.45C6.22 3.68 6.43 3.87 6.68 3.95C6.94 4.04 7.21 4.02 7.45 3.9C7.69 3.78 7.87 3.57 7.95 3.32C8.04 3.07 8.02 2.79 7.9 2.55L6.9 0.55C6.84 0.44 6.76 0.33 6.66 0.24C6.56 0.16 6.45 0.09 6.32 0.05C6.07 -0.03 5.8 -0.01 5.56 0.1C5.32 0.22 5.14 0.43 5.06 0.68C4.97 0.93 4.99 1.21 5.11 1.45L6.11 3.45Z' },
    ],
  },
  'barrier': {
    from: 'Interface-Essential/under-construction',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    shapes: [
      { tag: 'path', d: 'M0.5 4C0.5 3.45 0.95 3 1.5 3H12.5C13.05 3 13.5 3.45 13.5 4V8C13.5 8.55 13.05 9 12.5 9H1.5C0.95 9 0.5 8.55 0.5 8V4Z' },
      { tag: 'path', d: 'M3.5 1.5V3M3.5 12.5V9' },
      { tag: 'path', d: 'M10.5 1.5V3M10.5 12.5V9' },
      { tag: 'path', d: 'M0.5 5L4.5 9' },
      { tag: 'path', d: 'M2.25 3L8.25 9' },
      { tag: 'path', d: 'M6 3L12 9' },
      { tag: 'path', d: 'M9.75 3L13.5 6.75' },
    ],
  },
  'tag': {
    from: 'Interface-Essential/tag',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    shapes: [
      { tag: 'path', d: 'M0.72 9.39L4.61 13.28C4.75 13.42 4.94 13.5 5.14 13.5C5.34 13.5 5.53 13.42 5.67 13.28L13.39 5.56C13.43 5.52 13.46 5.48 13.48 5.43C13.5 5.38 13.5 5.32 13.5 5.27L12.91 1.44C12.9 1.35 12.87 1.26 12.8 1.2C12.74 1.13 12.65 1.1 12.56 1.09L8.73 0.5C8.68 0.5 8.62 0.5 8.57 0.52C8.52 0.54 8.48 0.57 8.44 0.61L0.72 8.33C0.58 8.47 0.5 8.66 0.5 8.86C0.5 9.06 0.58 9.25 0.72 9.39Z' },
      { tag: 'path', d: 'M9.89 4.61C9.61 4.61 9.39 4.39 9.39 4.11C9.39 3.83 9.61 3.61 9.89 3.61C10.17 3.61 10.39 3.83 10.39 4.11C10.39 4.39 10.17 4.61 9.89 4.61Z' },
    ],
  },
  'check-square': {
    from: 'Interface-Essential/check-square',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    shapes: [
      { tag: 'path', d: 'M10.5 0.5H3.5C1.84 0.5 0.5 1.84 0.5 3.5V10.5C0.5 12.16 1.84 13.5 3.5 13.5H10.5C12.16 13.5 13.5 12.16 13.5 10.5V3.5C13.5 1.84 12.16 0.5 10.5 0.5Z' },
      { tag: 'path', d: 'M9.92 4.75L5.92 9.75L3.92 8.25' },
    ],
  },
  // ── End of added icons ──
} as const satisfies Record<string, ScsIconDrawing>;

export type ScsIconName = keyof typeof SCS_ICONS;
