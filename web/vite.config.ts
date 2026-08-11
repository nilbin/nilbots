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
    // `/callback` is not an API route but must be proxied all the same: Google
    // returns the browser to /callback/login/google, which is mapped at the root
    // (app.MapExternalAuth), and OpenIddict derives that redirect URI from the
    // *request's* host. Proxying only /api meant the sign-in left from :5173 and
    // came back to :5173, where the dev server answered with the SPA fallback —
    // so the round trip ended on a blank app instead of a session, and looked
    // like Google had rejected it.
    //
    // changeOrigin stays at its default false on purpose: preserving the Host
    // header is what makes the app build a :5173 redirect URI in the first
    // place, keeping the whole flow same-origin so the session cookie sticks.
    proxy: {
      '/api': 'http://127.0.0.1:8080',
      '/callback': 'http://127.0.0.1:8080',
    },
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
