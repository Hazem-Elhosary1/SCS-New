/**
 * Example pictures for the gallery's media, drawn here so nothing is fetched or kept as a file
 * (spec 095, T039): a few calm scenes in the design's colours. Development only; never delivered.
 */
const scene = (sky: string, hill: string, sun: string, label: string): string =>
  'data:image/svg+xml,' +
  encodeURIComponent(
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 280 160" preserveAspectRatio="xMidYMid slice"><title>${label}</title>` +
      `<rect width="280" height="160" fill="${sky}"/>` +
      `<circle cx="210" cy="48" r="20" fill="${sun}"/>` +
      `<path d="M0 120 Q70 70 140 110 T280 100 V160 H0Z" fill="${hill}"/>` +
      `<path d="M0 140 Q90 105 180 135 T280 128 V160 H0Z" fill="${hill}" opacity="0.7"/></svg>`,
  );

export const EXAMPLE_PICTURES = [
  scene('#efe6fe', '#7c3aed', '#f0b429', 'Purple hills'),
  scene('#e6f2f6', '#1d66b3', '#ffffff', 'Blue hills'),
  scene('#e5f2ec', '#116932', '#f0b429', 'Green hills'),
  scene('#fef1e1', '#b45309', '#ffffff', 'Amber hills'),
] as const;

/** A dark poster for the video player. */
export const EXAMPLE_POSTER =
  'data:image/svg+xml,' +
  encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 280 160"><rect width="280" height="160" fill="#1c1c21"/></svg>',
  );
