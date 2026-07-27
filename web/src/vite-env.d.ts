/// <reference types="vite/client" />

// Raw text imports: the docs pages compile the repo's own markdown straight into the
// bundle so the site cannot drift from the canonical source.
declare module '*.md?raw' {
  const content: string;
  export default content;
}

/**
 * Substituted by `vite.cli.config.ts` when it scopes an artifact to a single theme.
 * Declared rather than defined, so builds that do not set it leave the fallback alone.
 */
declare const __BOTARENA_DEFAULT_THEME__: string | undefined;

/**
 * False only in the self-contained CLI build, where the dynamic renderer is stubbed to
 * keep three.js out of every copied replay.
 */
declare const __BOTARENA_DIMENSIONAL_RENDERER__: boolean | undefined;

/**
 * False in self-contained CLI viewers. HTTP builds lazy-load the runtime and
 * fetch content-addressed score assets only after explicit user activation.
 */
declare const __BOTARENA_EXTERNAL_SOUNDTRACK__: boolean | undefined;
