// The design components' gallery exists in development only (spec 095, FR-015, SC-006): this check
// fails when any file of the operators console's production build names it.
//
//   node tools/check-production.mjs [dist-folder]      (default: dist/ops)
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

/** Words that only the gallery's files contain. */
export const GALLERY_MARKS = ['dev/components', 'app-gallery-page', 'app-gallery-clinic-look', 'design-inventory', 'GALLERY_SECTIONS'];

/** The files of a build that name the gallery, with the word found. */
export function galleryTraces(folder) {
  const found = [];
  const walk = (directory) => {
    for (const name of readdirSync(directory)) {
      const path = join(directory, name);
      if (statSync(path).isDirectory()) walk(path);
      else if (/\.(js|mjs|css|html|json|txt|map)$/.test(name)) {
        const text = readFileSync(path, 'utf8');
        for (const mark of GALLERY_MARKS) {
          if (text.includes(mark)) found.push({ file: relative(folder, path).split('\\').join('/'), mark });
        }
      }
    }
  };
  walk(folder);
  return found;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const folder = process.argv[2] ?? fileURLToPath(new URL('../dist/ops', import.meta.url));
  if (!existsSync(folder)) {
    console.error(`No build at ${folder}: run "ng build ops" first.`);
    process.exit(2);
  }
  const traces = galleryTraces(folder);
  if (traces.length) {
    for (const trace of traces) console.error(`${trace.file} names the gallery ("${trace.mark}")`);
    process.exit(1);
  }
  console.log(`No file of ${relative(process.cwd(), folder) || folder} names the gallery.`);
}
