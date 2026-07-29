import clsx from 'clsx';
import { playerAccent } from '../presentation/playerAccent';
import { styleVariables } from '../presentation/styleVariables';

export type AccentRingSize = 18 | 22 | 24 | 26 | 30 | 42;

const sizeClasses: Record<AccentRingSize, string> = {
  18: 'size-[18px]',
  22: 'size-[22px]',
  24: 'size-6',
  26: 'size-[26px]',
  30: 'size-[30px]',
  42: 'size-[42px]',
};

/**
 * The shared chassis ground: player-selected colour around the darkest field.
 * Geometry lives here; the only runtime style value is the contrast-safe player colour.
 */
export default function AccentRing({
  accent,
  size,
  children,
  className,
}: {
  accent?: string | null;
  size: AccentRingSize;
  children?: React.ReactNode;
  className?: string;
}) {
  const ring = accent
    ? playerAccent(accent)
    : 'var(--color-arena-edge2)';

  return (
    <span
      className={clsx(
        'player-accent-border flex shrink-0 items-center justify-center rounded-full border-[1.5px] border-solid bg-arena-bg p-0.5',
        className,
      )}
      style={styleVariables({ '--player-accent': ring })}
    >
      <span className={clsx('flex items-center justify-center', sizeClasses[size])}>
        {children}
      </span>
    </span>
  );
}
