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
  preview: {
    /**
     * Vite rejects requests whose Host header it does not recognise — DNS-rebinding
     * protection — and a tunnel arrives with a hostname the config has never seen, so
     * every request 403s with "This host is not allowed".
     *
     * A leading dot matches the domain and its subdomains, which is what a quick tunnel
     * needs: its hostname is random per run. Scoped to the tunnel providers rather than
     * `true`, which would turn the protection off for every host.
     */
    allowedHosts: ['.trycloudflare.com', '.ngrok-free.app', '.ngrok.io', '.ts.net'],
  },
});
