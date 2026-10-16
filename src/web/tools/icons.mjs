// The icons of the design file, as the apps take them (spec 095, research R6, contracts/icons.md).
//
//   node tools/icons.mjs list <words>                               the copy's icons whose names hold the words
//   node tools/icons.mjs add <design name> --as <short name> [--points [rtl]]
//                                                                    copy one into the registry;
//                                                                    --points for one drawn pointing
//                                                                    left to right, --points rtl for
//                                                                    one drawn the other way
//   node tools/icons.mjs check                                       fail on a registered icon nothing uses
//
// The copy is tools/icons/library/part-*.json: every icon of the design file's Icons page and its
// 16-pixel menu icons, taken through the design connector. Adding an icon needs neither the design
// file nor a network.
import { readFileSync, readdirSync, writeFileSync, statSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
export const LIBRARY = join(here, 'icons', 'library');
export const REGISTRY = join(here, '..', 'projects', 'ui', 'src', 'lib', 'icon', 'icon-registry.ts');
export const PROJECTS = join(here, '..', 'projects');
const START_MARK = '// ── Added from the design file by `node tools/icons.mjs add` — keep this marker ──';
const END_MARK = '// ── End of added icons ──';

/** Every icon of the copy: { name, group, node, width, height, shapes }. */
export function readLibrary(folder = LIBRARY) {
  const icons = [];
  for (const file of readdirSync(folder).filter((name) => name.endsWith('.json')).sort()) {
    for (const [name, group, node, width, height, shapes] of JSON.parse(readFileSync(join(folder, file), 'utf8')).items) {
      icons.push({ name, group, node, width, height, shapes });
    }
  }
  return icons;
}

/** Searches the copy: names that hold every word, case aside. */
export function list(words, icons = readLibrary()) {
  const wanted = words.map((word) => word.toLowerCase());
  return icons.filter((icon) => wanted.every((word) => icon.name.toLowerCase().includes(word)));
}

const attributesOf = (text) => Object.fromEntries([...text.matchAll(/([a-z-]+)="([^"]*)"/g)].map((m) => [m[1], m[2]]));
const number = (value) => Number(value);

/** One shape of the copy as a registry shape. */
function shapeOf([code, value]) {
  if (code === 'S') return { tag: 'path', d: value, paint: 'stroke' };
  if (code === 'F') return { tag: 'path', d: value, paint: 'fill' };
  if (code === 'FE') return { tag: 'path', d: value, paint: 'fill', rule: 'evenodd' };
  if (['circle', 'rect', 'ellipse', 'line', 'polyline', 'polygon'].includes(code)) {
    const a = attributesOf(value);
    const paint = a.fill && a.fill !== 'none' ? 'fill' : 'stroke';
    const width = a['stroke-width'] ? { strokeWidth: number(a['stroke-width']) } : {};
    if (code === 'circle') return { tag: 'circle', cx: number(a.cx), cy: number(a.cy), r: number(a.r), paint, ...width };
    if (code === 'ellipse') return { tag: 'ellipse', cx: number(a.cx), cy: number(a.cy), rx: number(a.rx), ry: number(a.ry), paint, ...width };
    if (code === 'line') return { tag: 'line', x1: number(a.x1), y1: number(a.y1), x2: number(a.x2), y2: number(a.y2), ...width };
    if (code === 'rect') {
      return {
        tag: 'rect',
        x: number(a.x ?? 0),
        y: number(a.y ?? 0),
        width: number(a.width),
        height: number(a.height),
        ...(a.rx ? { rx: number(a.rx) } : {}),
        ...(a.ry ? { ry: number(a.ry) } : {}),
        paint,
        ...width,
      };
    }
    return { tag: code, points: a.points, paint, ...width };
  }
  // A path written with its own paint: `stroke="#1A2024" stroke-width="1.4" …` or `fill="#1A2024"`.
  const a = attributesOf(code);
  const filled = a.fill && a.fill !== 'none' && !a.stroke;
  return {
    tag: 'path',
    d: value,
    paint: filled ? 'fill' : 'stroke',
    ...(a['stroke-width'] ? { strokeWidth: number(a['stroke-width']) } : {}),
    ...(a['fill-rule'] === 'evenodd' ? { rule: 'evenodd' } : {}),
  };
}

/** A registry drawing for an icon of the copy: one paint and one stroke width lifted when shared. */
export function drawingOf(icon, points) {
  let shapes = icon.shapes.map(shapeOf);
  const paints = new Set(shapes.map((shape) => shape.paint ?? 'stroke'));
  const paint = paints.size === 1 ? [...paints][0] : 'stroke';
  const widths = new Set(shapes.map((shape) => shape.strokeWidth));
  const strokeWidth = widths.size === 1 ? [...widths][0] : undefined;
  shapes = shapes.map(({ paint: own, strokeWidth: width, ...rest }) => ({
    ...rest,
    ...(own && own !== paint ? { paint: own } : {}),
    ...(width !== undefined && width !== strokeWidth ? { strokeWidth: width } : {}),
  }));
  return {
    from: icon.name,
    viewBox: `0 0 ${icon.width} ${icon.height}`,
    paint,
    ...(strokeWidth !== undefined ? { strokeWidth } : {}),
    ...(points ? { points: points === 'rtl' ? 'rtl' : 'ltr' } : {}),
    shapes,
  };
}

const literal = (value) => (typeof value === 'number' ? String(value) : `'${String(value).replace(/'/g, "\\'")}'`);
const objectText = (object) =>
  `{ ${Object.entries(object)
    .map(([key, value]) => `${key}: ${literal(value)}`)
    .join(', ')} }`;

/** The registry entry's text, as the generated part of icon-registry.ts writes it. */
export function entryText(shortName, drawing) {
  const { shapes, ...head } = drawing;
  const lines = Object.entries(head).map(([key, value]) => `    ${key}: ${literal(value)},`);
  return `  '${shortName}': {\n${lines.join('\n')}\n    shapes: [\n${shapes.map((shape) => `      ${objectText(shape)},`).join('\n')}\n    ],\n  },\n`;
}

/** The registry's short names. */
export function registeredNames(registry = readFileSync(REGISTRY, 'utf8')) {
  const body = registry.slice(registry.indexOf('export const SCS_ICONS'), registry.lastIndexOf('} as const'));
  return [...body.matchAll(/^ {2}'([a-z0-9-]+)': \{/gm)].map((m) => m[1]);
}

/** The closest names of the copy to a name that is not in it. */
function closest(name, icons) {
  const tail = name.toLowerCase().split('/').pop();
  const words = tail.split(/[-_ ]+/).filter((word) => word.length > 2);
  return icons
    .map((icon) => ({ icon, score: words.filter((word) => icon.name.toLowerCase().includes(word)).length }))
    .filter((entry) => entry.score > 0)
    .sort((a, b) => b.score - a.score || a.icon.name.localeCompare(b.icon.name))
    .slice(0, 8)
    .map((entry) => entry.icon.name);
}

/** Adds an icon of the copy to the registry. Returns the new registry text, or throws. */
export function add(registry, designName, shortName, points, icons = readLibrary()) {
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(shortName ?? '')) {
    throw new Error(`The short name must be lower case words joined by "-": ${shortName}`);
  }
  const icon = icons.find((candidate) => candidate.name === designName);
  if (!icon) {
    const near = closest(designName, icons);
    throw new Error(`"${designName}" is not in the copy of the design file.${near.length ? ` Closest: ${near.join(', ')}` : ''}`);
  }
  if (registeredNames(registry).includes(shortName)) {
    throw new Error(`"${shortName}" is already a registered icon.`);
  }
  const at = registry.indexOf(END_MARK);
  if (registry.indexOf(START_MARK) < 0 || at < 0) throw new Error('The registry has lost its markers.');
  const lineStart = registry.lastIndexOf('\n', at) + 1;
  return registry.slice(0, lineStart) + entryText(shortName, drawingOf(icon, points)) + registry.slice(lineStart);
}

/** Registered names no file under projects/ uses (the registry, its test data and specs aside). */
export function unused(projects = PROJECTS, registry = readFileSync(REGISTRY, 'utf8')) {
  const names = registeredNames(registry);
  let text = '';
  const walk = (directory) => {
    for (const entry of readdirSync(directory)) {
      const path = join(directory, entry);
      if (statSync(path).isDirectory()) {
        if (entry !== 'node_modules') walk(path);
      } else if (/\.(ts|html)$/.test(entry) && !/icon-registry\.ts$|icon-drawn\.fixture\.ts$|\.spec\.ts$/.test(entry)) {
        text += readFileSync(path, 'utf8') + '\n';
      }
    }
  };
  walk(projects);
  return names.filter((name) => !new RegExp(`['"\`]${name}['"\`]`).test(text));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const [command, ...rest] = process.argv.slice(2);
  try {
    if (command === 'list') {
      const found = list(rest);
      for (const icon of found) console.log(`${icon.name}  (${icon.group}, ${icon.width}×${icon.height})`);
      console.log(`${found.length} found.`);
    } else if (command === 'add') {
      const at = rest.indexOf('--as');
      const designName = rest.find((value, index) => !value.startsWith('--') && rest[index - 1] !== '--as' && !(value === 'rtl' && rest[index - 1] === '--points'));
      const shortName = at >= 0 ? rest[at + 1] : undefined;
      const pointsAt = rest.indexOf('--points');
      const points = pointsAt < 0 ? false : rest[pointsAt + 1] === 'rtl' ? 'rtl' : true;
      writeFileSync(REGISTRY, add(readFileSync(REGISTRY, 'utf8'), designName, shortName, points));
      console.log(`Added "${shortName}" from "${designName}".`);
    } else if (command === 'check') {
      const lost = unused();
      if (lost.length) {
        console.error(`Registered icons that nothing uses: ${lost.join(', ')}`);
        process.exit(1);
      }
      console.log(`Every registered icon is used (${registeredNames(readFileSync(REGISTRY, 'utf8')).length}).`);
    } else {
      console.error('usage: node tools/icons.mjs list <words> | add <design name> --as <short name> [--points [rtl]] | check');
      process.exit(2);
    }
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }
}
