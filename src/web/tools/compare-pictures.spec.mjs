import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { deflateSync } from 'node:zlib';
import { compareFolders, decodePng, differingShare } from './compare-pictures.mjs';

/** A small RGB PNG of the given pixels (rows of [r, g, b]), each row with the filter given. */
function png(pixels, filter = 0) {
  const height = pixels.length;
  const width = pixels[0].length;
  const crc = (buffer) => {
    let c = ~0;
    for (const byte of buffer) {
      c ^= byte;
      for (let k = 0; k < 8; k++) c = (c >>> 1) ^ (0xedb88320 & -(c & 1));
    }
    return ~c >>> 0;
  };
  const chunk = (kind, body) => {
    const head = Buffer.alloc(8);
    head.writeUInt32BE(body.length, 0);
    head.write(kind, 4, 'ascii');
    const tail = Buffer.alloc(4);
    tail.writeUInt32BE(crc(Buffer.concat([Buffer.from(kind, 'ascii'), body])), 0);
    return Buffer.concat([head, body, tail]);
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header[8] = 8;
  header[9] = 2;
  const raw = [];
  pixels.forEach((row, y) => {
    raw.push(filter);
    row.forEach((pixel, x) => {
      pixel.forEach((value, channel) => {
        // Filter 1 (Sub) stores the difference from the pixel to the left; filter 2 (Up), from the one above.
        const left = x > 0 ? row[x - 1][channel] : 0;
        const above = y > 0 ? pixels[y - 1][x][channel] : 0;
        raw.push(filter === 1 ? (value - left) & 0xff : filter === 2 ? (value - above) & 0xff : value);
      });
    });
  });
  return Buffer.concat([
    Buffer.from('89504e470d0a1a0a', 'hex'),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(Buffer.from(raw))),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

const white = [255, 255, 255];
const black = [0, 0, 0];
const grid = (rows, cols, at = () => white) =>
  Array.from({ length: rows }, (_, y) => Array.from({ length: cols }, (_, x) => at(x, y)));

test('decodes a PNG whichever row filter it was written with', () => {
  const pixels = grid(3, 4, (x, y) => [x * 60, y * 80, 200]);
  for (const filter of [0, 1, 2]) {
    const picture = decodePng(png(pixels, filter));
    assert.equal(picture.width, 4);
    assert.equal(picture.height, 3);
    assert.deepEqual([...picture.rgba.subarray(4 * 5, 4 * 5 + 4)], [60, 80, 200, 255]);
  }
});

test('the share of differing pixels ignores small changes of smoothing', () => {
  const base = decodePng(png(grid(10, 10)));
  const smoothed = decodePng(png(grid(10, 10, (x) => (x === 0 ? [250, 250, 250] : white))));
  const changed = decodePng(png(grid(10, 10, (x, y) => (x === 0 && y < 5 ? black : white))));
  assert.equal(differingShare(base, smoothed), 0);
  assert.equal(differingShare(base, changed), 0.05);
  assert.equal(differingShare(base, decodePng(png(grid(10, 11)))), null);
});

test('a folder comparison flags what changed, what moved size and what is on one side only', () => {
  const root = mkdtempSync(join(tmpdir(), 'compare-'));
  const before = join(root, 'before');
  const after = join(root, 'after');
  for (const folder of [before, after]) mkdirSync(join(folder, 'narrow'), { recursive: true });
  writeFileSync(join(before, 'same.png'), png(grid(4, 4)));
  writeFileSync(join(after, 'same.png'), png(grid(4, 4)));
  writeFileSync(join(before, 'narrow', 'changed.png'), png(grid(4, 4)));
  writeFileSync(join(after, 'narrow', 'changed.png'), png(grid(4, 4, (x, y) => (x === 0 && y === 0 ? black : white))));
  writeFileSync(join(before, 'resized.png'), png(grid(4, 4)));
  writeFileSync(join(after, 'resized.png'), png(grid(4, 5)));
  writeFileSync(join(before, 'gone.png'), png(grid(4, 4)));
  try {
    const results = compareFolders(before, after);
    const by = Object.fromEntries(results.map((result) => [result.name, result]));
    assert.equal(by['same.png'].problem, null);
    assert.equal(by['narrow/changed.png'].problem, 'differs');
    assert.equal(by['narrow/changed.png'].share, 1 / 16);
    assert.match(by['resized.png'].problem, /^size 4x4 → 5x4$/);
    assert.equal(by['gone.png'].problem, 'only before');
    assert.equal(results.at(-1).name, 'same.png');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test('against several "before" runs, a picture is held against the closest one', () => {
  const root = mkdtempSync(join(tmpdir(), 'compare-'));
  const [one, two, after] = ['one', 'two', 'after'].map((name) => join(root, name));
  for (const folder of [one, two, after]) mkdirSync(folder, { recursive: true });
  const dot = grid(4, 4, (x, y) => (x === 0 && y === 0 ? black : white));
  writeFileSync(join(one, 'flaky.png'), png(grid(4, 4)));
  writeFileSync(join(two, 'flaky.png'), png(dot));
  writeFileSync(join(after, 'flaky.png'), png(dot));
  writeFileSync(join(one, 'real.png'), png(grid(4, 4)));
  writeFileSync(join(two, 'real.png'), png(grid(4, 4)));
  writeFileSync(join(after, 'real.png'), png(dot));
  try {
    const by = Object.fromEntries(compareFolders([one, two], after).map((result) => [result.name, result]));
    assert.equal(by['flaky.png'].problem, null);
    assert.equal(by['real.png'].problem, 'differs');
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
