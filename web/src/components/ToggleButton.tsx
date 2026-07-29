import clsx from 'clsx';

/** Shared pressed/unpressed control for compact filters, views, and sorts. */
export default function ToggleButton({
  children,
  pressed,
  disabled = false,
  describedBy,
  description,
  className,
  onClick,
}: {
  children: React.ReactNode;
  pressed: boolean;
  disabled?: boolean;
  describedBy?: string;
  description?: string;
  className?: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      aria-describedby={describedBy}
      aria-pressed={pressed}
      className={clsx('btn', pressed && 'btn-on', className)}
    >
      {children}
      {description && <span className="sr-only">. {description}</span>}
    </button>
  );
}
