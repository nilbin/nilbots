#!/usr/bin/env node
/**
 * Bake wall atlases down to the sizes a device actually needs.
 *
 * The masters are 4096×4096. That is 64 MB of RAM each once decoded — dimensions decide
 * decoded size, not file size, and WebP's excellent compression hides that completely:
 * ~550 KB on disk, 64 MB in memory. Sixteen of them is why mobile tabs died.
 *
 * But 4096 is also 1.5–2× oversampled even on a large desktop, and 1024 is *under*sampled
 * on any desktop — 48 content pixels per tile against the 84–126 a retina laptop needs.
 * So there is no single right size, and the choice belongs to the client at load, not to
 * the build. This emits the variants; `arenaThemes` picks between them.
 *
 * Output is generated and gitignored. Run before `dev` or `build` — both npm scripts do.
 */

import { mkdir, readdir, rm, stat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const sharp = createRequire(path.join(root, 'web', 'package.json'))('sharp');
const themesRoot = path.join(root, 'web', 'src', 'assets', 'themes');

/** Emitted widths. 4096 stays as the master and is used directly when nothing smaller fits. */
export const VARIANT_WIDTHS = [1024, 2048];

/** Only the wall atlases — they are the 4096 masters, and the only ones worth varying. */
const ATLAS = /^(wall-[a-z-]+)\.webp$/;

/**
 * The single-file artifact inlines every asset it can see, so variants there would be
 * added weight it can never use — it cannot fetch a chosen one at runtime. That build
 * cleans instead of generating, and the loader falls back to the master.
 */
const clean = process.argv.includes('--clean');

let written = 0;
let skipped = 0;
let removed = 0;

const themes = await readdir(themesRoot, { withFileTypes: true });
for (const theme of themes.filter((entry) => entry.isDirectory())) {
  const directory = path.join(themesRoot, theme.name);
  const variantsDir = path.join(directory, 'variants');

  for (const filename of await readdir(directory)) {
    const match = filename.match(ATLAS);
    if (!match) continue;

    if (clean) {
      if (existsSync(variantsDir)) {
        await rm(variantsDir, { recursive: true, force: true });
        removed += 1;
      }
      break;
    }

    const source = path.join(directory, filename);
    const sourceStat = await stat(source);

    for (const width of VARIANT_WIDTHS) {
      const target = path.join(variantsDir, `${match[1]}@${width}.webp`);
      // Regenerate only when the master is newer: this runs on every dev start.
      if (existsSync(target) && (await stat(target)).mtimeMs >= sourceStat.mtimeMs) {
        skipped += 1;
        continue;
      }
      await mkdir(variantsDir, { recursive: true });
      await sharp(source)
        .resize(width, width, { fit: 'inside', withoutEnlargement: true })
        .webp({ quality: 82, effort: 6 })
        .toFile(target);
      written += 1;
    }
  }
}

console.log(
  clean
    ? `Atlas variants: cleaned ${removed} theme director${removed === 1 ? 'y' : 'ies'}.`
    : `Atlas variants: ${written} written, ${skipped} up to date ` +
      `(${VARIANT_WIDTHS.join('px, ')}px).`,
);
