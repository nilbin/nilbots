import type { TacticalSheetCatalog } from '../api';
import { classSprite } from './classArt';
import { projectPhaseBots } from './botProjection';
import { condClauses } from './summary';
import {
  effectiveJobResolution, groupColor, phaseShade,
  type Tactic, type TacticsDocument,
} from './tacticsDocument';
import type { WizardStepId } from './wizardState';

export type MapFirstFocus =
  | { kind: 'start' }
  | { kind: 'abort' }
  | { kind: 'groups' }
  | { kind: 'phase'; phaseIndex: number }
  | { kind: 'transition'; phaseIndex: number }
  | { kind: 'group'; phaseIndex: number; groupId: string }
  | { kind: 'cores' };

function activeRows(cases: Array<{
  required?: Parameters<typeof condClauses>[0];
  triggers?: Parameters<typeof condClauses>[0];
  unless?: Parameters<typeof condClauses>[0];
}> | undefined): number {
  return (cases ?? []).reduce((total, entry) => total
    + condClauses(entry.required).length
    + condClauses(entry.triggers).length
    + condClauses(entry.unless).length, 0);
}

/** Tactic-wide controls sit above a real phase/transition chronology. Every
 * chip routes to the existing full editor; this component owns no condition
 * or transition mutations. */
export function MapFirstTimeline({ tactic, phaseIndex, step, onFocus }: {
  tactic: Tactic;
  phaseIndex: number;
  step: WizardStepId;
  onFocus: (focus: MapFirstFocus) => void;
}) {
  const startRows = activeRows(tactic.start.cases);
  const abortRows = activeRows(tactic.abort?.cases);
  return <div className="panel p-2" data-testid="map-first-timeline">
    <div className="mb-1.5 flex flex-wrap items-center gap-1.5 border-b border-arena-edge pb-1.5">
      <span className="lab mr-1">Whole tactic</span>
      <button type="button"
        className={`btn !min-h-7 ${step === 'start' ? 'btn-on' : ''}`}
        data-testid="map-first-start"
        onClick={() => onFocus({ kind: 'start' })}>
        ▶ Start · {tactic.start.mode === 'always' ? 'Always' : `${startRows} condition${startRows === 1 ? '' : 's'}`}
      </button>
      <button type="button"
        className={`btn !min-h-7 ${step === 'cores' ? 'btn-on' : ''}`}
        onClick={() => onFocus({ kind: 'cores' })}>
        ◐ Core plan
      </button>
      <span className="grow" />
      <button type="button"
        className={`btn !min-h-7 ${tactic.abort ? 'text-arena-hot' : 'text-arena-dim'}`}
        data-testid="map-first-abort"
        onClick={() => onFocus({ kind: 'abort' })}>
        ⨯ Abort · {tactic.abort ? `${abortRows} condition${abortRows === 1 ? '' : 's'}` : 'Not set'}
      </button>
    </div>
    <div className="flex min-w-0 items-stretch gap-1 overflow-x-auto pb-0.5"
      aria-label="Phase timeline">
      {tactic.phases.map((phase, index) => {
        const rows = activeRows(phase.continue);
        const active = step === 'phases' && phaseIndex === index;
        return <div key={phase.id} className="flex shrink-0 items-center gap-1">
          <button type="button"
            className={`flex min-w-[112px] items-center gap-2 rounded-sm border px-2 py-1.5 text-left ${active
              ? 'border-arena-material bg-arena-material/10'
              : 'border-arena-edge bg-arena-deep/35 hover:border-arena-edge2'}`}
            data-testid={`map-first-phase-${index + 1}`}
            onClick={() => onFocus({ kind: 'phase', phaseIndex: index })}>
            <span className="flex h-7 w-2 shrink-0 flex-col overflow-hidden rounded-[2px]">
              {tactic.groups.map((group) => <span key={group.id} className="grow"
                style={{ background: phaseShade(groupColor(tactic, group.id), index, tactic.phases.length) }} />)}
            </span>
            <span>
              <span className="block t-micro font-semibold uppercase tracking-wide">{index + 1} · {phase.name}</span>
              <span className="block text-[9.5px] text-arena-dim">{tactic.groups.length} Group{tactic.groups.length === 1 ? '' : 's'}</span>
            </span>
          </button>
          <button type="button"
            className="group flex min-w-[82px] flex-col items-center justify-center rounded-sm border border-arena-edge bg-arena-deep/20 px-2 py-1 text-[9.5px] text-arena-dim hover:border-[#c8763c88] hover:text-arena-text"
            data-testid={`map-first-transition-${index + 1}`}
            onClick={() => onFocus({ kind: 'transition', phaseIndex: index })}>
            <span className="font-semibold text-[#c8763c]">{index === tactic.phases.length - 1 ? 'FINISH' : 'CONTINUE'} →</span>
            <span>{rows ? `${phase.continue?.length ?? 0} case${phase.continue?.length === 1 ? '' : 's'} · ${rows} row${rows === 1 ? '' : 's'}` : index === tactic.phases.length - 1 ? 'holds forever' : 'set conditions'}</span>
          </button>
        </div>;
      })}
    </div>
  </div>;
}

/** Compact membership next to the map. Bots already represented on the map
 * collapse into honest readouts; an unposted bot remains a draggable token
 * which the real SVG accepts as an exact Post. Group-to-Group drops delegate
 * to the same membership-following mutation path as the classic editor. */
export function MapFirstGroupTrays({
  catalog, document, tactic, phaseIndex, selectedGroupId,
  onFocus, onMoveBot,
}: {
  catalog: TacticalSheetCatalog;
  document: TacticsDocument;
  tactic: Tactic;
  phaseIndex: number;
  selectedGroupId: string | null;
  onFocus: (focus: MapFirstFocus) => void;
  onMoveBot: (slot: number, groupId: string) => void;
}) {
  const projection = projectPhaseBots(catalog, document, tactic, phaseIndex);
  return <aside className="flex min-w-0 flex-col gap-1.5" data-testid="map-first-group-trays">
    <div className="flex items-center gap-1">
      <span className="lab">Groups</span>
      <span className="grow" />
      <button type="button" className="btn !min-h-6 !px-1.5 text-[9.5px]"
        onClick={() => onFocus({ kind: 'groups' })}>
        Edit membership
      </button>
    </div>
    {tactic.groups.map((group) => {
      const color = groupColor(tactic, group.id);
      const resolution = effectiveJobResolution(tactic, phaseIndex, group.id);
      const positions = projection.bots.filter((bot) => bot.groupId === group.id);
      const exact = new Set(positions.filter((bot) => bot.exact).map((bot) => bot.slot));
      const preview = new Set(positions.filter((bot) => !bot.exact).map((bot) => bot.slot));
      const posts = resolution?.job.kind === 'posts';
      const unresolved = projection.unresolvedGroupIds.includes(group.id);
      return <section key={group.id}
        className={`rounded-sm border bg-arena-deep/30 p-1.5 ${selectedGroupId === group.id
          ? 'border-arena-material/70' : 'border-arena-edge'}`}
        style={{ borderLeft: `3px solid ${color}` }}
        onDragOver={(event) => {
          if (event.dataTransfer.types.includes('application/x-nilbots-bot')) event.preventDefault();
        }}
        onDrop={(event) => {
          const raw = event.dataTransfer.getData('application/x-nilbots-bot');
          if (!raw) return;
          event.preventDefault();
          const slot = Number(raw);
          if (Number.isInteger(slot)) onMoveBot(slot, group.id);
        }}>
        <button type="button" className="mb-1 flex w-full items-center gap-1.5 text-left"
          onClick={() => onFocus({ kind: 'group', phaseIndex, groupId: group.id })}>
          <span className="t-micro min-w-0 grow truncate font-semibold">{group.name || 'Unnamed Group'}</span>
          <span className="val">{group.slots.length}</span>
        </button>
        <div className="flex flex-col gap-1">
          {group.slots.map((slot) => {
            const bot = document.squad[slot];
            if (!bot) return null;
            const placed = exact.has(slot) || preview.has(slot);
            const draggablePost = posts && !exact.has(slot);
            const state = exact.has(slot) ? 'On map'
              : preview.has(slot) ? 'Projected'
                : draggablePost ? 'Drag to map' : unresolved ? 'No preview' : 'Unplaced';
            return <button key={slot} type="button"
              draggable
              onDragStart={(event) => {
                event.dataTransfer.effectAllowed = 'move';
                event.dataTransfer.setData('application/x-nilbots-bot', String(slot));
                if (draggablePost) {
                  event.dataTransfer.setData('application/x-nilbots-post', JSON.stringify({
                    tacticId: tactic.id, phaseIndex, groupId: group.id, slot,
                  }));
                }
              }}
              onClick={() => onFocus({ kind: 'group', phaseIndex, groupId: group.id })}
              className={`flex min-h-8 items-center gap-1.5 rounded-sm border px-1.5 text-left ${draggablePost
                ? 'border-dashed border-arena-material/70 bg-arena-material/5'
                : placed ? 'border-transparent bg-arena-panel2/35' : 'border-arena-edge'}`}
              title={draggablePost
                ? `Drag ${bot.name} onto an exact Post tile`
                : `${bot.name} — ${state}`}>
              {classSprite(bot.class) && <img src={classSprite(bot.class)} alt=""
                className={`h-6 w-6 shrink-0 object-contain ${placed ? 'opacity-75' : ''}`} />}
              <span className="min-w-0 grow truncate text-[10.5px] font-semibold">{bot.name}</span>
              <span className={`text-[8.5px] uppercase tracking-wide ${draggablePost
                ? 'text-arena-material' : 'text-arena-dim'}`}>{state}</span>
            </button>;
          })}
        </div>
        {resolution?.inherited && <button type="button"
          className="mt-1 w-full text-left text-[9.5px] text-arena-material hover:underline"
          onClick={() => onFocus({
            kind: 'group', phaseIndex: resolution.sourcePhaseIndex, groupId: group.id,
          })}>
          Carried from {tactic.phases[resolution.sourcePhaseIndex]?.name} · edit source
        </button>}
        {unresolved && <p className="mt-1 text-[9.5px] text-arena-dim">
          This assignment has no honest position preview. Its saved behavior is unchanged.
        </p>}
      </section>;
    })}
    <p className="text-[9.5px] leading-snug text-arena-dim">
      <b className="text-arena-text">P</b> marks a stable preview. Preview bots are not draggable on the map.
    </p>
  </aside>;
}
