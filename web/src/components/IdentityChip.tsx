import clsx from 'clsx';
import { botLook } from '../render/arenaThemes';
import AccentRing, { type AccentRingSize } from './AccentRing';

export type IdentityChipSize = AccentRingSize;

/**
 * Who a bot is, in one chip: the chassis its owner chose, ringed in the accent its
 * owner chose.
 *
 * Those are two independent picks — `AppearanceEditor` offers a free colour input and a
 * separate look — and the interface used to show only the second, as an inset shadow
 * under a square frame. The chassis is the more distinctive of the two and it was going
 * half-used.
 *
 * The disc behind the ring is always the darkest ground, whatever surface this sits on.
 * That is not decoration: on the Forge palette a warm accent on a warm panel starts
 * reading as furniture, and a cold field underneath keeps a player's colour looking
 * like a choice.
 *
 * Deliberately presentational and in `components/`, not `site/`: the viewer resolves a
 * bot from a `ReplayModel` and the site resolves one from the API, so the shared thing
 * is the chip rather than the lookup. It lives on the viewer side because the boundary
 * only runs one way — the site may import this, the viewer may not import the site.
 */
export default function IdentityChip({
  name,
  accent,
  lookId,
  visualIndex,
  sub,
  size = 26,
  emphasized = false,
  className,
  nameClassName,
}: {
  name?: string | null;
  accent?: string | null;
  lookId?: string | null;
  /** Falls back to a deterministic look when a bot has not chosen one. */
  visualIndex?: number;
  sub?: string;
  /** Chassis size in pixels; the ring and gap scale from it. */
  size?: IdentityChipSize;
  emphasized?: boolean;
  className?: string;
  nameClassName?: string;
}) {
  const look = botLook(lookId ?? undefined, visualIndex);
  return (
    <span className={clsx('inline-flex min-w-0 items-center gap-2.5', className)}>
      <AccentRing accent={accent} size={size}>
        {look.image ? (
          <img
            src={look.imageUrl}
            alt=""
            loading="lazy"
            className="size-full object-contain"
          />
        ) : null}
      </AccentRing>
      <span className="min-w-0">
        <span
          className={clsx(
            'block truncate tracking-[-0.005em] text-arena-text',
            emphasized ? 'font-bold' : 'font-semibold',
            nameClassName,
          )}
        >
          {name ?? '?'}
        </span>
        {sub && (
          <span className="block truncate text-[11.5px] tracking-[0.04em] text-arena-dim [font-stretch:84%]">
            {sub}
          </span>
        )}
      </span>
    </span>
  );
}
