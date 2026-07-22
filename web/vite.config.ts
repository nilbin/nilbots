import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { viteSingleFile } from 'vite-plugin-singlefile';

// The build output is a single self-contained index.html so the CLI can copy it
// anywhere and inject a replay (see BotArena.Cli/ReplayOutput.cs).
export default defineConfig({
  plugins: [react(), tailwindcss(), viteSingleFile()],
  server: {
    proxy: { '/api': 'http://127.0.0.1:8080' },
  },
});
