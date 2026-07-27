/** A build's state, in the colours the rest of the site uses for outcomes. */
export default function StatusBadge({ status }: { status: string }) {
  const color =
    status === 'Built'
      ? 'text-arena-ok'
      : status === 'Failed'
        ? 'text-arena-hot'
        : 'text-arena-accent';
  return <span className={`font-mono text-[11px] ${color}`}>{status.toUpperCase()}</span>;
}
