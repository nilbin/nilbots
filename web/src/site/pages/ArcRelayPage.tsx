import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import clsx from 'clsx';
import EntrantCrest from '../../components/EntrantCrest';
import ClassIcon from '../../components/ClassIcon';
import { useAuth } from '../auth';
import {
  type ArcRelayCatalog,
  type ArcRelayClass,
  type ArcRelayEntrant,
  type ArcRelaySheet,
  type ArcRelaySheetDocument,
  type ArcRelaySheetPoint,
} from '../api';
import { ErrorState, LoadingState } from '../components/StateView';
import {
  useArcRelayCatalog,
  useArcRelayCrestOptions,
  useArcRelayEntrants,
  useArcRelayLadder,
  useArcRelayLadderOptIn,
  useArcRelayPreflight,
  useSetArcRelayCrest,
  useCreateArcRelayMind,
  useLoadArcRelayMind,
  useReviseArcRelayMind,
  useArcRelaySheets,
  useCreateArcRelayMatch,
  useSaveArcRelaySheet,
} from '../queries';

type DrawLayer = 'outbound' | 'return' | 'rally' | 'zone';

export default function ArcRelayPage() {
  const { user, loading } = useAuth();
  const navigate = useNavigate();
  const catalogQuery = useArcRelayCatalog();
  const sheetsQuery = useArcRelaySheets(user !== null);
  const entrantsQuery = useArcRelayEntrants(user !== null);
  const ladderQuery = useArcRelayLadder();
  const [activeId, setActiveId] = useState<string | null>(null);
  const [name, setName] = useState('Untitled sheet');
  const [document, setDocument] = useState<ArcRelaySheetDocument | null>(null);
  const [sheetA, setSheetA] = useState('');
  const [sheetB, setSheetB] = useState('');

  const sheets = sheetsQuery.data ?? [];
  const entrants = entrantsQuery.data ?? [];
  const active = sheets.find((sheet) => sheet.id === activeId) ?? null;

  useEffect(() => {
    if (!catalogQuery.data || document !== null) return;
    setDocument(copy(catalogQuery.data.newSheetTemplate));
  }, [catalogQuery.data, document]);

  useEffect(() => {
    if (activeId !== null || sheets.length === 0) return;
    openSheet(sheets[0]);
  // Deliberately only when the first server sheet arrives.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeId, sheets.length]);

  useEffect(() => {
    if (entrants.length < 2) return;
    if (!entrants.some((entry) => entry.id === sheetA)) setSheetA(entrants[0].id);
    if (!entrants.some((entry) => entry.id === sheetB) || sheetA === sheetB) {
      setSheetB(entrants.find((entry) => entry.id !== (sheetA || entrants[0].id))?.id ?? '');
    }
  }, [sheetA, sheetB, entrants]);

  const save = useSaveArcRelaySheet();
  const launch = useCreateArcRelayMatch();

  const saveSheet = (asCopy: boolean) => {
    if (!document) return;
    save.mutate({
      sheetId: active && !asCopy ? active.id : undefined,
      body: {
        name: asCopy && active ? `${name} copy` : name,
        expectedRevision: active && !asCopy ? active.revision : null,
        document,
      },
    }, {
      onSuccess: (saved) => {
        setActiveId(saved.id);
        setName(saved.name);
        setDocument(copy(saved.document));
      },
    });
  };

  const launchMatch = () => {
    launch.mutate({ entrantId: sheetA, opponentEntrantId: sheetB, seed: null }, {
      onSuccess: ({ id }) => navigate(`/matches/${id}`),
    });
  };

  const openSheet = (sheet: ArcRelaySheet) => {
    setActiveId(sheet.id);
    setName(sheet.name);
    setDocument(copy(sheet.document));
    save.reset();
  };

  const newSheet = () => {
    if (!catalogQuery.data) return;
    setActiveId(null);
    setName('Untitled sheet');
    setDocument(copy(catalogQuery.data.newSheetTemplate));
    save.reset();
  };

  if (loading) return <LoadingState label="Opening commander sheets…" />;
  if (!user) {
    return (
      <section className="panel pad mx-auto max-w-xl text-center">
        <p className="lab mb-2">Arc Relay</p>
        <h1 className="type-display text-[28px]">Draw the plan before the match</h1>
        <p className="t-meta mx-auto mt-3 max-w-md">
          Commander sheets belong to an account because unlocks, saved revisions,
          and match snapshots are enforced by the server.
        </p>
        <Link to="/login?return=/relay" className="btn btn-on mt-5 inline-flex">
          Sign in to author
        </Link>
      </section>
    );
  }
  if (catalogQuery.isError) {
    return <ErrorState error={catalogQuery.error} onRetry={() => void catalogQuery.refetch()} />;
  }
  if (catalogQuery.isPending || !catalogQuery.data || !document) {
    return <LoadingState label="Loading Arc Relay…" />;
  }

  const catalog = catalogQuery.data;
  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-4">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="lab mb-2">Arc Relay · commander workshop</p>
          <h1 className="type-display text-[30px]">Eight bodies. One drawn plan.</h1>
          <p className="t-meta mt-2 max-w-2xl">
            Pick from your unlocked classes, assign jobs, then draw routes, zones,
            rally lines and up to three ordered gambits. The frozen stock mind executes
            the sheet exactly as saved.
          </p>
        </div>
        <span className="pill">{catalog.mapId}</span>
      </header>

      <EntrantRoster entrants={entrants} ladder={ladderQuery.data?.entrants ?? []}
        templateClasses={catalogQuery.data?.newSheetTemplate.slots.map((slot) => slot.classId) ?? []}
        classes={catalog.classes} />

      <div className="grid min-w-0 gap-4 lg:grid-cols-[230px_minmax(0,1fr)]">
        <aside className="panel h-fit">
          <div className="pad flex items-center gap-2 border-b border-arena-edge">
            <p className="lab mr-auto">Saved sheets</p>
            <button type="button" className="btn" onClick={newSheet}>new</button>
          </div>
          {sheetsQuery.isPending ? (
            <div className="pad"><p className="t-micro">Loading…</p></div>
          ) : sheetsQuery.isError ? (
            <div className="pad"><p className="t-micro text-red-300">Could not load sheets.</p></div>
          ) : sheets.length === 0 ? (
            <div className="pad"><p className="t-meta">Save the open template to create your first sheet.</p></div>
          ) : (
            <div className="p-2">
              {sheets.map((sheet) => (
                <button
                  key={sheet.id}
                  type="button"
                  onClick={() => openSheet(sheet)}
                  className={clsx(
                    'mb-1 w-full rounded-sm border px-2.5 py-2 text-left',
                    sheet.id === activeId
                      ? 'border-arena-text bg-arena-edge/40'
                      : 'border-transparent hover:border-arena-edge',
                  )}
                >
                  <span className="t-body block truncate">{sheet.name}</span>
                  <span className="t-micro block">r{sheet.revision} · {sheet.contentHash.slice(0, 8)}</span>
                </button>
              ))}
            </div>
          )}
        </aside>

        <main className="flex min-w-0 flex-col gap-4">
          <section className="panel pad">
            <div className="flex flex-wrap items-end gap-2">
              <label className="min-w-[220px] flex-1">
                <span className="lab mb-1.5 block">Sheet name</span>
                <input className="field" value={name} maxLength={60}
                  onChange={(event) => setName(event.target.value)} />
              </label>
              <button type="button" className="btn btn-on"
                disabled={save.isPending}
                onClick={() => saveSheet(false)}>
                {save.isPending ? 'Saving…' : active ? `Save r${active.revision + 1}` : 'Save sheet'}
              </button>
              {active && (
                <button type="button" className="btn" disabled={save.isPending}
                  onClick={() => saveSheet(true)}>
                  Save a copy
                </button>
              )}
            </div>
            {save.isError && <p className="t-micro mt-2 text-red-300">{save.error.message}</p>}
            {save.isSuccess && <p className="t-micro mt-2 text-emerald-300">Saved and hash-pinned.</p>}
          </section>

          <LineupEditor catalog={catalog} document={document} onChange={setDocument} />
          <DrawingBoard catalog={catalog} document={document} onChange={setDocument} />
          <PolicyEditor document={document} onChange={setDocument} />
          <GambitEditor catalog={catalog} document={document} onChange={setDocument} />

          <section className="panel pad">
            <p className="lab mb-2">Run a scrimmage</p>
            <p className="t-meta mb-3">
              The worker snapshots both saved revisions, hashes both sheets and runs the
              same frozen algorithm. Later edits cannot rewrite this match.
            </p>
            <div className="flex flex-wrap items-end gap-2">
              <EntrantSelect label="cyan entrant" value={sheetA} entrants={entrants} onChange={setSheetA} />
              <span className="t-meta pb-2">vs</span>
              <EntrantSelect label="red entrant" value={sheetB} entrants={entrants} onChange={setSheetB} />
              <button type="button" className="btn btn-on"
                disabled={entrants.length < 2 || !sheetA || !sheetB || sheetA === sheetB || launch.isPending}
                onClick={launchMatch}>
                {launch.isPending ? 'Queueing…' : 'Run match'}
              </button>
            </div>
            {entrants.length < 2 && (
              <p className="t-micro mt-2">Create a second distinct entrant to run a comparison.</p>
            )}
            {launch.isError && <p className="t-micro mt-2 text-red-300">{launch.error.message}</p>}
          </section>

          <ClassCatalog catalog={catalog} />
        </main>
      </div>
    </div>
  );
}

function LineupEditor({ catalog, document, onChange }: EditorProps) {
  const counts = useMemo(() => countClasses(document), [document]);
  const setSlot = (unitId: number, patch: Partial<ArcRelaySheetDocument['slots'][number]>) => {
    onChange({
      ...document,
      slots: document.slots.map((slot) => slot.unitId === unitId ? { ...slot, ...patch } : slot),
    });
  };
  return (
    <section className="panel overflow-hidden">
      <div className="pad border-b border-arena-edge">
        <p className="lab">Lineup and jobs</p>
        <p className="t-micro mt-1">Exactly eight slots; no class may appear more than twice.</p>
      </div>
      <div className="overflow-x-auto">
        <table className="t-body w-full min-w-[680px] border-collapse">
          <thead className="t-micro text-left">
            <tr>
              <th className="px-3 py-2">slot</th><th>class</th><th>theater</th>
              <th>role</th><th>partner</th><th className="px-3">route</th>
            </tr>
          </thead>
          <tbody>
            {document.slots.map((slot) => (
              <tr key={slot.unitId} className="border-t border-arena-edge">
                <td className="px-3 py-2 font-mono">{slot.unitId + 1}</td>
                <td className="py-2 pr-2">
                  <select className="field py-1.5" value={slot.classId}
                    onChange={(event) => setSlot(slot.unitId, { classId: event.target.value })}>
                    {catalog.classes.map((entry) => {
                      const full = (counts.get(entry.id) ?? 0) >= catalog.maximumCopiesPerClass
                        && entry.id !== slot.classId;
                      return <option key={entry.id} value={entry.id} disabled={!entry.unlocked || full}>
                        {entry.name}{entry.unlocked ? full ? ' · 2/2' : '' : ' · locked'}
                      </option>;
                    })}
                  </select>
                </td>
                <td className="py-2 pr-2"><Select value={slot.theater} values={catalog.theaters}
                  onChange={(theater) => setSlot(slot.unitId, { theater })} /></td>
                <td className="py-2 pr-2"><Select value={slot.role} values={catalog.roles}
                  onChange={(role) => setSlot(slot.unitId, { role })} /></td>
                <td className="py-2 pr-2">
                  <select className="field py-1.5" value={slot.partnerUnitId}
                    onChange={(event) => setSlot(slot.unitId, { partnerUnitId: Number(event.target.value) })}>
                    {document.slots.filter((candidate) => candidate.unitId !== slot.unitId).map((candidate) =>
                      <option key={candidate.unitId} value={candidate.unitId}>slot {candidate.unitId + 1}</option>)}
                  </select>
                </td>
                <td className="px-3 py-2 t-micro">{slot.outboundPath.length} out · {slot.returnPath.length} home</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function DrawingBoard({ catalog, document, onChange }: EditorProps) {
  const svg = useRef<SVGSVGElement>(null);
  const [unitId, setUnitId] = useState(0);
  const [layer, setLayer] = useState<DrawLayer>('outbound');
  const [rallyId, setRallyId] = useState(document.rallyLines[0]?.id ?? 'home');
  const [zoneId, setZoneId] = useState(document.zones[0]?.id ?? 'north');
  const [zoneAnchor, setZoneAnchor] = useState<ArcRelaySheetPoint | null>(null);
  const slot = document.slots.find((value) => value.unitId === unitId) ?? document.slots[0];
  const rally = document.rallyLines.find((value) => value.id === rallyId) ?? document.rallyLines[0];
  const zone = document.zones.find((value) => value.id === zoneId) ?? document.zones[0];
  const width = catalog.mapRows[0]?.length ?? 31;
  const height = catalog.mapRows.length;

  const setPoints = (points: ArcRelaySheetPoint[]) => {
    if (layer === 'outbound' || layer === 'return') {
      onChange({ ...document, slots: document.slots.map((value) => value.unitId === unitId
        ? { ...value, [layer === 'outbound' ? 'outboundPath' : 'returnPath']: points }
        : value) });
    } else if (layer === 'rally') {
      onChange({ ...document, rallyLines: document.rallyLines.map((value) => value.id === rally.id
        ? { ...value, points }
        : value) });
    }
  };

  const activePoints = layer === 'outbound' ? slot.outboundPath
    : layer === 'return' ? slot.returnPath
      : layer === 'rally' ? rally.points : [];

  const clickMap = (event: React.PointerEvent<SVGSVGElement>) => {
    const bounds = svg.current?.getBoundingClientRect();
    if (!bounds) return;
    const point = {
      x: Math.min(width - 1, Math.max(0, Math.floor((event.clientX - bounds.left) / bounds.width * width))),
      y: Math.min(height - 1, Math.max(0, Math.floor((event.clientY - bounds.top) / bounds.height * height))),
    };
    if (catalog.mapRows[point.y]?.[point.x] === '#') return;
    if (layer === 'zone') {
      if (!zoneAnchor) {
        setZoneAnchor(point);
      } else {
        onChange({ ...document, zones: document.zones.map((value) => value.id === zone.id ? {
          ...value,
          minX: Math.min(zoneAnchor.x, point.x), minY: Math.min(zoneAnchor.y, point.y),
          maxX: Math.max(zoneAnchor.x, point.x), maxY: Math.max(zoneAnchor.y, point.y),
        } : value) });
        setZoneAnchor(null);
      }
      return;
    }
    const last = activePoints.at(-1);
    if (!last || last.x !== point.x || last.y !== point.y) setPoints([...activePoints, point]);
  };

  return (
    <section className="panel pad">
      <div className="flex flex-wrap items-start gap-2">
        <div className="mr-auto">
          <p className="lab">Map drawings</p>
          <p className="t-micro mt-1">Authored from the west side; the stock mind mirrors red automatically.</p>
        </div>
        <select className="field w-auto py-1.5" value={unitId} onChange={(event) => setUnitId(Number(event.target.value))}>
          {document.slots.map((value) => <option key={value.unitId} value={value.unitId}>
            slot {value.unitId + 1} · {value.classId}
          </option>)}
        </select>
      </div>
      <div className="mt-3 flex flex-wrap gap-1.5">
        {(['outbound', 'return', 'rally', 'zone'] as const).map((value) =>
          <button key={value} type="button" className={clsx('btn', layer === value && 'btn-on')}
            onClick={() => { setLayer(value); setZoneAnchor(null); }}>{value}</button>)}
        {layer === 'rally' && <select className="field w-auto py-1.5" value={rally.id}
          onChange={(event) => setRallyId(event.target.value)}>
          {document.rallyLines.map((value) => <option key={value.id}>{value.id}</option>)}
        </select>}
        {layer === 'zone' && <select className="field w-auto py-1.5" value={zone.id}
          onChange={(event) => { setZoneId(event.target.value); setZoneAnchor(null); }}>
          {document.zones.map((value) => <option key={value.id}>{value.id}</option>)}
        </select>}
        {layer !== 'zone' && <>
          <button type="button" className="btn" disabled={activePoints.length <= 1}
            onClick={() => setPoints(activePoints.slice(0, -1))}>undo point</button>
          <button type="button" className="btn" onClick={() => setPoints([])}>clear</button>
        </>}
        {layer === 'zone' && <span className="t-micro self-center">
          {zoneAnchor ? 'choose opposite corner' : 'choose first corner'}
        </span>}
      </div>

      <div className="mt-3 overflow-hidden rounded-sm border border-arena-edge bg-[#080c10]">
        <svg ref={svg} viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none"
          onPointerDown={clickMap} className="block aspect-[31/23] w-full touch-none cursor-crosshair"
          role="img" aria-label="Arc Relay sheet drawing map">
          {catalog.mapRows.flatMap((row, y) => [...row].map((tile, x) =>
            <rect key={`${x}:${y}`} x={x} y={y} width={1} height={1}
              fill={tile === '#' ? '#303943' : (x + y) % 2 === 0 ? '#111820' : '#0e151c'}
              stroke="#1c2730" strokeWidth={0.035} />))}
          <rect x={zone.minX} y={zone.minY} width={zone.maxX - zone.minX + 1}
            height={zone.maxY - zone.minY + 1} fill="#f6c45322" stroke="#f6c453" strokeWidth={0.15} />
          <Path points={slot.outboundPath} color="#22d3ee" />
          <Path points={slot.returnPath} color="#f59e0b" dashed />
          <Path points={rally.points} color="#e8de91" dashed />
          {[{ x: 15, y: 4 }, { x: 15, y: 11 }, { x: 15, y: 18 }].map((point) =>
            <circle key={point.y} cx={point.x + 0.5} cy={point.y + 0.5} r={0.32}
              fill="#f5d56b" stroke="#fff3bf" strokeWidth={0.12} />)}
          <circle cx={2.5} cy={11.5} r={0.4} fill="#22d3ee" />
          {zoneAnchor && <circle cx={zoneAnchor.x + 0.5} cy={zoneAnchor.y + 0.5} r={0.3} fill="#fff" />}
        </svg>
      </div>
      <div className="t-micro mt-2 flex flex-wrap gap-x-4 gap-y-1">
        <span className="text-cyan-300">solid · outbound</span>
        <span className="text-amber-300">dash · return</span>
        <span className="text-yellow-100">pale · rally</span>
        <span>gold box · selected zone</span>
      </div>
    </section>
  );
}

function Path({ points, color, dashed = false }: { points: ArcRelaySheetPoint[]; color: string; dashed?: boolean }) {
  if (points.length === 0) return null;
  const value = points.map((point) => `${point.x + 0.5},${point.y + 0.5}`).join(' ');
  return <g>
    <polyline points={value} fill="none" stroke="#05090c" strokeWidth={0.48} />
    <polyline points={value} fill="none" stroke={color} strokeWidth={0.22}
      strokeDasharray={dashed ? '0.55 0.35' : undefined} />
    {points.map((point, index) => <circle key={`${point.x}:${point.y}:${index}`}
      cx={point.x + 0.5} cy={point.y + 0.5} r={index === points.length - 1 ? 0.26 : 0.17}
      fill={color} stroke="#071017" strokeWidth={0.08} />)}
  </g>;
}

function PolicyEditor({ document, onChange }: Omit<EditorProps, 'catalog'>) {
  const policies = document.policies;
  const update = (next: ArcRelaySheetDocument['policies']) => onChange({ ...document, policies: next });
  return <section className="panel pad">
    <p className="lab mb-3">Execution policies</p>
    <div className="grid gap-3 md:grid-cols-3">
      <fieldset className="rounded-sm border border-arena-edge p-3">
        <legend className="t-micro px-1">carrier</legend>
        <NumberField label="handoff at hull" value={policies.carrier.handoffHealthAtOrBelow} min={1} max={5}
          onChange={(value) => update({ ...policies, carrier: { ...policies.carrier, handoffHealthAtOrBelow: value } })} />
        <NumberField label="route failure ticks" value={policies.carrier.routeFailureTicks} min={4} max={60}
          onChange={(value) => update({ ...policies, carrier: { ...policies.carrier, routeFailureTicks: value } })} />
        <Check label="prefer assigned theater" checked={policies.carrier.preferAssignedTheater}
          onChange={(value) => update({ ...policies, carrier: { ...policies.carrier, preferAssignedTheater: value } })} />
      </fieldset>
      <fieldset className="rounded-sm border border-arena-edge p-3">
        <legend className="t-micro px-1">escort</legend>
        <NumberField label="follow distance" value={policies.escort.followDistance} min={1} max={4}
          onChange={(value) => update({ ...policies, escort: { ...policies.escort, followDistance: value } })} />
        <Check label="focus enemy carrier" checked={policies.escort.focusEnemyCarrier}
          onChange={(value) => update({ ...policies, escort: { ...policies.escort, focusEnemyCarrier: value } })} />
      </fieldset>
      <fieldset className="rounded-sm border border-arena-edge p-3">
        <legend className="t-micro px-1">interception</legend>
        <Check label="focus enemy carrier" checked={policies.interception.focusEnemyCarrier}
          onChange={(value) => update({ ...policies, interception: { ...policies.interception, focusEnemyCarrier: value } })} />
        <Check label="recover loose Cores" checked={policies.interception.looseCoreFallback}
          onChange={(value) => update({ ...policies, interception: { ...policies.interception, looseCoreFallback: value } })} />
      </fieldset>
    </div>
  </section>;
}

function GambitEditor({ catalog, document, onChange }: EditorProps) {
  const update = (index: number, patch: Partial<ArcRelaySheetDocument['gambits'][number]>) =>
    onChange({ ...document, gambits: document.gambits.map((value, position) => position === index ? { ...value, ...patch } : value) });
  const move = (index: number, delta: number) => {
    const next = [...document.gambits];
    const [value] = next.splice(index, 1);
    next.splice(index + delta, 0, value);
    onChange({ ...document, gambits: next });
  };
  return <section className="panel pad">
    <div className="flex items-center gap-2">
      <div className="mr-auto"><p className="lab">Ordered gambits</p>
        <p className="t-micro mt-1">First active gambit wins; execution is edge-triggered with cooldown.</p></div>
      <button type="button" className="btn" disabled={document.gambits.length >= 3}
        onClick={() => onChange({ ...document, gambits: [...document.gambits, {
          id: `gambit-${document.gambits.length + 1}`,
          trigger: 'after-enemy-pulse', durationTicks: 16, cooldownTicks: 48,
          scopeRoles: ['reserve'], roleOverride: 'intercept',
          rallyLineId: document.rallyLines[0]?.id ?? 'home',
        }] })}>add gambit</button>
    </div>
    <div className="mt-3 flex flex-col gap-2">
      {document.gambits.length === 0 && <p className="t-meta">Static sheet — no conditional plan switch.</p>}
      {document.gambits.map((gambit, index) => <div key={`${gambit.id}:${index}`}
        className="rounded-sm border border-arena-edge p-3">
        <div className="grid gap-2 md:grid-cols-[1fr_1.3fr_90px_90px]">
          <label><span className="t-micro block">id</span><input className="field py-1.5" value={gambit.id}
            onChange={(event) => update(index, { id: event.target.value })} /></label>
          <label><span className="t-micro block">trigger</span><Select value={gambit.trigger} values={catalog.gambitTriggers}
            onChange={(trigger) => update(index, { trigger })} /></label>
          <NumberField label="duration" value={gambit.durationTicks} min={4} max={60}
            onChange={(durationTicks) => update(index, { durationTicks })} />
          <NumberField label="cooldown" value={gambit.cooldownTicks} min={8} max={180}
            onChange={(cooldownTicks) => update(index, { cooldownTicks })} />
        </div>
        <div className="mt-2 flex flex-wrap items-end gap-2">
          <label><span className="t-micro block">override role</span><Select value={gambit.roleOverride} values={catalog.roles}
            onChange={(roleOverride) => update(index, { roleOverride })} /></label>
          <label><span className="t-micro block">rally line</span><select className="field py-1.5"
            value={gambit.rallyLineId} onChange={(event) => update(index, { rallyLineId: event.target.value })}>
            {document.rallyLines.map((line) => <option key={line.id}>{line.id}</option>)}
          </select></label>
          <label className="min-w-[180px] flex-1"><span className="t-micro block">base roles in scope</span>
            <div className="flex flex-wrap gap-x-2 pt-1">{catalog.roles.map((role) => <Check key={role} label={role}
              checked={gambit.scopeRoles.includes(role)} onChange={(checked) => update(index, {
                scopeRoles: checked ? [...gambit.scopeRoles, role] : gambit.scopeRoles.filter((value) => value !== role),
              })} />)}</div>
          </label>
          <button type="button" className="btn" disabled={index === 0} onClick={() => move(index, -1)}>↑</button>
          <button type="button" className="btn" disabled={index === document.gambits.length - 1}
            onClick={() => move(index, 1)}>↓</button>
          <button type="button" className="btn" onClick={() => onChange({ ...document,
            gambits: document.gambits.filter((_, position) => position !== index) })}>remove</button>
        </div>
      </div>)}
    </div>
  </section>;
}

function ClassCatalog({ catalog }: { catalog: ArcRelayCatalog }) {
  return <section>
    <p className="lab mb-2">Launch classes</p>
    <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
      {catalog.classes.map((entry) => <article key={entry.id}
        className={clsx('panel pad min-h-[142px]', !entry.unlocked && 'opacity-55')}>
        <div className="flex items-start gap-2"><h3 className="type-display text-[18px]">{entry.name}</h3>
          <span className="pill ml-auto">{entry.unlocked ? 'unlocked' : 'locked'}</span></div>
        <p className="lab mt-2 text-arena-material">{entry.signatureName}</p>
        <p className="t-micro mt-2">{entry.fantasy}</p>
      </article>)}
    </div>
  </section>;
}

function EntrantRoster({ entrants, ladder, templateClasses, classes }: {
  entrants: ArcRelayEntrant[];
  ladder: ArcRelayEntrant[];
  templateClasses: string[];
  classes: ArcRelayClass[];
}) {
  const [name, setName] = useState('My relay mind');
  const [entryType, setEntryType] = useState('MyRelayMind');
  const [source, setSource] = useState(DEFAULT_MIND_SOURCE);
  const [composition, setComposition] = useState(templateClasses);
  const [editId, setEditId] = useState<string | null>(null);
  const [expectedRevision, setExpectedRevision] = useState(0);
  const create = useCreateArcRelayMind();
  const revise = useReviseArcRelayMind();
  const loadMind = useLoadArcRelayMind();
  const preflight = useArcRelayPreflight();
  const ladderOptIn = useArcRelayLadderOptIn();
  const crestOptions = useArcRelayCrestOptions();
  const setCrest = useSetArcRelayCrest();
  const submit = () => {
    const common = {
      name, entryType,
      files: [{ name: 'MyRelayMind.cs', content: source }],
      composition: { classIds: composition, adaptivePolicyId: null, adaptiveClassIds: [] },
    };
    if (editId) revise.mutate({ entrantId: editId, body: { ...common, expectedRevision } });
    else create.mutate({ ...common, crestVariant: 0 });
  };
  const beginRevision = (entrantId: string) => loadMind.mutate(entrantId, {
    onSuccess: (mind) => {
      setEditId(entrantId);
      setExpectedRevision(mind.entrant.revision);
      setName(mind.entrant.name);
      setEntryType(mind.entryType);
      setSource(mind.files[0]?.content ?? DEFAULT_MIND_SOURCE);
      setComposition([...mind.composition.classIds]);
      const editor = document.getElementById('mind-editor') as HTMLDetailsElement | null;
      if (editor) { editor.open = true; editor.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
    },
  });
  return <>
    <section className="panel overflow-hidden">
      <div className="pad flex flex-wrap items-end gap-3 border-b border-arena-edge">
        <div className="mr-auto">
          <p className="lab">Your entrants</p>
          <p className="t-micro mt-1">Sheets and submitted minds share one identity, crest and rating surface.</p>
        </div>
        <span className="pill">{entrants.filter((entry) => entry.ladderOptedIn).length}/3 fielded</span>
      </div>
      <div className="grid gap-2 p-3 md:grid-cols-2 xl:grid-cols-3">
        {entrants.map((entrant) => <article key={entrant.id}
          className="rounded-sm border border-arena-edge bg-arena-deep/40 p-3">
          <div className="flex items-center gap-3">
            <EntrantCrest crest={entrant.crest} size={52} />
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <h3 className="type-display truncate text-[18px]">{entrant.name}</h3>
                <span className="pill ml-auto">{entrant.kind}</span>
              </div>
              <p className="t-micro">{entrant.rating} rating · {entrant.rankedMatches} ranked · r{entrant.revision}</p>
              <p className={clsx('t-micro mt-1', entrant.status === 'suspended' ? 'text-red-300' : 'text-arena-material')}>
                {entrant.status}{entrant.suspensionReason ? ` · ${entrant.suspensionReason}` : ''}
              </p>
            </div>
          </div>
          <CompositionStrip entrant={entrant} />
          <div className="mt-3 flex flex-wrap gap-2">
            <button type="button" className="btn" disabled={crestOptions.isPending}
              onClick={() => crestOptions.mutate(entrant.id)}>Choose crest</button>
            {entrant.kind === 'mind' && <button type="button" className="btn"
              disabled={loadMind.isPending} onClick={() => beginRevision(entrant.id)}>Revise</button>}
            {entrant.kind === 'mind' && (entrant.status === 'required' || entrant.status === 'built') &&
              <button type="button" className="btn" disabled={preflight.isPending}
                onClick={() => preflight.mutate(entrant.id)}>Run preflight</button>}
            {(entrant.kind === 'sheet' || entrant.status === 'passed') && entrant.status !== 'suspended' &&
              <button type="button" className={clsx('btn', entrant.ladderOptedIn && 'btn-on')}
                disabled={ladderOptIn.isPending}
                onClick={() => ladderOptIn.mutate({ entrantId: entrant.id, optedIn: !entrant.ladderOptedIn })}>
                {entrant.ladderOptedIn ? 'Leave ladder' : 'Enter ladder'}
              </button>}
          </div>
          {crestOptions.data?.entrantId === entrant.id && (
            <div className="mt-3 flex flex-wrap gap-2 border-t border-arena-edge pt-3" aria-label="Crest choices">
              {crestOptions.data.options.map((crest) => <button key={crest.key} type="button"
                className="rounded-sm border border-arena-edge p-1 hover:border-arena-material"
                aria-label={`Choose crest ${crest.variant}`} disabled={setCrest.isPending}
                onClick={() => setCrest.mutate({ entrantId: entrant.id, variant: crest.variant })}>
                <EntrantCrest crest={crest} size={38} />
              </button>)}
            </div>
          )}
        </article>)}
        {entrants.length === 0 && <p className="t-meta p-2">Save a sheet or submit a mind to establish an entrant.</p>}
      </div>
    </section>

    <section className="panel pad">
      <details id="mind-editor">
        <summary className="lab cursor-pointer">{editId ? 'Revise custom mind' : 'Submit a custom mind'}</summary>
        <p className="t-meta mt-2 max-w-3xl">
          Sources follow the controlled server build path. The resulting artifact runs only in the WASM sandbox;
          its eight-class declaration is validated and snapshotted beside every match.
        </p>
        <div className="mt-3 grid gap-3 md:grid-cols-2">
          <label><span className="t-micro block">Entrant name</span><input className="field" value={name}
            maxLength={60} onChange={(event) => setName(event.target.value)} /></label>
          <label><span className="t-micro block">Entry type</span><input className="field" value={entryType}
            onChange={(event) => setEntryType(event.target.value)} /></label>
        </div>
        <fieldset className="mt-3">
          <legend className="t-micro">Declared eight-body composition · two copies maximum</legend>
          <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-8">
            {composition.map((classId, slot) => <label key={slot}>
              <span className="t-micro block">slot {slot + 1}</span>
              <select className="field py-1.5" value={classId} onChange={(event) => {
                const next = [...composition]; next[slot] = event.target.value; setComposition(next);
              }}>
                {classes.filter((entry) => entry.unlocked).map((entry) => {
                  const copies = composition.filter((value) => value === entry.id).length;
                  return <option key={entry.id} value={entry.id}
                    disabled={entry.id !== classId && copies >= 2}>{entry.name}</option>;
                })}
              </select>
            </label>)}
          </div>
        </fieldset>
        <label className="mt-3 block"><span className="t-micro block">C# source</span>
          <textarea className="field min-h-[260px] font-mono text-xs" spellCheck={false} value={source}
            onChange={(event) => setSource(event.target.value)} /></label>
        <div className="mt-3 flex items-center gap-3">
          <button type="button" className="btn btn-on" disabled={create.isPending || revise.isPending || composition.length !== 8}
            onClick={submit}>{create.isPending || revise.isPending ? 'Submitting…' : editId ? 'Build revised mind' : 'Build mind entrant'}</button>
          {editId && <button type="button" className="btn" onClick={() => {
            setEditId(null); setExpectedRevision(0); setName('My relay mind'); setEntryType('MyRelayMind');
            setSource(DEFAULT_MIND_SOURCE); setComposition(templateClasses);
          }}>New submission</button>}
          {(create.isError || revise.isError) && <span className="t-micro text-red-300">
            {(create.error ?? revise.error)?.message}</span>}
          {(create.isSuccess || revise.isSuccess) && <span className="t-micro text-emerald-300">
            Revision queued through the controlled toolchain; rating identity preserved.</span>}
        </div>
      </details>
    </section>

    <section className="panel overflow-hidden">
      <div className="pad border-b border-arena-edge">
        <p className="lab">Ranked Arc Relay</p>
        <p className="t-micro mt-1">Passive cross-account pairing · ratings reveal only when the broadcast does.</p>
      </div>
      <div className="divide-y divide-arena-edge">
        {ladder.map((entrant, index) => <div key={entrant.id} className="flex min-w-0 items-center gap-3 px-3 py-2.5">
          <span className="lab w-7 text-right">{index + 1}</span><EntrantCrest crest={entrant.crest} size={34} />
          <div className="min-w-0 flex-1"><p className="t-body truncate">{entrant.name}</p>
            <p className="t-micro">{entrant.kind} · {entrant.ownerDisplayName}</p></div>
          <CompositionStrip entrant={entrant} compact />
          <strong className="type-display text-[18px]">{entrant.rating}</strong>
        </div>)}
        {ladder.length === 0 && <p className="t-meta pad">The launch ladder is waiting for its first entrants.</p>}
      </div>
    </section>
  </>;
}

function CompositionStrip({ entrant, compact = false }: { entrant: ArcRelayEntrant; compact?: boolean }) {
  return <div className={clsx('flex min-w-0 gap-1', compact ? 'hidden max-w-[240px] lg:flex' : 'mt-3')}>
    {entrant.composition.map((slot) => <ClassIcon key={slot.slot}
      classId={slot.classId} label={`${slot.slot + 1}. ${slot.className}`} size={24} />)}
  </div>;
}

function EntrantSelect({ label, value, entrants, onChange }: {
  label: string; value: string; entrants: ArcRelayEntrant[]; onChange: (value: string) => void;
}) {
  return <label className="min-w-[180px] flex-1"><span className="t-micro block">{label}</span>
    <select className="field" value={value} onChange={(event) => onChange(event.target.value)}>
      <option value="">choose entrant</option>
      {entrants.map((entrant) => <option key={entrant.id} value={entrant.id}>
        {entrant.name} · {entrant.kind} r{entrant.revision}
      </option>)}
    </select>
  </label>;
}

const DEFAULT_MIND_SOURCE = `using BotArena.Sdk;

public sealed class MyRelayMind : IGenericMindBot
{
    public void StartMatch(MindStart start) { }

    public void Think(MindContext mind)
    {
        foreach (MindBody body in mind.Bodies)
            body.Hold("author your Arc Relay plan here");
    }

    public void EndMatch(MindEnd end) { }
}`;

function NumberField({ label, value, min, max, onChange }: {
  label: string; value: number; min: number; max: number; onChange: (value: number) => void;
}) {
  return <label className="block"><span className="t-micro block">{label}</span>
    <input type="number" className="field py-1.5" value={value} min={min} max={max}
      onChange={(event) => onChange(Number(event.target.value))} /></label>;
}

function Check({ label, checked, onChange }: {
  label: string; checked: boolean; onChange: (value: boolean) => void;
}) {
  return <label className="t-micro flex min-h-7 items-center gap-1.5">
    <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
    {label}
  </label>;
}

function Select<T extends string>({ value, values, onChange }: {
  value: string; values: readonly T[]; onChange: (value: T) => void;
}) {
  return <select className="field py-1.5" value={value}
    onChange={(event) => onChange(event.target.value as T)}>
    {values.map((entry) => <option key={entry}>{entry}</option>)}
  </select>;
}

function countClasses(document: ArcRelaySheetDocument) {
  const counts = new Map<string, number>();
  document.slots.forEach((slot) => counts.set(slot.classId, (counts.get(slot.classId) ?? 0) + 1));
  return counts;
}

function copy<T>(value: T): T {
  return structuredClone(value);
}

interface EditorProps {
  catalog: ArcRelayCatalog;
  document: ArcRelaySheetDocument;
  onChange: (document: ArcRelaySheetDocument) => void;
}
