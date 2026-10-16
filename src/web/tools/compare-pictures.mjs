// Compares two folders of pictures taken of the same screens (spec 095, research R9): for each
// pair, whether the size changed and the share of pixels that differ. Used to show that the
// clinic's and patients' screens did not change when shared parts did.
//
//   node tools/compare-pictures.mjs <before-folder> <after-folder> [--also <before-folder>]… [--limit 0.0005]
//
// Some screens are not the same twice (a side bar measured before its font loads, a last screen
// caught before its data): with `--also`, each picture is held against the closest of several
// "before" runs, so only what differs from all of them is flagged.
//
// Exits 1 when a picture is missing on one side, changed size, or differs in more than the limit
// (a share; 0.0005 is 0.05%). A pixel differs when its three channels differ by more than 30 in all.
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join, relative } from 'node:path';
import { inflateSync } from 'node:zlib';
import { fileURLToPath } from 'node:url';

/** Decodes an 8-bit, non-interlaced PNG (grey, grey+alpha, RGB or RGBA) into RGBA bytes. */
export function decodePng(buffer) {
  const signature = '89504e470d0a1a0a';
  if (buffer.subarray(0, 8).toString('hex') !== signature) throw new Error('not a PNG');
  let width = 0;
  let height = 0;
  let depth = 0;
  let type = 0;
  let interlace = 0;
  const data = [];
  for (let at = 8; at < buffer.length; ) {
    const length = buffer.readUInt32BE(at);
    const kind = buffer.toString('ascii', at + 4, at + 8);
    const body = buffer.subarray(at + 8, at + 8 + length);
    if (kind === 'IHDR') {
      width = body.readUInt32BE(0);
      height = body.readUInt32BE(4);
      depth = body[8];
      type = body[9];
      interlace = body[12];
    } else if (kind === 'IDAT') {
      data.push(body);
    } else if (kind === 'IEND') {
      break;
    }
    at += 12 + length;
  }
  const channels = { 0: 1, 2: 3, 4: 2, 6: 4 }[type];
  if (depth !== 8 || !channels || interlace !== 0) {
    throw new Error(`unsupported PNG (depth ${depth}, type ${type}, interlace ${interlace})`);
  }
  const raw = inflateSync(Buffer.concat(data));
  const stride = width * channels;
  const rows = Buffer.alloc(stride * height);
  for (let y = 0; y < height; y++) {
    const filter = raw[y * (stride + 1)];
    const line = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    const out = rows.subarray(y * stride, (y + 1) * stride);
    const up = y > 0 ? rows.subarray((y - 1) * stride, y * stride) : null;
    for (let x = 0; x < stride; x++) {
      const a = x >= channels ? out[x - channels] : 0;
      const b = up ? up[x] : 0;
      const c = up && x >= channels ? up[x - channels] : 0;
      let value = line[x];
      if (filter === 1) value += a;
      else if (filter === 2) value += b;
      else if (filter === 3) value += (a + b) >> 1;
      else if (filter === 4) {
        const p = a + b - c;
        const pa = Math.abs(p - a);
        const pb = Math.abs(p - b);
        const pc = Math.abs(p - c);
        value += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
      }
      out[x] = value & 0xff;
    }
  }
  const rgba = Buffer.alloc(width * height * 4);
  for (let i = 0, j = 0; i < width * height; i++, j += channels) {
    const grey = channels <= 2;
    rgba[i * 4] = rows[j];
    rgba[i * 4 + 1] = grey ? rows[j] : rows[j + 1];
    rgba[i * 4 + 2] = grey ? rows[j] : rows[j + 2];
    rgba[i * 4 + 3] = channels === 2 ? rows[j + 1] : channels === 4 ? rows[j + 3] : 255;
  }
  return { width, height, rgba };
}

/** The share (0–1) of pixels whose RGB channels differ by more than 30 in all, or null when the sizes differ. */
export function differingShare(one, two) {
  if (one.width !== two.width || one.height !== two.height) return null;
  let differing = 0;
  for (let i = 0; i < one.rgba.length; i += 4) {
    const sum =
      Math.abs(one.rgba[i] - two.rgba[i]) +
      Math.abs(one.rgba[i + 1] - two.rgba[i + 1]) +
      Math.abs(one.rgba[i + 2] - two.rgba[i + 2]);
    if (sum > 30) differing++;
  }
  return differing / (one.width * one.height);
}

function pictures(folder) {
  const found = [];
  const walk = (directory) => {
    for (const name of readdirSync(directory)) {
      const path = join(directory, name);
      if (statSync(path).isDirectory()) walk(path);
      else if (name.endsWith('.png')) found.push(relative(folder, path).split('\\').join('/'));
    }
  };
  walk(folder);
  return found.sort();
}

/** Every picture of either side, with what happened to it; the worst first. `before` is one folder or several runs. */
export function compareFolders(before, after, limit = 0.0005) {
  const runs = Array.isArray(before) ? before : [before];
  const names = [...new Set([...pictures(runs[0]), ...pictures(after)])];
  const results = names.map((name) => {
    const two = join(after, name);
    const ones = runs.map((run) => join(run, name)).filter((path) => existsSync(path));
    if (!ones.length) return { name, problem: 'only after', share: 1 };
    if (!existsSync(two)) return { name, problem: 'only before', share: 1 };
    const b = decodePng(readFileSync(two));
    let closest = null;
    for (const one of ones) {
      const a = decodePng(readFileSync(one));
      const share = differingShare(a, b);
      const result =
        share === null
          ? { name, problem: `size ${a.width}x${a.height} → ${b.width}x${b.height}`, share: 1 }
          : { name, problem: share > limit ? 'differs' : null, share };
      if (!closest || result.share < closest.share) closest = result;
    }
    return closest;
  });
  return results.sort((m, n) => n.share - m.share);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const [before, after, ...rest] = process.argv.slice(2);
  if (!before || !after) {
    console.error('usage: node tools/compare-pictures.mjs <before-folder> <after-folder> [--also <before-folder>]… [--limit 0.0005]');
    process.exit(2);
  }
  const at = rest.indexOf('--limit');
  const limit = at >= 0 ? Number(rest[at + 1]) : 0.0005;
  const also = rest.flatMap((value, index) => (rest[index - 1] === '--also' ? [value] : []));
  const results = compareFolders([before, ...also], after, limit);
  const flagged = results.filter((result) => result.problem);
  for (const result of results.slice(0, 20)) {
    console.log(`${(result.share * 100).toFixed(3).padStart(8)}%  ${result.name}${result.problem ? `  ← ${result.problem}` : ''}`);
  }
  console.log(`${results.length} pictures compared, ${flagged.length} flagged (limit ${(limit * 100).toFixed(3)}%).`);
  process.exit(flagged.length ? 1 : 0);
}
