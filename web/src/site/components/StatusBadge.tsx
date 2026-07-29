/** A machine-written build state. Colour is reserved for a completed pass or stop. */
export default function StatusBadge({ status }: { status: string }) {
  const color =
    status === 'Built'
      ? 'text-arena-ok'
      : status === 'Failed'
        ? 'text-arena-hot'
        : 'text-arena-dim';
  return <span className={`val uppercase ${color}`}>{status}</span>;
}
