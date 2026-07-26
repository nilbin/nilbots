/**
 * Loading, error and empty, in one place so pages stop re-inventing them.
 *
 * The failure this exists to prevent is specific and silent: pages model loading as
 * `data === null`, so a rejected request leaves that null forever and the page sits on
 * "Loading…" with no indication anything went wrong. An error state has to be reachable,
 * and it has to offer a way out.
 *
 * Every page that fetches should render all four states — loading, error, empty, content.
 * "No ranked sets yet" is intent; a blank panel is a bug the reader has to diagnose.
 */

export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <p className="py-8 text-center font-mono text-sm text-arena-dim" role="status">
      {label}
    </p>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  // The message, not just "something went wrong": on this app the useful case is a dead
  // dev server or a 404 for a mistyped id, and both are worth naming.
  const message = error instanceof Error ? error.message : String(error);
  return (
    <div className="py-8 text-center" role="alert">
      <p className="text-sm text-red-300">{message}</p>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-3 rounded-md border border-arena-edge px-3 py-1.5 font-mono text-xs text-arena-accent transition-colors hover:border-arena-accent"
        >
          Try again
        </button>
      )}
    </div>
  );
}

export function EmptyState({ title, detail }: { title: string; detail?: string }) {
  return (
    <div className="py-10 text-center">
      <p className="text-sm font-semibold text-slate-300">{title}</p>
      {detail && <p className="mt-1 text-xs text-arena-dim">{detail}</p>}
    </div>
  );
}
