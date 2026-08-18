import type { TacticalSheetCatalog } from '../api';
import type { SquadBot } from './tacticsDocument';

/** One squad bot as a draggable/selectable chip: name plus class. */
export function BotChip({ bot, catalog, armed, note, onClick, draggable, onDragStart, onDragEnd }: {
  bot: SquadBot;
  catalog: TacticalSheetCatalog;
  armed?: boolean;
  note?: string;
  onClick?: () => void;
  draggable?: boolean;
  onDragStart?: (event: React.DragEvent) => void;
  onDragEnd?: () => void;
}) {
  const entry = catalog.classes.find((value) => value.id === bot.class);
  return (
    <button
      type="button"
      onClick={onClick}
      draggable={draggable}
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      className={`flex min-h-8 items-center gap-1.5 rounded-sm border px-2 t-micro transition-colors ${armed
        ? 'border-arena-material bg-arena-material/15 text-arena-text'
        : 'border-arena-edge bg-arena-deep/40 text-arena-dim hover:text-arena-text'}`}
    >
      <span className="font-semibold text-arena-text">{bot.name}</span>
      <span className="text-arena-dim">{entry?.name ?? bot.class}</span>
      {note && <span className="text-arena-material">{note}</span>}
    </button>
  );
}
