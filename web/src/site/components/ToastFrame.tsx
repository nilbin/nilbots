import clsx from 'clsx';
import type { ReactNode } from 'react';
import { playerAccent } from '../../presentation/playerAccent';
import { styleVariables } from '../../presentation/styleVariables';

export type ToastTone = 'ok' | 'hot' | 'neutral';

const TONE_CLASSES = {
  ok: { glyph: '▲', className: 'text-arena-ok' },
  hot: { glyph: '▼', className: 'text-arena-hot' },
  neutral: null,
} satisfies Record<
  ToastTone,
  { glyph: string; className: string } | null
>;

export default function ToastFrame({
  tone,
  eyebrow,
  title,
  artwork,
  children,
  action,
  queuedLabel,
  value,
  dismissLabel,
  onDismiss,
}: {
  tone: ToastTone;
  eyebrow: ReactNode;
  title: ReactNode;
  artwork: ReactNode;
  children?: ReactNode;
  action: ReactNode;
  queuedLabel?: string;
  value?: ReactNode;
  dismissLabel: string;
  onDismiss: () => void;
}) {
  const signal = TONE_CLASSES[tone];

  return (
    <aside
      className="unlock-toast panel fixed top-4 right-4 z-50 w-[min(28rem,calc(100vw-2rem))] overflow-hidden shadow-2xl"
      role="status"
      aria-live="polite"
      aria-atomic="true"
    >
      <div className="pointer-events-none absolute inset-x-0 top-0 h-px bg-linear-to-r from-transparent via-arena-edge2 to-transparent" />
      <div className="pad relative flex gap-3">
        {artwork}

        <div className="min-w-0 flex-1">
          <p className="lab">
            {signal && (
              <span className={signal.className} aria-hidden>
                {signal.glyph}{' '}
              </span>
            )}
            {eyebrow}
          </p>
          <h2 className="type-display mt-1 truncate text-lg leading-tight text-arena-text">
            {title}
          </h2>
          {children}
          {action}
          {queuedLabel && <p className="t-micro mt-2">{queuedLabel}</p>}
        </div>

        {value !== undefined && value !== null && (
          <p className="val self-start text-xl text-arena-text">{value}</p>
        )}

        <button
          type="button"
          onClick={onDismiss}
          aria-label={dismissLabel}
          className="btn grid size-7 shrink-0 place-items-center p-0 text-base text-arena-dim hover:text-arena-text focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-arena-text"
        >
          <span aria-hidden>×</span>
        </button>
      </div>
    </aside>
  );
}

export function ToastArtwork({
  accent,
  badge,
  children,
}: {
  accent?: string | null;
  badge?: ReactNode;
  children: ReactNode;
}) {
  const visibleAccent = accent ? playerAccent(accent, 'panel') : undefined;

  return (
    <div
      className={clsx(
        'relative flex size-20 shrink-0 items-center justify-center overflow-hidden rounded-[3px] border border-arena-edge bg-arena-raise sm:size-24',
        visibleAccent && 'player-accent-border',
      )}
      style={
        visibleAccent
          ? styleVariables({ '--player-accent': visibleAccent })
          : undefined
      }
    >
      {children}
      {badge && (
        <span className="absolute right-1.5 bottom-1.5 flex size-9 items-center justify-center rounded-[3px] border border-arena-edge bg-arena-bg">
          {badge}
        </span>
      )}
    </div>
  );
}
