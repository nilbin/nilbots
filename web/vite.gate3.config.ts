import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

/**
 * Shared Gate-3 gallery viewer: Canvas2D, one map theme, external assets.
 *
 * The owner sample carries twelve broadcasts but only one viewer bundle. H0
 * explicitly parks the 3D renderer, and the complete gallery has an 8 MiB
 * ceiling, so this build uses the same compile-time exclusions as the CLI
 * viewer without inlining the result into every replay page.
 */
const THEME = 'ember-forge';

export default defineConfig({
  base: './',
  publicDir: false,
  plugins: [
    scopeToTheme(THEME),
    stubDimensionalRenderer(),
    stubExternalSoundtrack(),
    react(),
    tailwindcss(),
  ],
  define: {
    __BOTARENA_DEFAULT_THEME__: JSON.stringify(THEME),
    __BOTARENA_DIMENSIONAL_RENDERER__: 'false',
    __BOTARENA_EXTERNAL_SOUNDTRACK__: 'false',
  },
  build: {
    outDir: 'dist-gate3',
  },
});

function stubDimensionalRenderer(): Plugin {
  return {
    name: 'nilbots-gate3-stub-3d-renderer',
    enforce: 'pre',
    resolveId(source) {
      return source.includes('render3d/ArenaCanvas3D')
        ? '\0nilbots-gate3-no-3d'
        : null;
    },
    load(id) {
      return id === '\0nilbots-gate3-no-3d'
        ? 'export default function NoRenderer() { return null; }'
        : null;
    },
  };
}

function stubExternalSoundtrack(): Plugin {
  return {
    name: 'nilbots-gate3-stub-external-soundtrack',
    enforce: 'pre',
    resolveId(source) {
      return source.includes('soundtrack/AdaptiveSoundtrack')
        ? '\0nilbots-gate3-no-soundtrack'
        : null;
    },
    load(id) {
      return id === '\0nilbots-gate3-no-soundtrack'
        ? 'export default function NoSoundtrack() { return null; }'
        : null;
    },
  };
}

function scopeToTheme(theme: string): Plugin {
  return {
    name: 'nilbots-gate3-scope-theme',
    enforce: 'pre',
    transform(code, id) {
      if (!id.endsWith('render/arenaThemes.ts')) return null;
      const pattern = "'../assets/themes/*/";
      if (!code.includes(pattern)) {
        throw new Error(
          `Cannot scope Gate-3 viewer to ${theme}: theme glob moved in arenaThemes.ts.`,
        );
      }
      return code.split(pattern).join(`'../assets/themes/${theme}/`);
    },
  };
}
