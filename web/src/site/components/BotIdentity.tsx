import clsx from 'clsx';
import IdentityChip from '../../components/IdentityChip';

export type BotIdentitySize = 'xs' | 'sm' | 'md' | 'lg';

/** Chassis size in pixels per step, with the name size that reads beside it. */
const sizes: Record<BotIdentitySize, { chassis: number; name: string }> = {
  xs: { chassis: 18, name: 'text-sm' },
  sm: { chassis: 24, name: 'text-sm' },
  md: { chassis: 30, name: 'text-base' },
  lg: { chassis: 42, name: 'text-2xl' },
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
 *
 * The chip itself lives in `components/` so the viewer and the site render one identity
 * rather than two that drift; this keeps the site's own size vocabulary. The boundary
 * allows it in that direction only — the site may import the viewer's components, never
 * the reverse (see `structure.test.ts`).
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
  const step = sizes[size];
  return (
    <IdentityChip
      name={name}
      accent={accent}
      lookId={lookId}
      size={step.chassis}
      emphasized={emphasized}
      className={className}
      nameClassName={clsx(step.name, nameClassName)}
    />
  );
}
