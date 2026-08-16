import { useEffect, useMemo, useState } from 'react';
import clsx from 'clsx';
import ClassIcon from '../../components/ClassIcon';
import type { ReplayModel } from '../../replayModel';
import type { TacticalSheetCatalog } from '../api';
import IdentifierInput from './IdentifierInput';
import { ProjectionOrdersComposer } from './OrdersWorkspace';
import SignatureUseEditor from './SignatureUseEditor';
import PlayMapCanvas from './PlayMapCanvas';
import {
  deletePlay,
  detachPlay,
  playDrifts,
  projectionPriorityOptions,
  resolveProjectionOrders,
  signatureOpportunityViews,
  uniquePlayId,
} from './linkedPlays';
import {
  addPlayCondition,
  addPlayGeometry,
  addSignatureOpportunity,
  addSignatureOpportunityActivationZone,
  addSignatureOpportunityAnchor,
  addAmbushReserveSlot,
  appendPlayRouteWaypoint,
  configurePlayContingent,
  movePlayAnchor,
  moveSignatureOpportunity,
  rematerializeDraft,
  removeLastPlayRouteWaypoint,
  removeAmbushReserveSlot,
  removeSignatureOpportunity,
  renameSignatureOpportunity,
  renamePlayGeometry,
  replacePlayRole,
  setPlayLinkedValue,
  setPlayName,
  setPlayProjectionPlacement,
  setPlayContingentExit,
  setPlayZoneRect,
  setSignatureOpportunityCapacity,
  setSignatureOpportunityCommitmentHold,
  setSignatureOpportunityWhen,
  updatePlayCondition,
  updatePlayProjectionOrders,
  updateSignatureOpportunityUse,
} from './playEditing';
import {
  movePlayGeometryGroup,
  playGeometryIssues,
  setPlaySlotTolerance,
} from './playGeometry';
import {
  addBlankPlay,
  addBlankPlayProjection,
  addLaneClosurePlay,
  addNorthAmbushPlay,
  addRelayChainPlay,
  addSafeReturnPlay,
  addVeteranEscortPlay,
} from './playRecipes';
import type {
  ConditionDefinition,
  OrderNode,
  SheetDraft,
  SheetIssue,
} from './sheetAuthoring';
import {
  CONDITION_FACTS,
  conditionExpressionNames,
  conditionSubjectKind,
  defaultSignatureTarget,
  skillForClass,
} from './sheetAuthoring';
import {
  bestPlayFormationReadiness,
  bestPlayReadiness,
  signatureOpportunityPracticeSummary,
  type UnitTrack,
} from './sheetTrials';
import type {
  LinkedPlay,
  PlayGeometryKind,
  PlayProjection,
  PlayRecipe,
} from './workshopTypes';

type RecipeChoice = PlayRecipe;

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
    title: 'Running Relay',
    kicker: 'Forward-only Core relay',
    description: 'Two one-body Relays sprint ahead and Toss only when the receiver gains ground along one corridor.',
  },
  'veteran-escort': {
    title: 'Veteran escort',
    kicker: 'Route, screen, follow',
    description: 'A veteran patrols while an escort role screens, fights, recovers and follows.',
  },
  'safe-return': {
    title: 'Safe Core return',
    kicker: 'Latched route and handoff',
    description: 'A carrier keeps a long safe route after leaving enemy ground, then hands off to a Relay.',
  },
  'lane-closure': {
    title: 'Lane closure',
    kicker: 'Hardlight and two posts',
    description: 'A Mason closes one lane while two guards face and hold the remaining opening.',
  },
};

export default function PlaysWorkspace({
  catalog,
  draft,
  issues,
  tracks,
  practiceReplay,
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
  practiceReplay?: ReplayModel;
  selectedPlayId: string | null;
  onSelectPlay: (playId: string | null) => void;
  onChange: (next: SheetDraft) => void;
  onOpenSquad: () => void;
  onOpenOrders: (playId: string, projectionId?: string) => void;
}) {
  const [creating, setCreating] = useState<RecipeChoice | null>(null);
  const [selectedGeometry, setSelectedGeometry] = useState<string | null>(null);
  const [placementGeometry, setPlacementGeometry] = useState<string | null>(null);
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
    ...playGeometryIssues(draft, play.id, catalog.map.tileRows),
    ...playDrifts(draft.orders, draft.workshop, play.id).map((drift): SheetIssue => ({
      path: `workshop.plays.${play.id}.${drift.projectionId}`,
      severity: 'error',
      message: drift.message,
    })),
    ...issues.filter((issue) => issue.path.includes(play.id)),
  ] : [], [catalog.map.tileRows, draft, issues, play]);

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
              startDrawingId={placementGeometry}
              onDrawingStarted={() => setPlacementGeometry(null)}
              onSelect={setSelectedGeometry}
              onMoveAnchor={(anchorId, point) => commit(movePlayAnchor(draft, play.id, anchorId, point))}
              onSetZone={(zoneId, rect) => commit(setPlayZoneRect(draft, play.id, zoneId, rect))}
              onAppendRoute={(routeId, point) => commit(appendPlayRouteWaypoint(
                draft, play.id, routeId, point))} />
          </div>
          <GeometryControls catalog={catalog} draft={draft} play={play}
            selectedId={selectedGeometry} tracks={tracks}
            onSelect={setSelectedGeometry} onChange={commit} />
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
          {play.recipe === 'blank' && <PlayResponseControls draft={draft} play={play} tracks={tracks}
            onSelectGeometry={setSelectedGeometry} onChange={commit} />}
          <AlongTheWayControls draft={draft} play={play} catalog={catalog}
            practiceReplay={practiceReplay}
            onSelectGeometry={(id, startDrawing = false) => {
              setSelectedGeometry(id);
              if (startDrawing) setPlacementGeometry(id);
              document.querySelector('[data-sheet-path="workshop.plays"]')
                ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }}
            onOpenOrders={onOpenOrders} onChange={commit} />
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
  const oneBodyRoles = draft.orders.roles.filter((role) => role.classes.length === 1);
  const [masonRoleId, setMason] = useState(mason[0]?.id ?? '');
  const [longshotRoleId, setLongshot] = useState(longshot[0]?.id ?? '');
  const [hushRoleId, setHush] = useState(hush[0]?.id ?? '');
  const [reserveRoleId, setReserve] = useState(reserves[0]?.id ?? '');
  const [relayARoleId, setRelayA] = useState(relays[0]?.id ?? '');
  const [relayBRoleId, setRelayB] = useState(relays[1]?.id ?? '');
  const [veteranRoleId, setVeteran] = useState(oneBodyRoles[0]?.id ?? '');
  const [escortRoleId, setEscort] = useState(
    draft.orders.roles.find((role) => role.id !== oneBodyRoles[0]?.id)?.id ?? '',
  );
  const [carrierRoleId, setCarrier] = useState(oneBodyRoles[0]?.id ?? '');
  const [safeRelayRoleId, setSafeRelay] = useState(
    relays.find((role) => role.id !== oneBodyRoles[0]?.id)?.id ?? '',
  );
  const [guardRoleId, setGuard] = useState(
    reserves.find((role) => role.id !== mason[0]?.id)?.id ?? '',
  );
  const ready = recipe === 'blank'
    || recipe === 'ambush' && Boolean(masonRoleId && longshotRoleId && hushRoleId && reserveRoleId)
    || recipe === 'relay-chain' && Boolean(relayARoleId && relayBRoleId && relayARoleId !== relayBRoleId)
    || recipe === 'veteran-escort' && Boolean(veteranRoleId && escortRoleId && veteranRoleId !== escortRoleId)
    || recipe === 'safe-return' && Boolean(carrierRoleId && safeRelayRoleId && carrierRoleId !== safeRelayRoleId)
    || recipe === 'lane-closure' && Boolean(masonRoleId && guardRoleId && masonRoleId !== guardRoleId);

  const create = () => {
    try {
      let next: SheetDraft;
      if (recipe === 'ambush') next = addNorthAmbushPlay(draft, { masonRoleId, longshotRoleId, hushRoleId, reserveRoleId });
      else if (recipe === 'relay-chain') next = addRelayChainPlay(draft, { relayARoleId, relayBRoleId });
      else if (recipe === 'veteran-escort') next = addVeteranEscortPlay(draft, { veteranRoleId, escortRoleId });
      else if (recipe === 'safe-return') next = addSafeReturnPlay(draft, { carrierRoleId, relayRoleId: safeRelayRoleId });
      else if (recipe === 'lane-closure') next = addLaneClosurePlay(draft, { masonRoleId, guardRoleId });
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
    {recipe === 'veteran-escort' && <div className="mt-4 grid gap-2 sm:grid-cols-2">
      <RoleChoice label="Veteran role" value={veteranRoleId} roles={oneBodyRoles} onChange={setVeteran} />
      <RoleChoice label="Escort role" value={escortRoleId}
        roles={draft.orders.roles.filter((role) => role.id !== veteranRoleId)} onChange={setEscort} />
    </div>}
    {recipe === 'safe-return' && <div className="mt-4 grid gap-2 sm:grid-cols-2">
      <RoleChoice label="Carrier role" value={carrierRoleId} roles={oneBodyRoles} onChange={setCarrier} />
      <RoleChoice label="Relay handoff role" value={safeRelayRoleId}
        roles={relays.filter((role) => role.id !== carrierRoleId)} onChange={setSafeRelay} />
    </div>}
    {recipe === 'lane-closure' && <div className="mt-4 grid gap-2 sm:grid-cols-2">
      <RoleChoice label="Mason role" value={masonRoleId} roles={mason} onChange={setMason} />
      <RoleChoice label="Guard role · at least two bodies" value={guardRoleId}
        roles={reserves.filter((role) => role.id !== masonRoleId)} onChange={setGuard} />
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

function GeometryControls({ catalog, draft, play, selectedId, tracks, onSelect, onChange }: {
  catalog: TacticalSheetCatalog;
  draft: SheetDraft;
  play: LinkedPlay;
  selectedId: string | null;
  tracks: UnitTrack[];
  onSelect: (id: string) => void;
  onChange: (next: SheetDraft) => void;
}) {
  const selected = play.ownedGeometry.find((entry) => entry.id === selectedId);
  const reserveProjection = play.recipe === 'ambush'
    ? play.projections.find((entry) => entry.id.endsWith('-reserve')) : undefined;
  const reserveSlots = reserveProjection
    ? play.slots.filter((slot) => slot.roleId === reserveProjection.roleId) : [];
  const reserveBodies = reserveProjection
    ? draft.orders.roles.find((role) => role.id === reserveProjection.roleId)?.classes.length ?? 0 : 0;
  const readiness = useMemo(() => bestPlayReadiness(draft, play, tracks), [draft, play, tracks]);
  const readySlots = new Set(readiness.readySlotIds);
  return <div className="mt-3 grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto]">
    <div className="flex gap-1.5 overflow-x-auto pb-1">{play.ownedGeometry.filter((entry) => !entry.derivedFrom).map((entry) => <button key={`${entry.kind}:${entry.id}`}
      type="button" className={clsx('btn min-h-11 min-w-[150px] justify-start', entry.id === selectedId && 'btn-on')}
      onClick={() => onSelect(entry.id)}>{geometrySymbol(entry.kind)} {humanize(entry.id)}</button>)}</div>
    {play.recipe === 'blank' && <div className="flex flex-wrap gap-1.5 lg:col-span-2">
      {(['zone', 'route', 'anchor'] as const).map((kind) => <button key={kind}
        type="button" className="btn btn-secondary min-h-11" onClick={() => {
          const result = addPlayGeometry(draft, play.id, kind);
          onChange(result.draft);
          onSelect(result.id);
        }}>＋ {kind === 'zone' ? 'Draw area' : kind === 'route' ? 'Draw route' : 'Place point'}</button>)}
    </div>}
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
      {selected.kind === 'route' && !selected.derivedFrom && <div className="mt-3 flex flex-wrap items-center gap-2">
        <span className="t-micro mr-auto">{draft.mapPlan.routes[selected.id]?.waypoints.length ?? 0} ordered route points</span>
        <button type="button" className="btn min-h-11"
          disabled={(draft.mapPlan.routes[selected.id]?.waypoints.length ?? 0) === 0}
          onClick={() => onChange(removeLastPlayRouteWaypoint(draft, play.id, selected.id))}>
          Undo last point
        </button>
      </div>}
    </div>}
    {play.slots.length > 0 && <div className="grid gap-2 sm:grid-cols-2 lg:col-span-2">
      {readiness.total > 0 && <div className="rounded-sm border border-arena-material/45 bg-arena-material/10 p-3 sm:col-span-2">
        <div className="flex flex-wrap items-center gap-2">
          <div className="mr-auto"><p className="lab text-arena-material">Last Practice readiness</p>
            <p className="t-meta mt-1">{readiness.tick === null
              ? 'Run Practice to compare actual body tracks with these required positions.'
              : `Best frame at tick ${readiness.tick}`}</p></div>
          <strong className="type-display text-[24px]">{readiness.ready}/{readiness.total}</strong>
        </div>
      </div>}
      {play.slots.map((slot) => {
      const reserveIndex = reserveSlots.findIndex((entry) => entry.id === slot.id);
      return <article key={slot.id}
      className={clsx('rounded-sm border bg-arena-deep/25 p-3',
        slot.required && readySlots.has(slot.id) ? 'border-arena-cyan/65' : 'border-arena-edge')}>
      <div className="flex items-center gap-2"><b className="t-body mr-auto">{humanize(slot.roleId)} · {humanize(slot.anchorId)}</b>
        {slot.required && readiness.tick !== null && <span className={clsx('pill', readySlots.has(slot.id) && 'text-arena-cyan')}>{readySlots.has(slot.id) ? 'reached' : 'not reached'}</span>}
        <span className="pill">{slot.required ? 'required' : 'optional'}</span></div>
      <label className="mt-2 block"><span className="t-micro mb-1 block">Readiness range · {slot.tolerance} {slot.tolerance === 1 ? 'tile' : 'tiles'}</span>
        <input type="range" min={0} max={3} value={slot.tolerance} className="w-full accent-[var(--color-arena-material)]"
          onChange={(event) => onChange(setPlaySlotTolerance(draft, play.id, slot.id, Number(event.target.value)))} /></label>
      <p className="t-micro mt-1">capacity {slot.capacity ?? 1} · recomputed per resolved position, never a sticky reservation</p>
      {reserveIndex >= 2 && <button type="button" className="btn mt-2 min-h-10 text-arena-hot"
        onClick={() => onChange(removeAmbushReserveSlot(draft, play.id, slot.id))}>Remove pooled post</button>}
    </article>})}
      {reserveProjection && <div className="rounded-sm border border-dashed border-arena-material/45 p-3 sm:col-span-2">
        <div className="flex flex-wrap items-center gap-2"><div className="mr-auto"><b className="t-body block">Pooled reserve positions</b>
          <span className="t-micro mt-1 block">{reserveBodies} {reserveBodies === 1 ? 'body' : 'bodies'} available for {reserveSlots.length} required posts · capacity one per resolved post</span></div>
          <button type="button" className="btn btn-secondary min-h-11" disabled={reserveSlots.length >= 4}
            onClick={() => onChange(addAmbushReserveSlot(draft, play.id))}>＋ Add pooled post</button></div>
        {reserveSlots.length > reserveBodies && <p className="t-micro mt-2 text-arena-hot">This cannot arm: the role needs at least {reserveSlots.length} bodies, or one post must be removed.</p>}
      </div>}
    </div>}
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

function PlayResponseControls({ draft, play, tracks, onSelectGeometry, onChange }: {
  draft: SheetDraft;
  play: LinkedPlay;
  tracks: UnitTrack[];
  onSelectGeometry: (id: string, startDrawing?: boolean) => void;
  onChange: (next: SheetDraft) => void;
}) {
  const formationConditionIds = new Set((play.formations ?? [])
    .map((entry) => entry.conditionId));
  const responseConditionSets = play.projections.map((projection) => {
    const response = findOrderById(projection.orders, `${projection.id}-respond`);
    return new Set(conditionExpressionNames(response?.when)
      .filter((id) => !formationConditionIds.has(id)));
  }).filter((set) => set.size > 0);
  const common = responseConditionSets.length === 0 ? []
    : [...responseConditionSets[0]].filter((id) =>
      responseConditionSets.every((set) => set.has(id)));
  const candidates = play.ownedConditionIds.filter((id) =>
    draft.orders.conditions[id]
    && !formationConditionIds.has(id)
    && draft.orders.conditions[id].fact !== 'signature-commitment-active');
  const inferred = common[0] ?? candidates[0] ?? '';
  const [selectedTrigger, setSelectedTrigger] = useState(inferred);
  const triggerId = draft.orders.conditions[selectedTrigger]
    ? selectedTrigger : inferred;
  const zoneIds = play.ownedGeometry.filter((entry) =>
    entry.kind === 'zone' && !entry.derivedFrom).map((entry) => entry.id);
  const [newTriggerZone, setNewTriggerZone] = useState(zoneIds[0] ?? '');
  const selectedZone = zoneIds.includes(newTriggerZone)
    ? newTriggerZone : zoneIds[0] ?? '';

  return <section className="panel pad" data-testid="play-response-design">
    <p className="lab text-arena-material">Shared response</p>
    <h3 className="type-display mt-1 text-[19px]">One start, independent contingents</h3>
    <p className="t-micro mt-1">The shared start is one ordinary named Condition. Each role then adds its own readiness, timeout, exit and continuation in its real Orders projection.</p>
    {candidates.length === 0 ? <div className="mt-3 rounded-sm border border-arena-material/35 bg-arena-material/8 p-3">
      <p className="type-display text-[16px]">What starts the response?</p>
      <p className="t-micro mt-1">Start with enemies entering a drawn area. You can refine the exact fact and threshold after creation.</p>
      {zoneIds.length > 0 ? <>
        <label className="mt-3 block"><span className="lab mb-1 block">Watched area</span>
          <select className="field min-h-11" value={selectedZone}
            onChange={(event) => setNewTriggerZone(event.target.value)}>
            {zoneIds.map((id) => <option key={id} value={id}>{humanize(id)}</option>)}
          </select></label>
        <button type="button" className="btn btn-primary mt-3 min-h-11 w-full"
          onClick={() => {
            const result = addPlayCondition(draft, play.id,
              `${play.id}-response-start`, {
                fact: 'visible-enemies-in-zone', operator: 'at-least', value: 5,
                zone: selectedZone,
              });
            onChange(result.draft);
            setSelectedTrigger(result.id);
          }}>Create shared start · 5 enemies in {humanize(selectedZone)}</button>
      </> : <p className="t-meta mt-3">Draw either ambush area on the map above, then return here.</p>}
    </div> : <div className="mt-3 rounded-sm border border-arena-material/40 bg-arena-material/8 p-3">
      <div className="flex flex-wrap items-start gap-2">
        <div className="mr-auto"><p className="lab">Overall response starts when</p>
          <p className="type-display mt-1 text-[17px]">{conditionSummary(draft.orders.conditions[triggerId])}</p></div>
        {common.includes(triggerId) && <span className="pill text-arena-cyan">shared by every configured contingent</span>}
      </div>
      {candidates.length > 1 && <label className="mt-3 block"><span className="t-micro mb-1 block">Condition used as the shared start</span>
        <select className="field min-h-11" value={triggerId}
          onChange={(event) => setSelectedTrigger(event.target.value)}>
          {candidates.map((id) => <option key={id} value={id}>{humanize(id)}</option>)}
        </select></label>}
      <ConditionCard id={triggerId} value={draft.orders.conditions[triggerId]}
        zones={Object.keys(draft.mapPlan.zones)} roles={draft.orders.roles.map((role) => role.id)}
        onSelectGeometry={onSelectGeometry}
        onChange={(next) => onChange(updatePlayCondition(draft, play.id, triggerId, next))} />
    </div>}
    <div className="mt-3 grid gap-3">{play.projections.map((projection) =>
      <ContingentCard key={projection.id} draft={draft} play={play}
        projection={projection} triggerId={triggerId} tracks={tracks}
        onSelectGeometry={onSelectGeometry} onChange={onChange} />)}</div>
  </section>;
}

function ContingentCard({ draft, play, projection, triggerId, tracks, onSelectGeometry, onChange }: {
  draft: SheetDraft;
  play: LinkedPlay;
  projection: PlayProjection;
  triggerId: string;
  tracks: UnitTrack[];
  onSelectGeometry: (id: string, startDrawing?: boolean) => void;
  onChange: (next: SheetDraft) => void;
}) {
  const role = draft.orders.roles.find((entry) => entry.id === projection.roleId)!;
  const response = findOrderById(projection.orders, `${projection.id}-respond`);
  const formation = (play.formations ?? []).find((entry) =>
    entry.projectionId === projection.id);
  const ready = formation ? draft.orders.conditions[formation.conditionId] : undefined;
  const evidence = formation
    ? bestPlayFormationReadiness(draft, formation.conditionId, tracks)
    : null;
  const continuation = response?.orders?.find((child) =>
    child.move?.to !== 'current-position' && !child.use);
  const currentRoute = continuation?.move?.patrol;
  const currentZone = ready?.zone;
  const zones = play.ownedGeometry.filter((entry) =>
    entry.kind === 'zone' && !entry.derivedFrom).map((entry) => entry.id);
  const routes = play.ownedGeometry.filter((entry) =>
    entry.kind === 'route' && !entry.derivedFrom).map((entry) => entry.id);
  const [kind, setKind] = useState<'zone' | 'route'>(currentRoute ? 'route' : 'zone');
  const [destination, setDestination] = useState(currentRoute ?? currentZone
    ?? (currentRoute ? routes[0] : zones[0]) ?? '');
  const chosenIds = kind === 'route' ? routes : zones;
  const chosen = chosenIds.includes(destination) ? destination : chosenIds[0] ?? '';
  const [required, setRequired] = useState(ready?.value ?? role.classes.length);
  const [timeout, setTimeout] = useState(response?.timeout ?? 90);
  const configured = Boolean(response);
  const exit = typeof response?.until === 'string' ? response.until : '';
  const allConditions = Object.keys(draft.orders.conditions)
    .filter((id) => id !== triggerId && id !== formation?.conditionId);
  return <article className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
    <div className="flex flex-wrap items-center gap-2">
      <span className="flex -space-x-1">{role.classes.map((classId, index) =>
        <ClassIcon key={`${classId}:${index}`} classId={classId} size={34} decorative />)}</span>
      <div className="mr-auto min-w-0"><b className="type-display block truncate text-[16px]">{humanize(role.id)}</b>
        <span className="t-micro">{role.classes.length} {role.classes.length === 1 ? 'body' : 'bodies'}</span></div>
      {configured && <span className="pill text-arena-cyan">{ready
        ? tracks.length > 0
          ? `${evidence?.ready ?? 0}/${ready.value} best in Practice`
          : `${ready.value}/${role.classes.length} required`
        : 'acts immediately'}</span>}
    </div>
    <div className="mt-3 grid grid-cols-2 gap-1.5">
      <button type="button" className="choice-card min-h-14" data-selected={kind === 'zone'}
        onClick={() => setKind('zone')}><b className="t-body block">Form in an area</b><span className="t-micro">Wait for this role’s own readiness.</span></button>
      <button type="button" className="choice-card min-h-14" data-selected={kind === 'route'}
        onClick={() => setKind('route')}><b className="t-body block">Take a route now</b><span className="t-micro">No formation gate after the shared start.</span></button>
    </div>
    {chosenIds.length > 0 ? <label className="mt-3 block"><span className="lab mb-1 block">{kind === 'zone' ? 'Formation area' : 'Travel route'}</span>
      <select className="field min-h-11" value={chosen}
        onChange={(event) => setDestination(event.target.value)}>
        {chosenIds.map((id) => <option key={id} value={id}>{humanize(id)}</option>)}
      </select>
      <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full"
        onClick={() => onSelectGeometry(chosen)}>Edit {humanize(chosen)} on map</button>
    </label> : <p className="t-meta mt-3">Draw a Play-owned {kind} on the map first.</p>}
    {kind === 'zone' && <label className="mt-3 block"><span className="lab mb-1 block">Bodies required · {required}/{role.classes.length}</span>
      <input type="range" className="w-full accent-[var(--color-arena-material)]"
        min={1} max={Math.max(1, role.classes.length)} value={Math.min(required, role.classes.length)}
        onChange={(event) => setRequired(Number(event.target.value))} />
      <span className="t-micro mt-1 block">One role-qualified `role-in-zone-count` Condition; no per-body anchors to maintain.</span>
    </label>}
    <label className="mt-3 block"><span className="lab mb-1 block">Commitment timeout · {timeout} ticks</span>
      <input type="range" className="w-full accent-[var(--color-arena-material)]"
        min={10} max={300} step={5} value={timeout}
        onChange={(event) => setTimeout(Number(event.target.value))} />
    </label>
    <button type="button" className="btn btn-primary mt-3 min-h-11 w-full"
      disabled={!triggerId || !chosen}
      onClick={() => onChange(configurePlayContingent(draft, play.id, projection.id, {
        triggerConditionId: triggerId,
        destination: kind === 'zone' ? { zone: chosen } : { route: chosen },
        requiredBodies: kind === 'zone' ? required : 0,
        timeout,
      }))}>{configured ? 'Apply refinement' : 'Scaffold ordinary Orders'}</button>
    {configured && <div className="mt-3 grid gap-2 rounded-sm border border-arena-edge bg-arena-panel/45 p-3 sm:grid-cols-2">
      <div><p className="lab">Starts</p><p className="t-micro mt-1">{conditionSummary(draft.orders.conditions[triggerId])}{ready ? ` AND ${conditionSummary(ready)}` : ''}</p></div>
      <div><p className="lab">During</p><p className="t-micro mt-1">{currentRoute ? `Move on ${humanize(currentRoute)}` : 'Fight from the formed area'} · timeout {response?.timeout}</p></div>
      <label className="sm:col-span-2"><span className="lab mb-1 block">Success / explicit exit Condition</span>
        <select className="field min-h-11" value={exit}
          onChange={(event) => onChange(setPlayContingentExit(
            draft, play.id, projection.id, event.target.value || null))}>
          <option value="">No explicit exit · timeout or pre-emption</option>
          {allConditions.map((id) => <option key={id} value={id}>{humanize(id)}</option>)}
        </select></label>
      <p className="t-micro sm:col-span-2">On failure or timeout, this projection exits and the role’s next eligible Order continues. A higher role Order—including Core work—can pre-empt it at any tick.</p>
    </div>}
  </article>;
}

function AlongTheWayControls({
  draft,
  play,
  catalog,
  practiceReplay,
  onSelectGeometry,
  onOpenOrders,
  onChange,
}: {
  draft: SheetDraft;
  play: LinkedPlay;
  catalog: TacticalSheetCatalog;
  practiceReplay?: ReplayModel;
  onSelectGeometry: (id: string, startDrawing?: boolean) => void;
  onOpenOrders: (playId: string, projectionId?: string) => void;
  onChange: (next: SheetDraft) => void;
}) {
  return <section className="panel pad">
    <p className="lab text-arena-material">Along the way</p>
    <h3 className="type-display mt-1 text-[19px]">Signature opportunities inside real Orders</h3>
    <p className="t-micro mt-1">Each card is a real use child above the continuation shown beside it. Reordering changes that exact local child order; it creates no separate Play priority.</p>
    <div className="mt-3 space-y-3">{play.projections.map((projection) => {
      const role = draft.orders.roles.find((entry) => entry.id === projection.roleId);
      if (!role) return null;
      return <OpportunityProjection key={projection.id} draft={draft} play={play}
        projection={projection} role={role} catalog={catalog}
        practiceReplay={practiceReplay}
        onSelectGeometry={onSelectGeometry} onOpenOrders={onOpenOrders}
        onChange={onChange} />;
    })}</div>
  </section>;
}

function OpportunityProjection({
  draft,
  play,
  projection,
  role,
  catalog,
  practiceReplay,
  onSelectGeometry,
  onOpenOrders,
  onChange,
}: {
  draft: SheetDraft;
  play: LinkedPlay;
  projection: PlayProjection;
  role: SheetDraft['orders']['roles'][number];
  catalog: TacticalSheetCatalog;
  practiceReplay?: ReplayModel;
  onSelectGeometry: (id: string, startDrawing?: boolean) => void;
  onOpenOrders: (playId: string, projectionId?: string) => void;
  onChange: (next: SheetDraft) => void;
}) {
  const views = signatureOpportunityViews(projection, draft.orders.conditions);
  const hosts = opportunityHosts(projection.orders);
  const skills = [...new Set(role.classes.map((classId) =>
    skillForClass(catalog, classId)).filter((entry): entry is string => Boolean(entry)))];
  const [adding, setAdding] = useState(false);
  const [hostId, setHostId] = useState(hosts[0]?.id ?? '');
  const [skill, setSkill] = useState(skills[0] ?? '');
  const selectedHost = hosts.some((entry) => entry.id === hostId)
    ? hostId : hosts[0]?.id ?? '';
  const selectedSkill = skills.includes(skill) ? skill : skills[0] ?? '';
  return <article className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
    <div className="flex flex-wrap items-center gap-2">
      <span className="flex -space-x-1">{role.classes.map((classId, index) =>
        <ClassIcon key={`${classId}:${index}`} classId={classId} size={32} decorative />)}</span>
      <div className="mr-auto min-w-0"><b className="type-display block truncate text-[16px]">{humanize(role.id)}</b>
        <span className="t-micro">{views.length} {views.length === 1 ? 'opportunity' : 'opportunities'}</span></div>
      <button type="button" className="btn btn-secondary min-h-11"
        disabled={hosts.length === 0 || skills.length === 0}
        onClick={() => setAdding((value) => !value)}>＋ Add signature</button>
    </div>
    {adding && <div className="mt-3 rounded-sm border border-arena-material/35 bg-arena-material/5 p-3">
      <div className="grid gap-2 sm:grid-cols-2">
        <label><span className="lab mb-1 block">Continue this plan</span>
          <select className="field min-h-11" value={selectedHost}
            onChange={(event) => setHostId(event.target.value)}>
            {hosts.map((host) => <option key={host.id} value={host.id}>{host.label}</option>)}
          </select></label>
        <label><span className="lab mb-1 block">Role-owned signature</span>
          <select className="field min-h-11" value={selectedSkill}
            onChange={(event) => setSkill(event.target.value)}>
            {skills.map((candidate) => <option key={candidate} value={candidate}>
              {catalog.classes.find((entry) => entry.signatureId === candidate)?.signatureName ?? humanize(candidate)}
            </option>)}
          </select></label>
      </div>
      <p className="t-micro mt-2">A Move or Fight leaf becomes one ordinary parent branch in this undoable edit. Its original action remains the continuation.</p>
      <button type="button" className="btn btn-primary mt-3 min-h-11 w-full"
        disabled={!selectedHost || !selectedSkill}
        onClick={() => {
          const preserveCommitment = catalog.classes.find((entry) =>
            entry.signatureId === selectedSkill)?.signatureAuthoring
            .defaultCommitmentBehavior === 'preserve-by-default';
          onChange(addSignatureOpportunity(draft, play.id, projection.id,
            selectedHost, { skill: selectedSkill,
              target: defaultSignatureTarget(catalog, selectedSkill) },
            preserveCommitment));
          setAdding(false);
        }}>Add along this plan</button>
    </div>}
    <div className="mt-3 space-y-2">{views.map((view) => {
      const use = view.use.use!;
      const when = view.use.when;
      const simpleWhen = typeof when === 'string' ? when : '';
      const commitmentCondition = view.commitment;
      const descriptor = catalog.classes.find((entry) =>
        entry.signatureId === use.skill)?.signatureAuthoring;
      const practice = signatureOpportunityPracticeSummary(
        practiceReplay, draft, projection, view.binding);
      return <details key={view.binding.id} className="rounded-sm border border-arena-material/30 bg-arena-panel/35" open={views.length === 1}>
        <summary className="flex min-h-14 cursor-pointer items-center gap-2 px-3">
          <span className="pill">{view.ordinal + 1}</span>
          <ClassIcon classId={catalog.classes.find((entry) => entry.signatureId === use.skill)?.id ?? role.classes[0]} size={34} decorative />
          <span className="mr-auto min-w-0"><b className="type-display block truncate text-[15px]">{humanize(view.use.id ?? use.skill)}</b>
            <span className="t-micro block truncate">{humanize(use.skill)} → otherwise {orderName(view.continuation)}</span></span>
          <span className="disclosure-chevron">›</span>
        </summary>
        <div className="border-t border-arena-edge p-3">
          <label className="block"><span className="lab mb-1 block">Opportunity name</span>
            <IdentifierInput value={view.use.id ?? view.binding.useOrderId}
              isTaken={(id) => id !== view.binding.useOrderId && Boolean(findOrderById(projection.orders, id))}
              onCommit={(id) => id !== view.binding.useOrderId && onChange(
                renameSignatureOpportunity(draft, play.id, projection.id, view.binding.id, id))} /></label>
          <SignatureUseEditor value={use} role={role} roles={draft.orders.roles}
            catalog={catalog} mapPlan={draft.mapPlan}
            onOpenMap={(focus) => { if (focus?.id) onSelectGeometry(focus.id); }}
            onCreateAnchor={() => {
              const result = addSignatureOpportunityAnchor(
                draft, play.id, projection.id, view.binding.id);
              onChange(result.draft);
              onSelectGeometry(result.anchorId, true);
            }}
            onChange={(nextUse) => onChange(updateSignatureOpportunityUse(
              draft, play.id, projection.id, view.binding.id, nextUse))} />
          {(descriptor?.defaultCommitmentBehavior === 'preserve-by-default'
            || commitmentCondition) && <label className="mt-3 flex min-h-12 items-start gap-3 rounded-sm border border-arena-edge bg-arena-deep/35 p-3">
            <input type="checkbox" className="mt-0.5 h-5 w-5 accent-[var(--color-arena-material)]"
              checked={Boolean(commitmentCondition)}
              onChange={(event) => onChange(setSignatureOpportunityCommitmentHold(
                draft, play.id, projection.id, view.binding.id, event.target.checked))} />
            <span><span className="lab block">Preserve the public tell or channel</span>
              <span className="t-micro mt-1 block">Adds one visible ordinary hold after all opportunities in this plan. Turn it off only when cancelling through movement is intentional.</span></span>
          </label>}
          <div className="mt-3 grid gap-2 sm:grid-cols-2">
            <label><span className="lab mb-1 block">When</span>
              <select className="field min-h-11" value={simpleWhen}
                disabled={Boolean(when && typeof when !== 'string')}
                onChange={(event) => onChange(setSignatureOpportunityWhen(
                  draft, play.id, projection.id, view.binding.id,
                  event.target.value || undefined))}>
                <option value="">Whenever the parent is active</option>
                {Object.keys(draft.orders.conditions).map((id) =>
                  <option key={id} value={id}>{humanize(id)}</option>)}
              </select>
              {when && typeof when !== 'string' && <span className="t-micro mt-1 block">Combined condition · inspect in Orders to edit every term.</span>}
            </label>
            <label><span className="lab mb-1 block">Capacity · bodies on this resolved target</span>
              <select className="field min-h-11" value={view.use.capacity ?? ''}
                onChange={(event) => onChange(setSignatureOpportunityCapacity(
                  draft, play.id, projection.id, view.binding.id,
                  event.target.value ? Number(event.target.value) : undefined))}>
                <option value="">No added cap</option>
                {[1, 2, 3, 4, 5, 6, 7, 8].map((value) => <option key={value} value={value}>{value}</option>)}
              </select>
            </label>
          </div>
          {!when && <button type="button" className="btn btn-secondary mt-3 min-h-11 w-full"
            onClick={() => {
              const result = addSignatureOpportunityActivationZone(
                draft, play.id, projection.id, view.binding.id);
              onChange(result.draft);
              onSelectGeometry(result.zoneId);
            }}>▣ Mark an activation window on the map</button>}
          <div className="mt-3 rounded-sm border border-arena-edge bg-arena-deep/35 p-2">
            <p className="lab">Otherwise</p>
            <p className="t-meta mt-1">{orderName(view.continuation)}</p>
          </div>
          {practice && <OpportunityPracticeSummary summary={practice} />}
          <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
            <button type="button" className="btn min-h-11" disabled={view.ordinal === 0}
              onClick={() => onChange(moveSignatureOpportunity(draft, play.id, projection.id, view.binding.id, -1))}>↑ Earlier</button>
            <button type="button" className="btn min-h-11" disabled={view.ordinal === view.count - 1}
              onClick={() => onChange(moveSignatureOpportunity(draft, play.id, projection.id, view.binding.id, 1))}>↓ Later</button>
            <button type="button" className="btn min-h-11"
              onClick={() => onOpenOrders(play.id, projection.id)}>Inspect Orders</button>
            <button type="button" className="btn min-h-11 text-arena-hot"
              onClick={() => {
                if (window.confirm(`Remove “${humanize(view.use.id ?? use.skill)}”? The continuation stays.`))
                  onChange(removeSignatureOpportunity(draft, play.id, projection.id, view.binding.id));
              }}>Remove…</button>
          </div>
        </div>
      </details>;
    })}</div>
  </article>;
}

function OpportunityPracticeSummary({ summary }: {
  summary: NonNullable<ReturnType<typeof signatureOpportunityPracticeSummary>>;
}) {
  const activation = summary.firstActivationTick === null
    ? 'No authored activation area to measure'
    : `inside area t${summary.firstActivationTick}–${summary.lastActivationTick}`;
  return <section className="mt-3 rounded-sm border border-arena-cyan/35 bg-arena-cyan/5 p-3">
    <div className="flex flex-wrap items-center gap-2">
      <p className="lab text-arena-cyan">Last Practice evidence</p>
      <span className="pill ml-auto">{summary.acceptedUses} accepted / {summary.evaluations} evaluated</span>
    </div>
    <div className="mt-2 grid gap-2 sm:grid-cols-2">
      <p className="t-micro"><b className="block text-arena-ink">Where</b>{activation}</p>
      <p className="t-micro"><b className="block text-arena-ink">Authoritative events</b>{summary.signatureEvents}</p>
      <p className="t-micro"><b className="block text-arena-ink">Latest resolution</b>{humanize(summary.lastResolution ?? 'not evaluated')}</p>
      <p className="t-micro"><b className="block text-arena-ink">Then continued</b>{humanize(summary.continuation ?? 'not observed')}</p>
    </div>
    {summary.declineCauses.length > 0 && <div className="mt-2">
      <p className="lab">Why it continued instead</p>
      <div className="mt-1 flex flex-wrap gap-1">{summary.declineCauses.slice(0, 4).map((entry) =>
        <span key={entry.cause} className="pill">{entry.count}× {humanize(entry.cause)}</span>)}</div>
    </div>}
    {summary.routeContinuity && <p className="t-micro mt-2">After the latest accepted use at t{summary.routeContinuity.tick}: ({summary.routeContinuity.before.x},{summary.routeContinuity.before.y}) → ({summary.routeContinuity.after.x},{summary.routeContinuity.after.y}). These are authoritative occupied tiles, not inferred movement.</p>}
  </section>;
}

function opportunityHosts(orders: OrderNode[]) {
  const result: Array<{ id: string; label: string }> = [];
  const walk = (nodes: OrderNode[], depth: number) => nodes.forEach((node) => {
    if (node.id && (node.orders || depth < 2 && (node.move || node.fight)))
      result.push({ id: node.id, label: `${humanize(node.id)} · ${node.orders ? 'branch' : node.move ? 'Move' : 'Fight'}` });
    if (node.orders) walk(node.orders, depth + 1);
  });
  walk(orders, 0);
  return result;
}

function findOrderById(orders: OrderNode[], id: string): OrderNode | undefined {
  for (const node of orders) {
    if (node.id === id) return node;
    const nested = node.orders && findOrderById(node.orders, id);
    if (nested) return nested;
  }
  return undefined;
}

function orderName(order: OrderNode | null) {
  if (!order) return 'normal root fallthrough';
  if (order.move) return `${humanize(order.id ?? 'move')} · continue moving`;
  if (order.fight) return `${humanize(order.id ?? 'fight')} · continue fighting`;
  return humanize(order.id ?? 'next ordinary Order');
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
        zones={Object.keys(draft.mapPlan.zones)} roles={draft.orders.roles.map((role) => role.id)}
        onChange={(next) => onChange(updatePlayCondition(draft, play.id, conditionId, next))} />;
    })}</div>
  </details>;
}

function ConditionCard({
  id,
  value,
  zones = [],
  roles = [],
  onSelectGeometry,
  onChange,
}: {
  id: string;
  value: ConditionDefinition;
  zones?: string[];
  roles?: string[];
  onSelectGeometry?: (id: string) => void;
  onChange: (next: ConditionDefinition) => void;
}) {
  const subject = conditionSubjectKind(value.fact);
  const changeFact = (fact: string) => {
    const next: ConditionDefinition = {
      fact, operator: value.operator, value: value.value,
    };
    const kind = conditionSubjectKind(fact);
    if (kind === 'zone' || kind === 'role-zone') next.zone = value.zone ?? zones[0] ?? '';
    if (kind === 'role' || kind === 'role-zone') next.role = value.role ?? roles[0] ?? '';
    if (kind === 'well') next.well = value.well ?? 'centre';
    onChange(next);
  };
  return <article className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
    <b className="t-body block">{humanize(id)}</b>
    <label className="mt-2 block"><span className="t-micro mb-1 block">Observed fact</span>
      <select className="field min-h-11" value={value.fact}
        onChange={(event) => changeFact(event.target.value)}>
        {CONDITION_FACTS.map((fact) => <option key={fact} value={fact}>{humanize(fact)}</option>)}
      </select></label>
    <div className="mt-2 grid grid-cols-[minmax(0,1fr)_100px] gap-2">
      <select className="field min-h-11" value={value.operator}
        onChange={(event) => onChange({ ...value, operator: event.target.value as ConditionDefinition['operator'] })}>
        {['at-least', 'at-most', 'equals', 'less-than', 'greater-than'].map((operator) => <option key={operator}>{operator}</option>)}
      </select>
      <input type="number" className="field min-h-11" value={value.value}
        onChange={(event) => onChange({ ...value, value: Number(event.target.value) })} />
    </div>
    {(subject === 'zone' || subject === 'role-zone') && <label className="mt-2 block"><span className="t-micro mb-1 block">Zone</span>
      <select className="field min-h-11" value={value.zone ?? ''}
        onChange={(event) => onChange({ ...value, zone: event.target.value })}>
        <option value="">Choose a zone</option>
        {zones.map((zone) => <option key={zone} value={zone}>{humanize(zone)}</option>)}
      </select>
      {value.zone && onSelectGeometry && <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full"
        onClick={() => onSelectGeometry(value.zone!)}>Edit {humanize(value.zone)} on map</button>}
    </label>}
    {(subject === 'role' || subject === 'role-zone') && <label className="mt-2 block"><span className="t-micro mb-1 block">Role</span>
      <select className="field min-h-11" value={value.role ?? ''}
        onChange={(event) => onChange({ ...value, role: event.target.value })}>
        <option value="">Choose a role</option>
        {roles.map((role) => <option key={role} value={role}>{humanize(role)}</option>)}
      </select></label>}
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
  if (play.recipe === 'veteran-escort')
    return projection.id.endsWith('-veteran')
      ? draft.orders.roles.filter((role) => !claimed.has(role.id) && role.classes.length === 1)
      : draft.orders.roles.filter((role) => !claimed.has(role.id) && role.classes.length >= 1);
  if (play.recipe === 'safe-return')
    return projection.id.endsWith('-relay')
      ? draft.orders.roles.filter((role) => !claimed.has(role.id)
        && role.classes.length === 1 && role.classes[0] === 'relay')
      : draft.orders.roles.filter((role) => !claimed.has(role.id) && role.classes.length === 1);
  if (play.recipe === 'lane-closure')
    return projection.id.endsWith('-mason')
      ? draft.orders.roles.filter((role) => !claimed.has(role.id)
        && role.classes.length === 1 && role.classes[0] === 'mason')
      : draft.orders.roles.filter((role) => !claimed.has(role.id) && role.classes.length >= 2);
  return draft.orders.roles.filter((role) => !claimed.has(role.id));
}

function recipeName(recipe: PlayRecipe) {
  return recipe === 'relay-chain' ? 'Running Relay' : recipe === 'ambush' ? 'Ambush recipe' : recipe === 'blank' ? 'Blank Play' : humanize(recipe);
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

function conditionSummary(condition: ConditionDefinition | undefined) {
  if (!condition) return 'missing Condition';
  return [
    `${humanize(condition.fact)} ${humanize(condition.operator)} ${condition.value}`,
    condition.role && `for ${humanize(condition.role)}`,
    condition.zone && `in ${humanize(condition.zone)}`,
    condition.well && `at ${humanize(condition.well)} Well`,
  ].filter(Boolean).join(' ');
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
