import clsx from 'clsx';
import type { ProjectileLook } from '../render/arenaThemes';
import { playerAccent } from '../presentation/playerAccent';
import { styleVariables } from '../presentation/styleVariables';

interface ProjectilePreviewProps {
  look: ProjectileLook;
  accent: string;
  className?: string;
}

export default function ProjectilePreview({
  look,
  accent,
  className = 'size-10',
}: ProjectilePreviewProps) {
  const drawn = playerAccent(accent);
  return (
    <span
      aria-hidden
      className={clsx('projectile-preview inline-block', className)}
      style={styleVariables({
        '--player-accent': drawn,
        '--projectile-image': `url("${look.imageUrl}")`,
      })}
    />
  );
}
