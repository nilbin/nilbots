/** One line of drawn-geometry status: done tick, description, draw controls. */
export function GeoStatusRow({ complete, label, drawing, onDraw, onStop, onClear, drawLabel }: {
  complete: boolean;
  label: string;
  drawing: boolean;
  onDraw?: () => void;
  onStop?: () => void;
  onClear?: () => void;
  drawLabel?: string;
}) {
  return (
    <div className="flex min-h-9 items-center gap-2 rounded-sm border border-arena-edge bg-arena-deep/30 px-2 py-1">
      <span className={complete ? 'font-semibold text-arena-ok' : 'text-arena-material'}>
        {complete ? '✓' : '◌'}
      </span>
      <span className="t-micro">{label}</span>
      <span className="grow" />
      {drawing
        ? <button type="button" className="btn btn-on" onClick={onStop}>Stop drawing</button>
        : onDraw && (
          <button type="button" className="btn btn-secondary" onClick={onDraw}>
            {complete ? 'Edit on map' : drawLabel ?? 'Draw on map'}
          </button>
        )}
      {complete && onClear && (
        <button type="button" className="btn" onClick={onClear}>Clear</button>
      )}
    </div>
  );
}
