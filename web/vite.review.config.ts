import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

/**
 * Review-site build. Unlike the CLI artifact, a hosted page should let the
 * browser fetch atlases and audio separately instead of parsing a ~15 MiB
 * self-contained HTML document up front.
 */
export default defineConfig({
  base: './',
  plugins: [
    {
      name: 'nilbots-review-standalone-mode',
      transformIndexHtml(html) {
        return html.replace(
          '<!--BOTARENA_REPLAY-->',
          `<script>
            (() => {
              const reviewUrl = new URL(window.location.href);
              reviewUrl.searchParams.set('standalone', '');
              window.history.replaceState(null, '', reviewUrl);
            })();
          </script>`,
        );
      },
    },
    react(),
    tailwindcss(),
  ],
  build: {
    outDir: 'dist-review',
  },
});
