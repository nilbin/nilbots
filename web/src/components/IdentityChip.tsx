import clsx from 'clsx';
import type { ReplayModel, ReplayStableUnitKey } from '../replayModel';
import { botLook } from '../render/arenaThemes';
import {
  participantForUnit,
  visualIndexForUnit,
} from '../replayParticipants';

/**
 * Who a bot is, in one chip: the chassis its owner chose, ringed in the accent its
 * owner chose.
 *
 * Those are two independent picks — `AppearanceEditor` offers a free colour input and a
 * separate look — and the interface used to show only the second, as a dot beside a
 * name. The chassis is the more distinctive of the two and it was going unused.
 *
 * The disc behind the ring is always the darkest ground, whatever surface this sits on.
 * That is not decoration: on the Forge palette a warm accent on a warm panel starts
 * reading as furniture, and a cold field underneath keeps a player's colour looking
 * like a choice.
 */
export default function IdentityChip({
  replay,
  unitKey,
  name,
  sub,
  size = 26,
  className,
}: {
  replay: ReplayModel;
  unitKey: ReplayStableUnitKey;
  name: string;
  sub?: string;
  size?: number;
  className?: string;
}) {
  const participant = participantForUnit(replay, unitKey);
  const look = botLook(
    participant?.lookId ?? undefined,
    visualIndexForUnit(replay, unitKey),
  );
  const accent = participant?.accent ?? 'var(--color-arena-dim)';

  return (
    <span className={clsx('flex min-w-0 items-center gap-2.5', className)}>
      <span
        className="flex shrink-0 items-center justify-center rounded-full bg-arena-bg p-0.5"
        style={{ border: `1.5px solid ${accent}` }}
      >
        {look.image ? (
          <img
            src={look.imageUrl}
            alt=""
            style={{ width: size, height: size }}
            className="object-contain"
          />
        ) : (
          <span style={{ width: size, height: size }} />
        )}
      </span>
      <span className="min-w-0">
        <span className="block truncate text-[14px] font-semibold tracking-[-0.005em] text-arena-text">
          {name}
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
