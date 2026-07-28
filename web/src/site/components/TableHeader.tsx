import clsx from 'clsx';

/** One heading treatment for every comparison table in the site. */
export default function TableHeader({
  children,
  className,
  numeric = false,
}: {
  children: React.ReactNode;
  className?: string;
  numeric?: boolean;
}) {
  return (
    <th
      scope="col"
      className={clsx(
        'lab border-b border-arena-edge px-2 pb-2',
        numeric ? 'text-right' : 'text-left',
        className,
      )}
    >
      {children}
    </th>
  );
}
