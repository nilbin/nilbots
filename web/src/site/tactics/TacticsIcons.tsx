export type TacticsIconName =
  | 'posts' | 'patrol' | 'observe' | 'hold' | 'aggressive'
  | 'march' | 'respond' | 'brawl'
  | 'fight-on' | 'heal-tile' | 'heal-up'
  | 'core' | 'vision' | 'attack-range' | 'transition' | 'add-phase'
  | 'finish' | 'signature' | 'route' | 'edit' | 'members' | 'close' | 'clear' | 'deselect'
  | 'undo' | 'redo'
  | 'rotate' | 'move' | 'formation' | 'layers' | 'save' | 'start' | 'abort';

const paths: Record<TacticsIconName, React.ReactNode> = {
  posts: <><circle cx="7" cy="7" r="2.2" /><circle cx="17" cy="7" r="2.2" /><circle cx="7" cy="17" r="2.2" /><circle cx="17" cy="17" r="2.2" /><path d="M9.5 7h5M9.5 17h5M7 9.5v5M17 9.5v5" /></>,
  patrol: <><path d="M5 7c2-3 5-4 8-3l4 2" /><path d="m15 3 3 3-4 2" /><path d="M19 17c-2 3-5 4-8 3l-4-2" /><path d="m9 21-3-3 4-2" /></>,
  observe: <><path d="M3 12s3.5-6 9-6 9 6 9 6-3.5 6-9 6-9-6-9-6Z" /><circle cx="12" cy="12" r="2.5" /></>,
  hold: <><path d="M5 4v16M19 4v16M5 7h14M5 17h14" /><path d="M9 7v10M15 7v10" /></>,
  aggressive: <><path d="m4 19 6-6M14 9l6-6M10 13l4-4" /><path d="m13 4 7-1-1 7M4 14v6h6" /></>,
  march: <><path d="M4 16h8M9 12l4 4-4 4" /><path d="M5 6h14M5 9h10" /></>,
  respond: <><path d="M4 12h11M11 8l4 4-4 4" /><path d="M20 7v10M17 9l3-2 3 2M17 15l3 2 3-2" /></>,
  brawl: <><path d="m5 18 5-5M14 9l5-5M9 4l10 10M5 8l11 11" /><path d="m4 5 3-1 1 3M16 17l1 3 3-1" /></>,
  'fight-on': <><path d="m6 19 12-14M5 6l13 13" /><path d="m4 4 4 1-3 3M20 20l-4-1 3-3" /></>,
  'heal-tile': <><path d="M5 5h14v14H5z" /><path d="M9 12h6M12 9v6" /></>,
  'heal-up': <><path d="M12 20s-7-4.5-7-10a4 4 0 0 1 7-2.5A4 4 0 0 1 19 10c0 5.5-7 10-7 10Z" /><path d="M9 13h6M12 10v6" /></>,
  core: <><circle cx="12" cy="12" r="6" /><circle cx="12" cy="12" r="2" /><path d="M12 2v4M12 18v4M2 12h4M18 12h4" /></>,
  vision: <><path d="M3 12s3.5-5.5 9-5.5 9 5.5 9 5.5-3.5 5.5-9 5.5S3 12 3 12Z" /><path d="M12 9v6M9 12h6" /></>,
  'attack-range': <><circle cx="12" cy="12" r="8" /><circle cx="12" cy="12" r="3" /><path d="M12 1v6M12 17v6M1 12h6M17 12h6" /></>,
  transition: <><path d="M3 12h15" /><path d="m14 7 5 5-5 5" /></>,
  'add-phase': <><path d="M4 6h10v12H4zM18 7v10M13 12h10" /></>,
  finish: <><path d="M5 4v16" /><path d="M6 5h12l-2.5 4L18 13H6" /></>,
  signature: <><path d="m12 2 2.2 6.2L21 10l-5 4.2.5 6.8-4.5-3.5L7.5 21l.5-6.8L3 10l6.8-1.8Z" /></>,
  route: <><circle cx="5" cy="18" r="2" /><circle cx="19" cy="6" r="2" /><path d="M7 18c7 0 3-10 10-11" /></>,
  edit: <><path d="m5 19 3.5-.8L19 7.7 16.3 5 5.8 15.5Z" /><path d="m14.8 6.5 2.7 2.7" /></>,
  members: <><circle cx="8" cy="8" r="3" /><circle cx="17" cy="9" r="2.5" /><path d="M3 20c.5-4 2-6 5-6s4.5 2 5 6M13 20c.3-3 1.5-4.8 4-4.8 2.2 0 3.4 1.6 4 4.8" /></>,
  close: <path d="m5 5 14 14M19 5 5 19" />,
  clear: <><path d="M4 7h16M9 4h6M7 7l1 13h8l1-13" /><path d="M10 10v7M14 10v7" /></>,
  deselect: <><path d="M8 4H4v4M16 4h4v4M4 16v4h4M20 16v4h-4" /><path d="m9 9 6 6M15 9l-6 6" /></>,
  undo: <><path d="M9 7 4 12l5 5" /><path d="M5 12h8a6 6 0 0 1 6 6" /></>,
  redo: <><path d="m15 7 5 5-5 5" /><path d="M19 12h-8a6 6 0 0 0-6 6" /></>,
  rotate: <><path d="M19 8a8 8 0 1 0 1 7" /><path d="m19 3v5h-5" /></>,
  move: <><path d="M12 2v20M2 12h20" /><path d="m8 6 4-4 4 4M8 18l4 4 4-4M6 8l-4 4 4 4M18 8l4 4-4 4" /></>,
  formation: <><circle cx="6" cy="7" r="2" /><circle cx="12" cy="12" r="2" /><circle cx="18" cy="7" r="2" /><path d="M6 9v8M18 9v8M12 14v6" /></>,
  layers: <><path d="m12 3 9 5-9 5-9-5Z" /><path d="m4 12 8 4 8-4M4 16l8 4 8-4" /></>,
  save: <><path d="M5 3h12l2 2v16H5z" /><path d="M8 3v6h8V3M8 21v-7h8v7" /></>,
  start: <><path d="M5 3h10v18H5z" /><path d="M9 12h11M16 8l4 4-4 4" /></>,
  abort: <><path d="M7 4h10l3 3v10l-3 3H7l-3-3V7Z" /><path d="m9 9 6 6M15 9l-6 6" /></>,
};

export function TacticsIcon({ name, size = 18, className }: {
  name: TacticsIconName;
  size?: number;
  className?: string;
}) {
  return <svg viewBox="0 0 24 24" width={size} height={size} aria-hidden="true"
    fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round"
    strokeLinejoin="round" className={className}>
    {paths[name]}
  </svg>;
}

export function TacticsIconButton({
  icon, label, pressed, configured, disabled, onClick, className = '', testId,
  contextTrigger = false,
}: {
  icon: TacticsIconName;
  label: string;
  pressed?: boolean;
  configured?: boolean;
  disabled?: boolean;
  onClick?: () => void;
  className?: string;
  testId?: string;
  contextTrigger?: boolean;
}) {
  return <button type="button" aria-label={label} title={label}
    aria-pressed={pressed} data-configured={configured || undefined}
    data-testid={testId} data-map-first-context-trigger={contextTrigger || undefined}
    disabled={disabled} onClick={onClick}
    className={`group relative inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-sm border transition-colors ${pressed
      ? 'border-arena-material bg-arena-material/15 text-arena-material'
      : configured
        ? 'border-arena-ok/55 bg-arena-ok/8 text-arena-ok'
        : 'border-arena-edge bg-arena-deep/30 text-arena-dim hover:border-arena-edge2 hover:text-arena-text'} disabled:cursor-not-allowed disabled:opacity-35 ${className}`}>
    <TacticsIcon name={icon} />
    {configured && !pressed && <span className="absolute right-0.5 top-0.5 h-1.5 w-1.5 rounded-full bg-arena-ok" />}
  </button>;
}

const audition: TacticsIconName[] = [
  'posts', 'patrol', 'observe', 'hold', 'aggressive', 'march', 'respond', 'brawl',
  'fight-on', 'heal-tile', 'heal-up', 'core', 'vision', 'attack-range', 'transition', 'finish',
  'add-phase', 'signature', 'route', 'edit', 'members', 'rotate', 'move', 'formation', 'layers',
  'save', 'start', 'abort', 'close', 'clear', 'deselect', 'undo', 'redo',
];

/** Development-only review fixture: the whole visual vocabulary in all
 * meaningful states, kept together so an icon cannot quietly drift. */
export function TacticsIconAudition() {
  return <div className="grid grid-cols-6 gap-2" data-testid="tactics-icon-audition">
    {audition.map((name, index) => <div key={name} className="flex flex-col items-center gap-1">
      <TacticsIconButton icon={name} label={name} pressed={index % 4 === 1}
        configured={index % 4 === 2} disabled={index % 4 === 3} />
      <span className="text-[8px] text-arena-dim">{name}</span>
    </div>)}
  </div>;
}
