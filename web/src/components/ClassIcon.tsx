import clsx from 'clsx';
import { classIconLook } from '../render/arenaThemes';

export default function ClassIcon({
  classId,
  label,
  accent,
  size = 24,
  framed = true,
  decorative = false,
  className,
}: {
  classId: string;
  label?: string;
  accent?: string;
  size?: number;
  framed?: boolean;
  decorative?: boolean;
  className?: string;
}) {
  const look = classIconLook(classId);
  const accessibleLabel = label ?? look?.label ?? classId;
  return (
    <span
      role={decorative ? undefined : 'img'}
      aria-hidden={decorative || undefined}
      aria-label={decorative ? undefined : accessibleLabel}
      title={decorative ? undefined : accessibleLabel}
      className={clsx(
        'inline-flex shrink-0 items-center justify-center overflow-hidden',
        framed && 'rounded-sm border border-white/10 bg-black/25',
        className,
      )}
      style={{
        width: size,
        height: size,
        borderColor: framed && accent ? `${accent}88` : undefined,
        boxShadow: framed && accent ? `0 0 8px ${accent}24` : undefined,
      }}
    >
      {look ? (
        <img
          src={look.imageUrl}
          alt=""
          draggable={false}
          className="size-full object-contain drop-shadow-[0_1px_2px_rgba(0,0,0,.75)]"
        />
      ) : (
        <span className="font-mono text-[9px] uppercase text-arena-material">
          {classId.slice(0, 2)}
        </span>
      )}
    </span>
  );
}
