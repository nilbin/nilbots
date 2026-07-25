import { Platform } from 'react-native';

/**
 * The site's design language, mirrored from web/src/index.css and its Tailwind usage so
 * the two surfaces read as one product. Tokens, not an API contract — keep in sync by
 * hand; a build step would cost more than it saves.
 *
 * The nilbots look is: near-black field, panels separated by a hairline edge rather than
 * shadow, one cyan accent used sparingly, and monospace for anything a machine produced
 * (ratings, hashes, versions, tick counts). Prose is sans; data is mono.
 */
export const Arena = {
  bg: '#0a0e14',
  panel: '#111823',
  edge: '#1d2836',
  text: '#c9d5e3',
  dim: '#64748b',
  accent: '#38bdf8',
  /** Only for broadcast/live indicators — never as a generic error tint. */
  live: '#ef4444',
  ok: '#22c55e',
  /** The bright centre of the logo mark. */
  spark: '#e2f3ff',
} as const;

export const Space = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 } as const;

/** Matches Tailwind's rounded-md / -lg / -xl, which is what the site uses. */
export const Radius = { sm: 6, md: 8, lg: 12 } as const;

/**
 * The site's font-mono stack, resolved per platform. Menlo is the iOS system monospace;
 * Android exposes its as plain "monospace".
 */
export const Mono = Platform.select({
  ios: { fontFamily: 'Menlo' },
  android: { fontFamily: 'monospace' },
  default: { fontFamily: 'ui-monospace' },
}) as { fontFamily: string };

/**
 * Uppercase, widely tracked, dim monospace — the site's section heading treatment
 * (`font-mono text-xs tracking-widest text-arena-dim`).
 */
export const SectionLabelText = {
  ...Mono,
  color: Arena.dim,
  fontSize: 11,
  letterSpacing: 2,
} as const;

/** The footer line, and the app's voice: lowercase, technical, middot-separated. */
export const TAGLINE = 'deterministic robot combat · every match is reproducible';
