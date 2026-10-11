// Tests for the translation check. Run with: node --test tools/
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { check, comparePair, keysUsed, literalText, templatesOf } from './i18n-check.mjs';

function workspace(files) {
  const root = mkdtempSync(join(tmpdir(), 'scs-i18n-'));
  for (const [path, content] of Object.entries(files)) {
    const file = join(root, path);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, typeof content === 'string' ? content : JSON.stringify(content));
  }
  return root;
}

const page = (template) =>
  `@Component({ selector: 'app-x', template: \`${template}\` })\nexport class X {}\n`;

const good = {
  'projects/clinic/public/i18n/ar.json': { app: { shell: { name: 'لوحة العيادة' } } },
  'projects/clinic/public/i18n/en.json': { app: { shell: { name: 'Clinic console' } } },
  'projects/ui/i18n/ar.json': { ui: { state: { empty: { title: 'لا توجد بيانات' } } } },
  'projects/ui/i18n/en.json': { ui: { state: { empty: { title: 'Nothing here yet' } } } },
  'projects/clinic/src/app/x.page.ts': page(
    "<h1>{{ 'app.shell.name' | transloco }}</h1><p>{{ 'ui.state.empty.title' | transloco }}</p>",
  ),
};

function run(overrides) {
  const root = workspace({ ...good, ...overrides });
  try {
    return check(root);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

test('a complete workspace passes', () => {
  assert.deepEqual(run({}), []);
});

test('fails when a key exists only in ar.json', () => {
  const problems = run({
    'projects/clinic/public/i18n/ar.json': {
      app: { shell: { name: 'لوحة العيادة', extra: 'زائد' } },
    },
  });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /"app\.shell\.extra" is in ar\.json but not in en\.json/);
});

test('fails when a key exists only in en.json', () => {
  const problems = run({
    'projects/ui/i18n/en.json': { ui: { state: { empty: { title: 'Nothing', hint: 'Add one' } } } },
  });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /"ui\.state\.empty\.hint" is in en\.json but not in ar\.json/);
});

test('fails when a value is empty', () => {
  const problems = run({
    'projects/clinic/public/i18n/en.json': { app: { shell: { name: '   ' } } },
  });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /"app\.shell\.name" is empty in en\.json/);
});

test('fails when a template contains literal visible text', () => {
  const problems = run({ 'projects/clinic/src/app/x.page.ts': page('<p>المنصّة متاحة</p>') });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /text written directly in the template: المنصّة متاحة/);
});

test('fails on literal text in an .html template and in a text attribute', () => {
  const problems = run({
    'projects/clinic/src/app/y.html':
      '<button aria-label="Close">{{ x }}</button>\n<span>Saved</span>',
  });
  assert.equal(problems.length, 2);
  assert.ok(problems.some((p) => p.includes('aria-label="Close"')));
  assert.ok(problems.some((p) => p.endsWith('Saved')));
});

test('fails when a template uses a key that no file has', () => {
  const problems = run({
    'projects/clinic/src/app/x.page.ts': page("<p>{{ 'app.shell.missing' | transloco }}</p>"),
  });
  assert.equal(problems.length, 1);
  assert.match(problems[0], /key "app\.shell\.missing" is in no translation file/);
});

test('a shared component may use a key that every app supplies', () => {
  const problems = run({
    'projects/ui/src/lib/panel.ts': page("<h1>{{ 'app.shell.name' | transloco }}</h1>"),
  });
  assert.deepEqual(problems, []);
});

test('spec files and control flow are not mistaken for text', () => {
  const problems = run({
    'projects/clinic/src/app/x.spec.ts': page('<p>Text in a test host is fine</p>'),
    'projects/clinic/src/app/z.page.ts': page(
      '@if (ready(); as value) { <bdi>{{ value }}</bdi> } @else if (busy()) { <i></i> } @else { <b [attr.aria-label]="\'app.shell.name\' | transloco"></b> }' +
        " @for (item of items; track item.id) { <i>{{ item }}</i> } @empty { <i></i> } @switch (kind()) { @case ('a') { <i></i> } @default { <i></i> } }",
    ),
  });
  assert.deepEqual(problems, []);
});

test('helpers', () => {
  assert.deepEqual(comparePair({ a: 'x' }, { a: 'y' }, 'p'), []);
  assert.deepEqual(literalText('<p>{{ a }}</p><!-- note --><code>:</code>'), []);
  assert.deepEqual(literalText('<p>Hello</p>'), ['Hello']);
  assert.deepEqual(keysUsed("{{ 'a.b.c' | transloco }} [x]=\"'d.e' | transloco\""), [
    'a.b.c',
    'd.e',
  ]);
  assert.deepEqual(templatesOf('a.ts', "x = { template: '<p>hi</p>' }"), ['<p>hi</p>']);
});
