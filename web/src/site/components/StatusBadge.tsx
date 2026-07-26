/** A build's state, in the colours the rest of the site uses for outcomes. */
export default function StatusBadge({ status }: { status: string }) {
  const color =
    status === 'Built'
      ? 'text-emerald-400'
      : status === 'Failed'
        ? 'text-red-400'
        : 'text-amber-300';
  return <span className={`font-mono text-[11px] ${color}`}>{status.toUpperCase()}</span>;
}
