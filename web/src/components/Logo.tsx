/**
 * The nilbots wordmark and mark.
 *
 * Not set in a typeface — constructed on the arena's own tile grid. Letters are
 * polylines through tile centres (right angles only, because that is how a bot moves),
 * rasterised to tiles, traced, and chamfered at the radius `render3d/wallSolids.ts`
 * mills wall corners with. The generator is `docs/design/logotype.py`; this file holds
 * its output, so regenerate rather than hand-edit the path.
 *
 * One path, `currentColor`, no gradient and no second colour. That matters beyond
 * tidiness: accent is a player's pick from a free colour input, so the mark cannot
 * reserve a hue. It takes whatever the surface gives it.
 */

/** The wordmark: 0 0 336 84, seven tile rows, stroke one tile. */
const WORDMARK = 'M0 27.84 A3.84 3.84 0 0 1 3.84 24 L44.16 24 A3.84 3.84 0 0 1 48 27.84 L48 80.16 A3.84 3.84 0 0 1 44.16 84 L39.84 84 A3.84 3.84 0 0 1 36 80.16 L36 39.84 A3.84 3.84 0 0 0 32.16 36 L15.84 36 A3.84 3.84 0 0 0 12 39.84 L12 80.16 A3.84 3.84 0 0 1 8.16 84 L3.84 84 A3.84 3.84 0 0 1 0 80.16 Z M60 3.84 A3.84 3.84 0 0 1 63.84 0 L68.16 0 A3.84 3.84 0 0 1 72 3.84 L72 8.16 A3.84 3.84 0 0 1 68.16 12 L63.84 12 A3.84 3.84 0 0 1 60 8.16 Z M60 27.84 A3.84 3.84 0 0 1 63.84 24 L68.16 24 A3.84 3.84 0 0 1 72 27.84 L72 80.16 A3.84 3.84 0 0 1 68.16 84 L63.84 84 A3.84 3.84 0 0 1 60 80.16 Z M84 3.84 A3.84 3.84 0 0 1 87.84 0 L92.16 0 A3.84 3.84 0 0 1 96 3.84 L96 80.16 A3.84 3.84 0 0 1 92.16 84 L87.84 84 A3.84 3.84 0 0 1 84 80.16 Z M108 3.84 A3.84 3.84 0 0 1 111.84 0 L116.16 0 A3.84 3.84 0 0 1 120 3.84 L120 20.16 A3.84 3.84 0 0 0 123.84 24 L152.16 24 A3.84 3.84 0 0 1 156 27.84 L156 80.16 A3.84 3.84 0 0 1 152.16 84 L111.84 84 A3.84 3.84 0 0 1 108 80.16 Z M123.84 36 A3.84 3.84 0 0 0 120 39.84 L120 68.16 A3.84 3.84 0 0 0 123.84 72 L140.16 72 A3.84 3.84 0 0 0 144 68.16 L144 39.84 A3.84 3.84 0 0 0 140.16 36 Z M168 27.84 A3.84 3.84 0 0 1 171.84 24 L212.16 24 A3.84 3.84 0 0 1 216 27.84 L216 80.16 A3.84 3.84 0 0 1 212.16 84 L171.84 84 A3.84 3.84 0 0 1 168 80.16 Z M183.84 36 A3.84 3.84 0 0 0 180 39.84 L180 68.16 A3.84 3.84 0 0 0 183.84 72 L200.16 72 A3.84 3.84 0 0 0 204 68.16 L204 39.84 A3.84 3.84 0 0 0 200.16 36 Z M228 27.84 A3.84 3.84 0 0 1 231.84 24 L236.16 24 A3.84 3.84 0 0 0 240 20.16 L240 15.84 A3.84 3.84 0 0 1 243.84 12 L248.16 12 A3.84 3.84 0 0 1 252 15.84 L252 20.16 A3.84 3.84 0 0 0 255.84 24 L272.16 24 A3.84 3.84 0 0 1 276 27.84 L276 32.16 A3.84 3.84 0 0 1 272.16 36 L255.84 36 A3.84 3.84 0 0 0 252 39.84 L252 68.16 A3.84 3.84 0 0 0 255.84 72 L260.16 72 A3.84 3.84 0 0 1 264 75.84 L264 80.16 A3.84 3.84 0 0 1 260.16 84 L243.84 84 A3.84 3.84 0 0 1 240 80.16 L240 39.84 A3.84 3.84 0 0 0 236.16 36 L231.84 36 A3.84 3.84 0 0 1 228 32.16 Z M288 27.84 A3.84 3.84 0 0 1 291.84 24 L332.16 24 A3.84 3.84 0 0 1 336 27.84 L336 32.16 A3.84 3.84 0 0 1 332.16 36 L303.84 36 A3.84 3.84 0 0 0 300 39.84 L300 44.16 A3.84 3.84 0 0 0 303.84 48 L332.16 48 A3.84 3.84 0 0 1 336 51.84 L336 80.16 A3.84 3.84 0 0 1 332.16 84 L291.84 84 A3.84 3.84 0 0 1 288 80.16 L288 75.84 A3.84 3.84 0 0 1 291.84 72 L320.16 72 A3.84 3.84 0 0 0 324 68.16 L324 63.84 A3.84 3.84 0 0 0 320.16 60 L291.84 60 A3.84 3.84 0 0 1 288 56.16 Z';

/** The mark: an n cut through a wall block, which is also a U-turn corridor. Square,
 *  and inset far enough to survive iOS's own corner mask. */
const MARK = 'M-24 9.84 A3.84 3.84 0 0 1 -20.16 6 L68.16 6 A3.84 3.84 0 0 1 72 9.84 L72 98.16 A3.84 3.84 0 0 1 68.16 102 L-20.16 102 A3.84 3.84 0 0 1 -24 98.16 Z M0 27.84 A3.84 3.84 0 0 1 3.84 24 L44.16 24 A3.84 3.84 0 0 1 48 27.84 L48 80.16 A3.84 3.84 0 0 1 44.16 84 L39.84 84 A3.84 3.84 0 0 1 36 80.16 L36 39.84 A3.84 3.84 0 0 0 32.16 36 L15.84 36 A3.84 3.84 0 0 0 12 39.84 L12 80.16 A3.84 3.84 0 0 1 8.16 84 L3.84 84 A3.84 3.84 0 0 1 0 80.16 Z';

export default function Logo({ size = 22 }: { size?: number }) {
  return (
    <svg
      height={size}
      viewBox="0 0 336 84"
      fill="none"
      role="img"
      aria-label="nilbots"
      className="block w-auto"
    >
      <path d={WORDMARK} fill="currentColor" fillRule="evenodd" />
    </svg>
  );
}

/** The mark alone, for anywhere square: avatar, tile, tab. */
export function LogoMark({ size = 24 }: { size?: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="-24 6 96 96"
      fill="none"
      role="img"
      aria-label="nilbots"
    >
      <path d={MARK} fill="currentColor" fillRule="evenodd" />
    </svg>
  );
}
