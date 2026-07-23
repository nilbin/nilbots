import { useEffect, useMemo, useRef } from 'react';
import clsx from 'clsx';
import type { GameEvent, ReplayDocument } from '../types';

export default function EventFeed({
  replay,
  tick,
}: {
  replay: ReplayDocument;
  tick: number;
}) {
  const feedRef = useRef<HTMLOListElement>(null);

  const entries = useMemo(() => {
    const list: { tick: number; event: GameEvent }[] = [];
    for (const t of replay.ticks) {
      if (t.tick > tick) break;
      for (const event of t.events) {
        if (event.type === 'Move' || event.type === 'Turn') continue; // Too chatty for a feed.
        list.push({ tick: t.tick, event });
      }
    }
    return list.slice(-80);
  }, [replay, tick]);

  useEffect(() => {
    // Scroll only the feed's own container — scrollIntoView walks every
    // scrollable ancestor, which on stacked (mobile) layouts yanked the whole
    // page down to the feed on every event, hiding the arena.
    const el = feedRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [entries.length]);

  const name = (slot: number | undefined) =>
    slot === undefined ? '?' : (replay.header.participants[slot]?.name ?? `slot ${slot}`);

  const describe = ({ event }: { event: GameEvent }): string => {
    switch (event.type) {
      case 'Shot':
        return event.hitSlot !== undefined && event.hitSlot !== null
          ? `${name(event.slot)} hits ${name(event.hitSlot)}`
          : `${name(event.slot)} fires and misses`;
      case 'Damage':
        return `${name(event.targetSlot)} takes ${event.amount} damage (${event.newHealth} hp left)`;
      case 'Destroyed':
        return `${name(event.slot)} is destroyed`;
      case 'MoveBlocked':
        return `${name(event.slot)} bumps into something`;
      case 'Fault':
        return `${name(event.slot)} runtime fault: ${event.message ?? ''}`;
      case 'Disqualified':
        return `${name(event.slot)} is disqualified`;
      default:
        return event.type;
    }
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col rounded-lg border border-arena-edge bg-arena-panel">
      <h2 className="border-b border-arena-edge px-3 py-2 font-mono text-xs tracking-widest text-arena-dim">
        EVENT FEED
      </h2>
      <ol
        ref={feedRef}
        className="max-h-56 min-h-0 flex-1 space-y-1 overflow-y-auto p-3 font-mono text-xs lg:max-h-none"
        aria-live="polite"
      >
        {entries.length === 0 && (
          <li className="text-arena-dim italic">No combat events yet…</li>
        )}
        {entries.map(({ tick: t, event }, index) => (
          <li
            key={index}
            className={clsx('flex gap-2', {
              'text-red-300': event.type === 'Destroyed' || event.type === 'Damage',
              'text-amber-300': event.type === 'Fault' || event.type === 'Disqualified',
              'text-arena-text':
                event.type === 'Shot' || event.type === 'MoveBlocked',
            })}
          >
            <span className="text-arena-dim">{String(t).padStart(3, '0')}</span>
            <span>{describe({ event })}</span>
          </li>
        ))}
      </ol>
    </div>
  );
}
