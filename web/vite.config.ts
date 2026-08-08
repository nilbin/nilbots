import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

/**
 * What gets served over HTTP: the site, and the arena the mobile app loads in its WebView.
 *
 * Ordinary hashed assets, so a browser streams the atlases it needs and caches them across
 * visits. The single-file build lives in `vite.cli.config.ts` and exists for one consumer —
 * `nilbots play`, whose output must survive being copied somewhere and opened from disk.
 * Serving that artifact to browsers meant every visitor parsed ~15 MB inline before
 * anything rendered, and the phone paid it again on each cold WebView.
 */
export default defineConfig({
  plugins: [docsSourceHash(), react(), tailwindcss()],
  server: {
    proxy: { '/api': 'http://127.0.0.1:8080' },
  },
});

/**
 * Emit a content identity for the source whose freshness the server test cares
 * about. mtimes change across ordinary checkouts; bytes do not. Keeping the
 * stamp inside dist also proves the exact source the served build consumed.
 */
function docsSourceHash(): Plugin {
  const source = readFileSync(
    new URL('./src/site/pages/DocsPage.tsx', import.meta.url),
  );
  const sha256 = createHash('sha256').update(source).digest('hex');
  return {
    name: 'nilbots-docs-source-hash',
    generateBundle() {
      this.emitFile({
        type: 'asset',
        fileName: 'source-hashes.json',
        source: `${JSON.stringify({ docsPageSha256: sha256 }, null, 2)}\n`,
      });
    },
  };
}
