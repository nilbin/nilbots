/** One line of drawn-geometry status: done tick, description, draw controls. */
export function GeoStatusRow({ complete, label, drawing, onDraw, onStop, onClear, accent, drawLabel }: {
  complete: boolean;
  label: string;
  drawing: boolean;
  onDraw?: () => void;
  onStop?: () => void;
  onClear?: () => void;
  accent?: string;
  drawLabel?: string;
}) {
  return (
    <div className="flex min-h-9 items-center gap-2 rounded-sm border border-arena-edge bg-arena-deep/30 px-2 py-1">
      <span
        className={complete ? 'font-semibold' : 'text-arena-dim'}
        style={complete ? { color: accent ?? 'var(--color-arena-ok)' } : undefined}
      >
        {complete ? '✓' : '◌'}
      </span>
      <span className="t-micro">{label}</span>
      <span className="grow" />
      {drawing
        ? <button type="button" className="btn btn-on" onClick={onStop}>Stop drawing</button>
        : onDraw && (
          <button type="button" className="btn btn-secondary" onClick={onDraw}>
            {complete ? 'Redraw' : drawLabel ?? 'Draw on map'}
          </button>
        )}
      {complete && onClear && (
        <button type="button" className="btn" onClick={onClear}>Clear</button>
      )}
    </div>
  );
}
