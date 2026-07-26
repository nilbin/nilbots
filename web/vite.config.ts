import { defineConfig } from 'vite';
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
  plugins: [react(), tailwindcss()],
  server: {
    proxy: { '/api': 'http://127.0.0.1:8080' },
  },
});
