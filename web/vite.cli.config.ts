import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { viteSingleFile } from 'vite-plugin-singlefile';

/**
 * The CLI's replay artifact: one self-contained `index.html`.
 *
 * `nilbots play` writes a `viewer.html` the player can copy anywhere, mail to someone, or
 * open from disk — so it has to inline everything, and `file:` URLs cannot fetch sibling
 * modules anyway. That constraint belongs to this build alone.
 *
 * It used to belong to *every* build. `web/dist` was this output, and the App served it,
 * so the hosted site and the mobile WebView both parsed a ~15 MB inline document to show a
 * landing page — paying the price of a constraint neither of them has. `vite.config.ts` is
 * now the ordinary hashed-asset build for everything served over HTTP, and this is the one
 * place `viteSingleFile` appears.
 */
export default defineConfig({
  plugins: [react(), tailwindcss(), viteSingleFile()],
  build: {
    outDir: 'dist-cli',
  },
});
