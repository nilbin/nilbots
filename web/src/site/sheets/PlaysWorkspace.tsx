import { useEffect, useMemo, useState } from 'react';
import clsx from 'clsx';
import ClassIcon from '../../components/ClassIcon';
import type { ReplayModel } from '../../replayModel';
import type { TacticalSheetCatalog } from '../api';
import IdentifierInput from './IdentifierInput';
import SignatureUseEditor from './SignatureUseEditor';
import PlayMapCanvas from './PlayMapCanvas';
import {
  deletePlay,
  detachPlay,
  playDrifts,
  opportunityViews,
  resolveProjectionOrders,
  uniquePlayId,
} from './linkedPlays';
import {
  addPlayGeometry,
  addPlayPost,
  addCoreRecoveryOpportunity,
  addFightOpportunity,
  addHealthRecoveryOpportunity,
  addSignatureOpportunity,
  addSignatureOpportunityActivationZone,
  addSignatureOpportunityAnchor,
  addAmbushReserveSlot,
  appendPlayRouteWaypoint,
  configurePlayContingent,
  configurePlayLifecycle,
  configurePlayPostDeployment,
  configurePlaySharedTrigger,
  movePlayAnchor,
  moveProgram,
  moveSignatureOpportunity,
  rematerializeDraft,
  removeLastPlayRouteWaypoint,
  removePlayReadinessGroup,
  removeAmbushReserveSlot,
  removeSignatureOpportunity,
  renameSignatureOpportunity,
  renamePlayGeometry,
  replacePlayRole,
  setPlayLinkedValue,
  setPlayName,
  setPlayPostFacing,
  setPlayPostRequired,
  setPlayContingentExit,
  setPlayReadinessGroup,
  setPlayZoneRect,
  setSignatureOpportunityCapacity,
  setSignatureOpportunityCommitmentHold,
  setSignatureOpportunityWhen,
  updatePlayCondition,
  updateCoreRecoveryOpportunity,
  updateFightOpportunity,
  updateHealthRecoveryOpportunity,
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
    title: 'Blank Program',
    kicker: 'Start exact',
    description: 'Draw one response and add its participants without a recipe lens.',
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
  practiceDraft,
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
  practiceDraft?: SheetDraft;
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
          <p className="lab text-arena-material">Tactical Programs</p>
          <h2 className="type-display mt-1 text-[24px]">Build the response where it happens</h2>
          <p className="t-meta mt-1">Programs are tried top-to-bottom. Each Program keeps every participant, trigger, position and signature opportunity together; the game executes the generated Instructions underneath.</p>
        </div>
        <button type="button" className="btn btn-secondary min-h-11" data-testid="new-play"
          onClick={() => setCreating(creating ? null : 'ambush')}>＋ New Program</button>
      </div>
      <div className="mt-4 grid max-h-[360px] gap-2 overflow-y-auto pr-1" aria-label="Programs in priority order">
        {plays.map((entry, index) => <article key={entry.id}
          className={clsx('grid grid-cols-[minmax(0,1fr)_44px] overflow-hidden rounded-sm border',
            entry.id === play?.id ? 'border-arena-material/70 bg-arena-material/10' : 'border-arena-edge bg-arena-deep/30')}>
          <button type="button" className="min-h-16 min-w-0 p-2.5 text-left"
            onClick={() => { onSelectPlay(entry.id); setSelectedGeometry(entry.ownedGeometry[0]?.id ?? null); }}>
            <span className="lab text-arena-material">{index + 1} · {recipeName(entry.recipe)}</span>
            <b className="type-display mt-1 block truncate text-[16px]">{entry.name}</b>
            <span className="t-micro mt-1 block">{entry.projections.length} {entry.projections.length === 1 ? 'participant' : 'participants'} · {entry.ownedGeometry.length} map objects</span>
          </button>
          <span className="grid border-l border-arena-edge">
            <button type="button" className="btn min-h-10 rounded-none border-0" disabled={index === 0}
              aria-label={`Move ${entry.name} earlier`} onClick={() => commit(moveProgram(draft, entry.id, -1))}>↑</button>
            <button type="button" className="btn min-h-10 rounded-none border-0 border-t border-arena-edge" disabled={index === plays.length - 1}
              aria-label={`Move ${entry.name} later`} onClick={() => commit(moveProgram(draft, entry.id, 1))}>↓</button>
          </span>
        </article>)}
        {plays.length === 0 && <div className="rounded-sm border border-dashed border-arena-edge px-4 py-3 t-meta">No Programs yet. Start on the map; advanced unlinked Instructions remain available separately.</div>}
      </div>
      {plays.length > 0 && <p className="t-micro mt-2">Priority runs top to bottom. Reordering one Program moves every participant’s Instruction block together.</p>}
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
            <label className="mt-2 block"><span className="lab mb-1 block">Program name</span>
              <input className="field min-h-12 text-[17px] font-semibold" value={play.name} maxLength={60}
                onChange={(event) => commit(setPlayName(draft, play.id, event.target.value))} /></label>
            <p className="t-micro mt-2">Authoring sugar only · this Program becomes ordinary Instructions, never runtime Program state or a second scheduler.</p>
          </section>

          {playIssues.length > 0 && <section className="rounded-sm border border-arena-hot/55 bg-arena-hot/8 p-3" role="alert">
            <p className="lab text-arena-hot">Needs attention</p>
            <ul className="mt-2 space-y-1 t-micro">{playIssues.map((issue, index) => <li key={`${issue.path}:${index}`}>{issue.message}</li>)}</ul>
            {playDrifts(draft.orders, draft.workshop, play.id).length > 0 && <div className="mt-3 flex flex-wrap gap-2">
              <button type="button" className="btn btn-secondary min-h-11" onClick={() => commit(rematerializeDraft(draft, true).draft)}>Regenerate Instructions</button>
              <button type="button" className="btn min-h-11" onClick={() => {
                const next = structuredClone(draft);
                next.workshop = detachPlay(next.workshop, play.id);
                commit(next);
              }}>Detach to unlinked Instructions</button>
            </div>}
          </section>}

          <ParticipantControls draft={draft} play={play} catalog={catalog}
            onChange={commit} />
          <PostControls draft={draft} play={play} catalog={catalog}
            onSelectGeometry={(id, startDrawing = false) => {
              setSelectedGeometry(id);
              if (startDrawing) setPlacementGeometry(id);
              document.querySelector('[data-sheet-path="workshop.plays"]')
                ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }}
            onChange={commit} />
          {play.recipe === 'blank' && <PlayResponseControls draft={draft} play={play} tracks={tracks}
            onSelectGeometry={setSelectedGeometry} onChange={commit} />}
          <AlongTheWayControls draft={draft} play={play} catalog={catalog}
            practiceReplay={practiceReplay} practiceDraft={practiceDraft}
            onSelectGeometry={(id, startDrawing = false) => {
              setSelectedGeometry(id);
              if (startDrawing) setPlacementGeometry(id);
              document.querySelector('[data-sheet-path="workshop.plays"]')
                ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }}
            onOpenOrders={onOpenOrders} onChange={commit} />
          <LinkedOrdersControls draft={draft} play={play} onChange={commit} />
          <ConditionControls draft={draft} play={play} onChange={commit} />
          <GeneratedOrders play={play} onOpenOrders={onOpenOrders} />

          <section className="panel pad">
            <p className="lab">Ownership</p>
            <p className="t-micro mt-1">Detach keeps the current Instructions and map plan but removes linkage. Delete removes only this Program’s generated roots after confirmation.</p>
            <label className="mt-3 flex min-h-11 items-center gap-2 rounded-sm border border-arena-edge px-3 t-body">
              <input type="checkbox" checked={removeSources} onChange={(event) => setRemoveSources(event.target.checked)} />
              Also remove unused Program-owned conditions, capacities and geometry
            </label>
            <div className="mt-3 grid gap-2 sm:grid-cols-2">
              <button type="button" className="btn min-h-11" onClick={() => {
                const next = structuredClone(draft);
                next.workshop = detachPlay(next.workshop, play.id);
                commit(next);
              }}>Detach Program</button>
              <button type="button" className="btn min-h-11 text-arena-hot" onClick={() => {
                const roots = play.projections.flatMap((projection) => projection.lastGenerated.map((entry) => entry.orderId));
                if (!window.confirm(`Delete “${play.name}” and ${roots.length} generated root Instructions?`)) return;
                const deleted = deletePlay(draft, play.id, removeSources);
                commit(deleted.draft);
                onSelectPlay(null);
              }}>Delete Program…</button>
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
      window.alert(cause instanceof Error ? cause.message : 'Could not create this Program.');
    }
  };

  return <section className="panel pad" data-testid="create-play-panel">
    <div className="flex items-start gap-2"><div className="mr-auto"><p className="lab text-arena-material">New Program</p><h3 className="type-display mt-1 text-[20px]">Choose a map-first starting point</h3></div>
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
      <p className="t-micro mt-1">Roles are not invented or silently split by a Program.</p>
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
      <button type="button" className="btn min-h-11 min-w-11" aria-label="Move Program left" onClick={() => onChange(movePlayGeometryGroup(draft, play.id, play.geometryGroups[0].id, -1, 0, catalog.map.width, catalog.map.height))}>←</button>
      <button type="button" className="btn min-h-11 min-w-11" aria-label="Move Program up" onClick={() => onChange(movePlayGeometryGroup(draft, play.id, play.geometryGroups[0].id, 0, -1, catalog.map.width, catalog.map.height))}>↑</button>
      <button type="button" className="btn min-h-11 min-w-11" aria-label="Move Program down" onClick={() => onChange(movePlayGeometryGroup(draft, play.id, play.geometryGroups[0].id, 0, 1, catalog.map.width, catalog.map.height))}>↓</button>
      <button type="button" className="btn min-h-11 min-w-11" aria-label="Move Program right" onClick={() => onChange(movePlayGeometryGroup(draft, play.id, play.geometryGroups[0].id, 1, 0, catalog.map.width, catalog.map.height))}>→</button>
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

function ParticipantControls({ draft, play, catalog, onChange }: {
  draft: SheetDraft; play: LinkedPlay; catalog: TacticalSheetCatalog;
  onChange: (next: SheetDraft) => void;
}) {
  const available = draft.orders.roles.filter((role) =>
    !play.projections.some((projection) => projection.roleId === role.id));
  const [blankRole, setBlankRole] = useState(available[0]?.id ?? '');
  const selectedBlankRole = available.some((role) => role.id === blankRole)
    ? blankRole : available[0]?.id ?? '';
  return <section className="panel pad">
    <p className="lab text-arena-material">Participants</p>
    <p className="t-micro mt-1">Each participating role gets one generated Instruction block at this Program’s global position. Changing a participant updates its owned conditions, positions and partner targets together.</p>
    <div className="mt-3 space-y-2">{play.projections.map((projection) => {
      const role = draft.orders.roles.find((entry) => entry.id === projection.roleId);
      const allowed = compatibleRoles(draft, play, projection);
      return <article key={projection.id} className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
        <div className="flex items-center gap-2">
          <span className="flex -space-x-1">{role?.classes.map((classId, index) => <ClassIcon key={`${classId}:${index}`} classId={classId} size={34} decorative />)}</span>
          <div className="min-w-0 flex-1"><b className="type-display block truncate text-[15px]">{humanize(projection.roleId)}</b><span className="t-micro block">{projection.orders.length} generated root {projection.orders.length === 1 ? 'Instruction' : 'Instructions'}</span></div>
        </div>
        <label className="mt-2 block"><span className="t-micro mb-1 block">Role</span>
          <select className="field min-h-11" value={projection.roleId}
            onChange={(event) => onChange(replacePlayRole(draft, play.id, projection.id, event.target.value))}>
            {allowed.map((candidate) => <option key={candidate.id} value={candidate.id}>{humanize(candidate.id)} · {candidate.classes.map(humanize).join(', ')}</option>)}
          </select></label>
        {role && <p className="t-micro mt-2">{role.classes.map((classId) => catalog.classes.find((entry) => entry.id === classId)?.signatureName ?? humanize(classId)).join(' · ')}</p>}
        {role && <details className="mt-3 rounded-sm border border-arena-edge bg-arena-panel/40">
          <summary className="flex min-h-11 cursor-pointer items-center gap-2 px-3"><span className="t-body mr-auto">Generated Instructions</span><span className="pill">advanced · read-only</span><span className="disclosure-chevron">›</span></summary>
          <pre className="max-h-64 overflow-auto border-t border-arena-edge bg-[#080c10] p-3 text-[11px] leading-relaxed text-arena-muted">{JSON.stringify(resolveProjectionOrders(play, projection), null, 2)}</pre>
        </details>}
      </article>;
    })}</div>
    {play.recipe === 'blank' && available.length > 0 && <div className="mt-3 grid gap-2 rounded-sm border border-dashed border-arena-material/40 p-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-end">
      <label><span className="lab mb-1 block">Add a participating role</span>
        <select className="field min-h-11" value={selectedBlankRole} onChange={(event) => setBlankRole(event.target.value)}>
          {available.map((role) => <option key={role.id} value={role.id}>{humanize(role.id)}</option>)}
        </select></label>
      <button type="button" className="btn btn-secondary min-h-11" disabled={!selectedBlankRole}
        onClick={() => onChange(addBlankPlayProjection(draft, play.id, selectedBlankRole))}>＋ Add participant</button>
    </div>}
  </section>;
}

function PostControls({ draft, play, catalog, onSelectGeometry, onChange }: {
  draft: SheetDraft;
  play: LinkedPlay;
  catalog: TacticalSheetCatalog;
  onSelectGeometry: (id: string, startDrawing?: boolean) => void;
  onChange: (next: SheetDraft) => void;
}) {
  const [roleId, setRoleId] = useState(play.projections[0]?.roleId ?? '');
  const [name, setName] = useState('Guard post');
  const selectedRoleId = play.projections.some((entry) => entry.roleId === roleId)
    ? roleId : play.projections[0]?.roleId ?? '';
  const selectedProjection = play.projections.find((entry) =>
    entry.roleId === selectedRoleId);
  return <section className="panel pad" data-testid="program-posts">
    <p className="lab text-arena-material">Posts</p>
    <h3 className="type-display mt-1 text-[19px]">Stand here. Face there. Hold the ground.</h3>
    <p className="t-micro mt-1">A Post is one map-first composition. Underneath it remains an anchor, readiness area, capacity-one Move, facing point and optional deployment Instructions.</p>
    <div className="mt-3 space-y-2">{play.slots.map((slot) => {
      const projection = play.projections.find((entry) => entry.roleId === slot.roleId);
      const role = draft.orders.roles.find((entry) => entry.id === slot.roleId);
      const durable = role?.classes.flatMap((classId) => {
        const entry = catalog.classes.find((candidate) => candidate.id === classId);
        return entry?.signatureAuthoring.durableDeployment ? [entry] : [];
      }) ?? [];
      return <article key={slot.id} className="rounded-sm border border-arena-edge bg-arena-deep/30 p-3">
        <div className="flex flex-wrap items-start gap-2">
          <button type="button" className="min-h-11 min-w-0 flex-1 text-left"
            onClick={() => onSelectGeometry(slot.anchorId)}>
            <span className="lab text-arena-material">{humanize(slot.roleId)} · {slot.capacity ?? 1} body</span>
            <b className="type-display mt-1 block text-[16px]">{slot.name ?? humanize(slot.anchorId)}</b>
            <span className="t-micro mt-1 block">Stand {slot.tolerance === 0 ? 'exactly' : `within ${slot.tolerance} tiles`} → face {humanize(slot.facing ?? 'toward point')}</span>
          </button>
          <span className="pill">{slot.required ? 'required to arm' : 'optional setup'}</span>
        </div>
        <div className="mt-3 grid grid-cols-4 gap-1.5" aria-label={`Facing for ${slot.name ?? slot.id}`}>
          {(['north', 'east', 'south', 'west'] as const).map((facing) => <button
            key={facing} type="button" className="btn min-h-11" data-selected={slot.facing === facing}
            aria-pressed={slot.facing === facing}
            onClick={() => onChange(setPlayPostFacing(draft, play.id, slot.id, facing))}>
            {facing === 'north' ? '↑ N' : facing === 'east' ? '→ E' : facing === 'south' ? '↓ S' : '← W'}
          </button>)}
        </div>
        <label className="mt-3 flex min-h-12 items-start gap-3 rounded-sm border border-arena-edge bg-arena-panel/35 p-3">
          <input type="checkbox" className="mt-0.5 h-5 w-5 accent-[var(--color-arena-material)]"
            checked={slot.required} disabled={!findOrderById(projection?.orders ?? [], `${projection?.id}-respond`)}
            onChange={(event) => onChange(setPlayPostRequired(
              draft, play.id, slot.id, event.target.checked))} />
          <span><span className="lab block">Required before response</span>
            <span className="t-micro mt-1 block">Every required Post gets its own role-qualified readiness check. {!projection || !findOrderById(projection.orders, `${projection.id}-respond`) ? 'Build this participant response first.' : ''}</span></span>
        </label>
        {slot.deployment ? <div className="mt-3 rounded-sm border border-arena-cyan/35 bg-arena-cyan/5 p-3">
          <div className="flex flex-wrap items-center gap-2"><b className="t-body mr-auto">Keep {humanize(slot.deployment.skill)} at {humanize(slot.deployment.targetAnchorId)}</b>
            <span className="pill">{slot.deployment.requiredForReadiness ? 'required' : 'opportunity'}</span></div>
          <p className="t-micro mt-1">{slot.deployment.rebuildWhenMissing ? 'Rebuild when missing' : 'No missing rebuild'}{slot.deployment.refreshDurabilityAtMost !== undefined ? ` · refresh at ${slot.deployment.refreshDurabilityAtMost}% durability` : ''}{slot.deployment.refreshLifetimeAtMost !== undefined ? ` · refresh at ${slot.deployment.refreshLifetimeAtMost}% lifetime` : ''}</p>
          <button type="button" className="btn btn-secondary mt-2 min-h-11 w-full"
            onClick={() => onSelectGeometry(slot.deployment!.targetAnchorId)}>Move effect target on map</button>
        </div> : durable.length > 0 && projection && <button type="button"
          className="btn btn-secondary mt-3 min-h-11 w-full"
          onClick={() => {
            const stand = draft.mapPlan.anchors[slot.anchorId] ?? [15, 13];
            const target: [number, number] = [Math.min(catalog.map.width - 2, stand[0] + 2), stand[1]];
            const next = configurePlayPostDeployment(draft, play.id, projection.id, slot.id, {
              skill: durable[0].signatureId, target,
              requiredForReadiness: Boolean(findOrderById(projection.orders, `${projection.id}-respond`)),
              rebuildWhenMissing: true,
            });
            onChange(next);
            const deployed = next.workshop.plays.find((entry) => entry.id === play.id)
              ?.slots.find((entry) => entry.id === slot.id)?.deployment;
            if (deployed) onSelectGeometry(deployed.targetAnchorId, true);
          }}>＋ Maintain {durable[0].signatureName} from this Post</button>}
      </article>;
    })}</div>
    {play.projections.length > 0 && <div className="mt-3 rounded-sm border border-dashed border-arena-material/45 p-3">
      <div className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto] sm:items-end">
        <label><span className="lab mb-1 block">Who stands here</span>
          <select className="field min-h-11" value={selectedRoleId}
            onChange={(event) => setRoleId(event.target.value)}>
            {play.projections.map((projection) => <option key={projection.id} value={projection.roleId}>{humanize(projection.roleId)}</option>)}
          </select></label>
        <label><span className="lab mb-1 block">Post name</span>
          <input className="field min-h-11" value={name} maxLength={40}
            onChange={(event) => setName(event.target.value)} /></label>
        <button type="button" className="btn btn-primary min-h-11" disabled={!selectedProjection}
          onClick={() => {
            const next = addPlayPost(draft, play.id, selectedProjection!.id, {
              name, point: [15, 13], facing: 'east', required: false,
            });
            onChange(next);
            const created = next.workshop.plays.find((entry) => entry.id === play.id)!.slots.at(-1)!;
            onSelectGeometry(created.anchorId, true);
          }}>＋ Place Post</button>
      </div>
      <p className="t-micro mt-2">Place it on the map, then set facing and any signature effect independently. Required setup becomes available after this participant has a response.</p>
    </div>}
    {play.projections.length === 0 && <p className="t-meta mt-3">Add a participant before placing Posts.</p>}
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
  const inferred = play.responseTriggerConditionId
    ?? common[0] ?? '';
  const triggerId = draft.orders.conditions[inferred] ? inferred : '';
  const trigger = triggerId ? draft.orders.conditions[triggerId] : undefined;
  const zoneIds = play.ownedGeometry.filter((entry) =>
    entry.kind === 'zone' && !entry.derivedFrom).map((entry) => entry.id);
  const [newTriggerZone, setNewTriggerZone] = useState(zoneIds[0] ?? '');
  const selectedZone = zoneIds.includes(newTriggerZone)
    ? newTriggerZone : zoneIds[0] ?? '';
  const directEnemyTrigger = trigger?.fact === 'visible-enemies-in-zone'
    && Boolean(trigger.zone);

  return <section className="panel pad" data-testid="play-response-design">
    <p className="lab text-arena-material">Shared response</p>
    <h3 className="type-display mt-1 text-[19px]">One start, independent contingents</h3>
    <p className="t-micro mt-1">Choose the situation once. The Program derives the shared Condition and every participant gate underneath.</p>
    {!trigger ? <div className="mt-3 rounded-sm border border-arena-material/35 bg-arena-material/8 p-3">
      <p className="type-display text-[16px]">What starts the response?</p>
      <p className="t-micro mt-1">Start with enemy pressure in a drawn area. Exact Condition source remains available in Advanced.</p>
      {zoneIds.length > 0 ? <>
        <label className="mt-3 block"><span className="lab mb-1 block">Watched area</span>
          <select className="field min-h-11" value={selectedZone}
            onChange={(event) => setNewTriggerZone(event.target.value)}>
            {zoneIds.map((id) => <option key={id} value={id}>{humanize(id)}</option>)}
          </select></label>
        <button type="button" className="btn btn-primary mt-3 min-h-11 w-full"
          onClick={() => onChange(configurePlaySharedTrigger(draft, play.id, {
            fact: 'visible-enemies-in-zone', operator: 'at-least', value: 5,
            zone: selectedZone,
          }))}>Create shared start · 5 enemies in {humanize(selectedZone)}</button>
      </> : <p className="t-meta mt-3">Draw either ambush area on the map above, then return here.</p>}
    </div> : <div className="mt-3 rounded-sm border border-arena-material/40 bg-arena-material/8 p-3">
      <div className="flex flex-wrap items-start gap-2">
        <div className="mr-auto"><p className="lab">Overall response starts when</p>
          <p className="type-display mt-1 text-[17px]">{conditionSummary(draft.orders.conditions[triggerId])}</p></div>
        {common.includes(triggerId) && <span className="pill text-arena-cyan">shared by every configured contingent</span>}
      </div>
      {directEnemyTrigger ? <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <label><span className="lab mb-1 block">Enemies required · {trigger.value}</span>
          <input type="range" className="w-full accent-[var(--color-arena-material)]"
            min={1} max={8} value={trigger.value}
            onChange={(event) => onChange(configurePlaySharedTrigger(draft, play.id, {
              ...trigger, value: Number(event.target.value),
            }))} /></label>
        <label><span className="lab mb-1 block">Watched area</span>
          <select className="field min-h-11" value={trigger.zone}
            onChange={(event) => onChange(configurePlaySharedTrigger(draft, play.id, {
              ...trigger, zone: event.target.value,
            }))}>
            {zoneIds.map((id) => <option key={id} value={id}>{humanize(id)}</option>)}
          </select></label>
        <button type="button" className="btn btn-secondary min-h-11 sm:col-span-2"
          onClick={() => onSelectGeometry(trigger.zone!)}>Edit {humanize(trigger.zone!)} on map</button>
      </div> : <p className="t-micro mt-3">This Program keeps its exact Advanced trigger. No participant wiring is required.</p>}
    </div>}
    <div className="mt-3 grid gap-3">{play.projections.map((projection) =>
      <ContingentCard key={projection.id} draft={draft} play={play}
        projection={projection} triggerId={triggerId} tracks={tracks}
        onSelectGeometry={onSelectGeometry} onChange={onChange} />)}</div>
    <CombinedReadinessControls key={play.id} draft={draft} play={play} onChange={onChange} />
    <ProgramLifecycleControls draft={draft} play={play} onChange={onChange} />
  </section>;
}

function ProgramLifecycleControls({ draft, play, onChange }: {
  draft: SheetDraft;
  play: LinkedPlay;
  onChange: (next: SheetDraft) => void;
}) {
  const current = play.lifecycle;
  const [minimum, setMinimum] = useState(current?.minimumCommitmentTicks ?? 0);
  const [maximum, setMaximum] = useState(current?.maximumTicks ?? 90);
  const [cooldown, setCooldown] = useState(current?.cooldownTicks ?? 15);
  const [waiting, setWaiting] = useState(current?.waiting ?? 'hold-posts');
  const [success, setSuccess] = useState(current?.successConditionIds?.[0] ?? '');
  const conditions = Object.keys(draft.orders.conditions).filter((id) =>
    !id.startsWith(`${play.id}-minimum-commitment`));
  return <details className="mt-3 rounded-sm border border-arena-edge bg-arena-panel/35" open={Boolean(current)}>
    <summary className="flex min-h-14 cursor-pointer items-center gap-2 px-3">
      <div className="mr-auto"><p className="lab text-arena-material">Commitment and continuation</p>
        <p className="t-micro mt-1">How long the response persists, retries and waits</p></div>
      <span className="pill">{current ? 'configured' : 'optional'}</span><span className="disclosure-chevron">›</span>
    </summary>
    <div className="space-y-3 border-t border-arena-edge p-3">
      <div className="grid grid-cols-3 gap-2">
        <label><span className="lab mb-1 block">Commit at least</span>
          <input type="number" className="field min-h-11" min={0} max={1200} value={minimum}
            onChange={(event) => setMinimum(Number(event.target.value))} /></label>
        <label><span className="lab mb-1 block">Give up after</span>
          <input type="number" className="field min-h-11" min={1} max={1200} value={maximum}
            onChange={(event) => setMaximum(Number(event.target.value))} /></label>
        <label><span className="lab mb-1 block">Retry delay</span>
          <input type="number" className="field min-h-11" min={0} max={1200} value={cooldown}
            onChange={(event) => setCooldown(Number(event.target.value))} /></label>
      </div>
      <label><span className="lab mb-1 block">Success</span>
        <select className="field min-h-11" value={success}
          onChange={(event) => setSuccess(event.target.value)}>
          <option value="">Minimum commitment or timeout only</option>
          {conditions.map((id) => <option key={id} value={id}>{programSourceName(id, play.id)}</option>)}
        </select></label>
      <div><span className="lab mb-1 block">Before the response is ready</span>
        <div className="grid gap-1.5 sm:grid-cols-3">{([
          ['hold-posts', 'Hold Posts', 'Stay set and visible'],
          ['prepare-conditionally', 'Prepare conditionally', 'Only under the shared trigger'],
          ['fall-through', 'Fall through', 'Run the next Program'],
        ] as const).map(([value, label, copy]) => <button key={value} type="button"
          className="choice-card min-h-16 text-left" data-selected={waiting === value}
          aria-pressed={waiting === value} onClick={() => setWaiting(value)}>
          <b className="t-body block">{label}</b><span className="t-micro mt-1 block">{copy}</span>
        </button>)}</div>
      </div>
      <button type="button" className="btn btn-primary min-h-11 w-full"
        disabled={maximum < minimum}
        onClick={() => onChange(configurePlayLifecycle(draft, play.id, {
          minimumCommitmentTicks: minimum,
          successConditionIds: success ? [success] : [],
          maximumTicks: maximum,
          cooldownTicks: cooldown,
          waiting,
          ...(waiting === 'prepare-conditionally' && play.responseTriggerConditionId
            ? { preparationConditionId: play.responseTriggerConditionId } : {}),
        }))}>Apply commitment</button>
      {maximum < minimum && <p className="t-micro text-arena-hot">Give up after must be at least the minimum commitment.</p>}
    </div>
  </details>;
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
  const [activation, setActivation] = useState<'program-trigger' | 'immediate'>(
    projection.responseActivation ?? 'program-trigger');
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
    </label> : <p className="t-meta mt-3">Draw a Program-owned {kind} on the map first.</p>}
    <div className="mt-3"><span className="lab mb-1 block">Participant starts</span>
      <div className="grid grid-cols-2 gap-1.5">
        <button type="button" className="choice-card min-h-14" data-selected={activation === 'program-trigger'}
          aria-pressed={activation === 'program-trigger'} onClick={() => setActivation('program-trigger')}>
          <b className="t-body block">With Program</b><span className="t-micro">Wait for the shared start.</span></button>
        <button type="button" className="choice-card min-h-14" data-selected={activation === 'immediate'}
          aria-pressed={activation === 'immediate'} onClick={() => setActivation('immediate')}>
          <b className="t-body block">Immediately</b><span className="t-micro">Begin independently now.</span></button>
      </div>
    </div>
    {kind === 'zone' && <label className="mt-3 block"><span className="lab mb-1 block">Bodies required · {required}/{role.classes.length}</span>
      <input type="range" className="w-full accent-[var(--color-arena-material)]"
        min={1} max={Math.max(1, role.classes.length)} value={Math.min(required, role.classes.length)}
        onChange={(event) => setRequired(Number(event.target.value))} />
      <span className="t-micro mt-1 block">One role-qualified readiness Condition; no per-body anchors to maintain.</span>
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
        activation,
        destination: kind === 'zone' ? { zone: chosen } : { route: chosen },
        requiredBodies: kind === 'zone' ? required : 0,
        timeout,
      }))}>{configured ? 'Apply refinement' : 'Build participant Instructions'}</button>
    {configured && <div className="mt-3 grid gap-2 rounded-sm border border-arena-edge bg-arena-panel/45 p-3 sm:grid-cols-2">
      <div><p className="lab">Starts</p><p className="t-micro mt-1">{projection.responseActivation === 'immediate'
        ? 'Immediately, independent of the shared Program trigger'
        : conditionSummary(draft.orders.conditions[triggerId])}{ready ? ` AND ${conditionSummary(ready)}` : ''}</p></div>
      <div><p className="lab">During</p><p className="t-micro mt-1">{currentRoute ? `Move on ${humanize(currentRoute)}` : 'Fight from the formed area'} · timeout {response?.timeout}</p></div>
      <p className="t-micro sm:col-span-2">On failure, timeout, or no executable Instruction, this role falls through to its next participating Program. After all Programs, advanced unlinked Instructions remain the compatibility fallback.</p>
      <details className="sm:col-span-2 rounded-sm border border-arena-edge bg-arena-deep/30">
        <summary className="flex min-h-11 cursor-pointer items-center gap-2 px-3"><span className="t-body mr-auto">Advanced explicit exit</span><span className="pill">optional</span><span className="disclosure-chevron">›</span></summary>
        <label className="block border-t border-arena-edge p-3"><span className="lab mb-1 block">Exact exit Condition</span>
          <select className="field min-h-11" value={exit}
            onChange={(event) => onChange(setPlayContingentExit(
              draft, play.id, projection.id, event.target.value || null))}>
            <option value="">None · timeout or pre-emption</option>
            {allConditions.map((id) => <option key={id} value={id}>{programSourceName(id, play.id)}</option>)}
          </select>
          <span className="t-micro mt-2 block">Natural choices such as “four enemies destroyed” and “Core acquired” are a follow-up; raw Conditions remain the escape hatch.</span>
        </label>
      </details>
    </div>}
  </article>;
}

function CombinedReadinessControls({ draft, play, onChange }: {
  draft: SheetDraft;
  play: LinkedPlay;
  onChange: (next: SheetDraft) => void;
}) {
  const eligible = readinessParticipants(draft, play);
  const groups = play.readinessGroups ?? [];
  const grouped = new Set(groups.flatMap((entry) => entry.projectionIds));
  const available = eligible.filter((entry) => !grouped.has(entry.projectionId));
  const [creating, setCreating] = useState(false);

  return <section className="mt-3 rounded-sm border border-arena-edge bg-arena-panel/35 p-3">
    <p className="lab text-arena-material">Readiness relationships</p>
    <h4 className="type-display mt-1 text-[17px]">Independent by default, combined when the tactic needs it</h4>
    <p className="t-micro mt-1">Each area contingent already owns its role count. Combining them makes one visible ALL gate for every selected participant—no manual Condition wiring.</p>
    <div className="mt-3 space-y-2">{groups.map((group) =>
      <ReadinessGroupEditor key={group.id} draft={draft} play={play}
        groupId={group.id} initialName={group.name}
        initialProjectionIds={group.projectionIds} eligible={eligible}
        onChange={onChange} />)}</div>
    {creating ? <ReadinessGroupEditor draft={draft} play={play}
      groupId={null} initialName="Return ambush"
      initialProjectionIds={available.map((entry) => entry.projectionId)}
      eligible={eligible} onChange={(next) => { onChange(next); setCreating(false); }}
      onCancel={() => setCreating(false)} />
      : <button type="button" className="btn btn-secondary mt-3 min-h-11 w-full"
        disabled={eligible.length < 2}
        onClick={() => setCreating(true)}>＋ Combine participant readiness</button>}
    {eligible.length < 2 && <p className="t-micro mt-2">Configure at least two participants to form in areas before combining them.</p>}
  </section>;
}

function ReadinessGroupEditor({
  draft,
  play,
  groupId,
  initialName,
  initialProjectionIds,
  eligible,
  onChange,
  onCancel,
}: {
  draft: SheetDraft;
  play: LinkedPlay;
  groupId: string | null;
  initialName: string;
  initialProjectionIds: string[];
  eligible: ReturnType<typeof readinessParticipants>;
  onChange: (next: SheetDraft) => void;
  onCancel?: () => void;
}) {
  const [name, setName] = useState(initialName);
  const [selected, setSelected] = useState(initialProjectionIds);
  useEffect(() => {
    setName(initialName);
    setSelected(initialProjectionIds);
  }, [initialName, initialProjectionIds.join('|')]);
  const selectedEntries = eligible.filter((entry) => selected.includes(entry.projectionId));
  const required = selectedEntries.reduce((sum, entry) => sum + entry.required, 0);
  const bodies = selectedEntries.reduce((sum, entry) => sum + entry.bodies, 0);
  const toggle = (projectionId: string) => setSelected((current) =>
    current.includes(projectionId)
      ? current.filter((id) => id !== projectionId)
      : [...current, projectionId]);

  return <article className="rounded-sm border border-arena-material/35 bg-arena-deep/35 p-3">
    <div className="flex items-center gap-2">
      <label className="min-w-0 flex-1"><span className="lab mb-1 block">Readiness group</span>
        <input className="field min-h-11" value={name} maxLength={48}
          onChange={(event) => setName(event.target.value)} /></label>
      <span className="pill shrink-0 text-arena-cyan">{required}/{bodies} ready</span>
    </div>
    <div className="mt-3 grid gap-2 sm:grid-cols-2">{eligible.map((entry) => {
      const active = selected.includes(entry.projectionId);
      return <button key={entry.projectionId} type="button"
        className="choice-card min-h-16 text-left" data-selected={active}
        aria-pressed={active} onClick={() => toggle(entry.projectionId)}>
        <b className="t-body block">{humanize(entry.roleId)}</b>
        <span className="t-micro mt-1 block">{entry.required}/{entry.bodies} in {humanize(entry.zoneId)}</span>
      </button>;
    })}</div>
    <p className="t-micro mt-2">Selected roles each keep their own generated role-in-area Condition. Their participant Instructions receive the same flat ALL gate.</p>
    <div className="mt-3 flex flex-wrap gap-2">
      <button type="button" className="btn btn-primary min-h-11 flex-1"
        disabled={selected.length < 2}
        onClick={() => onChange(setPlayReadinessGroup(
          draft, play.id, groupId, name, selected))}>
        {groupId ? `Apply ${required}/${bodies} refinement` : `Arm ${required}/${bodies} together`}
      </button>
      {groupId ? <button type="button" className="btn min-h-11 text-arena-hot"
        onClick={() => onChange(removePlayReadinessGroup(draft, play.id, groupId))}>Separate</button>
        : <button type="button" className="btn min-h-11" onClick={onCancel}>Cancel</button>}
    </div>
  </article>;
}

function readinessParticipants(draft: SheetDraft, play: LinkedPlay) {
  return (play.formations ?? []).flatMap((formation) => {
    const projection = play.projections.find((entry) =>
      entry.id === formation.projectionId);
    const condition = draft.orders.conditions[formation.conditionId];
    const role = draft.orders.roles.find((entry) => entry.id === projection?.roleId);
    if (!projection || !condition?.zone || !role) return [];
    return [{
      projectionId: projection.id,
      roleId: role.id,
      conditionId: formation.conditionId,
      zoneId: condition.zone,
      required: condition.value,
      bodies: role.classes.length,
    }];
  });
}

function AlongTheWayControls({
  draft,
  play,
  catalog,
  practiceReplay,
  practiceDraft,
  onSelectGeometry,
  onOpenOrders,
  onChange,
}: {
  draft: SheetDraft;
  play: LinkedPlay;
  catalog: TacticalSheetCatalog;
  practiceReplay?: ReplayModel;
  practiceDraft?: SheetDraft;
  onSelectGeometry: (id: string, startDrawing?: boolean) => void;
  onOpenOrders: (playId: string, projectionId?: string) => void;
  onChange: (next: SheetDraft) => void;
}) {
  return <section className="panel pad">
    <p className="lab text-arena-material">Opportunities</p>
    <h3 className="type-display mt-1 text-[19px]">What may interrupt each participant’s main plan?</h3>
    <p className="t-micro mt-1">Skill, Fight, nearby Core and health cards share one real top-to-bottom local order. A card that cannot act yields in the same tick; the participant’s ordinary continuation is pinned last.</p>
    <div className="mt-3 space-y-3">{play.projections.map((projection) => {
      const role = draft.orders.roles.find((entry) => entry.id === projection.roleId);
      if (!role) return null;
      return <OpportunityProjection key={projection.id} draft={draft} play={play}
        projection={projection} role={role} catalog={catalog}
        practiceReplay={practiceReplay} practiceDraft={practiceDraft}
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
  practiceDraft,
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
  practiceDraft?: SheetDraft;
  onSelectGeometry: (id: string, startDrawing?: boolean) => void;
  onOpenOrders: (playId: string, projectionId?: string) => void;
  onChange: (next: SheetDraft) => void;
}) {
  const views = opportunityViews(projection, draft.orders.conditions);
  const hosts = opportunityHosts(projection.orders, projection.id);
  const skills = [...new Set(role.classes.map((classId) =>
    skillForClass(catalog, classId)).filter((entry): entry is string => Boolean(entry)))];
  const [adding, setAdding] = useState(false);
  const [kind, setKind] = useState<'use' | 'fight' | 'core-recovery' | 'health-recovery'>('use');
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
        disabled={hosts.length === 0}
        onClick={() => setAdding((value) => !value)}>＋ Add opportunity</button>
    </div>
    {adding && <div className="mt-3 rounded-sm border border-arena-material/35 bg-arena-material/5 p-3">
      <p className="lab text-arena-material">Choose the interruption</p>
      <div className="mt-2 grid grid-cols-2 gap-1.5 sm:grid-cols-4">
        {([
          ['use', 'Use skill', 'Signature timing and target'],
          ['fight', 'Fight', 'Contact, targets and chase'],
          ['core-recovery', 'Nearby Core', 'Visible Core within a detour'],
          ['health-recovery', 'Recover health', 'Heal with real hysteresis'],
        ] as const).map(([value, label, description]) => <button key={value}
          type="button" className="choice-card min-h-20 text-left" data-selected={kind === value}
          aria-pressed={kind === value} onClick={() => setKind(value)}>
          <b className="t-body block">{label}</b><span className="t-micro mt-1 block">{description}</span>
        </button>)}
      </div>
      <div className="mt-3 grid gap-2 sm:grid-cols-2">
        <label><span className="lab mb-1 block">During which behavior?</span>
          <select className="field min-h-11" value={selectedHost}
            onChange={(event) => setHostId(event.target.value)}>
            {hosts.map((host) => <option key={host.id} value={host.id}>{host.label}</option>)}
          </select></label>
        {kind === 'use' && <label><span className="lab mb-1 block">Role-owned skill</span>
          <select className="field min-h-11" value={selectedSkill}
            onChange={(event) => setSkill(event.target.value)}>
            {skills.map((candidate) => <option key={candidate} value={candidate}>
              {catalog.classes.find((entry) => entry.signatureId === candidate)?.signatureName ?? humanize(candidate)}
            </option>)}
          </select></label>}
      </div>
      <p className="t-micro mt-2">The selected Move, Follow, Patrol, Post or durable Fight becomes one ordinary branch. Its current action remains pinned as the final continuation.</p>
      <button type="button" className="btn btn-primary mt-3 min-h-11 w-full"
        disabled={!selectedHost || kind === 'use' && !selectedSkill}
        onClick={() => {
          let next: SheetDraft;
          if (kind === 'use') {
            const preserveCommitment = catalog.classes.find((entry) =>
              entry.signatureId === selectedSkill)?.signatureAuthoring
              .defaultCommitmentBehavior === 'preserve-by-default';
            next = addSignatureOpportunity(draft, play.id, projection.id,
              selectedHost, { skill: selectedSkill,
                target: defaultSignatureTarget(catalog, selectedSkill) },
              preserveCommitment);
          } else if (kind === 'fight') next = addFightOpportunity(
            draft, play.id, projection.id, selectedHost, {
              withinWalkingSteps: 4,
              targets: ['carrier', 'weakest', 'nearest'],
              style: 'advance', chaseLimit: 3, capacity: null,
            });
          else if (kind === 'core-recovery') next = addCoreRecoveryOpportunity(
            draft, play.id, projection.id, selectedHost, {
              withinWalkingSteps: 5, preference: 'closest',
              pursuersPerCore: 1, coordinateAcrossSheet: true,
            });
          else next = addHealthRecoveryOpportunity(
            draft, play.id, projection.id, selectedHost, {
              startBelow: 30, recoverUntil: 70, withinWalkingSteps: 8,
              timeout: 60, cooldown: 20, patientsPerTile: 1,
              coordinateAcrossSheet: true, allowCoreCarriers: false,
            });
          onChange(next);
          setAdding(false);
        }}>Add {kind === 'use' ? 'skill' : kind === 'fight' ? 'Fight' : kind === 'core-recovery' ? 'Core recovery' : 'health recovery'}</button>
    </div>}
    <div className="mt-3 space-y-2">{views.map((view) => {
      const use = view.action.use;
      const when = view.action.when;
      const simpleWhen = typeof when === 'string' ? when : '';
      const commitmentCondition = view.commitment;
      const descriptor = use ? catalog.classes.find((entry) =>
        entry.signatureId === use.skill)?.signatureAuthoring : undefined;
      const evidencePlay = practiceDraft?.workshop.plays.find((entry) => entry.id === play.id);
      const evidenceProjection = evidencePlay?.projections.find((entry) => entry.id === projection.id);
      const evidenceBinding = evidenceProjection?.signatureOpportunities?.find((entry) => entry.id === view.binding.id);
      const practice = use && practiceDraft && evidenceProjection && evidenceBinding
        ? signatureOpportunityPracticeSummary(
          practiceReplay, practiceDraft, evidenceProjection, evidenceBinding)
        : use && !practiceDraft ? null : null;
      const opportunityKind = view.binding.kind ?? 'use';
      return <details key={view.binding.id} className="rounded-sm border border-arena-material/30 bg-arena-panel/35" open={views.length === 1}>
        <summary className="flex min-h-14 cursor-pointer items-center gap-2 px-3">
          <span className="pill">{view.ordinal + 1}</span>
          <ClassIcon classId={use ? catalog.classes.find((entry) => entry.signatureId === use.skill)?.id ?? role.classes[0] : role.classes[0]} size={34} decorative />
          <span className="mr-auto min-w-0"><b className="type-display block truncate text-[15px]">{opportunityTitle(view.action, opportunityKind)}</b>
            <span className="t-micro block truncate">{opportunitySummary(view.action, view.binding, draft)} → otherwise {orderName(view.continuation, projection.id)}</span></span>
          <span className="disclosure-chevron">›</span>
        </summary>
        <div className="border-t border-arena-edge p-3">
          <label className="block"><span className="lab mb-1 block">Opportunity name</span>
            <IdentifierInput value={view.action.id ?? view.binding.useOrderId}
              isTaken={(id) => id !== view.binding.useOrderId && Boolean(findOrderById(projection.orders, id))}
              onCommit={(id) => id !== view.binding.useOrderId && onChange(
                renameSignatureOpportunity(draft, play.id, projection.id, view.binding.id, id))} /></label>
          {use ? <><SignatureUseEditor value={use} role={role} roles={draft.orders.roles}
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
                  <option key={id} value={id}>{programSourceName(id, play.id)}</option>)}
              </select>
              {when && typeof when !== 'string' && <span className="t-micro mt-1 block">Combined condition · inspect advanced Instructions to see every term.</span>}
            </label>
            <label><span className="lab mb-1 block">Capacity · bodies on this resolved target</span>
              <select className="field min-h-11" value={view.action.capacity ?? ''}
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
          </> : <GenericOpportunityEditor draft={draft} play={play}
            projection={projection} view={view} onChange={onChange} />}
          <div className="mt-3 rounded-sm border border-arena-edge bg-arena-deep/35 p-2">
            <p className="lab">Otherwise</p>
            <p className="t-meta mt-1">{orderName(view.continuation, projection.id)}</p>
          </div>
          {practice && <OpportunityPracticeSummary summary={practice} />}
          <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
            <button type="button" className="btn min-h-11" disabled={view.ordinal === 0}
              onClick={() => onChange(moveSignatureOpportunity(draft, play.id, projection.id, view.binding.id, -1))}>↑ Earlier</button>
            <button type="button" className="btn min-h-11" disabled={view.ordinal === view.count - 1}
              onClick={() => onChange(moveSignatureOpportunity(draft, play.id, projection.id, view.binding.id, 1))}>↓ Later</button>
            <button type="button" className="btn min-h-11"
              onClick={() => onOpenOrders(play.id, projection.id)}>Inspect Instructions</button>
            <button type="button" className="btn min-h-11 text-arena-hot"
              onClick={() => {
                if (window.confirm(`Remove “${opportunityTitle(view.action, opportunityKind)}”? The continuation stays.`))
                  onChange(removeSignatureOpportunity(draft, play.id, projection.id, view.binding.id));
              }}>Remove…</button>
          </div>
        </div>
      </details>;
    })}</div>
  </article>;
}

function GenericOpportunityEditor({ draft, play, projection, view, onChange }: {
  draft: SheetDraft;
  play: LinkedPlay;
  projection: PlayProjection;
  view: ReturnType<typeof opportunityViews>[number];
  onChange: (next: SheetDraft) => void;
}) {
  const kind = view.binding.kind;
  if (kind === 'fight' && view.action.fight) {
    const contactId = view.binding.ownedConditionIds?.find((id) =>
      draft.orders.conditions[id]?.fact === 'self-nearest-visible-enemy-distance');
    const contact = contactId ? draft.orders.conditions[contactId] : undefined;
    const fight = view.action.fight;
    const setup = {
      withinWalkingSteps: contact?.value ?? 4,
      targets: fight.targets,
      style: fight.style,
      chaseLimit: fight.chaseLimit,
      zone: fight.zone,
      capacity: view.binding.capacityPolicy?.limit ?? null,
    };
    const update = (patch: Partial<typeof setup>) => onChange(updateFightOpportunity(
      draft, play.id, projection.id, view.binding.id, { ...setup, ...patch }));
    return <div className="space-y-3">
      <div className="grid gap-2 sm:grid-cols-2">
        <label><span className="lab mb-1 block">Join when reachable within</span>
          <input type="number" className="field min-h-11" min={0} max={31}
            value={setup.withinWalkingSteps}
            onChange={(event) => update({ withinWalkingSteps: Number(event.target.value) })} /></label>
        <label><span className="lab mb-1 block">Chase limit</span>
          <input type="number" className="field min-h-11" min={0} max={12}
            value={setup.chaseLimit}
            onChange={(event) => update({ chaseLimit: Number(event.target.value) })} /></label>
      </div>
      <div><span className="lab mb-1 block">Movement in the fight</span>
        <div className="grid grid-cols-3 gap-1.5">{(['advance', 'hold', 'kite'] as const).map((style) =>
          <button key={style} type="button" className="choice-card min-h-12"
            data-selected={setup.style === style} aria-pressed={setup.style === style}
            onClick={() => update({ style })}>{humanize(style)}</button>)}</div>
      </div>
      <label><span className="lab mb-1 block">Only fight inside</span>
        <select className="field min-h-11" value={setup.zone ?? ''}
          onChange={(event) => update({ zone: event.target.value || undefined })}>
          <option value="">Anywhere</option>
          {Object.keys(draft.mapPlan.zones).map((zone) =>
            <option key={zone} value={zone}>{humanize(zone)}</option>)}
        </select></label>
      <label><span className="lab mb-1 block">Maximum attackers on one enemy</span>
        <select className="field min-h-11" value={setup.capacity ?? ''}
          onChange={(event) => update({ capacity: event.target.value ? Number(event.target.value) : null })}>
          <option value="">Everyone who qualifies</option>
          {[1, 2, 3, 4, 5, 6, 7, 8].map((value) => <option key={value}>{value}</option>)}
        </select></label>
    </div>;
  }
  if (kind === 'core-recovery' && view.action.move) {
    const distanceId = view.binding.ownedConditionIds?.find((id) =>
      draft.orders.conditions[id]?.fact === 'self-nearest-visible-loose-core-distance');
    const distance = distanceId ? draft.orders.conditions[distanceId] : undefined;
    const preference = view.action.move.to === 'highest-bank-value-visible-loose-core'
      ? 'highest-value' as const : 'closest' as const;
    const setup = {
      withinWalkingSteps: view.action.move.withinWalkingSteps ?? null,
      preference,
      coreBankValue: distance?.coreBankValue,
      pursuersPerCore: view.binding.capacityPolicy?.limit ?? null,
      coordinateAcrossSheet: view.binding.capacityPolicy?.scope !== 'local',
      linkedBankProgramId: view.binding.linkedBankProgramId,
    };
    const update = (patch: Partial<typeof setup>) => onChange(updateCoreRecoveryOpportunity(
      draft, play.id, projection.id, view.binding.id, { ...setup, ...patch }));
    return <div className="space-y-3">
      <div className="grid gap-2 sm:grid-cols-2">
        <label><span className="lab mb-1 block">Detour up to walking steps</span>
          <select className="field min-h-11" value={setup.withinWalkingSteps ?? ''}
            onChange={(event) => update({ withinWalkingSteps: event.target.value ? Number(event.target.value) : null })}>
            <option value="">Any reachable Core</option>
            {[2, 3, 4, 5, 6, 8, 10, 12].map((value) => <option key={value}>{value}</option>)}
          </select></label>
        <label><span className="lab mb-1 block">Choose</span>
          <select className="field min-h-11" value={setup.preference}
            onChange={(event) => update({ preference: event.target.value as typeof preference })}>
            <option value="closest">Closest visible Core</option>
            <option value="highest-value">Highest bank value</option>
          </select></label>
      </div>
      <div className="grid gap-2 sm:grid-cols-2">
        <label><span className="lab mb-1 block">Bodies per Core</span>
          <select className="field min-h-11" value={setup.pursuersPerCore ?? ''}
            onChange={(event) => update({ pursuersPerCore: event.target.value ? Number(event.target.value) : null })}>
            <option value="">No cap</option>
            {[1, 2, 3, 4].map((value) => <option key={value}>{value}</option>)}
          </select></label>
        <label className="flex min-h-11 items-center gap-3 rounded-sm border border-arena-edge px-3">
          <input type="checkbox" className="h-5 w-5 accent-[var(--color-arena-material)]"
            checked={setup.coordinateAcrossSheet}
            onChange={(event) => update({ coordinateAcrossSheet: event.target.checked })} />
          <span className="t-meta">Share that cap across the sheet</span>
        </label>
      </div>
      <label><span className="lab mb-1 block">Continue into a banking Program</span>
        <select className="field min-h-11" value={setup.linkedBankProgramId ?? ''}
          onChange={(event) => update({ linkedBankProgramId: event.target.value || undefined })}>
          <option value="">No linked banking Program</option>
          {draft.workshop.plays.filter((candidate) => candidate.id !== play.id).map((candidate) =>
            <option key={candidate.id} value={candidate.id}>{candidate.name}</option>)}
        </select>
        <span className="t-micro mt-1 block">Linkage is an authoring explanation only; ordinary Instruction order remains authoritative.</span>
      </label>
    </div>;
  }
  if (kind === 'health-recovery' && view.action.move) {
    const owned = (fact: string, operator?: string) => view.binding.ownedConditionIds
      ?.map((id) => draft.orders.conditions[id])
      .find((condition) => condition?.fact === fact
        && (!operator || condition.operator === operator));
    const low = owned('self-health-percent', 'at-most')?.value ?? 30;
    const recovered = owned('self-health-percent', 'at-least')?.value ?? 70;
    const allowCoreCarriers = !owned('self-carrying', 'equals');
    const setup = {
      startBelow: low,
      recoverUntil: recovered,
      withinWalkingSteps: view.action.move.withinWalkingSteps ?? null,
      timeout: view.action.timeout,
      cooldown: view.action.cooldown,
      patientsPerTile: view.binding.capacityPolicy?.limit ?? null,
      coordinateAcrossSheet: view.binding.capacityPolicy?.scope !== 'local',
      allowCoreCarriers,
    };
    const update = (patch: Partial<typeof setup>) => onChange(updateHealthRecoveryOpportunity(
      draft, play.id, projection.id, view.binding.id, { ...setup, ...patch }));
    return <div className="space-y-3">
      <div className="grid grid-cols-2 gap-2">
        <label><span className="lab mb-1 block">Leave below health %</span>
          <input type="number" className="field min-h-11" min={0} max={99} value={setup.startBelow}
            onChange={(event) => update({ startBelow: Number(event.target.value) })} /></label>
        <label><span className="lab mb-1 block">Return at health %</span>
          <input type="number" className="field min-h-11" min={1} max={100} value={setup.recoverUntil}
            onChange={(event) => update({ recoverUntil: Number(event.target.value) })} /></label>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <label><span className="lab mb-1 block">Give up after ticks</span>
          <input type="number" className="field min-h-11" min={1} max={1200} value={setup.timeout ?? 60}
            onChange={(event) => update({ timeout: Number(event.target.value) })} /></label>
        <label><span className="lab mb-1 block">Retry delay</span>
          <input type="number" className="field min-h-11" min={1} max={1200} value={setup.cooldown ?? 20}
            onChange={(event) => update({ cooldown: Number(event.target.value) })} /></label>
      </div>
      <label><span className="lab mb-1 block">Bodies per healing tile</span>
        <select className="field min-h-11" value={setup.patientsPerTile ?? ''}
          onChange={(event) => update({ patientsPerTile: event.target.value ? Number(event.target.value) : null })}>
          <option value="">No cap</option>
          {[1, 2, 3, 4].map((value) => <option key={value}>{value}</option>)}
        </select></label>
      <p className="t-micro">{allowCoreCarriers ? 'Core carriers may use this recovery.' : 'A body carrying a Core falls through instead of abandoning its delivery.'}</p>
    </div>;
  }
  return <p className="t-micro">This advanced Opportunity remains an ordinary Instruction. Open its source for fields not yet represented by the guided card.</p>;
}

function opportunityTitle(action: OrderNode, kind: string) {
  if (kind === 'use') return action.use ? `Use ${humanize(action.use.skill)}` : 'Use skill';
  if (kind === 'fight') return 'Fight visible enemies';
  if (kind === 'core-recovery') return 'Recover a loose Core';
  if (kind === 'health-recovery') return 'Recover health';
  if (kind === 'handoff') return 'Hand off the Core';
  if (kind === 'drop') return 'Drop the Core';
  return humanize(kind);
}

function opportunitySummary(action: OrderNode, binding: NonNullable<PlayProjection['signatureOpportunities']>[number], draft: SheetDraft) {
  if (action.use) return `Use ${humanize(action.use.skill)} when its Conditions allow`;
  if (action.fight) return `${humanize(action.fight.style)} within ${action.fight.chaseLimit} chase tiles`;
  if (binding.kind === 'core-recovery') {
    const distance = action.move?.withinWalkingSteps;
    return distance === undefined ? 'any reachable visible Core' : `visible Core within ${distance} walking steps`;
  }
  if (binding.kind === 'health-recovery') {
    const low = binding.ownedConditionIds?.map((id) => draft.orders.conditions[id])
      .find((condition) => condition?.fact === 'self-health-percent' && condition.operator === 'at-most');
    return `leave below ${low?.value ?? '?'}% health`;
  }
  return 'ordinary conditional action';
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

function opportunityHosts(orders: OrderNode[], projectionId: string) {
  const result: Array<{ id: string; label: string }> = [];
  const walk = (nodes: OrderNode[], depth: number) => nodes.forEach((node) => {
    if (node.id && (node.orders || depth < 2 && (node.move || node.fight)))
      result.push({ id: node.id, label: `${playOrderName(node.id, projectionId)} · ${node.orders ? 'branch' : node.move ? 'Move' : 'Fight'}` });
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

function orderName(order: OrderNode | null, projectionId?: string) {
  if (!order) return 'normal root fallthrough';
  if (order.move) return `${playOrderName(order.id ?? 'move', projectionId)} · continue moving`;
  if (order.fight) return `${playOrderName(order.id ?? 'fight', projectionId)} · continue fighting`;
  return playOrderName(order.id ?? 'next ordinary Instruction', projectionId);
}

/** Stable generated ids preserve every Program link across a title edit. They
 * are source identifiers, not player copy, so labels show only the local Instruction. */
function playOrderName(id: string, projectionId?: string) {
  const localId = projectionId && id.startsWith(`${projectionId}-`)
    ? id.slice(projectionId.length + 1)
    : id;
  return humanize(localId);
}

function LinkedOrdersControls({ draft, play, onChange }: {
  draft: SheetDraft; play: LinkedPlay; onChange: (next: SheetDraft) => void;
}) {
  return <section className="panel pad">
    <p className="lab text-arena-material">Shared Instruction values</p>
    <p className="t-micro mt-1">One edit updates every listed participant. Technical field labels remain exact so the advanced explanation is reproducible.</p>
    <div className="mt-3 space-y-2">{play.linkedValues.map((linked) => <article key={linked.id}
      className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
      <div className="flex items-center gap-2"><b className="type-display text-[15px]">{linked.field}</b><span className="pill ml-auto">{linked.targets.length} linked Instructions</span></div>
      {typeof linked.value === 'number' ? <label className="mt-2 block"><span className="t-micro mb-1 block">{humanize(linked.id)}</span>
        <input type="number" className="field min-h-11" min={linked.field === 'cooldown' ? 0 : 1} max={1200}
          value={linked.value} onChange={(event) => onChange(setPlayLinkedValue(draft, play.id, linked.id, Number(event.target.value)))} /></label>
        : <div className="mt-2 rounded-sm border border-arena-edge bg-[#080c10] p-2 font-mono text-[11px] text-arena-muted">{JSON.stringify(linked.value)}</div>}
      <p className="t-micro mt-2">{linked.targets.map((target) => `${humanize(target.projectionId)} → ${humanize(target.orderId)}.${target.field}`).join(' · ')}</p>
    </article>)}</div>
  </section>;
}

function ConditionControls({ draft, play, onChange }: {
  draft: SheetDraft; play: LinkedPlay; onChange: (next: SheetDraft) => void;
}) {
  return <details className="panel overflow-hidden">
    <summary className="flex min-h-14 cursor-pointer items-center gap-2 p-3"><div className="mr-auto"><p className="lab">Generated Conditions</p><p className="t-micro mt-1">Advanced · exact source derived from the tactical choices above</p></div><span className="pill">{play.ownedConditionIds.length}</span><span className="disclosure-chevron">›</span></summary>
    <div className="grid gap-2 border-t border-arena-edge p-3">{play.ownedConditionIds.map((conditionId) => {
      const condition = draft.orders.conditions[conditionId];
      if (!condition) return <p key={conditionId} className="t-micro text-arena-hot">Missing {conditionId}</p>;
      return <ConditionCard key={conditionId} id={conditionId}
        label={programSourceName(conditionId, play.id)} value={condition}
        zones={Object.keys(draft.mapPlan.zones)} roles={draft.orders.roles.map((role) => role.id)}
        onChange={(next) => onChange(updatePlayCondition(draft, play.id, conditionId, next))} />;
    })}</div>
  </details>;
}

function ConditionCard({
  id,
  label,
  value,
  zones = [],
  roles = [],
  onSelectGeometry,
  onChange,
}: {
  id: string;
  label?: string;
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
    <b className="t-body block">{label ?? humanize(id)}</b>
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
    <summary className="flex min-h-14 cursor-pointer items-center gap-2 p-3"><div className="mr-auto"><p className="lab">Generated Instructions</p><p className="t-micro mt-1">Advanced, read-only explanation · copied behavior survives detach</p></div><span className="disclosure-chevron">›</span></summary>
    <div className="space-y-3 border-t border-arena-edge p-3">{play.projections.map((projection) => <article key={projection.id}>
      <div className="flex items-center gap-2"><b className="t-body">{humanize(projection.roleId)}</b><button type="button" className="btn ml-auto min-h-9" onClick={() => onOpenOrders(play.id, projection.id)}>Open advanced source</button></div>
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
  return recipe === 'relay-chain' ? 'Running Relay' : recipe === 'ambush' ? 'Ambush pattern' : recipe === 'blank' ? 'Blank Program' : humanize(recipe);
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

function programSourceName(id: string, programId: string) {
  return humanize(id.startsWith(`${programId}-`) ? id.slice(programId.length + 1) : id);
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

// Kept in this module so blank-Program participant authoring can use the same
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
