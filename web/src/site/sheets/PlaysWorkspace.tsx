import { useEffect, useMemo, useState } from 'react';
import clsx from 'clsx';
import ClassIcon from '../../components/ClassIcon';
import type { TacticalSheetCatalog } from '../api';
import IdentifierInput from './IdentifierInput';
import { ProjectionOrdersComposer } from './OrdersWorkspace';
import PlayMapCanvas from './PlayMapCanvas';
import {
  deletePlay,
  detachPlay,
  playDrifts,
  projectionPriorityOptions,
  resolveProjectionOrders,
  uniquePlayId,
} from './linkedPlays';
import {
  movePlayAnchor,
  rematerializeDraft,
  renamePlayGeometry,
  replacePlayRole,
  setPlayLinkedValue,
  setPlayName,
  setPlayProjectionPlacement,
  updatePlayCondition,
  updatePlayProjectionOrders,
} from './playEditing';
import {
  movePlayGeometryGroup,
  playGeometryIssues,
  setPlaySlotTolerance,
} from './playGeometry';
import {
  addBlankPlay,
  addBlankPlayProjection,
  addNorthAmbushPlay,
  addRelayChainPlay,
} from './playRecipes';
import type {
  ConditionDefinition,
  OrderNode,
  SheetDraft,
  SheetIssue,
} from './sheetAuthoring';
import type { UnitTrack } from './sheetTrials';
import type {
  LinkedPlay,
  PlayGeometryKind,
  PlayProjection,
  PlayRecipe,
} from './workshopTypes';

type RecipeChoice = Extract<PlayRecipe, 'blank' | 'ambush' | 'relay-chain'>;

const RECIPE_COPY: Record<RecipeChoice, { title: string; kicker: string; description: string }> = {
  blank: {
    title: 'Blank Play',
    kicker: 'Start exact',
    description: 'Add role projections and ordinary Orders without a recipe lens.',
  },
  ambush: {
    title: 'Corridor ambush',
    kicker: 'Five required positions',
    description: 'Mason, Longshot, Hush and a two-body reserve assemble before a linked fight.',
  },
  'relay-chain': {
    title: 'Relay chain',
    kicker: 'Two alternating carriers',
    description: 'Two one-body Relays move and Arc Toss along one linked corridor.',
  },
};

export default function PlaysWorkspace({
  catalog,
  draft,
  issues,
  tracks,
  selectedPlayId,
  onSelectPlay,
  onChange,
  onOpenSquad,
  onOpenOrders,
}: {
  catalog: TacticalSheetCatalog;
  draft: SheetDraft;
  issues: SheetIssue[];
  tracks: UnitTrack[];
  selectedPlayId: string | null;
  onSelectPlay: (playId: string | null) => void;
  onChange: (next: SheetDraft) => void;
  onOpenSquad: () => void;
  onOpenOrders: (playId: string, projectionId?: string) => void;
}) {
  const [creating, setCreating] = useState<RecipeChoice | null>(null);
  const [selectedGeometry, setSelectedGeometry] = useState<string | null>(null);
  const [side, setSide] = useState<'west' | 'east'>('west');
  const [createError, setCreateError] = useState<string | null>(null);
  const [removeSources, setRemoveSources] = useState(true);
  const plays = draft.workshop.plays;
  const play = plays.find((entry) => entry.id === selectedPlayId) ?? plays[0] ?? null;

  useEffect(() => {
    if (play && selectedPlayId !== play.id) onSelectPlay(play.id);
    if (!play && selectedPlayId) onSelectPlay(null);
  }, [onSelectPlay, play, selectedPlayId]);

  const playIssues = useMemo(() => play ? [
    ...playGeometryIssues(draft, play.id),
    ...playDrifts(draft.orders, draft.workshop, play.id).map((drift): SheetIssue => ({
      path: `workshop.plays.${play.id}.${drift.projectionId}`,
      severity: 'error',
      message: drift.message,
    })),
    ...issues.filter((issue) => issue.path.includes(play.id)),
  ] : [], [draft, issues, play]);

  const commit = (next: SheetDraft) => {
    onChange(next);
    setCreateError(null);
  };

  return <section className="grid min-w-0 gap-3" data-sheet-path="workshop.plays">
    <header className="panel panel-feature pad overflow-hidden">
      <div className="flex flex-wrap items-start gap-3">
        <div className="mr-auto max-w-3xl">
          <p className="lab text-arena-material">Linked Plays</p>
          <h2 className="type-display mt-1 text-[24px]">Coordinate ordinary Orders on the map</h2>
          <p className="t-meta mt-1">A Play links map geometry and role projections for editing. Matches still execute only the exact Orders shown in the Orders workspace.</p>
        </div>
        <button type="button" className="btn btn-secondary min-h-11" data-testid="new-play"
          onClick={() => setCreating(creating ? null : 'ambush')}>＋ New Play</button>
      </div>
      <div className="mt-4 flex gap-2 overflow-x-auto pb-1" aria-label="Linked Plays">
        {plays.map((entry) => <button key={entry.id} type="button"
          className={clsx('min-h-16 min-w-[210px] rounded-sm border p-2.5 text-left',
            entry.id === play?.id ? 'border-arena-material/70 bg-arena-material/10' : 'border-arena-edge bg-arena-deep/30')}
          onClick={() => { onSelectPlay(entry.id); setSelectedGeometry(entry.ownedGeometry[0]?.id ?? null); }}>
          <span className="lab text-arena-material">{recipeName(entry.recipe)}</span>
          <b className="type-display mt-1 block truncate text-[16px]">{entry.name}</b>
          <span className="t-micro mt-1 block">{entry.projections.length} role {entry.projections.length === 1 ? 'projection' : 'projections'} · {entry.ownedGeometry.length} map objects</span>
        </button>)}
        {plays.length === 0 && <div className="rounded-sm border border-dashed border-arena-edge px-4 py-3 t-meta">No linked Plays yet. Ordinary Orders remain fully usable.</div>}
      </div>
    </header>

    {creating && <CreatePlayPanel recipe={creating} draft={draft}
      error={createError} onRecipe={setCreating} onOpenSquad={onOpenSquad}
      onCancel={() => { setCreating(null); setCreateError(null); }}
      onCreate={(next, playId) => { commit(next); onSelectPlay(playId); setCreating(null); }} />}

    {play && <>
      <div className="grid min-w-0 gap-3 xl:grid-cols-[minmax(0,1.55fr)_minmax(360px,.85fr)]">
        <section className="panel pad min-w-0 xl:sticky xl:top-[76px] xl:h-fit">
          <div className="mb-3 flex flex-wrap items-center gap-2">
            <div className="mr-auto">
              <p className="lab text-arena-material">Mark the ground</p>
              <p className="t-micro mt-1">Tap an owned object for its exact purpose. Drag west-side anchors; derived zones follow.</p>
            </div>
            <div className="flex rounded-sm border border-arena-edge p-1" aria-label="Team-side preview">
              {(['west', 'east'] as const).map((value) => <button key={value} type="button"
                className={clsx('btn min-h-11', side === value && 'btn-on')}
                aria-pressed={side === value} onClick={() => setSide(value)}>{value}</button>)}
            </div>
          </div>
          <div className="overflow-auto">
            <PlayMapCanvas catalog={catalog} mapPlan={draft.mapPlan} play={play} side={side}
              selectedId={selectedGeometry} tracks={tracks}
              onSelect={setSelectedGeometry}
              onMoveAnchor={(anchorId, point) => commit(movePlayAnchor(draft, play.id, anchorId, point))} />
          </div>
          <GeometryControls catalog={catalog} draft={draft} play={play}
            selectedId={selectedGeometry} onSelect={setSelectedGeometry} onChange={commit} />
        </section>

        <div className="min-w-0 space-y-3">
          <section className="panel pad">
            <p className="lab text-arena-material">{recipeName(play.recipe)}</p>
            <label className="mt-2 block"><span className="lab mb-1 block">Play name</span>
              <input className="field min-h-12 text-[17px] font-semibold" value={play.name} maxLength={60}
                onChange={(event) => commit(setPlayName(draft, play.id, event.target.value))} /></label>
            <p className="t-micro mt-2">Editor linkage only · no Play state or global Play priority exists at runtime.</p>
          </section>

          {playIssues.length > 0 && <section className="rounded-sm border border-arena-hot/55 bg-arena-hot/8 p-3" role="alert">
            <p className="lab text-arena-hot">Needs attention</p>
            <ul className="mt-2 space-y-1 t-micro">{playIssues.map((issue, index) => <li key={`${issue.path}:${index}`}>{issue.message}</li>)}</ul>
            {playDrifts(draft.orders, draft.workshop, play.id).length > 0 && <div className="mt-3 flex flex-wrap gap-2">
              <button type="button" className="btn btn-secondary min-h-11" onClick={() => commit(rematerializeDraft(draft, true).draft)}>Regenerate from Play</button>
              <button type="button" className="btn min-h-11" onClick={() => {
                const next = structuredClone(draft);
                next.workshop = detachPlay(next.workshop, play.id);
                commit(next);
              }}>Detach to ordinary Orders</button>
            </div>}
          </section>}

          <ParticipantControls draft={draft} play={play} catalog={catalog} issues={issues}
            onSelectGeometry={setSelectedGeometry} onChange={commit} />
          <LinkedOrdersControls draft={draft} play={play} onChange={commit} />
          <PriorityControls draft={draft} play={play} onChange={commit} onOpenOrders={onOpenOrders} />
          <ConditionControls draft={draft} play={play} onChange={commit} />
          <GeneratedOrders play={play} onOpenOrders={onOpenOrders} />

          <section className="panel pad">
            <p className="lab">Ownership</p>
            <p className="t-micro mt-1">Detach keeps the current Orders and map plan but removes linkage. Delete previews and removes only this Play’s generated roots.</p>
            <label className="mt-3 flex min-h-11 items-center gap-2 rounded-sm border border-arena-edge px-3 t-body">
              <input type="checkbox" checked={removeSources} onChange={(event) => setRemoveSources(event.target.checked)} />
              Also remove unused Play-owned conditions, capacities and geometry
            </label>
            <div className="mt-3 grid gap-2 sm:grid-cols-2">
              <button type="button" className="btn min-h-11" onClick={() => {
                const next = structuredClone(draft);
                next.workshop = detachPlay(next.workshop, play.id);
                commit(next);
              }}>Detach Play</button>
              <button type="button" className="btn min-h-11 text-arena-hot" onClick={() => {
                const roots = play.projections.flatMap((projection) => projection.lastGenerated.map((entry) => entry.orderId));
                if (!window.confirm(`Delete “${play.name}” and ${roots.length} generated root Orders?`)) return;
                const deleted = deletePlay(draft, play.id, removeSources);
                commit(deleted.draft);
                onSelectPlay(null);
              }}>Delete Play…</button>
            </div>
          </section>
        </div>
      </div>
    </>}
  </section>;
}

function CreatePlayPanel({ recipe, draft, error, onRecipe, onCreate, onCancel, onOpenSquad }: {
  recipe: RecipeChoice;
  draft: SheetDraft;
  error: string | null;
  onRecipe: (recipe: RecipeChoice) => void;
  onCreate: (draft: SheetDraft, playId: string) => void;
  onCancel: () => void;
  onOpenSquad: () => void;
}) {
  const rolesFor = (classId: string, bodies = 1) => draft.orders.roles.filter((role) =>
    role.classes.length === bodies && role.classes.every((entry) => entry === classId));
  const mason = rolesFor('mason');
  const longshot = rolesFor('longshot');
  const hush = rolesFor('hush');
  const reserves = draft.orders.roles.filter((role) => role.classes.length >= 2);
  const relays = rolesFor('relay');
  const [masonRoleId, setMason] = useState(mason[0]?.id ?? '');
  const [longshotRoleId, setLongshot] = useState(longshot[0]?.id ?? '');
  const [hushRoleId, setHush] = useState(hush[0]?.id ?? '');
  const [reserveRoleId, setReserve] = useState(reserves[0]?.id ?? '');
  const [relayARoleId, setRelayA] = useState(relays[0]?.id ?? '');
  const [relayBRoleId, setRelayB] = useState(relays[1]?.id ?? '');
  const ready = recipe === 'blank'
    || recipe === 'ambush' && masonRoleId && longshotRoleId && hushRoleId && reserveRoleId
    || recipe === 'relay-chain' && relayARoleId && relayBRoleId && relayARoleId !== relayBRoleId;

  const create = () => {
    try {
      let next: SheetDraft;
      if (recipe === 'ambush') next = addNorthAmbushPlay(draft, { masonRoleId, longshotRoleId, hushRoleId, reserveRoleId });
      else if (recipe === 'relay-chain') next = addRelayChainPlay(draft, { relayARoleId, relayBRoleId });
      else next = addBlankPlay(draft);
      onCreate(next, next.workshop.plays.at(-1)!.id);
    } catch (cause) {
      // Parent error text is intentionally not used here because recipe role
      // eligibility is visible inline. Preserve the draft on any exception.
      window.alert(cause instanceof Error ? cause.message : 'Could not create this Play.');
    }
  };

  return <section className="panel pad" data-testid="create-play-panel">
    <div className="flex items-start gap-2"><div className="mr-auto"><p className="lab text-arena-material">New linked Play</p><h3 className="type-display mt-1 text-[20px]">Choose an authoring lens</h3></div>
      <button type="button" className="btn min-h-11" onClick={onCancel}>Cancel</button></div>
    <div className="mt-3 grid gap-2 md:grid-cols-3">{(Object.keys(RECIPE_COPY) as RecipeChoice[]).map((candidate) => {
      const copy = RECIPE_COPY[candidate];
      return <button key={candidate} type="button" className="choice-card min-h-28 text-left"
        data-selected={candidate === recipe} onClick={() => onRecipe(candidate)}>
        <span className="lab text-arena-material">{copy.kicker}</span><b className="type-display mt-1 block text-[17px]">{copy.title}</b>
        <span className="t-micro mt-1 block">{copy.description}</span>
      </button>;
    })}</div>
    {recipe === 'ambush' && <div className="mt-4 grid gap-2 sm:grid-cols-2">
      <RoleChoice label="Mason role" value={masonRoleId} roles={mason} onChange={setMason} />
      <RoleChoice label="Longshot role" value={longshotRoleId} roles={longshot} onChange={setLongshot} />
      <RoleChoice label="Hush role" value={hushRoleId} roles={hush} onChange={setHush} />
      <RoleChoice label="Two-body reserve role" value={reserveRoleId} roles={reserves} onChange={setReserve} />
    </div>}
    {recipe === 'relay-chain' && <div className="mt-4 grid gap-2 sm:grid-cols-2">
      <RoleChoice label="Relay A role" value={relayARoleId} roles={relays} onChange={setRelayA} />
      <RoleChoice label="Relay B role" value={relayBRoleId} roles={relays.filter((role) => role.id !== relayARoleId)} onChange={setRelayB} />
    </div>}
    {!ready && <div className="mt-3 rounded-sm border border-arena-material/40 bg-arena-material/10 p-3">
      <p className="t-body">This recipe needs eligible roles in the Squad first.</p>
      <p className="t-micro mt-1">Roles are not invented or silently split by a Play.</p>
      <button type="button" className="btn btn-secondary mt-2 min-h-11" onClick={onOpenSquad}>Open Squad</button>
    </div>}
    {error && <p className="mt-3 t-micro text-arena-hot">{error}</p>}
    <button type="button" className="btn btn-primary mt-4 min-h-12 w-full" disabled={!ready} onClick={create}>
      Create {RECIPE_COPY[recipe].title}
    </button>
  </section>;
}

function GeometryControls({ catalog, draft, play, selectedId, onSelect, onChange }: {
  catalog: TacticalSheetCatalog;
  draft: SheetDraft;
  play: LinkedPlay;
  selectedId: string | null;
  onSelect: (id: string) => void;
  onChange: (next: SheetDraft) => void;
}) {
  const selected = play.ownedGeometry.find((entry) => entry.id === selectedId);
  return <div className="mt-3 grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto]">
    <div className="flex gap-1.5 overflow-x-auto pb-1">{play.ownedGeometry.filter((entry) => !entry.derivedFrom).map((entry) => <button key={`${entry.kind}:${entry.id}`}
      type="button" className={clsx('btn min-h-11 min-w-[150px] justify-start', entry.id === selectedId && 'btn-on')}
      onClick={() => onSelect(entry.id)}>{geometrySymbol(entry.kind)} {humanize(entry.id)}</button>)}</div>
    {play.geometryGroups[0] && <div className="flex gap-1" aria-label="Move linked geometry together">
      <button type="button" className="btn min-h-11 min-w-11" aria-label="Move Play left" onClick={() => onChange(movePlayGeometryGroup(draft, play.id, play.geometryGroups[0].id, -1, 0, catalog.map.width, catalog.map.height))}>←</button>
      <button type="button" className="btn min-h-11 min-w-11" aria-label="Move Play up" onClick={() => onChange(movePlayGeometryGroup(draft, play.id, play.geometryGroups[0].id, 0, -1, catalog.map.width, catalog.map.height))}>↑</button>
      <button type="button" className="btn min-h-11 min-w-11" aria-label="Move Play down" onClick={() => onChange(movePlayGeometryGroup(draft, play.id, play.geometryGroups[0].id, 0, 1, catalog.map.width, catalog.map.height))}>↓</button>
      <button type="button" className="btn min-h-11 min-w-11" aria-label="Move Play right" onClick={() => onChange(movePlayGeometryGroup(draft, play.id, play.geometryGroups[0].id, 1, 0, catalog.map.width, catalog.map.height))}>→</button>
    </div>}
    {selected && <div className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3 lg:col-span-2">
      <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <div><p className="lab">{humanize(selected.kind)} purpose</p><p className="t-meta mt-1">{selected.purpose}</p>
          {selected.derivedFrom && <p className="t-micro mt-1 text-[#b49cff]">Derived from {humanize(selected.derivedFrom)}; move the source instead.</p>}</div>
        {!selected.derivedFrom && <label><span className="lab mb-1 block">Map object name</span>
          <IdentifierInput value={selected.id} isTaken={(candidate) => candidate !== selected.id && geometryExists(draft, selected.kind, candidate)}
            onCommit={(nextId) => {
              if (nextId === selected.id) return;
              onChange(renamePlayGeometry(draft, play.id, selected.kind, selected.id, nextId));
              onSelect(nextId);
            }} /></label>}
      </div>
    </div>}
    {play.slots.length > 0 && <div className="grid gap-2 sm:grid-cols-2 lg:col-span-2">{play.slots.map((slot) => <article key={slot.id}
      className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
      <div className="flex items-center gap-2"><b className="t-body mr-auto">{humanize(slot.roleId)} · {humanize(slot.anchorId)}</b><span className="pill">{slot.required ? 'required' : 'optional'}</span></div>
      <label className="mt-2 block"><span className="t-micro mb-1 block">Readiness range · {slot.tolerance} {slot.tolerance === 1 ? 'tile' : 'tiles'}</span>
        <input type="range" min={0} max={3} value={slot.tolerance} className="w-full accent-[var(--color-arena-material)]"
          onChange={(event) => onChange(setPlaySlotTolerance(draft, play.id, slot.id, Number(event.target.value)))} /></label>
      <p className="t-micro mt-1">capacity {slot.capacity ?? 1} · recomputed per resolved position, never a sticky reservation</p>
    </article>)}</div>}
  </div>;
}

function ParticipantControls({ draft, play, catalog, issues, onSelectGeometry, onChange }: {
  draft: SheetDraft; play: LinkedPlay; catalog: TacticalSheetCatalog; issues: SheetIssue[];
  onSelectGeometry: (id: string) => void; onChange: (next: SheetDraft) => void;
}) {
  const available = draft.orders.roles.filter((role) =>
    !play.projections.some((projection) => projection.roleId === role.id));
  const [blankRole, setBlankRole] = useState(available[0]?.id ?? '');
  const selectedBlankRole = available.some((role) => role.id === blankRole)
    ? blankRole : available[0]?.id ?? '';
  return <section className="panel pad">
    <p className="lab text-arena-material">Roles and linked Orders</p>
    <p className="t-micro mt-1">Each projection sits in one role’s real priority list. Changing a role updates its owned conditions, slots and partner targets together.</p>
    <div className="mt-3 space-y-2">{play.projections.map((projection) => {
      const role = draft.orders.roles.find((entry) => entry.id === projection.roleId);
      const allowed = compatibleRoles(draft, play, projection);
      return <article key={projection.id} className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
        <div className="flex items-center gap-2">
          <span className="flex -space-x-1">{role?.classes.map((classId, index) => <ClassIcon key={`${classId}:${index}`} classId={classId} size={34} decorative />)}</span>
          <div className="min-w-0 flex-1"><b className="type-display block truncate text-[15px]">{humanize(projection.roleId)}</b><span className="t-micro block">{projection.orders.length} generated root Orders</span></div>
        </div>
        <label className="mt-2 block"><span className="t-micro mb-1 block">Role</span>
          <select className="field min-h-11" value={projection.roleId}
            onChange={(event) => onChange(replacePlayRole(draft, play.id, projection.id, event.target.value))}>
            {allowed.map((candidate) => <option key={candidate.id} value={candidate.id}>{humanize(candidate.id)} · {candidate.classes.map(humanize).join(', ')}</option>)}
          </select></label>
        {role && <p className="t-micro mt-2">{role.classes.map((classId) => catalog.classes.find((entry) => entry.id === classId)?.signatureName ?? humanize(classId)).join(' · ')}</p>}
        {role && play.recipe === 'blank' && <details className="mt-3 rounded-sm border border-arena-edge bg-arena-panel/40">
          <summary className="flex min-h-11 cursor-pointer items-center gap-2 px-3"><span className="t-body mr-auto">Edit linked Orders</span><span className="pill">exact Orders controls</span><span className="disclosure-chevron">›</span></summary>
          <div className="border-t border-arena-edge p-2">
            <ProjectionOrdersComposer catalog={catalog} orders={draft.orders} mapPlan={draft.mapPlan}
              role={role} source={resolveProjectionOrders(play, projection)} issues={issues}
              onChange={(orders) => onChange(updatePlayProjectionOrders(draft, play.id, projection.id, orders))}
              onOpenMap={(focus) => {
                if (focus?.id) onSelectGeometry(focus.id);
                document.querySelector('[data-sheet-path="workshop.plays"]')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
              }} />
          </div>
        </details>}
        {role && play.recipe !== 'blank' && <details className="mt-3 rounded-sm border border-arena-edge bg-arena-panel/40">
          <summary className="flex min-h-11 cursor-pointer items-center gap-2 px-3"><span className="t-body mr-auto">Inspect generated Orders</span><span className="pill">read-only</span><span className="disclosure-chevron">›</span></summary>
          <pre className="max-h-64 overflow-auto border-t border-arena-edge bg-[#080c10] p-3 text-[11px] leading-relaxed text-arena-muted">{JSON.stringify(resolveProjectionOrders(play, projection), null, 2)}</pre>
        </details>}
      </article>;
    })}</div>
    {play.recipe === 'blank' && available.length > 0 && <div className="mt-3 grid gap-2 rounded-sm border border-dashed border-arena-material/40 p-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-end">
      <label><span className="lab mb-1 block">Role for a linked Orders projection</span>
        <select className="field min-h-11" value={selectedBlankRole} onChange={(event) => setBlankRole(event.target.value)}>
          {available.map((role) => <option key={role.id} value={role.id}>{humanize(role.id)}</option>)}
        </select></label>
      <button type="button" className="btn btn-secondary min-h-11" disabled={!selectedBlankRole}
        onClick={() => onChange(addBlankPlayProjection(draft, play.id, selectedBlankRole))}>＋ Add role projection</button>
    </div>}
  </section>;
}

function LinkedOrdersControls({ draft, play, onChange }: {
  draft: SheetDraft; play: LinkedPlay; onChange: (next: SheetDraft) => void;
}) {
  return <section className="panel pad">
    <p className="lab text-arena-material">Linked Orders fields</p>
    <p className="t-micro mt-1">One edit updates every listed projection. Labels below are the exact Orders field names.</p>
    <div className="mt-3 space-y-2">{play.linkedValues.map((linked) => <article key={linked.id}
      className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
      <div className="flex items-center gap-2"><b className="type-display text-[15px]">{linked.field}</b><span className="pill ml-auto">{linked.targets.length} linked Orders</span></div>
      {typeof linked.value === 'number' ? <label className="mt-2 block"><span className="t-micro mb-1 block">{humanize(linked.id)}</span>
        <input type="number" className="field min-h-11" min={linked.field === 'cooldown' ? 0 : 1} max={1200}
          value={linked.value} onChange={(event) => onChange(setPlayLinkedValue(draft, play.id, linked.id, Number(event.target.value)))} /></label>
        : <div className="mt-2 rounded-sm border border-arena-edge bg-[#080c10] p-2 font-mono text-[11px] text-arena-muted">{JSON.stringify(linked.value)}</div>}
      <p className="t-micro mt-2">{linked.targets.map((target) => `${humanize(target.projectionId)} → ${humanize(target.orderId)}.${target.field}`).join(' · ')}</p>
    </article>)}</div>
  </section>;
}

function PriorityControls({ draft, play, onChange, onOpenOrders }: {
  draft: SheetDraft; play: LinkedPlay; onChange: (next: SheetDraft) => void;
  onOpenOrders: (playId: string, projectionId?: string) => void;
}) {
  return <section className="panel pad">
    <p className="lab text-arena-material">Priority and continuation</p>
    <p className="t-micro mt-1">There is no Play priority. Each role projection has one explicit insertion point; the next eligible ordinary Order is its continuation.</p>
    <div className="mt-3 space-y-2">{play.projections.map((projection) => {
      const options = projectionPriorityOptions(draft.orders, projection);
      const selected = projection.placement.beforeOrderKey ?? '__end__';
      return <label key={projection.id} className="block rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
        <span className="flex items-center gap-2"><b className="t-body">{humanize(projection.roleId)}</b>
          <button type="button" className="btn ml-auto min-h-9" onClick={() => onOpenOrders(play.id, projection.id)}>Inspect in Orders</button></span>
        <span className="t-micro mb-1 mt-2 block">Insert projection</span>
        <select className="field min-h-11" value={selected}
          onChange={(event) => onChange(setPlayProjectionPlacement(draft, play.id, projection.id,
            event.target.value === '__end__' ? null : event.target.value))}>
          {options.map((option) => <option key={option.value ?? '__end__'} value={option.value ?? '__end__'}>{option.label}</option>)}
        </select>
      </label>;
    })}</div>
  </section>;
}

function ConditionControls({ draft, play, onChange }: {
  draft: SheetDraft; play: LinkedPlay; onChange: (next: SheetDraft) => void;
}) {
  return <details className="panel overflow-hidden">
    <summary className="flex min-h-14 cursor-pointer items-center gap-2 p-3"><div className="mr-auto"><p className="lab">Conditions</p><p className="t-micro mt-1">Exact Play-owned condition facts and thresholds</p></div><span className="pill">{play.ownedConditionIds.length}</span><span className="disclosure-chevron">›</span></summary>
    <div className="grid gap-2 border-t border-arena-edge p-3">{play.ownedConditionIds.map((conditionId) => {
      const condition = draft.orders.conditions[conditionId];
      if (!condition) return <p key={conditionId} className="t-micro text-arena-hot">Missing {conditionId}</p>;
      return <ConditionCard key={conditionId} id={conditionId} value={condition}
        onChange={(next) => onChange(updatePlayCondition(draft, play.id, conditionId, next))} />;
    })}</div>
  </details>;
}

function ConditionCard({ id, value, onChange }: {
  id: string; value: ConditionDefinition; onChange: (next: ConditionDefinition) => void;
}) {
  return <article className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
    <b className="t-body block">{humanize(id)}</b><code className="t-micro mt-1 block text-arena-material">{value.fact}</code>
    <div className="mt-2 grid grid-cols-[minmax(0,1fr)_100px] gap-2">
      <select className="field min-h-11" value={value.operator}
        onChange={(event) => onChange({ ...value, operator: event.target.value as ConditionDefinition['operator'] })}>
        {['at-least', 'at-most', 'equals', 'less-than', 'greater-than'].map((operator) => <option key={operator}>{operator}</option>)}
      </select>
      <input type="number" className="field min-h-11" value={value.value}
        onChange={(event) => onChange({ ...value, value: Number(event.target.value) })} />
    </div>
    <p className="t-micro mt-2">{[
      value.role && `role ${value.role}`,
      value.zone && `zone ${value.zone}`,
      value.since && `since ${value.since}`,
      value.windowTicks && `windowTicks ${value.windowTicks}`,
      value.freshnessTicks && `freshnessTicks ${value.freshnessTicks}`,
    ].filter(Boolean).join(' · ') || 'No qualifier'}</p>
  </article>;
}

function GeneratedOrders({ play, onOpenOrders }: {
  play: LinkedPlay; onOpenOrders: (playId: string, projectionId?: string) => void;
}) {
  return <details className="panel overflow-hidden">
    <summary className="flex min-h-14 cursor-pointer items-center gap-2 p-3"><div className="mr-auto"><p className="lab">Show generated Orders</p><p className="t-micro mt-1">Read-only ordinary source · copied behavior survives detach</p></div><span className="disclosure-chevron">›</span></summary>
    <div className="space-y-3 border-t border-arena-edge p-3">{play.projections.map((projection) => <article key={projection.id}>
      <div className="flex items-center gap-2"><b className="t-body">{humanize(projection.roleId)}</b><button type="button" className="btn ml-auto min-h-9" onClick={() => onOpenOrders(play.id, projection.id)}>See real priority</button></div>
      <pre className="mt-2 max-h-80 overflow-auto rounded-sm border border-arena-edge bg-[#080c10] p-3 text-[11px] leading-relaxed text-arena-muted">{JSON.stringify(resolveProjectionOrders(play, projection), null, 2)}</pre>
    </article>)}</div>
  </details>;
}

function RoleChoice({ label, value, roles, onChange }: {
  label: string; value: string; roles: SheetDraft['orders']['roles']; onChange: (value: string) => void;
}) {
  return <label className="block"><span className="lab mb-1 block">{label}</span>
    <select className="field min-h-12" value={value} onChange={(event) => onChange(event.target.value)}>
      <option value="">Choose a role</option>
      {roles.map((role) => <option key={role.id} value={role.id}>{humanize(role.id)} · {role.classes.map(humanize).join(', ')}</option>)}
    </select></label>;
}

function compatibleRoles(draft: SheetDraft, play: LinkedPlay, projection: PlayProjection) {
  const current = draft.orders.roles.find((role) => role.id === projection.roleId);
  if (!current) return draft.orders.roles;
  const claimed = new Set(play.projections.filter((entry) => entry.id !== projection.id)
    .map((entry) => entry.roleId));
  if (play.recipe === 'relay-chain')
    return draft.orders.roles.filter((role) => !claimed.has(role.id)
      && role.classes.length === 1 && role.classes[0] === 'relay');
  if (play.recipe === 'ambush') {
    const requiredClass = projection.id.endsWith('-mason') ? 'mason'
      : projection.id.endsWith('-longshot') ? 'longshot'
        : projection.id.endsWith('-hush') ? 'hush' : null;
    return requiredClass
      ? draft.orders.roles.filter((role) => !claimed.has(role.id)
        && role.classes.length === 1 && role.classes[0] === requiredClass)
      : draft.orders.roles.filter((role) => !claimed.has(role.id) && role.classes.length >= 2);
  }
  return draft.orders.roles.filter((role) => !claimed.has(role.id));
}

function recipeName(recipe: PlayRecipe) {
  return recipe === 'relay-chain' ? 'Relay chain' : recipe === 'ambush' ? 'Ambush recipe' : recipe === 'blank' ? 'Blank Play' : humanize(recipe);
}

function geometryExists(draft: SheetDraft, kind: PlayGeometryKind, id: string) {
  return kind === 'anchor' ? Boolean(draft.mapPlan.anchors[id])
    : kind === 'zone' ? Boolean(draft.mapPlan.zones[id]) : Boolean(draft.mapPlan.routes[id]);
}

function geometrySymbol(kind: PlayGeometryKind) {
  return kind === 'anchor' ? '⌖' : kind === 'zone' ? '▣' : '⌁';
}

function humanize(value: string) {
  return value.replace(/[-_]+/g, ' ').replace(/\b\w/g, (letter) => letter.toUpperCase());
}

// Kept in this module so blank-Play projection authoring can use the same
// deterministic id namespace when its richer UI is added without changing the
// persisted sidecar shape.
export function nextBlankProjectionId(play: LinkedPlay) {
  return uniquePlayId(`${play.id}-projection`, {
    schema: 'arc-relay-workshop-v1',
    plays: play.projections.map((projection) => ({ ...play, id: projection.id })),
  });
}

export function generatedRoots(play: LinkedPlay): OrderNode[] {
  return play.projections.flatMap((projection) => resolveProjectionOrders(play, projection));
}
