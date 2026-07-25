import clsx from 'clsx';
import { botLook } from '../../render/arenaThemes';

export type BotIdentitySize = 'xs' | 'sm' | 'md' | 'lg';

const sizes: Record<
  BotIdentitySize,
  { frame: string; image: string; name: string; gap: string }
> = {
  xs: {
    frame: 'size-6 rounded-md',
    image: 'size-5',
    name: 'text-sm',
    gap: 'gap-1.5',
  },
  sm: {
    frame: 'size-8 rounded-md',
    image: 'size-7',
    name: 'text-sm',
    gap: 'gap-2',
  },
  md: {
    frame: 'size-10 rounded-lg',
    image: 'size-9',
    name: 'text-base',
    gap: 'gap-2.5',
  },
  lg: {
    frame: 'size-12 rounded-lg',
    image: 'size-11',
    name: 'text-2xl',
    gap: 'gap-3',
  },
};

interface BotIdentityProps {
  name?: string | null;
  accent?: string | null;
  lookId?: string | null;
  size?: BotIdentitySize;
  emphasized?: boolean;
  className?: string;
  nameClassName?: string;
}

/**
 * The consistent public identity for a bot: its selected chassis, accent, and name.
 * Match-history callers pass snapshot values so old fights keep their original look.
 */
export default function BotIdentity({
  name,
  accent,
  lookId,
  size = 'sm',
  emphasized = false,
  className,
  nameClassName,
}: BotIdentityProps) {
  const look = botLook(lookId ?? undefined);
  const classes = sizes[size];

  return (
    <span className={clsx('inline-flex min-w-0 items-center', classes.gap, className)}>
      <span
        className={clsx(
          'flex shrink-0 items-center justify-center border border-arena-edge bg-arena-panel',
          classes.frame,
        )}
        style={{ boxShadow: accent ? `inset 0 -2px 0 ${accent}66` : undefined }}
      >
        <img
          src={look.imageUrl}
          alt=""
          loading="lazy"
          className={clsx('object-contain', classes.image)}
        />
      </span>
      <span
        className={clsx(
          'truncate',
          classes.name,
          emphasized && 'font-bold',
          nameClassName,
        )}
      >
        {name ?? '?'}
      </span>
    </span>
  );
}
