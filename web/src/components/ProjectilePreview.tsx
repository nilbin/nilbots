import type { CSSProperties } from 'react';
import type { ProjectileLook } from '../render/arenaThemes';

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
  const mask: CSSProperties = {
    backgroundColor: accent,
    maskImage: `url("${look.imageUrl}")`,
    WebkitMaskImage: `url("${look.imageUrl}")`,
    maskPosition: 'center',
    WebkitMaskPosition: 'center',
    maskRepeat: 'no-repeat',
    WebkitMaskRepeat: 'no-repeat',
    maskSize: 'contain',
    WebkitMaskSize: 'contain',
    filter: `drop-shadow(0 0 5px ${accent})`,
  };
  return <span aria-hidden className={`inline-block ${className}`} style={mask} />;
}
