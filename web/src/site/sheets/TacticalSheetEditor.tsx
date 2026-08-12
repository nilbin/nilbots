import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { Link, useNavigate } from 'react-router-dom';
import clsx from 'clsx';
import type { TacticalSheet, TacticalSheetCatalog, TacticalSheetSummary } from '../api';
import {
  useDeleteTacticalSheet,
  useMatchLive,
  useMatchReplay,
  useSaveTacticalSheet,
  useTacticalSheet,
  useTacticalSheets,
  useTrialTacticalSheet,
} from '../queries';
import {
  deleteLocalDraft,
  listLocalDrafts,
  loadLocalDraft,
  parseDraft,
  pinDraft,
  saveLocalDraft,
  sheetIssueFromCompilerError,
  stringifyDocument,
  validateDraft,
  type MapPlanDocument,
  type OrderNode,
  type OrdersDocument,
  type SheetDraft,
  type SheetIssue,
} from './sheetAuthoring';
import {
  describeTrialChanges,
  loadTrialHistory,
  replayUnitTracks,
  saveTrialHistory,
  snapshotForTrial,
  type SheetTrialRun,
} from './sheetTrials';
import { OrdersWorkspace, SquadEditor } from './OrdersWorkspace';
import MapPlanEditor from './MapPlanEditor';

interface TacticalSheetEditorProps {
  catalog: TacticalSheetCatalog;
  initialSheetId?: string | null;
}

type Section = 'squad' | 'orders' | 'map';
type PlotFocus = { kind: 'route' | 'zone' | 'anchor'; id: string } | null;

const SECTIONS: ReadonlyArray<{ id: Section; label: string; description: string }> = [
  { id: 'squad', label: 'Squad', description: 'Cast eight bodies into roles' },
  { id: 'orders', label: 'Orders', description: 'Write each role’s priorities' },
  { id: 'map', label: 'Map plan', description: 'Plot the named ground' },
];

export default function TacticalSheetEditor({ catalog, initialSheetId = null }: TacticalSheetEditorProps) {
  const navigate = useNavigate();
  const sheetsQuery = useTacticalSheets(true);
  const [activeId, setActiveId] = useState<string | null>(initialSheetId);
  const sheetQuery = useTacticalSheet(activeId);
  const [draft, setDraft] = useState<SheetDraft | null>(null);
  const draftRef = useRef<SheetDraft | null>(null);
  const undoStack = useRef<SheetDraft[]>([]);
  const redoStack = useRef<SheetDraft[]>([]);
  const [, forceHistory] = useState(0);
  const [localDrafts, setLocalDrafts] = useState<SheetDraft[]>([]);
  const [section, setSection] = useState<Section>('orders');
  const [mapFocus, setMapFocus] = useState<PlotFocus>(null);
  const [libraryOpen, setLibraryOpen] = useState(false);
  const [practiceOpen, setPracticeOpen] = useState(false);
  const [toolsOpen, setToolsOpen] = useState(false);
  const [restored, setRestored] = useState(false);
  const [compilerIssue, setCompilerIssue] = useState<SheetIssue | null>(null);
  const [stockId, setStockId] = useState(catalog.stockOpponents[0]?.id ?? '');
  const [seed, setSeed] = useState('104729');
  const [randomSeed, setRandomSeed] = useState(false);
  const [trialHistory, setTrialHistory] = useState<SheetTrialRun[]>(() => loadTrialHistory());
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const save = useSaveTacticalSheet();
  const remove = useDeleteTacticalSheet();
  const trial = useTrialTacticalSheet();

  const replaceDraft = useCallback((next: SheetDraft | null) => {
    draftRef.current = next;
    undoStack.current = [];
    redoStack.current = [];
    forceHistory((value) => value + 1);
    setDraft(next);
  }, []);

  const templateDraft = useCallback(() => parseDraft(
    'Untitled tactical sheet', null, null,
    catalog.templateOrdersJson, catalog.templateMapPlanJson,
  ), [catalog.templateMapPlanJson, catalog.templateOrdersJson]);

  useEffect(() => {
    if (initialSheetId) {
      setActiveId(initialSheetId);
      setRestored(false);
      return;
    }
    let live = true;
    void Promise.all([loadLocalDraft(), listLocalDrafts()]).then(([local, locals]) => {
      if (!live) return;
      const v2Locals = locals.filter(isOrdersDraft);
      setLocalDrafts(v2Locals);
      const usable = local && isOrdersDraft(local) && local.mapPlan.mapId === catalog.map.id ? local : null;
      replaceDraft(usable ?? templateDraft());
      setActiveId(null);
      setRestored(Boolean(usable));
    }).catch(() => {
      if (live) replaceDraft(templateDraft());
    });
    return () => { live = false; };
  }, [catalog.map.id, initialSheetId, replaceDraft, templateDraft]);

  useEffect(() => {
    if (!sheetQuery.data) return;
    let serverDraft: SheetDraft;
    try {
      serverDraft = fromServer(sheetQuery.data);
      if (!isOrdersDraft(serverDraft)) throw new Error('retired sheet grammar');
    } catch {
      replaceDraft(templateDraft());
      setActiveId(null);
      setCompilerIssue({
        path: 'orders',
        severity: 'error',
        message: 'That saved revision uses the retired sheet grammar. Start from this Orders v2 draft instead; old documents are not translated.',
      });
      return;
    }
    let live = true;
    void loadLocalDraft(serverDraft.localId).then((local) => {
      if (!live) return;
      const usable = local && isOrdersDraft(local) && local.mapPlan.mapId === catalog.map.id
        && (local.revision ?? 0) >= (serverDraft.revision ?? 0) ? local : serverDraft;
      replaceDraft(usable);
      setRestored(usable === local);
    }).catch(() => replaceDraft(serverDraft));
    return () => { live = false; };
  }, [catalog.map.id, replaceDraft, sheetQuery.data, templateDraft]);

  useEffect(() => {
    if (!draft) return;
    const timer = window.setTimeout(() => {
      void saveLocalDraft(draft).then(() => setLocalDrafts((current) => [
        draft,
        ...current.filter((entry) => entry.localId !== draft.localId),
      ]));
    }, 180);
    return () => window.clearTimeout(timer);
  }, [draft]);

  const clientIssues = useMemo(() => draft ? validateDraft(draft, catalog) : [], [catalog, draft]);
  const issues = compilerIssue ? [...clientIssues, compilerIssue] : clientIssues;
  const blocking = issues.filter((issue) => issue.severity === 'error');
  const runsForSheet = draft?.sheetId
    ? trialHistory.filter((run) => run.sheetId === draft.sheetId)
    : [];
  const selectedRun = runsForSheet.find((run) => run.matchId === selectedRunId) ?? runsForSheet[0];
  const { data: selectedRunLive } = useMatchLive(selectedRun?.matchId);
  const { data: selectedRunReplay } = useMatchReplay(selectedRun?.matchId, selectedRunLive);
  const selectedTracks = useMemo(() => replayUnitTracks(selectedRunReplay?.replay), [selectedRunReplay?.replay]);

  const mutate = (change: (next: SheetDraft) => void) => {
    const current = draftRef.current;
    if (!current) return;
    const next = structuredClone(current);
    change(next);
    undoStack.current = [...undoStack.current.slice(-79), structuredClone(current)];
    redoStack.current = [];
    draftRef.current = next;
    setDraft(next);
    setCompilerIssue(null);
    forceHistory((value) => value + 1);
  };

  const undo = useCallback(() => {
    const previous = undoStack.current.pop(); const current = draftRef.current;
    if (!previous || !current) return;
    redoStack.current.push(structuredClone(current));
    draftRef.current = previous; setDraft(previous); forceHistory((value) => value + 1);
  }, []);
  const redo = useCallback(() => {
    const next = redoStack.current.pop(); const current = draftRef.current;
    if (!next || !current) return;
    undoStack.current.push(structuredClone(current));
    draftRef.current = next; setDraft(next); forceHistory((value) => value + 1);
  }, []);

  const saveDraft = async (asCopy = false): Promise<TacticalSheet> => {
    const current = draftRef.current;
    if (!current) throw new Error('No draft is open.');
    setCompilerIssue(null);
    const prepared = await pinDraft(current);
    const targetId = asCopy ? null : prepared.draft.sheetId;
    const previousLocalId = current.localId;
    try {
      const saved = await save.mutateAsync({
        sheetId: targetId ?? undefined,
        body: {
          name: asCopy ? `${prepared.draft.name} copy` : prepared.draft.name,
          expectedRevision: targetId ? prepared.draft.revision : null,
          ordersJson: prepared.ordersJson,
          mapPlanJson: prepared.mapPlanJson,
          enterLadder: prepared.draft.enterLadder,
        },
      });
      const persisted = fromServer(saved);
      await saveLocalDraft(persisted);
      if (previousLocalId !== persisted.localId) await deleteLocalDraft(previousLocalId);
      setActiveId(saved.id);
      replaceDraft(persisted);
      setLocalDrafts((currentDrafts) => [persisted, ...currentDrafts.filter((entry) =>
        entry.localId !== previousLocalId && entry.localId !== persisted.localId)]);
      setRestored(false);
      if (!targetId) navigate(`/entrants/sheets/${saved.id}/edit`, { replace: true });
      return saved;
    } catch (cause) {
      const issue = sheetIssueFromCompilerError(cause);
      setCompilerIssue(issue);
      revealIssue(issue.path);
      throw cause;
    }
  };

  const runTrial = async () => {
    const current = draftRef.current;
    if (!current || blocking.length > 0 || !stockId) return;
    try {
      const snapshot = snapshotForTrial(current);
      const saved = await saveDraft(false);
      const parsed = Number(seed);
      const resolvedSeed = randomSeed || !Number.isSafeInteger(parsed) ? null : parsed;
      const match = await trial.mutateAsync({
        sheetId: saved.id,
        body: { stockSheetId: stockId, seed: resolvedSeed },
      });
      const previous = trialHistory.find((run) => run.sheetId === saved.id);
      const stock = catalog.stockOpponents.find((entry) => entry.id === stockId);
      const recorded: SheetTrialRun = {
        matchId: match.id,
        sheetId: saved.id,
        sheetName: saved.name,
        revision: saved.revision,
        contentHash: saved.contentHash,
        stockSheetId: stockId,
        stockName: stock?.name ?? stockId,
        seed: resolvedSeed,
        createdAt: new Date().toISOString(),
        changes: describeTrialChanges(previous?.snapshot, snapshot),
        snapshot,
      };
      const updated = saveTrialHistory([recorded, ...trialHistory.filter((entry) => entry.matchId !== match.id)]);
      setTrialHistory(updated);
      setSelectedRunId(match.id);
      navigate(`/matches/${match.id}`, {
        state: { returnTo: `/entrants/sheets/${saved.id}/edit`, returnLabel: saved.name },
      });
    } catch {
      // Mutation state owns the visible error; IndexedDB still owns the draft.
    }
  };

  const startNew = () => {
    const next = templateDraft();
    setActiveId(null); replaceDraft(next); setCompilerIssue(null); setRestored(false);
    void saveLocalDraft(next).then(() => navigate('/entrants/sheets/new'));
  };

  const openSaved = (id: string) => {
    setActiveId(id); setLibraryOpen(false); setCompilerIssue(null);
    navigate(`/entrants/sheets/${id}/edit`);
  };
  const openLocal = (local: SheetDraft) => {
    setActiveId(local.sheetId); replaceDraft(local); setRestored(true); setLibraryOpen(false);
    navigate(local.sheetId ? `/entrants/sheets/${local.sheetId}/edit` : '/entrants/sheets/new');
  };

  const deleteDraft = async () => {
    const current = draftRef.current;
    if (!current?.sheetId || !window.confirm(`Delete “${current.name}”?`)) return;
    await remove.mutateAsync(current.sheetId);
    await deleteLocalDraft(current.localId);
    startNew();
  };

  useEffect(() => {
    const keyboard = (event: globalThis.KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey)) return;
      const target = event.target;
      const editing = target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement
        || target instanceof HTMLSelectElement || target instanceof HTMLElement && target.isContentEditable;
      if (event.key.toLowerCase() === 'z' && !editing) {
        event.preventDefault(); if (event.shiftKey) redo(); else undo();
      }
      if (event.key.toLowerCase() === 's') {
        event.preventDefault(); if (!save.isPending && blocking.length === 0) void saveDraft().catch(() => undefined);
      }
      if (event.key === 'Enter' && !editing) {
        event.preventDefault(); if (!trial.isPending && !save.isPending) void runTrial();
      }
    };
    window.addEventListener('keydown', keyboard);
    return () => window.removeEventListener('keydown', keyboard);
  });

  if (initialSheetId && sheetQuery.isError) return <section className="panel pad" role="alert">
    <p className="lab text-arena-hot">Sheet unavailable</p><p className="t-meta mt-2">It may have been deleted or belongs to another account.</p>
    <button type="button" className="btn btn-primary mt-3 min-h-11" onClick={startNew}>Start a new sheet</button>
  </section>;
  if (!draft) return <section className="panel pad"><p className="t-meta">Restoring your workshop…</p></section>;

  const practiceLabel = trial.isPending ? 'Starting match…' : runsForSheet.length ? 'Practice again' : 'Practice & watch';
  return <div className="grid min-w-0 gap-4 lg:grid-cols-[248px_minmax(0,1fr)]">
    <SheetRail current={draft} restored={restored} localDrafts={localDrafts}
      saved={sheetsQuery.data ?? []} loading={sheetsQuery.isPending} runs={runsForSheet}
      selectedRunId={selectedRun?.matchId ?? null} onNew={startNew} onOpenLocal={openLocal}
      onOpenSaved={openSaved} onPlotRun={(id) => { setSelectedRunId(id); setSection('map'); }} />

    <main className="flex min-w-0 flex-col gap-3 pb-20 sm:pb-0" aria-label="Open tactical sheet">
      <section className="panel panel-feature pad">
        <div className="grid gap-3 xl:grid-cols-[minmax(0,1fr)_auto] xl:items-end">
          <label><span className="lab mb-1 block">Entrant name</span><input className="field min-h-12 text-[17px] font-semibold"
            value={draft.name} maxLength={60} onChange={(event) => mutate((next) => { next.name = event.target.value; })} /></label>
          <div className="flex flex-wrap gap-2">
            <button type="button" className="btn min-h-11 lg:hidden" onClick={() => setLibraryOpen(true)}>Sheets &amp; runs</button>
            <button type="button" className="btn min-h-11 min-w-11" aria-label="Undo" disabled={undoStack.current.length === 0} onClick={undo}>↶</button>
            <button type="button" className="btn min-h-11 min-w-11" aria-label="Redo" disabled={redoStack.current.length === 0} onClick={redo}>↷</button>
            <button type="button" className="btn min-h-11" onClick={() => setToolsOpen(true)}>Sheet tools</button>
            <button type="button" className="btn btn-primary min-h-11 flex-1" disabled={save.isPending || blocking.length > 0}
              onClick={() => void saveDraft().catch(() => undefined)}>{save.isPending ? 'Compiling…' : draft.sheetId ? `Save revision ${(draft.revision ?? 0) + 1}` : 'Save & field'}</button>
          </div>
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-2 border-t border-arena-edge pt-3">
          <span className="pill">{draft.sheetId ? `revision ${draft.revision}` : 'device draft'}</span>
          <span className="t-micro">Autosaved on this device · server save runs the authoritative compiler</span>
          {save.isSuccess && <span className="t-micro text-arena-ok">Saved</span>}
          {(save.isError || remove.isError) && <span className="t-micro text-arena-hot">{(save.error ?? remove.error)?.message}</span>}
        </div>
      </section>

      <IssueSummary issues={issues} onOpen={(issue) => revealIssue(issue.path)} />

      <nav className="panel sticky top-0 z-30 flex items-center gap-1 overflow-x-auto p-1.5" aria-label="Sheet workshop">
        {SECTIONS.map((entry) => {
          const count = sectionIssues(entry.id, issues).length;
          return <button key={entry.id} type="button" className={clsx('btn min-h-12 min-w-[110px] flex-1', section === entry.id && 'btn-on')}
            aria-current={section === entry.id ? 'step' : undefined} onClick={() => { setSection(entry.id); setMapFocus(null); }}>
            <span><b className="block">{entry.label}{count ? ` · ${count}` : ''}</b><span className="t-micro mt-0.5 hidden md:block">{entry.description}</span></span>
          </button>;
        })}
        <button type="button" className="btn min-h-12 shrink-0" onClick={() => setPracticeOpen(true)}>Scenario</button>
        <button type="button" className="btn btn-secondary hidden min-h-12 shrink-0 gap-2 sm:inline-flex"
          disabled={blocking.length > 0 || save.isPending || trial.isPending || !stockId} onClick={() => void runTrial()}>
          <span className="btn-secondary-symbol">▶</span>{practiceLabel}
        </button>
      </nav>

      <div className="flex flex-wrap items-center gap-2 px-1 t-micro">
        <span>Practice: {catalog.stockOpponents.find((entry) => entry.id === stockId)?.name ?? stockId}</span><span>·</span>
        <span>{randomSeed ? 'new seed' : `seed ${seed}`}</span><span>·</span><span>unranked</span>
        {trial.isError && <span className="text-arena-hot">{trial.error.message}</span>}
      </div>

      {section === 'squad' && <SquadEditor catalog={catalog} orders={draft.orders} issues={issues}
        onChange={(orders) => mutate((next) => { next.orders = orders; })} />}
      {section === 'orders' && <OrdersWorkspace catalog={catalog} orders={draft.orders} mapPlan={draft.mapPlan} issues={issues}
        onChange={(orders) => mutate((next) => { next.orders = orders; })}
        onOpenMap={(focus) => { setMapFocus(focus ?? null); setSection('map'); }} />}
      {section === 'map' && <MapPlanEditor catalog={catalog} mapPlan={draft.mapPlan} issues={issues}
        tracks={selectedTracks} focus={mapFocus}
        trackLabel={selectedRun ? `r${selectedRun.revision} vs ${selectedRun.stockName} · seed ${selectedRun.seed ?? 'automatic'}` : null}
        onChange={(mapPlan) => mutate((next) => { next.mapPlan = mapPlan; })}
        onRename={(kind, previous, nextId) => mutate((next) => renameGeometry(next, kind, previous, nextId))} />}
    </main>

    <div className="fixed inset-x-0 bottom-0 z-50 border-t border-arena-edge bg-arena-panel/95 p-2 backdrop-blur sm:hidden">
      <div className="mx-auto grid max-w-xl grid-cols-[auto_minmax(0,1fr)] gap-2">
        <button type="button" className="btn min-h-12" onClick={() => setPracticeOpen(true)}>Scenario</button>
        <button type="button" className="btn btn-secondary min-h-12 gap-2" disabled={blocking.length > 0 || save.isPending || trial.isPending || !stockId}
          onClick={() => void runTrial()}><span className="btn-secondary-symbol">▶</span>{practiceLabel}</button>
      </div>
    </div>

    {libraryOpen && <Modal title="Sheets and practice runs" onClose={() => setLibraryOpen(false)}>
      <SheetLibrary current={draft} localDrafts={localDrafts} saved={sheetsQuery.data ?? []} runs={runsForSheet}
        selectedRunId={selectedRun?.matchId ?? null} onNew={startNew} onOpenLocal={openLocal} onOpenSaved={openSaved}
        onPlotRun={(id) => { setSelectedRunId(id); setSection('map'); setLibraryOpen(false); }} />
    </Modal>}
    {practiceOpen && <Modal title="Practice scenario" onClose={() => setPracticeOpen(false)}>
      <PracticeSetup catalog={catalog} stockId={stockId} setStockId={setStockId} seed={seed} setSeed={setSeed}
        randomSeed={randomSeed} setRandomSeed={setRandomSeed} />
    </Modal>}
    {toolsOpen && <Modal title="Sheet tools" onClose={() => setToolsOpen(false)}>
      <SheetTools draft={draft} canDelete={Boolean(draft.sheetId)} onEnterLadder={(value) => mutate((next) => { next.enterLadder = value; })}
        onSaveCopy={() => void saveDraft(true).catch(() => undefined)} onDelete={() => void deleteDraft()}
        onImport={(orders, mapPlan) => mutate((next) => { next.orders = orders; next.mapPlan = mapPlan; })} />
    </Modal>}
  </div>;

  function revealIssue(path: string) {
    const target = path.startsWith('mapPlan') ? 'map' : path.startsWith('orders.roles') ? 'squad' : 'orders';
    setSection(target);
    window.setTimeout(() => {
      const exact = document.querySelector<HTMLElement>(`[data-sheet-path="${CSS.escape(path)}"]`);
      const prefix = [...document.querySelectorAll<HTMLElement>('[data-sheet-path]')]
        .filter((element) => path.startsWith(element.dataset.sheetPath ?? ''))
        .sort((a, b) => (b.dataset.sheetPath?.length ?? 0) - (a.dataset.sheetPath?.length ?? 0))[0];
      (exact ?? prefix)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 30);
  }
}

function SheetRail({ current, restored, localDrafts, saved, loading, runs, selectedRunId, onNew, onOpenLocal, onOpenSaved, onPlotRun }: {
  current: SheetDraft; restored: boolean; localDrafts: SheetDraft[]; saved: TacticalSheetSummary[]; loading: boolean;
  runs: SheetTrialRun[]; selectedRunId: string | null; onNew: () => void; onOpenLocal: (draft: SheetDraft) => void;
  onOpenSaved: (id: string) => void; onPlotRun: (id: string) => void;
}) {
  return <aside className="panel hidden h-fit overflow-hidden lg:sticky lg:top-4 lg:block">
    <div className="flex items-center gap-2 border-b border-arena-edge p-3"><div className="mr-auto"><p className="lab">Workshop</p><p className="t-micro mt-1">Sheets &amp; recent runs</p></div><button type="button" className="btn min-h-11" onClick={onNew}>New</button></div>
    {restored && <p className="m-2 rounded-sm border border-arena-material/40 bg-arena-material/10 p-2 t-micro text-arena-material">Restored this device’s newer draft.</p>}
    <div className="max-h-64 overflow-y-auto p-2">
      {loading && <p className="t-micro p-2">Loading saved sheets…</p>}
      {localDrafts.filter((draft) => !draft.sheetId).map((draft) => <SheetButton key={draft.localId} name={draft.name} detail="Device draft"
        selected={current.localId === draft.localId} onClick={() => onOpenLocal(draft)} />)}
      {saved.map((sheet) => <SheetButton key={sheet.id} name={sheet.name} detail={`Revision ${sheet.revision}`}
        selected={current.sheetId === sheet.id} onClick={() => onOpenSaved(sheet.id)} />)}
      {!loading && saved.length === 0 && !localDrafts.some((draft) => !draft.sheetId) && <p className="t-micro p-2">Your first save creates a Season entrant.</p>}
    </div>
    <div className="border-t border-arena-edge p-2"><p className="lab px-1 pb-2">Recent practice</p>
      {runs.length === 0 ? <p className="t-micro px-1 pb-2">Run this draft once to compare changes and overlay actual tracks.</p>
        : <div className="max-h-72 space-y-2 overflow-y-auto">{runs.slice(0, 8).map((run) => <RunCard key={run.matchId} run={run}
          selected={selectedRunId === run.matchId} onPlot={() => onPlotRun(run.matchId)} returnTo={current.sheetId ? `/entrants/sheets/${current.sheetId}/edit` : '/entrants/sheets/new'} />)}</div>}
    </div>
  </aside>;
}

function SheetLibrary({ current, localDrafts, saved, runs, selectedRunId, onNew, onOpenLocal, onOpenSaved, onPlotRun }: {
  current: SheetDraft; localDrafts: SheetDraft[]; saved: TacticalSheetSummary[]; runs: SheetTrialRun[]; selectedRunId: string | null;
  onNew: () => void; onOpenLocal: (draft: SheetDraft) => void; onOpenSaved: (id: string) => void; onPlotRun: (id: string) => void;
}) {
  return <div className="grid gap-4 md:grid-cols-2"><section><div className="flex items-center gap-2"><p className="lab mr-auto">Sheets</p><button type="button" className="btn btn-primary min-h-11" onClick={onNew}>New sheet</button></div>
    <div className="mt-2 space-y-1.5">{localDrafts.filter((draft) => !draft.sheetId).map((draft) => <SheetButton key={draft.localId} name={draft.name} detail="Device draft" selected={current.localId === draft.localId} onClick={() => onOpenLocal(draft)} />)}
      {saved.map((sheet) => <SheetButton key={sheet.id} name={sheet.name} detail={`Revision ${sheet.revision}`} selected={current.sheetId === sheet.id} onClick={() => onOpenSaved(sheet.id)} />)}</div></section>
    <section><p className="lab">Practice runs</p><div className="mt-2 space-y-2">{runs.map((run) => <RunCard key={run.matchId} run={run} selected={selectedRunId === run.matchId}
      onPlot={() => onPlotRun(run.matchId)} returnTo={current.sheetId ? `/entrants/sheets/${current.sheetId}/edit` : '/entrants/sheets/new'} />)}{runs.length === 0 && <p className="t-meta">No runs for this sheet yet.</p>}</div></section>
  </div>;
}

function SheetButton({ name, detail, selected, onClick }: { name: string; detail: string; selected: boolean; onClick: () => void }) {
  return <button type="button" className={clsx('mb-1 min-h-12 w-full rounded-sm border px-2.5 py-2 text-left', selected ? 'border-arena-material/65 bg-arena-material/10' : 'border-transparent bg-arena-deep/25')}
    onClick={onClick}><b className="t-body block truncate">{name}</b><span className="t-micro block">{detail}</span></button>;
}

function RunCard({ run, selected, onPlot, returnTo }: { run: SheetTrialRun; selected: boolean; onPlot: () => void; returnTo: string }) {
  return <article className={clsx('rounded-sm border p-2', selected ? 'border-arena-ok/60 bg-arena-ok/10' : 'border-arena-edge')}>
    <div className="flex gap-2"><b className="t-body truncate">r{run.revision} · {run.stockName}</b><span className="val ml-auto">{run.seed ?? 'auto'}</span></div>
    <p className="t-micro mt-1">Changed: {run.changes.join(', ')}</p>
    <div className="mt-2 grid grid-cols-2 gap-1"><button type="button" className="btn min-h-11" onClick={onPlot}>Plot tracks</button>
      <Link to={`/matches/${run.matchId}`} state={{ returnTo, returnLabel: run.sheetName }} className="btn min-h-11">Watch</Link></div>
  </article>;
}

function PracticeSetup({ catalog, stockId, setStockId, seed, setSeed, randomSeed, setRandomSeed }: {
  catalog: TacticalSheetCatalog; stockId: string; setStockId: (value: string) => void;
  seed: string; setSeed: (value: string) => void; randomSeed: boolean; setRandomSeed: (value: boolean) => void;
}) {
  return <div><p className="t-meta">Practice is immediate and unranked. Saving and starting never discard the local draft.</p>
    <p className="lab mb-2 mt-4">Opponent</p><div className="grid gap-2 sm:grid-cols-2">{catalog.stockOpponents.map((stock) => <button key={stock.id} type="button" className="choice-card min-h-24" data-selected={stock.id === stockId}
      onClick={() => setStockId(stock.id)}><b className="type-display block text-[17px]">{stock.name}</b><span className="t-micro mt-1 block">{stock.description}</span></button>)}</div>
    <p className="lab mb-2 mt-4">Scenario</p><div className="grid grid-cols-2 gap-2"><button type="button" className="choice-card min-h-20" data-selected={!randomSeed} onClick={() => setRandomSeed(false)}><b className="t-body block">Repeat a seed</b><span className="t-micro mt-1 block">Compare changes in the same scenario.</span></button>
      <button type="button" className="choice-card min-h-20" data-selected={randomSeed} onClick={() => setRandomSeed(true)}><b className="t-body block">Fresh scenario</b><span className="t-micro mt-1 block">Let the server choose a seed.</span></button></div>
    {!randomSeed && <label className="mt-3 block max-w-sm"><span className="t-micro mb-1 block">Seed</span><input type="number" inputMode="numeric" className="field min-h-12" value={seed} onChange={(event) => setSeed(event.target.value)} /></label>}
  </div>;
}

function SheetTools({ draft, canDelete, onEnterLadder, onSaveCopy, onDelete, onImport }: {
  draft: SheetDraft; canDelete: boolean; onEnterLadder: (value: boolean) => void; onSaveCopy: () => void; onDelete: () => void;
  onImport: (orders: OrdersDocument, mapPlan: MapPlanDocument) => void;
}) {
  const file = useRef<HTMLInputElement>(null);
  const exportPair = () => {
    download(`${draft.orders.sheetId || 'sheet'}.json`, stringifyDocument(draft.orders));
    download(`${draft.mapPlan.mapPlanId || 'map-plan'}.json`, stringifyDocument(draft.mapPlan));
  };
  return <div className="grid gap-4 md:grid-cols-2"><section className="rounded-sm border border-arena-edge p-3">
    <p className="lab">Season</p><label className="mt-3 flex min-h-12 items-center gap-3 rounded-sm border border-arena-edge px-3 t-body"><input type="checkbox" checked={draft.enterLadder} onChange={(event) => onEnterLadder(event.target.checked)} />Field this entrant when saved<span className="t-micro ml-auto">default on</span></label>
    {draft.sheetId && <button type="button" className="btn mt-3 min-h-11" onClick={onSaveCopy}>Save as a new entrant</button>}
  </section><section className="rounded-sm border border-arena-edge p-3"><p className="lab">Portable source</p><p className="t-meta mt-2">JSON is an import/export utility, not the authoring surface. Derived hashes are never fields.</p>
    <div className="mt-3 flex flex-wrap gap-2"><button type="button" className="btn min-h-11" onClick={exportPair}>Export source pair</button><button type="button" className="btn min-h-11" onClick={() => file.current?.click()}>Import source pair</button></div>
    <input ref={file} type="file" accept="application/json,.json" multiple className="sr-only" onChange={(event) => {
      const files = [...(event.target.files ?? [])]; if (files.length !== 2) return;
      void Promise.all(files.map((entry) => entry.text())).then((texts) => {
        const parsed = texts.map((text) => JSON.parse(text) as OrdersDocument | MapPlanDocument);
        const orders = parsed.find((entry) => entry.schema === 'arc-relay-orders-v2') as OrdersDocument | undefined;
        const mapPlan = parsed.find((entry) => entry.schema === 'arc-relay-map-plan-v2') as MapPlanDocument | undefined;
        if (orders && mapPlan) onImport(orders, mapPlan);
      });
    }} />
  </section>{canDelete && <section className="rounded-sm border border-arena-hot/30 p-3 md:col-span-2"><p className="lab text-arena-hot">Danger zone</p><button type="button" className="btn mt-3 min-h-11 text-arena-hot" onClick={onDelete}>Delete this sheet</button></section>}</div>;
}

function IssueSummary({ issues, onOpen }: { issues: SheetIssue[]; onOpen: (issue: SheetIssue) => void }) {
  if (issues.length === 0) return <section className="rounded-sm border border-arena-ok/35 bg-arena-ok/5 px-3 py-2"><p className="t-body text-arena-ok">Draft is ready for the shared compiler.</p></section>;
  const errors = issues.filter((issue) => issue.severity === 'error');
  return <details className={clsx('panel overflow-hidden', errors.length && 'border-arena-hot/55')} open={errors.length > 0}>
    <summary className="flex min-h-12 cursor-pointer items-center gap-2 px-3"><span className="disclosure-chevron">›</span><b className={clsx('t-body', errors.length ? 'text-arena-hot' : 'text-arena-material')}>{errors.length ? `${errors.length} fixes before save` : `${issues.length} design notes`}</b><span className="t-micro ml-auto">Tap a finding to jump there</span></summary>
    <ul className="border-t border-arena-edge p-2">{issues.map((issue, index) => <li key={`${issue.path}:${index}`}><button type="button" className="min-h-11 w-full rounded-sm px-2 text-left text-xs hover:bg-arena-raise" onClick={() => onOpen(issue)}><b className={issue.severity === 'error' ? 'text-arena-hot' : 'text-arena-material'}>{issue.severity === 'error' ? 'Fix' : 'Note'}:</b> {issue.message}</button></li>)}</ul>
  </details>;
}

function Modal({ title, children, onClose }: { title: string; children: ReactNode; onClose: () => void }) {
  return <div className="fixed inset-0 z-[90] overflow-y-auto bg-black/75 p-2 sm:p-6" role="dialog" aria-modal="true" aria-label={title}
    onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}><section className="panel mx-auto max-w-5xl overflow-hidden"><header className="sticky top-0 z-10 flex items-center gap-2 border-b border-arena-edge bg-arena-panel p-3"><h2 className="type-display mr-auto text-[22px]">{title}</h2><button type="button" className="btn min-h-11 min-w-11" aria-label="Close" onClick={onClose}>×</button></header><div className="p-3 sm:p-4">{children}</div></section></div>;
}

function fromServer(sheet: TacticalSheet): SheetDraft {
  return parseDraft(sheet.name, sheet.id, sheet.revision, sheet.ordersJson, sheet.mapPlanJson, sheet.entrant.ladderOptedIn);
}

function isOrdersDraft(value: SheetDraft): boolean {
  return value?.orders?.schema === 'arc-relay-orders-v2' && value?.mapPlan?.schema === 'arc-relay-map-plan-v2';
}

function sectionIssues(section: Section, issues: SheetIssue[]) {
  return issues.filter((issue) => section === 'map' ? issue.path.startsWith('mapPlan')
    : section === 'squad' ? issue.path.startsWith('orders.roles') && !issue.path.includes('.orders')
      : issue.path.startsWith('orders'));
}

function renameGeometry(draft: SheetDraft, kind: 'route' | 'zone' | 'anchor', previous: string, nextId: string) {
  if (kind === 'route') { draft.mapPlan.routes[nextId] = draft.mapPlan.routes[previous]; delete draft.mapPlan.routes[previous]; }
  if (kind === 'zone') { draft.mapPlan.zones[nextId] = draft.mapPlan.zones[previous]; delete draft.mapPlan.zones[previous]; }
  if (kind === 'anchor') { draft.mapPlan.anchors[nextId] = draft.mapPlan.anchors[previous]; delete draft.mapPlan.anchors[previous]; }
  walkOrders(draft.orders, (node) => {
    if (kind === 'route') {
      if (node.move?.patrol === previous) node.move.patrol = nextId;
      if (node.move?.via === previous) node.move.via = nextId;
    }
    if (kind === 'zone' && typeof node.move?.to === 'object' && 'zone' in node.move.to && node.move.to.zone === previous) node.move.to.zone = nextId;
    if (kind === 'anchor' && typeof node.move?.to === 'object' && 'anchor' in node.move.to && node.move.to.anchor === previous) node.move.to.anchor = nextId;
  });
  if (kind === 'zone') for (const condition of Object.values(draft.orders.conditions)) if (condition.zone === previous) condition.zone = nextId;
}

function walkOrders(orders: OrdersDocument, visit: (node: OrderNode) => void) {
  const walk = (node: OrderNode) => { visit(node); node.orders?.forEach(walk); };
  orders.roles.forEach((role) => role.orders.forEach(walk));
  Object.values(orders.sharedOrders).forEach(walk);
}

function download(name: string, content: string) {
  const url = URL.createObjectURL(new Blob([content], { type: 'application/json' }));
  const link = document.createElement('a'); link.href = url; link.download = name; link.click(); URL.revokeObjectURL(url);
}
