import clsx from 'clsx';

export function LiveDot({ className }: { className?: string }) {
  return (
    <span
      aria-hidden
      className={clsx(
        'inline-block size-1.5 shrink-0 animate-pulse rounded-full bg-current motion-reduce:animate-none',
        className,
      )}
    />
  );
}

/** One achromatic, motion-safe broadcast status across site and standalone viewer. */
export default function LiveStatus({
  label = 'Live',
  className,
}: {
  label?: string;
  className?: string;
}) {
  return (
    <span
      className={clsx(
        'pill inline-flex items-center gap-1.5 text-arena-text',
        className,
      )}
    >
      <LiveDot />
      {label}
    </span>
  );
}
