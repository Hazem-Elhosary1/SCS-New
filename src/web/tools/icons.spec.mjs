import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { add, drawingOf, list, readLibrary, registeredNames, unused } from './icons.mjs';

const copy = [
  { name: 'Interface-Essential/add-circle', group: 'Interface-Essential', node: '771:5', width: 14, height: 14, shapes: [['S', 'M7 4V10'], ['S', 'M4 7H10']] },
  { name: 'Interface-Essential/search-1', group: 'Interface-Essential', node: '771:9', width: 14, height: 14, shapes: [['FE', 'M1 1H2Z']] },
  {
    name: 'Menu/menu-icon/key',
    group: 'Menu',
    node: '2324:252',
    width: 16,
    height: 16,
    shapes: [
      ['circle', 'cx="5.5" cy="10.4" r="2.6" stroke="#1A2024" stroke-width="1.4"'],
      ['stroke="#1A2024" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"', 'M8.1 7.9L14 2'],
    ],
  },
  { name: 'Menu/menu-icon/more', group: 'Menu', node: '2200:91', width: 16, height: 16, shapes: [['fill="#1A2024"', 'M3 9Z']] },
];

const registry = `export const SCS_ICONS = {
  'mail': {
    from: 'drawn',
    viewBox: '0 0 24 24',
    paint: 'stroke',
    shapes: [],
  },
  // ── Added from the design file by \`node tools/icons.mjs add\` — keep this marker ──
  // ── End of added icons ──
} as const satisfies Record<string, ScsIconDrawing>;
`;

test('the copy holds every icon of the Icons page and the menu icons, each once', () => {
  const icons = readLibrary();
  const fromIconsPage = icons.filter((icon) => icon.group !== 'Menu');
  assert.equal(fromIconsPage.length, 1144);
  assert.equal(icons.filter((icon) => icon.group === 'Menu').length, 18);
  assert.equal(new Set(icons.map((icon) => icon.node)).size, icons.length, 'no node twice');
  for (const icon of icons) assert.ok(icon.shapes.length > 0, `${icon.name} has shapes`);
});

test('list finds the icons whose names hold every word', () => {
  assert.deepEqual(list(['circle'], copy).map((icon) => icon.name), ['Interface-Essential/add-circle']);
  assert.deepEqual(list(['MENU', 'key'], copy).map((icon) => icon.name), ['Menu/menu-icon/key']);
  assert.deepEqual(list(['nothing-like-this'], copy), []);
});

test('a drawing keeps the shapes as drawn, lifting a shared paint and stroke width', () => {
  assert.deepEqual(drawingOf(copy[0]), {
    from: 'Interface-Essential/add-circle',
    viewBox: '0 0 14 14',
    paint: 'stroke',
    shapes: [{ tag: 'path', d: 'M7 4V10' }, { tag: 'path', d: 'M4 7H10' }],
  });
  assert.deepEqual(drawingOf(copy[1]).shapes, [{ tag: 'path', d: 'M1 1H2Z', rule: 'evenodd' }]);
  assert.equal(drawingOf(copy[1]).paint, 'fill');
  const key = drawingOf(copy[2], true);
  assert.equal(key.strokeWidth, 1.4);
  assert.equal(key.points, 'ltr');
  assert.deepEqual(key.shapes[0], { tag: 'circle', cx: 5.5, cy: 10.4, r: 2.6 });
  assert.equal(drawingOf(copy[3]).paint, 'fill');
});

test('add writes the icon between the markers, and refuses an unknown name or a taken one', () => {
  const after = add(registry, 'Interface-Essential/add-circle', 'add-circle', false, copy);
  assert.deepEqual(registeredNames(after), ['mail', 'add-circle']);
  assert.ok(after.indexOf("'add-circle'") > after.indexOf('keep this marker'));
  assert.ok(after.indexOf("'add-circle'") < after.indexOf('End of added icons'));
  assert.throws(() => add(registry, 'Interface-Essential/circle-add', 'x', false, copy), /not in the copy.*Closest: Interface-Essential\/add-circle/);
  assert.throws(() => add(after, 'Interface-Essential/search-1', 'add-circle', false, copy), /already a registered icon/);
  assert.throws(() => add(registry, 'Interface-Essential/search-1', 'Search Icon', false, copy), /lower case words/);
});

test('check names the registered icons that no file uses', () => {
  const root = mkdtempSync(join(tmpdir(), 'icons-'));
  try {
    mkdirSync(join(root, 'app'));
    writeFileSync(join(root, 'app', 'page.ts'), `const icon = 'mail';`);
    const withOne = add(registry, 'Interface-Essential/add-circle', 'add-circle', false, copy);
    assert.deepEqual(unused(root, withOne), ['add-circle']);
    writeFileSync(join(root, 'app', 'other.html'), `<scs-icon name="add-circle" />`);
    assert.deepEqual(unused(root, withOne), []);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
