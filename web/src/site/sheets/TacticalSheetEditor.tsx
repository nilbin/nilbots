import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type PointerEvent as ReactPointerEvent,
  type ReactNode,
} from 'react';
import { Link, useNavigate } from 'react-router-dom';
import clsx from 'clsx';
import ClassIcon from '../../components/ClassIcon';
import type { TacticalSheet, TacticalSheetCatalog } from '../api';
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
  CONDITION_FACTS,
  CONDITION_OPERATORS,
  TACTICAL_VERBS,
  UI_STATE_KEY,
  assignmentIdFromName,
  destinationOf,
  freshMode,
  loadLocalDraft,
  parseDraft,
  pinDraft,
  predicateSubjectKind,
  renameAssignment,
  saveLocalDraft,
  setAssignmentTarget,
  sheetIssueFromCompilerError,
  validateDraft,
  verbOf,
  type CustodyPolicy,
  type CohortDefinition,
  type Doctrine,
  type FightBlock,
  type FocusDefinition,
  type JsonObject,
  type LayoutAnchor,
  type LayoutDocument,
  type LayoutRoute,
  type LayoutZone,
  type Point,
  type Predicate,
  type RepairBlock,
  type RoleDefinition,
  type SheetDraft,
  type SheetIssue,
  type TacticalMode,
  type TacticalVerb,
} from './sheetAuthoring';
import {
  describeTrialChanges,
  loadTrialHistory,
  replayUnitTracks,
  saveTrialHistory,
  snapshotForTrial,
  type SheetTrialRun,
  type UnitTrack,
} from './sheetTrials';

interface TacticalSheetEditorProps {
  catalog: TacticalSheetCatalog;
  initialSheetId?: string | null;
}

type PlotKind = 'route' | 'zone' | 'anchor';
type Side = 'west' | 'east';
type EditorSection = 'composition' | 'doctrines' | 'map';
type DoctrineTool = 'conditions' | 'custody' | 'coordination' | 'memory' | null;

export default function TacticalSheetEditor({ catalog, initialSheetId = null }: TacticalSheetEditorProps) {
  const navigate = useNavigate();
  const sheetsQuery = useTacticalSheets(true);
  const [activeId, setActiveId] = useState<string | null>(initialSheetId);
  const sheetQuery = useTacticalSheet(activeId);
  const [draft, setDraft] = useState<SheetDraft | null>(null);
  const [layoutHash, setLayoutHash] = useState<string | null>(null);
  const [stockId, setStockId] = useState(catalog.stockOpponents[0]?.id ?? '');
  const [seed, setSeed] = useState('104729');
  const [restored, setRestored] = useState(false);
  const [section, setSection] = useState<EditorSection>('doctrines');
  const [doctrineTool, setDoctrineTool] = useState<DoctrineTool>(null);
  const [compilerIssue, setCompilerIssue] = useState<SheetIssue | null>(null);
  const [trialHistory, setTrialHistory] = useState<SheetTrialRun[]>(() => loadTrialHistory());
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  // Assume the configured action is visible until the observer proves otherwise.
  // This avoids flashing a duplicate mobile shortcut while the editor hydrates.
  const [practiceActionVisible, setPracticeActionVisible] = useState(true);
  const [practiceActionSeen, setPracticeActionSeen] = useState(false);
  const practiceActionRef = useRef<HTMLButtonElement | null>(null);
  const save = useSaveTacticalSheet();
  const remove = useDeleteTacticalSheet();
  const trial = useTrialTacticalSheet();

  useEffect(() => {
    if (initialSheetId) {
      setActiveId(initialSheetId);
      setRestored(false);
      return;
    }
    let live = true;
    void loadLocalDraft().then((local) => {
      if (!live) return;
      const currentLocal = local?.playbook?.doctrines?.roles
        && local.layout.mapId === catalog.map.id ? local : null;
      setDraft(currentLocal ?? parseDraft(
        'Untitled tactical sheet',
        null,
        null,
        catalog.templatePlaybookJson,
        catalog.templateLayoutJson,
      ));
      // Keep an unsaved local draft authoritative on restore. Selecting its
      // server row explicitly is what replaces it with the saved revision.
      setActiveId(null);
      setRestored(currentLocal !== null);
    }).catch(() => {
      if (!live) return;
      setDraft(parseDraft(
        'Untitled tactical sheet', null, null,
        catalog.templatePlaybookJson, catalog.templateLayoutJson,
      ));
    });
    return () => { live = false; };
  }, [catalog.templateLayoutJson, catalog.templatePlaybookJson, initialSheetId]);

  useEffect(() => {
    if (!sheetQuery.data) return;
    setDraft(fromServer(sheetQuery.data));
    setRestored(false);
  }, [sheetQuery.data]);

  useEffect(() => {
    const action = practiceActionRef.current;
    if (!action || typeof IntersectionObserver === 'undefined') return;
    const observer = new IntersectionObserver(
      ([entry]) => {
        setPracticeActionVisible(entry.isIntersecting);
        if (entry.isIntersecting) setPracticeActionSeen(true);
      },
      { threshold: 0.25 },
    );
    observer.observe(action);
    return () => observer.disconnect();
  }, [draft?.sheetId]);

  useEffect(() => {
    if (!draft) return;
    let live = true;
    const timer = window.setTimeout(() => {
      void saveLocalDraft(draft);
      void pinDraft(draft).then((prepared) => {
        if (live) setLayoutHash(prepared.layoutSha256);
      });
    }, 180);
    return () => {
      live = false;
      window.clearTimeout(timer);
    };
  }, [draft]);

  const clientIssues = useMemo(
    () => draft ? validateDraft(draft, catalog, layoutHash) : [],
    [catalog, draft, layoutHash],
  );
  const issues = compilerIssue ? [...clientIssues, compilerIssue] : clientIssues;
  const blocking = issues.filter((issue) =>
    issue.severity === 'error' && issue.path !== 'playbook.layout.sha256');
  const runsForSheet = draft?.sheetId
    ? trialHistory.filter((run) => run.sheetId === draft.sheetId)
    : [];
  const selectedRun = runsForSheet.find((run) => run.matchId === selectedRunId)
    ?? runsForSheet[0];
  const selectedRunMatchId = selectedRun?.matchId;
  const { data: selectedRunLive } = useMatchLive(selectedRunMatchId);
  const { data: selectedRunReplay } = useMatchReplay(selectedRunMatchId, selectedRunLive);
  const selectedTracks = useMemo(
    () => replayUnitTracks(selectedRunReplay?.replay),
    [selectedRunReplay?.replay],
  );
  const revealIssue = (path: string) => {
    setSection(sectionForIssue(path));
    setDoctrineTool(doctrineToolForIssue(path));
    window.setTimeout(() => scrollToIssue(path), 0);
  };

  const startNew = () => {
    const next = parseDraft(
      'Untitled tactical sheet', null, null,
      catalog.templatePlaybookJson, catalog.templateLayoutJson,
    );
    setActiveId(null);
    setDraft(next);
    setRestored(false);
    setCompilerIssue(null);
    resetMutations();
    // The /new route restores the browser draft by design. Persist the fresh
    // document before navigating so an older autosave cannot flash back in.
    void saveLocalDraft(next).then(() => navigate('/entrants/sheets/new'));
  };

  const saveDraft = async (asCopy = false): Promise<TacticalSheet> => {
    if (!draft) throw new Error('No draft is open.');
    setCompilerIssue(null);
    const prepared = await pinDraft(draft);
    const targetId = asCopy ? null : prepared.draft.sheetId;
    try {
      const saved = await save.mutateAsync({
        sheetId: targetId ?? undefined,
        body: {
          name: asCopy ? `${prepared.draft.name} copy` : prepared.draft.name,
          expectedRevision: targetId ? prepared.draft.revision : null,
          playbookJson: prepared.playbookJson,
          layoutJson: prepared.layoutJson,
          enterLadder: prepared.draft.enterLadder,
        },
      });
      const persisted = fromServer(saved);
      await saveLocalDraft(persisted);
      setActiveId(saved.id);
      setDraft(persisted);
      setLayoutHash(prepared.layoutSha256);
      setRestored(false);
      if (!targetId)
        navigate(`/entrants/sheets/${saved.id}/edit`, { replace: true });
      return saved;
    } catch (cause) {
      const issue = sheetIssueFromCompilerError(cause);
      setCompilerIssue(issue);
      revealIssue(issue.path);
      throw cause;
    }
  };

  const runTrial = async () => {
    if (!draft) return;
    try {
      const snapshot = snapshotForTrial(draft);
      const saved = await saveDraft(false);
      const parsedSeed = seed.trim() === '' ? null : Number(seed);
      const resolvedSeed = Number.isSafeInteger(parsedSeed) ? parsedSeed : null;
      const match = await trial.mutateAsync({
        sheetId: saved.id,
        body: {
          stockSheetId: stockId,
          seed: resolvedSeed,
        },
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
      const updatedHistory = saveTrialHistory([
        recorded,
        ...trialHistory.filter((run) => run.matchId !== recorded.matchId),
      ]);
      setTrialHistory(updatedHistory);
      setSelectedRunId(recorded.matchId);
      navigate(`/matches/${match.id}`, {
        state: { returnTo: `/entrants/sheets/${saved.id}/edit`, returnLabel: saved.name },
      });
    } catch {
      // Both mutations own their visible error state; the local draft remains intact.
    }
  };

  const deleteDraft = async () => {
    if (!draft?.sheetId) return;
    if (!window.confirm(`Delete “${draft.name}”? Ranked-history sheets cannot be deleted.`)) return;
    await remove.mutateAsync(draft.sheetId);
    startNew();
  };

  const mutate = (change: (next: SheetDraft) => void) => {
    setDraft((current) => {
      if (!current) return current;
      const next = structuredClone(current);
      change(next);
      return next;
    });
  };

  const resetMutations = () => {
    save.reset();
    trial.reset();
    remove.reset();
    setCompilerIssue(null);
  };

  if (initialSheetId && sheetQuery.isError) return (
    <section className="panel pad" role="alert">
      <p className="lab text-arena-hot">Sheet unavailable</p>
      <p className="t-meta mt-2">It may have been deleted, or it belongs to another account.</p>
      <button type="button" className="btn mt-3 min-h-11" onClick={startNew}>Open a new sheet</button>
    </section>
  );
  if (!draft) return <section className="panel pad"><p className="t-meta">Restoring local draft…</p></section>;

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-4 lg:grid-cols-[250px_minmax(0,1fr)]">
      <aside className="panel h-fit lg:sticky lg:top-4">
        <div className="pad flex items-center gap-2 border-b border-arena-edge">
          <div className="mr-auto">
            <p className="lab">Saved sheets</p>
            <p className="t-micro mt-1">Server revisions</p>
          </div>
          <button type="button" className="btn min-h-11" onClick={startNew}>New</button>
        </div>
        {restored && (
          <p className="mx-2 mt-2 rounded-sm border border-arena-material/35 bg-arena-material/10 p-2 t-micro text-arena-material">
            Restored the last local draft from this device.
          </p>
        )}
        <div className="flex gap-2 overflow-x-auto p-2 lg:block lg:max-h-[300px] lg:overflow-y-auto">
          {sheetsQuery.isPending && <p className="t-micro p-2">Loading…</p>}
          {sheetsQuery.isError && <p className="t-micro p-2 text-arena-hot">Could not load saved sheets.</p>}
          {sheetsQuery.data?.map((sheet) => (
            <button
              key={sheet.id}
              type="button"
              className={clsx(
                'min-h-12 min-w-[190px] rounded-sm border px-2.5 py-2 text-left lg:mb-1 lg:w-full lg:min-w-0',
                draft.sheetId === sheet.id
                  ? 'border-arena-ok/60 bg-arena-ok/10'
                  : 'border-transparent active:border-arena-edge',
              )}
              onClick={() => {
                setActiveId(sheet.id);
                resetMutations();
                navigate(`/entrants/sheets/${sheet.id}/edit`);
              }}
            >
              <span className="t-body block truncate">{sheet.name}</span>
              <span className="t-micro block">Revision {sheet.revision}</span>
            </button>
          ))}
          {sheetsQuery.data?.length === 0 && (
            <p className="t-meta p-2">Your first save creates a sheet entrant.</p>
          )}
        </div>
        <div className="border-t border-arena-edge p-2">
          <p className="lab px-1 pb-2">Recent practice</p>
          {runsForSheet.length === 0 ? (
            <p className="t-micro px-1 pb-1">Practice with this draft once to compare revisions and plot its actual tracks.</p>
          ) : (
            <div className="flex gap-2 overflow-x-auto lg:block lg:max-h-[260px] lg:overflow-y-auto">
              {runsForSheet.slice(0, 8).map((run) => (
                <article key={run.matchId} className={clsx(
                  'min-w-[220px] rounded-sm border p-2 lg:mb-2 lg:min-w-0',
                  selectedRun?.matchId === run.matchId
                    ? 'border-arena-ok/60 bg-arena-ok/10'
                    : 'border-arena-edge',
                )}>
                  <div className="flex items-center gap-2">
                    <span className="t-body truncate">r{run.revision} · {run.stockName}</span>
                    <span className="val ml-auto">{run.seed ?? 'auto'}</span>
                  </div>
                  <p className="t-micro mt-1 line-clamp-2">Changed: {run.changes.join(', ')}</p>
                  <div className="mt-2 flex gap-1">
                    <button type="button" className="btn min-h-11 flex-1"
                      aria-pressed={selectedRun?.matchId === run.matchId}
                      onClick={() => {
                        setSelectedRunId(run.matchId);
                        setSection('map');
                      }}>
                      Plot tracks
                    </button>
                    <Link to={`/matches/${run.matchId}`}
                      state={{ returnTo: `/entrants/sheets/${draft.sheetId}/edit`, returnLabel: draft.name }}
                      className="btn inline-flex min-h-11 flex-1 items-center justify-center">Watch</Link>
                  </div>
                </article>
              ))}
            </div>
          )}
        </div>
        <div className="border-t border-arena-edge p-2">
          <p className="t-micro px-1">Draft changes autosave on this device.</p>
        </div>
      </aside>

      <section className="flex min-w-0 flex-col gap-4" aria-label="Open sheet draft">
        <section className="panel pad">
          <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-end">
            <label>
              <span className="lab mb-1.5 block">Sheet name</span>
              <input
                className="field min-h-11"
                value={draft.name}
                maxLength={60}
                onChange={(event) => mutate((next) => { next.name = event.target.value; })}
              />
            </label>
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                className="btn btn-primary min-h-11 flex-1"
                disabled={save.isPending || blocking.length > 0}
                onClick={() => void saveDraft(false).catch(() => undefined)}
              >
                {save.isPending ? 'Compiling…' : draft.sheetId ? `Save r${(draft.revision ?? 0) + 1}` : 'Save & field in Season'}
              </button>
              {draft.sheetId && (
                <button type="button" className="btn min-h-11" disabled={save.isPending}
                  onClick={() => void saveDraft(true).catch(() => undefined)}>Save copy</button>
              )}
              {draft.sheetId && (
                <button type="button" className="btn min-h-11 text-arena-hot" disabled={remove.isPending}
                  onClick={() => void deleteDraft()}>Delete</button>
              )}
            </div>
          </div>
          <label className="mt-3 flex min-h-11 items-center gap-2 rounded-sm border border-arena-edge px-3 t-body">
            <input
              type="checkbox"
              checked={draft.enterLadder}
              onChange={(event) => mutate((next) => { next.enterLadder = event.target.checked; })}
            />
            Field this sheet in Season 1 when saved
            <span className="t-micro ml-auto">on by default</span>
          </label>
          {(save.isError || remove.isError) && (
            <p className="t-micro mt-2 text-arena-hot">{(save.error ?? remove.error)?.message}</p>
          )}
          {save.isSuccess && <p className="t-micro mt-2 text-arena-ok">Battle plan checked and saved.</p>}
        </section>

        <IssueSummary issues={issues} onNavigate={(issue) => {
          revealIssue(issue.path);
        }} />
        <section id="trial-run" className="panel panel-feature pad scroll-mt-16">
          <div className="flex flex-wrap items-start gap-3">
            <div className="mr-auto max-w-2xl">
              <p className="lab text-arena-material">Change → practice → watch → change</p>
              <h2 className="type-display mt-1 text-[22px]">Practice with this draft</h2>
              <p className="t-meta mt-1">
                Practice is immediate and unranked. The draft is saved locally first,
                compiled on the server, then opened in the replay viewer. Return here to
                compare the run and overlay its actual movement on your map.
              </p>
            </div>
            <span className="pill">local draft safe</span>
          </div>
          <div className="mt-3 grid gap-3 sm:grid-cols-[minmax(0,1fr)_150px_auto] sm:items-end">
            <label>
              <span className="t-micro mb-1 block">Practice opponent</span>
              <select className="field min-h-11" value={stockId} onChange={(event) => setStockId(event.target.value)}>
                {catalog.stockOpponents.map((stock) => (
                  <option key={stock.id} value={stock.id}>{stock.name}</option>
                ))}
              </select>
            </label>
            <label>
              <span className="t-micro mb-1 block">Seed</span>
              <input className="field min-h-11 font-mono" inputMode="numeric" value={seed}
                onChange={(event) => setSeed(event.target.value)} />
            </label>
            <button ref={practiceActionRef} type="button" className="btn btn-primary min-h-11"
              disabled={blocking.length > 0 || save.isPending || trial.isPending || !stockId}
              onClick={() => void runTrial()}>
              {trial.isPending ? 'Starting practice…' : runsForSheet.length ? 'Practice again & watch' : 'Practice & watch'}
            </button>
          </div>
          {catalog.stockOpponents.find((stock) => stock.id === stockId) && (
            <p className="t-micro mt-2">
              {catalog.stockOpponents.find((stock) => stock.id === stockId)!.description}
            </p>
          )}
          {trial.isError && <p className="t-micro mt-2 text-arena-hot">{trial.error.message}</p>}
        </section>

        <nav className="panel sticky top-0 z-20 flex gap-1 overflow-x-auto p-1.5"
          aria-label="Sheet editor sections">
          {EDITOR_SECTIONS.map((entry) => {
            const count = issues.filter((issue) => entry.matches(issue.path)).length;
            return <button key={entry.id} type="button"
              className={clsx('btn min-h-11 shrink-0 flex-1', section === entry.id && 'btn-on')}
              aria-current={section === entry.id ? 'step' : undefined}
              onClick={() => {
                setSection(entry.id);
                setDoctrineTool(null);
              }}>
              {entry.label}{count > 0 ? ` · ${count}` : ''}
            </button>;
          })}
        </nav>

        {section === 'composition' && (
          <CompositionEditor draft={draft} catalog={catalog} issues={issues} mutate={mutate} />
        )}
        {section === 'doctrines' && (
          <DoctrineEditor draft={draft} catalog={catalog} issues={issues} mutate={mutate}
            openConditions={() => setDoctrineTool('conditions')}
            openCustody={() => setDoctrineTool('custody')}
            openCoordination={() => setDoctrineTool('coordination')}
            openMemory={() => setDoctrineTool('memory')} />
        )}
        {section === 'map' && (
          <MapPlotter catalog={catalog} layout={draft.layout} issues={issues} tracks={selectedTracks}
            trackLabel={selectedRun
              ? `r${selectedRun.revision} vs ${selectedRun.stockName}, seed ${selectedRun.seed ?? 'auto'}`
              : null}
            onChange={(layout) => mutate((next) => { next.layout = layout; })} />
        )}
      </section>
      {practiceActionSeen && !practiceActionVisible && (
        <button type="button"
          className="btn btn-primary fixed right-3 bottom-[calc(3.5rem+env(safe-area-inset-bottom)+10px)] z-30 min-h-12 px-5 shadow-xl min-[640px]:hidden"
          disabled={blocking.length > 0 || save.isPending || trial.isPending || !stockId}
          onClick={() => void runTrial()}>
          {trial.isPending ? 'Starting…' : runsForSheet.length ? '▶ Practice again' : '▶ Practice'}
        </button>
      )}
      {doctrineTool === 'conditions' && (
        <SheetSubEditorModal title="Doctrine triggers"
          description="Reusable facts that decide when priority modes start and stop."
          onClose={() => setDoctrineTool(null)}>
          <PredicateEditor draft={draft} issues={issues} mutate={mutate} />
        </SheetSubEditorModal>
      )}
      {doctrineTool === 'custody' && (
        <SheetSubEditorModal title="Core custody"
          description="Specialized doctrine policy for carrying, recovery and delivery."
          onClose={() => setDoctrineTool(null)}>
          <CustodyEditor draft={draft} issues={issues} mutate={mutate} />
        </SheetSubEditorModal>
      )}
      {doctrineTool === 'coordination' && (
        <SheetSubEditorModal title="Coordination limits"
          description="Small shared caps. Assignments still own every behavior in their own ladders."
          onClose={() => setDoctrineTool(null)}>
          <CoordinationEditor draft={draft} issues={issues} mutate={mutate} />
        </SheetSubEditorModal>
      )}
      {doctrineTool === 'memory' && (
        <SheetSubEditorModal title="Team memory"
          description="How long sightings and objective events remain useful to triggers."
          onClose={() => setDoctrineTool(null)}>
          <MemoryEditor draft={draft} issues={issues} mutate={mutate} />
        </SheetSubEditorModal>
      )}
    </div>
  );
}

function CompositionEditor({ draft, catalog, issues, mutate }: {
  draft: SheetDraft;
  catalog: TacticalSheetCatalog;
  issues: SheetIssue[];
  mutate: Mutate;
}) {
  const counts = new Map<string, number>();
  draft.playbook.composition.forEach((id) => counts.set(id, (counts.get(id) ?? 0) + 1));
  const assignments = draft.playbook.roles ?? [];
  const [activeSlot, setActiveSlot] = useState(0);
  const [activeAssignmentId, setActiveAssignmentId] = useState(assignments[0]?.roleId ?? '');
  const activeIndex = assignments.findIndex((entry) => entry.roleId === activeAssignmentId);
  const activeAssignment = assignments[activeIndex] ?? assignments[0];
  const totalTargets = assignments.reduce((sum, entry) => sum + (entry.preferred ?? 0), 0);
  const classById = new Map(catalog.classes.map((entry) => [entry.id, entry]));

  useEffect(() => {
    if (activeAssignment && activeAssignment.roleId !== activeAssignmentId)
      setActiveAssignmentId(activeAssignment.roleId);
  }, [activeAssignment, activeAssignmentId]);

  const updateAssignment = (roleId: string, change: (role: RoleDefinition) => void) =>
    mutate((next) => {
      const target = next.playbook.roles.find((entry) => entry.roleId === roleId);
      if (target) change(target);
    });

  return (
    <div className="flex flex-col gap-4">
      <section className="panel pad">
        <div className="flex flex-wrap items-end gap-2">
          <div className="mr-auto">
            <p className="lab">Composition</p>
            <h2 className="type-display mt-1 text-[21px]">Choose the eight bodies</h2>
            <p className="t-micro mt-1">Unlocked classes · two copies maximum</p>
          </div>
          <div className="flex gap-1">
            {draft.playbook.composition.map((classId, index) => (
              <ClassIcon key={index} classId={classId} label={`${index + 1}. ${classId}`} size={28} />
            ))}
          </div>
        </div>
        <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-8" aria-label="Fielded bodies">
          {Array.from({ length: catalog.slotCount }, (_, index) => {
            const current = draft.playbook.composition[index] ?? '';
            const classInfo = catalog.classes.find((entry) => entry.id === current);
            return (
              <button key={index} type="button" data-sheet-path={'composition.' + index}
                aria-pressed={activeSlot === index}
                className={clsx(
                  'relative min-h-[104px] overflow-hidden rounded-sm border p-2 text-left transition',
                  activeSlot === index
                    ? 'border-arena-material bg-arena-material/10 shadow-[inset_0_-3px_0_var(--color-arena-material)]'
                    : 'border-arena-edge bg-arena-deep/25',
                )}
                onClick={() => setActiveSlot(index)}>
                <span className="absolute right-1.5 top-1.5 font-mono text-[10px] text-arena-dim">#{index + 1}</span>
                <ClassIcon classId={current} label={classInfo?.name ?? current} size={42} />
                <span className="type-display mt-1 block truncate text-[14px]">{classInfo?.name ?? humanizeToken(current)}</span>
                <span className="t-micro block truncate text-arena-material">✦ {classInfo?.signatureName ?? 'Unknown'}</span>
                <FieldIssues issues={issues} path={'composition.' + index} />
              </button>
            );
          })}
        </div>
        <div className="mt-3 rounded-sm border border-arena-edge bg-arena-deep/20 p-3">
          <div className="flex items-end gap-2">
            <div className="mr-auto">
              <p className="lab text-arena-material">Choose body {activeSlot + 1}</p>
              <p className="t-micro mt-1">Signature hardware is class identity. The sheet decides how it is used.</p>
            </div>
            <span className="pill">two-copy cap</span>
          </div>
          <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4 xl:grid-cols-8">
            {catalog.classes.map((entry) => {
              const current = draft.playbook.composition[activeSlot] ?? '';
              const unavailable = !entry.unlocked || (entry.id !== current
                && (counts.get(entry.id) ?? 0) >= catalog.maximumCopiesPerClass);
              const selected = entry.id === current;
              return (
                <button key={entry.id} type="button" disabled={unavailable} aria-pressed={selected}
                  className={clsx(
                    'min-h-[108px] rounded-sm border p-2 text-left transition disabled:opacity-35',
                    selected
                      ? 'border-arena-material bg-arena-material/15'
                      : 'border-arena-edge bg-arena-panel/35 active:border-arena-material',
                  )}
                  onClick={() => mutate((next) => { next.playbook.composition[activeSlot] = entry.id; })}>
                  <span className="flex items-start gap-2">
                    <ClassIcon classId={entry.id} label={entry.name} size={34} />
                    <span className="pill ml-auto">{counts.get(entry.id) ?? 0}/2</span>
                  </span>
                  <span className="type-display mt-1 block truncate text-[14px]">{entry.name}</span>
                  <span className="t-micro block truncate text-arena-material">✦ {entry.signatureName}</span>
                  <span className="t-micro mt-1 block line-clamp-2">{entry.fantasy}</span>
                </button>
              );
            })}
          </div>
        </div>
      </section>

      <section className="panel overflow-hidden">
        <header className="border-b border-arena-edge bg-[linear-gradient(115deg,rgba(210,110,45,.10),transparent_45%)] p-4">
          <p className="lab text-arena-material">Squad assignments</p>
          <div className="mt-1 flex flex-wrap items-end gap-3">
            <div className="mr-auto max-w-2xl">
              <h2 className="type-display text-[23px]">Turn chassis into jobs</h2>
              <p className="t-meta mt-1">
                A class says what a body can do. An assignment says what it should do in this sheet.
                Several classes—or several copies of one class—can answer the same assignment.
              </p>
            </div>
            <span className={clsx('pill', totalTargets > catalog.slotCount && 'text-arena-hot')}>
              {totalTargets}/{catalog.slotCount} target bodies
            </span>
          </div>
        </header>

        <div className="grid min-w-0 grid-cols-1 lg:grid-cols-[230px_minmax(0,1fr)]">
          <aside className="border-b border-arena-edge bg-arena-deep/30 p-2 lg:border-b-0 lg:border-r">
            <p className="lab px-2 py-1">Assignments</p>
            <div className="mt-1 flex gap-2 overflow-x-auto pb-1 lg:flex-col lg:overflow-visible">
              {assignments.map((assignment, index) => {
                const selected = activeAssignment?.roleId === assignment.roleId;
                const assignmentIssues = issues.filter((issue) => issue.path.startsWith(`roles.${index}`)).length;
                const classNames = assignment.candidateClasses
                  .map((id) => classById.get(id)?.name ?? humanizeToken(id));
                return (
                  <button key={assignment.roleId} type="button"
                    data-assignment-select={index}
                    aria-selected={selected}
                    className={clsx(
                      'min-w-[205px] rounded-sm border px-2.5 py-2 text-left transition lg:min-w-0',
                      selected
                        ? 'border-arena-material bg-arena-material/10 shadow-[inset_3px_0_0_var(--color-arena-material)]'
                        : 'border-transparent bg-arena-panel/40 active:border-arena-edge',
                    )}
                    onClick={() => setActiveAssignmentId(assignment.roleId)}>
                    <span className="flex items-center gap-2">
                      <AssignmentClassStack classIds={assignment.candidateClasses} size={30} />
                      <span className="min-w-0">
                        <span className="type-display block truncate text-[16px]">{humanizeToken(assignment.roleId)}</span>
                        <span className="t-micro block truncate">{classNames.join(' · ')}</span>
                      </span>
                      {assignmentIssues > 0 && <span className="pill ml-auto text-arena-hot">{assignmentIssues}</span>}
                    </span>
                    <span className="mt-2 flex items-center gap-2">
                      <AssignmentMeter count={assignment.preferred ?? 0} total={catalog.slotCount} />
                      <span className="t-micro ml-auto">{assignment.preferred ?? 0} target</span>
                    </span>
                  </button>
                );
              })}
            </div>
          </aside>

          {activeAssignment ? (() => {
            const roleIndex = assignments.indexOf(activeAssignment);
            const candidateSet = new Set(activeAssignment.candidateClasses);
            const classOptions = catalog.classes
              .filter((entry) => counts.has(entry.id) || candidateSet.has(entry.id))
              .sort((a, b) => {
                const aRank = activeAssignment.candidateClasses.indexOf(a.id);
                const bRank = activeAssignment.candidateClasses.indexOf(b.id);
                if (aRank >= 0 || bRank >= 0)
                  return (aRank < 0 ? 999 : aRank) - (bRank < 0 ? 999 : bRank);
                return a.name.localeCompare(b.name);
              });
            const signatureClasses = activeAssignment.candidateClasses
              .map((id) => classById.get(id)).filter((entry) => entry !== undefined);
            const target = activeAssignment.preferred ?? 0;
            return (
              <article className="min-w-0 p-3 sm:p-4" data-sheet-path={`roles.${roleIndex}`}>
                <div className="flex items-center gap-3 border-b border-arena-edge pb-3">
                  <AssignmentClassStack classIds={activeAssignment.candidateClasses} size={42} />
                  <div className="min-w-0 flex-1">
                    <p className="lab">Selected assignment</p>
                    <h3 className="type-display truncate text-[22px]">{humanizeToken(activeAssignment.roleId)}</h3>
                    <p className="t-micro">Commands, triggers and custody follow this assignment.</p>
                  </div>
                </div>

                <div className="mt-3 grid gap-3 xl:grid-cols-[minmax(0,1fr)_220px]">
                  <AssignmentNameEditor key={activeAssignment.roleId}
                    roleId={activeAssignment.roleId}
                    roleIds={assignments.map((entry) => entry.roleId)}
                    onRename={(nextId) => {
                      const previousId = activeAssignment.roleId;
                      mutate((next) => renameAssignment(next.playbook, previousId, nextId));
                      setActiveAssignmentId(nextId);
                    }} />
                  <div className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
                    <p className="lab">Normal staffing target</p>
                    <div className="mt-2 flex items-center justify-between gap-2">
                      <button type="button" className="btn min-h-11 min-w-11 text-[20px]"
                        aria-label={`Decrease ${humanizeToken(activeAssignment.roleId)} target`}
                        disabled={target <= 0}
                        onClick={() => mutate((next) => setAssignmentTarget(
                          next.playbook, activeAssignment.roleId, target - 1,
                        ))}>−</button>
                      <span className="text-center">
                        <span className="type-display block text-[28px] tabular">{target}</span>
                        <span className="t-micro">of {catalog.slotCount} bodies</span>
                      </span>
                      <button type="button" className="btn min-h-11 min-w-11 text-[20px]"
                        aria-label={`Increase ${humanizeToken(activeAssignment.roleId)} target`}
                        disabled={target >= 8 || totalTargets >= catalog.slotCount}
                        onClick={() => mutate((next) => setAssignmentTarget(
                          next.playbook, activeAssignment.roleId, target + 1,
                        ))}>+</button>
                    </div>
                    {totalTargets >= catalog.slotCount && target < catalog.slotCount && (
                      <p className="t-micro mt-2 text-center">Lower another assignment before moving its body here.</p>
                    )}
                  </div>
                </div>

                <section className="mt-3 rounded-sm border border-arena-edge bg-arena-deep/20 p-3">
                  <div>
                    <p className="lab">Eligible chassis · priority order</p>
                    <p className="t-micro mt-1">
                      Selected chassis are tried from left to right. The roster count shows how many can answer now.
                    </p>
                  </div>
                  <div className="mt-3 grid grid-cols-2 gap-2 xl:grid-cols-3">
                    {classOptions.map((entry) => {
                      const selected = candidateSet.has(entry.id);
                      const priority = activeAssignment.candidateClasses.indexOf(entry.id);
                      const onlyCandidate = selected && activeAssignment.candidateClasses.length === 1;
                      return (
                        <div key={entry.id} className={clsx(
                          'rounded-sm border p-2',
                          selected ? 'border-arena-material bg-arena-material/10' : 'border-arena-edge bg-arena-panel/35',
                        )}>
                          <button type="button" className="flex min-h-11 w-full items-center gap-2 text-left"
                            aria-pressed={selected}
                            disabled={onlyCandidate}
                            onClick={() => updateAssignment(activeAssignment.roleId, (next) => {
                              next.candidateClasses = selected
                                ? next.candidateClasses.filter((id) => id !== entry.id)
                                : [...next.candidateClasses, entry.id];
                            })}>
                            <ClassIcon classId={entry.id} label={entry.name} size={36} />
                            <span className="min-w-0">
                              <span className="type-display block truncate text-[15px]">{entry.name}</span>
                              <span className="t-micro block">
                                {counts.get(entry.id) ?? 0} fielded{selected ? ` · choice ${priority + 1}` : ''}
                              </span>
                            </span>
                          </button>
                          {selected && activeAssignment.candidateClasses.length > 1 && (
                            <div className="mt-1 flex gap-1 border-t border-arena-edge pt-1">
                              <button type="button" className="btn min-h-11 flex-1" aria-label={`Prefer ${entry.name} earlier`}
                                disabled={priority === 0}
                                onClick={() => updateAssignment(activeAssignment.roleId, (next) => {
                                  const at = next.candidateClasses.indexOf(entry.id);
                                  [next.candidateClasses[at - 1], next.candidateClasses[at]] =
                                    [next.candidateClasses[at], next.candidateClasses[at - 1]];
                                })}>↑ earlier</button>
                              <button type="button" className="btn min-h-11 flex-1" aria-label={`Prefer ${entry.name} later`}
                                disabled={priority === activeAssignment.candidateClasses.length - 1}
                                onClick={() => updateAssignment(activeAssignment.roleId, (next) => {
                                  const at = next.candidateClasses.indexOf(entry.id);
                                  [next.candidateClasses[at + 1], next.candidateClasses[at]] =
                                    [next.candidateClasses[at], next.candidateClasses[at + 1]];
                                })}>↓ later</button>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                  <FieldIssues issues={issues} path={`roles.${roleIndex}.candidateClasses`} />
                </section>

                {signatureClasses.length > 0 && (
                  <section className="mt-3">
                    <p className="lab text-arena-material">What these chassis bring</p>
                    <div className="mt-2 grid gap-2 sm:grid-cols-2">
                      {signatureClasses.map((entry) => (
                        <div key={entry.id} className="rounded-sm border border-arena-material/30 bg-arena-material/10 p-3">
                          <p className="type-display text-[15px]">{entry.name} · {entry.signatureName}</p>
                          <p className="t-micro mt-1">{entry.fantasy}</p>
                        </div>
                      ))}
                    </div>
                  </section>
                )}

                <details className="mt-3 rounded-sm border border-arena-edge bg-arena-deep/20">
                  <summary className="flex min-h-11 cursor-pointer items-center px-3 t-body">Advanced allocation rules</summary>
                  <div className="border-t border-arena-edge p-3">
                    <div className="grid gap-2 sm:grid-cols-3">
                      <NumberField label="minimum bodies" value={activeAssignment.minimum ?? 0} min={0} max={target}
                        issues={issues} path={`roles.${roleIndex}.minimum`}
                        onChange={(value) => updateAssignment(activeAssignment.roleId, (next) => { next.minimum = value; })} />
                      <div>
                        <span className="t-micro mb-1 block">target bodies</span>
                        <div className="field flex min-h-11 items-center tabular">{target}</div>
                      </div>
                      <NumberField label="maximum bodies" value={activeAssignment.maximum ?? target} min={target} max={8}
                        issues={issues} path={`roles.${roleIndex}.maximum`}
                        onChange={(value) => updateAssignment(activeAssignment.roleId, (next) => { next.maximum = value; })} />
                    </div>
                    <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                      <SelectField label="Core carrier" value={activeAssignment.carrierPreference ?? 'allow'}
                        values={['forbid', 'allow', 'prefer', 'require']} issues={issues}
                        path={`roles.${roleIndex}.carrierPreference`}
                        onChange={(value) => updateAssignment(activeAssignment.roleId, (next) => { next.carrierPreference = value; })} />
                      <SelectField label="On casualty" value={activeAssignment.deathPolicy ?? 'hold-vacancy'}
                        values={['hold-vacancy', 'promote-best', 'rebalance']} issues={issues}
                        path={`roles.${roleIndex}.deathPolicy`}
                        onChange={(value) => updateAssignment(activeAssignment.roleId, (next) => { next.deathPolicy = value; })} />
                      <SelectField label="On respawn" value={activeAssignment.respawnPolicy ?? 'rejoin'}
                        values={['resume', 'rejoin', 'rally', 'replace']} issues={issues}
                        path={`roles.${roleIndex}.respawnPolicy`}
                        onChange={(value) => updateAssignment(activeAssignment.roleId, (next) => { next.respawnPolicy = value; })} />
                      <SelectField label="Overflow assignment" value={activeAssignment.overflowRoleId ?? ''}
                        values={assignments.map((entry) => entry.roleId)} issues={issues}
                        path={`roles.${roleIndex}.overflowRoleId`}
                        onChange={(value) => updateAssignment(activeAssignment.roleId, (next) => { next.overflowRoleId = value; })} />
                    </div>
                    <fieldset className="mt-3 rounded-sm border border-arena-edge p-3">
                      <legend className="lab px-1">Upgrade path</legend>
                      <p className="t-micro">Ordered picks earned by this assignment. Repeated upgrades are intentional.</p>
                      <div className="mt-2 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                        {(activeAssignment.build ?? []).map((upgrade, slot) => (
                          <div key={slot} className="rounded-sm border border-arena-edge bg-arena-panel/35 p-2">
                            <p className="t-micro">Level {slot + 1}</p>
                            <div className="mt-1 grid grid-cols-2 gap-1">
                              {['damage', 'vision', 'reach', 'vitality'].map((candidate) => (
                                <button key={candidate} type="button" aria-pressed={upgrade === candidate}
                                  className={clsx(
                                    'min-h-11 rounded-sm border px-1 t-micro',
                                    upgrade === candidate
                                      ? 'border-arena-material bg-arena-material/15 text-arena-text'
                                      : 'border-arena-edge text-arena-dim',
                                  )}
                                  onClick={() => updateAssignment(activeAssignment.roleId, (next) => {
                                    if (!next.build) next.build = [];
                                    next.build[slot] = candidate;
                                  })}>{upgradeGlyph(candidate)} {humanizeToken(candidate)}</button>
                              ))}
                            </div>
                            <button type="button" className="btn mt-1 min-h-11 w-full text-arena-hot"
                              onClick={() => updateAssignment(activeAssignment.roleId, (next) => {
                                next.build?.splice(slot, 1);
                                if (!next.build?.length) delete next.build;
                              })}>Remove level</button>
                          </div>
                        ))}
                      </div>
                      {(activeAssignment.build?.length ?? 0) < 8 && (
                        <button type="button" className="btn mt-2 min-h-11"
                          onClick={() => updateAssignment(activeAssignment.roleId, (next) => {
                            next.build = [...(next.build ?? []), 'vitality'];
                          })}>＋ Add upgrade level</button>
                      )}
                    </fieldset>
                  </div>
                </details>
                <InlineIssues issues={issues} prefix={`roles.${roleIndex}`} />
              </article>
            );
          })() : (
            <div className="pad"><p className="t-meta">This sheet has no assignments.</p></div>
          )}
        </div>
      </section>
    </div>
  );
}

function AssignmentClassStack({ classIds, size }: { classIds: string[]; size: number }) {
  const visible = [...new Set(classIds)].slice(0, 3);
  return (
    <span className="flex shrink-0 items-center" aria-label={`${classIds.length} eligible chassis`}>
      {visible.map((classId, index) => (
        <span key={classId} className={index === 0 ? '' : '-ml-2'} style={{ zIndex: visible.length - index }}>
          <ClassIcon classId={classId} label={classId} size={size} />
        </span>
      ))}
      {classIds.length > visible.length && <span className="pill -ml-1">+{classIds.length - visible.length}</span>}
    </span>
  );
}

function AssignmentMeter({ count, total }: { count: number; total: number }) {
  return (
    <span className="flex min-w-0 flex-1 gap-1" aria-label={`${count} target bodies out of ${total}`}>
      {Array.from({ length: total }, (_, index) => (
        <span key={index} className={clsx(
          'h-1.5 min-w-1 flex-1 rounded-full',
          index < count ? 'bg-arena-material/70' : 'bg-arena-edge',
        )} />
      ))}
    </span>
  );
}

function AssignmentNameEditor({ roleId, roleIds, onRename }: {
  roleId: string;
  roleIds: string[];
  onRename: (nextId: string) => void;
}) {
  const [value, setValue] = useState(humanizeToken(roleId));
  const nextId = assignmentIdFromName(value);
  const duplicate = nextId !== roleId && roleIds.includes(nextId);
  const invalid = nextId.length === 0 || duplicate;
  const commit = () => {
    if (invalid) return;
    if (nextId !== roleId) onRename(nextId);
  };
  return (
    <label className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3">
      <span className="lab block">Assignment name</span>
      <input className="field mt-2 min-h-11 text-[16px]" value={value}
        aria-invalid={invalid}
        onChange={(event) => setValue(event.target.value)}
        onBlur={commit}
        onKeyDown={(event) => {
          if (event.key === 'Enter') event.currentTarget.blur();
        }} />
      <span className={clsx('t-micro mt-1 block', invalid && 'text-arena-hot')}>
        {duplicate
          ? 'Another assignment already uses that name.'
          : nextId.length === 0
            ? 'Give this assignment a name.'
            : 'Every command, trigger and custody reference follows a rename automatically.'}
      </span>
    </label>
  );
}

function DoctrineToolButton({ title, detail, issueCount, onClick }: {
  title: string;
  detail: string;
  issueCount: number;
  onClick: () => void;
}) {
  return (
    <button type="button"
      className="min-h-16 rounded-sm border border-arena-edge bg-arena-deep/25 px-3 py-2 text-left active:border-arena-material"
      onClick={onClick}>
      <span className="flex items-center gap-2">
        <span className="type-display text-[17px]">{title}</span>
        {issueCount > 0 && <span className="pill ml-auto text-arena-hot">{issueCount} issues</span>}
      </span>
      <span className="t-micro mt-1 block">{detail}</span>
    </button>
  );
}

function SheetSubEditorModal({ title, description, onClose, children }: {
  title: string;
  description: string;
  onClose: () => void;
  children: ReactNode;
}) {
  const titleId = useId();
  const dialog = useRef<HTMLDivElement>(null);
  const closeButton = useRef<HTMLButtonElement>(null);
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;
  useEffect(() => {
    const previouslyFocused = document.activeElement instanceof HTMLElement
      ? document.activeElement : null;
    const priorOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    closeButton.current?.focus();
    const closeOnEscape = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        onCloseRef.current();
        return;
      }
      if (event.key !== 'Tab') return;
      const focusable = [...(dialog.current?.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), select:not([disabled]), summary, [tabindex]:not([tabindex="-1"])',
      ) ?? [])].filter((entry) => entry.getClientRects().length > 0);
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable.at(-1)!;
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    window.addEventListener('keydown', closeOnEscape);
    return () => {
      document.body.style.overflow = priorOverflow;
      window.removeEventListener('keydown', closeOnEscape);
      previouslyFocused?.focus();
    };
  }, []);

  return (
    <div ref={dialog} className="fixed inset-0 z-[70] flex items-end bg-black/70 sm:items-center sm:justify-center sm:p-5"
      role="dialog" aria-modal="true" aria-labelledby={titleId}
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}>
      <section className="flex max-h-[94dvh] min-w-0 w-full flex-col overflow-hidden rounded-t-md border border-arena-edge bg-arena-panel shadow-2xl sm:max-w-5xl sm:rounded-md">
        <header className="flex items-start gap-3 border-b border-arena-edge px-4 py-3">
          <div className="mr-auto">
            <h2 id={titleId} className="type-display text-[21px]">{title}</h2>
            <p className="t-micro mt-1">{description}</p>
          </div>
          <button ref={closeButton} type="button" className="btn min-h-11" onClick={onClose}>
            Done
          </button>
        </header>
        <div className="min-h-0 min-w-0 overflow-y-auto p-3 sm:p-4">{children}</div>
      </section>
    </div>
  );
}

function DoctrineEditor({ draft, catalog, issues, mutate, openConditions, openCustody, openCoordination, openMemory }: {
  draft: SheetDraft;
  catalog: TacticalSheetCatalog;
  issues: SheetIssue[];
  mutate: Mutate;
  openConditions: () => void;
  openCustody: () => void;
  openCoordination: () => void;
  openMemory: () => void;
}) {
  const doctrines = Object.entries(draft.playbook.doctrines.roles ?? {});
  const roleById = new Map(draft.playbook.roles.map((role) => [role.roleId, role]));
  const roleIds = [...roleById.keys()];
  const custodyIds = draft.playbook.custodyPolicies.map((policy) => policy.custodyId);
  const routeIds = draft.layout.routes.map((route) => route.routeId);
  const zoneIds = draft.layout.zones.map((zone) => zone.zoneId);
  const anchorIds = draft.layout.anchors.map((anchor) => anchor.anchorId);
  const predicateIds = Object.keys(draft.playbook.conditions ?? {});
  const focusIds = Object.keys(draft.playbook.doctrines.focus ?? {});
  const cohortIds = Object.keys(draft.playbook.doctrines.cohorts ?? {});
  const [activeRoleId, setActiveRoleId] = useState(doctrines[0]?.[0] ?? '');
  const activeEntry = doctrines.find(([id]) => id === activeRoleId) ?? doctrines[0];

  useEffect(() => {
    if (activeEntry && activeEntry[0] !== activeRoleId) setActiveRoleId(activeEntry[0]);
  }, [activeEntry, activeRoleId]);

  const updateDoctrine = (roleId: string, change: (doctrine: Doctrine) => void) =>
    mutate((next) => change(next.playbook.doctrines.roles[roleId]));

  return (
    <section className="panel overflow-hidden">
      <header className="border-b border-arena-edge bg-[radial-gradient(circle_at_12%_0%,rgba(210,110,45,.18),transparent_38%),linear-gradient(115deg,rgba(210,110,45,.08),transparent_60%)] p-4">
        <p className="lab text-arena-material">Battle plan</p>
        <div className="mt-1 flex flex-wrap items-end gap-3">
          <div className="mr-auto max-w-3xl">
            <h2 className="type-display text-[25px]">Give every assignment its own playbook</h2>
            <p className="t-meta mt-1">
              Read top to bottom. The first live play takes command; the green patrol is the safe fallback.
            </p>
          </div>
          <span className="pill">Six tactical verbs · one ordered ladder</span>
        </div>
      </header>

      <div className="grid min-w-0 grid-cols-1 lg:grid-cols-[240px_minmax(0,1fr)]">
        <aside className="border-b border-arena-edge bg-arena-deep/30 p-2 lg:border-b-0 lg:border-r">
          <p className="lab px-2 py-1">Assignments</p>
          <div className="mt-1 flex gap-2 overflow-x-auto pb-1 lg:flex-col lg:overflow-visible">
            {doctrines.map(([roleId, doctrine]) => {
              const role = roleById.get(roleId);
              const classIds = role?.candidateClasses ?? [];
              const classInfos = classIds.flatMap((id) => {
                const found = catalog.classes.find((entry) => entry.id === id);
                return found ? [found] : [];
              });
              const doctrineIssues = issues.filter((issue) =>
                issue.path.startsWith('doctrines.roles.' + roleId)).length;
              const selected = activeEntry?.[0] === roleId;
              return (
                <button key={roleId} type="button" data-doctrine-select={roleId}
                  aria-selected={selected}
                  className={clsx(
                    'min-w-[215px] rounded-sm border px-2.5 py-2 text-left transition lg:min-w-0',
                    selected
                      ? 'border-arena-material bg-arena-material/10 shadow-[inset_3px_0_0_var(--color-arena-material)]'
                      : 'border-transparent bg-arena-panel/40 active:border-arena-edge',
                  )}
                  onClick={() => setActiveRoleId(roleId)}>
                  <span className="flex items-center gap-2">
                    <AssignmentClassStack classIds={classIds} size={30} />
                    <span className="min-w-0 flex-1">
                      <span className="type-display block truncate text-[16px]">{humanizeToken(roleId)}</span>
                      <span className="t-micro block truncate">{classInfos.map((entry) => entry.name).join(' · ')}</span>
                    </span>
                    {doctrineIssues > 0 && <span className="pill text-arena-hot">{doctrineIssues}</span>}
                  </span>
                  <span className="mt-2 flex items-center gap-1">
                    {doctrine.modes.map((_, index) => (
                      <span key={index} className={clsx(
                        'h-1.5 flex-1 rounded-full',
                        index === doctrine.modes.length - 1 ? 'bg-arena-ok/70' : 'bg-arena-material/60',
                      )} />
                    ))}
                    <span className="t-micro ml-1">{doctrine.modes.length}</span>
                  </span>
                  {classInfos.length > 0 && (
                    <span className="t-micro mt-1 block truncate text-arena-material">
                      ✦ {classInfos.map((entry) => entry.signatureName).join(' · ')}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
          <div className="mt-2 grid grid-cols-2 gap-2 lg:grid-cols-1">
            <DoctrineToolButton title="Triggers" detail={predicateIds.length + ' named moments'}
              issueCount={issues.filter((issue) => issue.path.startsWith('conditions')).length}
              onClick={openConditions} />
            <DoctrineToolButton title="Core play" detail={draft.playbook.custodyPolicies.length + ' custody plans'}
              issueCount={issues.filter((issue) => issue.path.startsWith('custodyPolicies')).length}
              onClick={openCustody} />
            <DoctrineToolButton title="Coordination" detail={focusIds.length + ' focus · ' + cohortIds.length + ' cohorts'}
              issueCount={issues.filter((issue) =>
                issue.path.startsWith('doctrines.focus') || issue.path.startsWith('doctrines.cohorts')).length}
              onClick={openCoordination} />
            <DoctrineToolButton title="Intel memory" detail="Four team memory windows"
              issueCount={issues.filter((issue) => issue.path.startsWith('memory')).length}
              onClick={openMemory} />
          </div>
        </aside>

        {activeEntry ? (() => {
          const [roleId, doctrine] = activeEntry;
          const role = roleById.get(roleId);
          const classIds = role?.candidateClasses ?? [];
          const classInfos = classIds.flatMap((id) => {
            const found = catalog.classes.find((entry) => entry.id === id);
            return found ? [found] : [];
          });
          return (
            <div className="min-w-0 p-3 sm:p-4" data-sheet-path={'doctrines.roles.' + roleId}>
              <div className="flex items-center gap-3 border-b border-arena-edge pb-3">
                <AssignmentClassStack classIds={classIds} size={44} />
                <div className="min-w-0 flex-1">
                  <p className="lab">Assignment battle plan</p>
                  <h3 className="type-display truncate text-[24px]">{humanizeToken(roleId)}</h3>
                  <p className="t-micro">
                    {role?.preferred ?? 0} target bodies · {classInfos.map((entry) => entry.name).join(' · ')}
                  </p>
                </div>
                <span className="pill">{doctrine.modes.length} plays</span>
              </div>

              {classInfos.length > 0 && (
                <div className="mt-3 grid gap-2 sm:grid-cols-2">
                  {classInfos.map((entry) => (
                    <div key={entry.id}
                      className="rounded-sm border border-arena-material/35 bg-[linear-gradient(120deg,rgba(210,110,45,.14),transparent)] p-3">
                      <p className="lab text-arena-material">{entry.name} · {entry.signatureName}</p>
                      <p className="t-meta mt-1">{entry.fantasy}</p>
                    </div>
                  ))}
                </div>
              )}

              <div className="mt-3 flex flex-col gap-2">
                {doctrine.modes.map((mode, index) => (
                  <ModeEditor key={index}
                    roleId={roleId} doctrine={doctrine} mode={mode} index={index}
                    routeIds={routeIds} zoneIds={zoneIds} anchorIds={anchorIds}
                    roleIds={roleIds} custodyIds={custodyIds} predicateIds={predicateIds}
                    focus={draft.playbook.doctrines.focus} cohorts={draft.playbook.doctrines.cohorts}
                    issues={issues}
                    onChange={(nextMode) => updateDoctrine(roleId, (next) => { next.modes[index] = nextMode; })}
                    onMove={(delta) => updateDoctrine(roleId, (next) => {
                      const [moving] = next.modes.splice(index, 1);
                      next.modes.splice(index + delta, 0, moving);
                    })}
                    onRemove={() => updateDoctrine(roleId, (next) => { next.modes.splice(index, 1); })} />
                ))}
                <button type="button" className="btn min-h-11 self-start border-arena-material/45"
                  disabled={doctrine.modes.length >= 8}
                  onClick={() => updateDoctrine(roleId, (next) => {
                    const floor = next.modes.pop();
                    const mode = freshMode(
                      'intercept', routeIds, anchorIds, zoneIds, custodyIds,
                      roleIds.filter((id) => id !== roleId),
                    );
                    mode.while = predicateIds[0] ?? '';
                    next.modes.push(mode);
                    if (floor) next.modes.push(floor);
                  })}>＋ Add a higher-priority play</button>
              </div>

              <details className="mt-3 rounded-sm border border-arena-edge bg-arena-deep/20">
                <summary className="flex min-h-11 cursor-pointer items-center px-3 t-body">
                  Assignment instincts <span className="t-micro ml-auto">custody · scavenging · combat</span>
                </summary>
                <div className="border-t border-arena-edge p-3">
                  <div className="grid gap-2 sm:grid-cols-2">
                    <SelectField label="Default Core plan" value={doctrine.custody} values={custodyIds}
                      issues={issues} path={'doctrines.roles.' + roleId + '.custody'}
                      onChange={(value) => updateDoctrine(roleId, (next) => { next.custody = value; })} />
                    <ChoiceCard label="Concealment" active={doctrine.conceal ?? true}
                      activeLabel="Seek ambush cover" inactiveLabel="Fight in the open"
                      onChange={(value) => updateDoctrine(roleId, (next) => { next.conceal = value; })} />
                  </div>
                  <ChoiceGrid label="Pick up loose Cores while crossing these zones" values={zoneIds}
                    selected={typeof doctrine.scavenge === 'string' ? [doctrine.scavenge] : doctrine.scavenge ?? []}
                    issues={issues} path={'doctrines.roles.' + roleId + '.scavenge'}
                    onChange={(value) => updateDoctrine(roleId, (next) => {
                      next.scavenge = value.length ? value : undefined;
                    })} />
                  <FightEditor fight={doctrine.fight} label="Default combat instincts"
                    onChange={(fight) => updateDoctrine(roleId, (next) => { next.fight = fight; })} />
                  <RepairEditor repair={doctrine.repair}
                    onChange={(repair) => updateDoctrine(roleId, (next) => { next.repair = repair; })} />
                </div>
              </details>
              <InlineIssues issues={issues} prefix={'doctrines.roles.' + roleId} />
            </div>
          );
        })() : <div className="pad"><p className="t-meta">Every assignment needs a battle plan.</p></div>}
      </div>
    </section>
  );
}

function ModeEditor({
  roleId, doctrine, mode, index, routeIds, zoneIds, anchorIds, roleIds,
  custodyIds, predicateIds, focus, cohorts, issues, onChange, onMove, onRemove,
}: {
  roleId: string;
  doctrine: Doctrine;
  mode: TacticalMode;
  index: number;
  routeIds: string[];
  zoneIds: string[];
  anchorIds: string[];
  roleIds: string[];
  custodyIds: string[];
  predicateIds: string[];
  focus: Record<string, FocusDefinition>;
  cohorts: Record<string, CohortDefinition>;
  issues: SheetIssue[];
  onChange: (mode: TacticalMode) => void;
  onMove: (delta: number) => void;
  onRemove: () => void;
}) {
  const verb = verbOf(mode) ?? 'patrol';
  const floor = index === doctrine.modes.length - 1;
  const issuePrefix = 'doctrines.roles.' + roleId + '.modes.' + index;
  const set = (patch: Partial<TacticalMode>) => onChange({ ...mode, ...patch });
  const changeVerb = (nextVerb: TacticalVerb) => {
    const next = freshMode(
      nextVerb, routeIds, anchorIds, zoneIds, custodyIds,
      roleIds.filter((id) => id !== roleId),
    );
    if (!floor && nextVerb !== 'recover' && nextVerb !== 'fetch')
      next.while = mode.while ?? predicateIds[0] ?? '';
    if (!floor && mode.until && nextVerb !== 'recover' && nextVerb !== 'fetch')
      next.until = mode.until;
    if (mode.fight && nextVerb !== 'fetch') next.fight = mode.fight;
    onChange(next);
  };
  const selectableVerbs = floor ? TACTICAL_VERBS.filter((entry) => entry === 'patrol') : TACTICAL_VERBS;
  const destination = destinationOf(mode);

  return (
    <article data-sheet-path={issuePrefix} className={clsx(
      'overflow-hidden rounded-sm border bg-arena-deep/25',
      floor ? 'border-arena-ok/50' : 'border-arena-edge',
    )}>
      <header className="flex items-start gap-3 p-3">
        <span className={clsx(
          'flex size-11 shrink-0 items-center justify-center rounded-sm border font-mono text-xl',
          floor
            ? 'border-arena-ok/50 bg-arena-ok/10 text-arena-ok'
            : 'border-arena-material/45 bg-arena-material/10 text-arena-material',
        )} aria-hidden="true">{commandGlyph(verb)}</span>
        <div className="min-w-0 flex-1">
          <p className="lab">{floor ? 'Fallback patrol' : 'Priority ' + (index + 1)}</p>
          <h4 className="type-display mt-0.5 text-[20px]">{humanizeToken(verb)}</h4>
          <p className="t-meta mt-0.5">{modeCommandSummary(mode)}</p>
          {!floor && verb !== 'recover' && verb !== 'fetch' && (
            <div className="mt-2 flex flex-wrap gap-1.5">
              {mode.while && <span className="pill border-arena-material/35 text-arena-material">
                WHEN {humanizeToken(mode.while)}
              </span>}
              {mode.until && <span className="pill">UNTIL {humanizeToken(mode.until)}</span>}
            </div>
          )}
        </div>
        <div className="flex shrink-0 gap-1">
          <button type="button" aria-label="Move play up" className="btn min-h-11 min-w-11"
            disabled={index === 0} onClick={() => onMove(-1)}>↑</button>
          <button type="button" aria-label="Move play down" className="btn min-h-11 min-w-11"
            disabled={floor} onClick={() => onMove(1)}>↓</button>
        </div>
      </header>

      <details className="border-t border-arena-edge">
        <summary className="flex min-h-11 cursor-pointer items-center px-3 t-body">
          Tune this play <span className="t-micro ml-auto">action · moment · combat</span>
        </summary>
        <div className="border-t border-arena-edge p-3">
          <fieldset>
            <legend className="lab mb-2">Tactical action</legend>
            <div className="grid grid-cols-3 gap-1.5 sm:grid-cols-6">
              {selectableVerbs.map((candidate) => (
                <button key={candidate} type="button" aria-pressed={candidate === verb}
                  className={clsx(
                    'min-h-14 rounded-sm border px-2 py-1.5 text-center transition',
                    candidate === verb
                      ? 'border-arena-material bg-arena-material/15 text-arena-text shadow-[inset_0_-2px_0_var(--color-arena-material)]'
                      : 'border-arena-edge bg-arena-panel/40 text-arena-dim',
                  )}
                  onClick={() => changeVerb(candidate)}>
                  <span className="font-mono text-lg text-arena-material" aria-hidden="true">{commandGlyph(candidate)}</span>
                  <span className="t-micro mt-0.5 block">{humanizeToken(candidate)}</span>
                </button>
              ))}
            </div>
          </fieldset>

          {(verb === 'patrol' || verb === 'assault') && (
            <DestinationPicker verb={verb} destination={destination} routeIds={routeIds} zoneIds={zoneIds}
              path={issuePrefix + '.' + verb} issues={issues}
              onChange={(kind, id) => set({ [verb]: kind === 'route' ? id : { zone: id } })} />
          )}
          {verb === 'intercept' && (
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <SegmentedField label="Intercept" value={mode.intercept ?? 'enemy-carriers'}
                values={['enemy-carriers', 'inbound']}
                onChange={(value) => set({ intercept: value })} />
              <TokenField label="Stage from" value={mode.from ?? ''} values={anchorIds}
                path={issuePrefix + '.from'} issues={issues}
                onChange={(value) => set({ from: value })} />
            </div>
          )}
          {verb === 'recover' && (
            <div className="mt-3 rounded-sm border border-arena-ok/35 bg-arena-ok/5 p-3">
              <p className="lab text-arena-ok">Automatic recovery window</p>
              <p className="t-meta mt-1">A hurt body commits to a safe beacon for a bounded attempt, then cools down until its health changes again.</p>
            </div>
          )}
          {verb === 'fetch' && (
            <TokenField label="Core custody plan" value={mode.fetch ?? ''} values={custodyIds}
              path={issuePrefix + '.fetch'} issues={issues}
              onChange={(value) => set({ fetch: value })} />
          )}
          {verb === 'follow' && (
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <TokenField label="Follow assignment" value={mode.follow ?? ''}
                values={roleIds.filter((id) => id !== roleId)}
                path={issuePrefix + '.follow'} issues={issues}
                onChange={(value) => set({ follow: value })} />
              <SegmentedField label="Posture" value={mode.posture ?? 'trail'}
                values={['trail', 'screen']} onChange={(value) => set({ posture: value })} />
            </div>
          )}

          {(verb === 'patrol' || verb === 'assault') && (
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <SegmentedField label="Position in the push" value={mode.posture ?? ''}
                values={['', 'front', 'core', 'rear']} emptyLabel="Natural"
                onChange={(value) => set({ posture: value || undefined })} />
              {!floor && <CohortField value={mode.cohort} cohorts={cohorts}
                path={issuePrefix + '.cohort'} issues={issues}
                onChange={(value) => set({ cohort: value })} />}
            </div>
          )}

          {!floor && verb !== 'recover' && verb !== 'fetch' && (
            <div className="mt-3 grid gap-2 lg:grid-cols-2">
              <ConditionField label="Start when" value={mode.while ?? ''} predicates={predicateIds}
                issues={issues} path={issuePrefix + '.while'}
                onChange={(value) => set({ while: value })} />
              <ConditionField label="Release when" value={mode.until ?? ''} predicates={predicateIds}
                issues={issues} path={issuePrefix + '.until'}
                onChange={(value) => set({ until: value || undefined })} />
            </div>
          )}

          {verb !== 'fetch' && <FocusField value={mode.focus} focus={focus}
            path={issuePrefix + '.focus'} issues={issues}
            onChange={(value) => set({ focus: value })} />}
          {verb !== 'fetch' && <FightEditor fight={mode.fight} label="Combat override for this play"
            onChange={(fightBlock) => set({ fight: fightBlock })} />}
          <InlineIssues issues={issues} prefix={issuePrefix} />
          {!floor && (
            <div className="mt-3 flex justify-end">
              <button type="button" className="btn min-h-11 text-arena-hot" onClick={onRemove}>Remove play</button>
            </div>
          )}
        </div>
      </details>
    </article>
  );
}

function DestinationPicker({ verb, destination, routeIds, zoneIds, path, issues, onChange }: {
  verb: 'patrol' | 'assault';
  destination: { kind: 'route' | 'zone'; id: string } | null;
  routeIds: string[];
  zoneIds: string[];
  path: string;
  issues: SheetIssue[];
  onChange: (kind: 'route' | 'zone', id: string) => void;
}) {
  const kind = destination?.kind ?? 'route';
  const values = kind === 'route' ? routeIds : zoneIds;
  return (
    <fieldset className="mt-3 rounded-sm border border-arena-edge bg-arena-panel/30 p-3" data-sheet-path={path}>
      <legend className="lab px-1">{humanizeToken(verb)} destination</legend>
      <div className="flex gap-1">
        {(['route', 'zone'] as const).map((candidate) => (
          <button key={candidate} type="button" className={clsx(
            'min-h-11 flex-1 rounded-sm border px-3 t-micro',
            candidate === kind
              ? 'border-arena-material bg-arena-material/15 text-arena-material'
              : 'border-arena-edge text-arena-dim',
          )} aria-pressed={candidate === kind}
          onClick={() => onChange(candidate, (candidate === 'route' ? routeIds : zoneIds)[0] ?? '')}>
            {candidate === 'route' ? 'Follow a route' : 'Control a zone'}
          </button>
        ))}
      </div>
      <div className="mt-2 flex flex-wrap gap-1.5">
        {values.map((id) => (
          <button key={id} type="button" aria-pressed={destination?.id === id}
            className={clsx(
              'min-h-11 rounded-full border px-3 t-micro',
              destination?.id === id
                ? 'border-arena-material bg-arena-material/15 text-arena-text'
                : 'border-arena-edge bg-arena-deep/20 text-arena-dim',
            )}
            onClick={() => onChange(kind, id)}>{humanizeToken(id)}</button>
        ))}
      </div>
      <FieldIssues issues={issues} path={path} />
    </fieldset>
  );
}

function FocusField({ value, focus, path, issues, onChange }: {
  value: TacticalMode['focus'];
  focus: Record<string, FocusDefinition>;
  path: string;
  issues: SheetIssue[];
  onChange: (value: string | FocusDefinition) => void;
}) {
  const shared = typeof value === 'string' ? value : null;
  const inline = typeof value === 'object' ? value : null;
  return (
    <fieldset className="mt-3 rounded-sm border border-arena-edge p-3" data-sheet-path={path}>
      <legend className="lab px-1">Focus-fire ceiling</legend>
      <p className="t-micro">How many consumers of this scope may attack the same enemy.</p>
      <div className="mt-2 flex flex-wrap gap-1.5">
        {Object.entries(focus).map(([id, definition]) => (
          <button key={id} type="button" aria-pressed={shared === id}
            className={clsx('min-h-11 rounded-full border px-3 t-micro',
              shared === id ? 'border-arena-material bg-arena-material/15' : 'border-arena-edge')}
            onClick={() => onChange(id)}>
            {humanizeToken(id)} · {definition.maximumAttackers}
          </button>
        ))}
        <button type="button" aria-pressed={Boolean(inline)}
          className={clsx('min-h-11 rounded-full border px-3 t-micro',
            inline ? 'border-arena-material bg-arena-material/15' : 'border-arena-edge')}
          onClick={() => onChange({ maximumAttackers: inline?.maximumAttackers ?? 1 })}>
          This play only
        </button>
      </div>
      {inline && <div className="mt-2 max-w-[220px]">
        <NumberField label="maximum attackers" value={inline.maximumAttackers} min={1} max={8}
          onChange={(maximumAttackers) => onChange({ maximumAttackers })} />
      </div>}
      <FieldIssues issues={issues} path={path} />
    </fieldset>
  );
}

function CohortField({ value, cohorts, path, issues, onChange }: {
  value: TacticalMode['cohort'];
  cohorts: Record<string, CohortDefinition>;
  path: string;
  issues: SheetIssue[];
  onChange: (value: string | CohortDefinition | undefined) => void;
}) {
  const shared = typeof value === 'string' ? value : null;
  const inline = typeof value === 'object' ? value : null;
  return (
    <fieldset className="rounded-sm border border-arena-edge p-3" data-sheet-path={path}>
      <legend className="lab px-1">Who joins this play?</legend>
      <div className="flex flex-wrap gap-1.5">
        <button type="button" aria-pressed={value === undefined} className={clsx(
          'min-h-11 rounded-full border px-3 t-micro',
          value === undefined ? 'border-arena-material bg-arena-material/15' : 'border-arena-edge',
        )} onClick={() => onChange(undefined)}>Everyone eligible</button>
        {Object.entries(cohorts).map(([id, definition]) => (
          <button key={id} type="button" aria-pressed={shared === id} className={clsx(
            'min-h-11 rounded-full border px-3 t-micro',
            shared === id ? 'border-arena-material bg-arena-material/15' : 'border-arena-edge',
          )} onClick={() => onChange(id)}>
            {humanizeToken(id)} · nearest {definition.take}
          </button>
        ))}
        <button type="button" aria-pressed={Boolean(inline)} className={clsx(
          'min-h-11 rounded-full border px-3 t-micro',
          inline ? 'border-arena-material bg-arena-material/15' : 'border-arena-edge',
        )} onClick={() => onChange({ take: inline?.take ?? 1, select: 'nearest' })}>Nearest…</button>
      </div>
      {inline && <div className="mt-2 max-w-[220px]">
        <NumberField label="bodies admitted" value={inline.take} min={1} max={8}
          onChange={(take) => onChange({ take, select: 'nearest' })} />
      </div>}
      <FieldIssues issues={issues} path={path} />
    </fieldset>
  );
}

function PredicateEditor({ draft, issues, mutate }: {
  draft: SheetDraft;
  issues: SheetIssue[];
  mutate: Mutate;
}) {
  const predicates = draft.playbook.conditions ?? {};
  const entries = Object.entries(predicates);
  const roles = draft.playbook.roles.map((role) => role.roleId);
  const zones = draft.layout.zones.map((zone) => zone.zoneId);
  const [activeId, setActiveId] = useState(entries[0]?.[0] ?? '');
  const activeEntry = entries.find(([id]) => id === activeId) ?? entries[0];

  useEffect(() => {
    if (activeEntry && activeEntry[0] !== activeId) setActiveId(activeEntry[0]);
  }, [activeEntry, activeId]);

  const rename = (previous: string, nextId: string) => mutate((next) => {
    if (!nextId || previous === nextId) return;
    next.playbook.conditions = Object.fromEntries(
      Object.entries(next.playbook.conditions).map(([key, value]) =>
        [key === previous ? nextId : key, value]),
    );
    rewriteConditionReferences(next.playbook, previous, nextId);
    setActiveId(nextId);
  });
  const add = () => {
    const id = uniqueId('trigger', Object.keys(predicates));
    mutate((next) => {
      next.playbook.conditions[id] = { fact: 'tick', operator: 'at-least', value: 1 };
    });
    setActiveId(id);
  };

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-3 lg:grid-cols-[240px_minmax(0,1fr)]">
      <aside>
        <button type="button" className="btn btn-primary min-h-11 w-full" onClick={add}>＋ New trigger</button>
        <div className="mt-2 flex gap-2 overflow-x-auto lg:flex-col">
          {entries.map(([id, predicate]) => (
            <button key={id} type="button" data-predicate-select={id}
              aria-selected={activeEntry?.[0] === id}
              className={clsx(
                'min-w-[210px] rounded-sm border p-3 text-left lg:min-w-0',
                activeEntry?.[0] === id
                  ? 'border-arena-material bg-arena-material/10'
                  : 'border-arena-edge bg-arena-deep/25',
              )}
              onClick={() => setActiveId(id)}>
              <span className="type-display block truncate text-[16px]">{humanizeToken(id)}</span>
              <span className="t-micro mt-1 block line-clamp-2">{predicateSentence(predicate)}</span>
            </button>
          ))}
        </div>
      </aside>
      {activeEntry ? (() => {
        const [id, predicate] = activeEntry;
        const kind = predicateSubjectKind(predicate.fact);
        const path = 'conditions.' + id;
        const update = (change: (target: Predicate) => void) => mutate((next) =>
          change(next.playbook.conditions[id]));
        return (
          <article className="rounded-sm border border-arena-edge bg-arena-deep/20 p-3 sm:p-4"
            data-sheet-path={path}>
            <p className="lab text-arena-material">Named moment</p>
            <p className="type-display mt-1 text-[22px]">{predicateSentence(predicate)}</p>
            <p className="t-micro mt-1">Use this trigger on any play. The sentence updates as you tune the fact.</p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <TextField label="Trigger name" value={id} issues={issues} path={path}
                onChange={(value) => rename(id, assignmentIdFromName(value))} />
              <SelectField label="Observed fact" value={predicate.fact}
                values={[...CONDITION_FACTS]} issues={issues} path={path + '.fact'}
                onChange={(fact) => update((target) => {
                  target.fact = fact;
                  delete target.subject;
                  delete target.zone;
                  delete target.freshnessTicks;
                })} />
              {(kind === 'role' || kind === 'optional-role' || kind === 'role-zone') && (
                <SelectField label={kind === 'optional-role' ? 'Assignment (optional)' : 'Assignment'}
                  value={predicate.subject ?? ''} values={kind === 'optional-role' ? ['', ...roles] : roles}
                  issues={issues} path={path + '.subject'}
                  onChange={(subject) => update((target) => {
                    if (subject) target.subject = subject; else delete target.subject;
                  })} />
              )}
              {(kind === 'zone' || kind === 'role-zone') && (
                <SelectField label="Map zone" value={predicate.zone ?? ''} values={zones}
                  issues={issues} path={path + '.zone'}
                  onChange={(zone) => update((target) => { target.zone = zone; })} />
              )}
              {kind === 'well' && (
                <SegmentedField label="Well" value={predicate.subject ?? 'centre'}
                  values={['own', 'centre', 'enemy', 'north', 'south']}
                  onChange={(subject) => update((target) => { target.subject = subject; })} />
              )}
              <SegmentedField label="Comparison" value={predicate.operator}
                values={[...CONDITION_OPERATORS]}
                onChange={(operator) => update((target) => { target.operator = operator; })} />
              <NumberField label="Threshold" value={predicate.value} min={-2147483648} max={2147483647}
                issues={issues} path={path + '.value'}
                onChange={(value) => update((target) => { target.value = value; })} />
              {(predicate.fact === 'remembered-enemies-in-zone' || predicate.fact === 'secured-cores') && (
                <OptionalNumber label="Fresh for ticks" value={predicate.freshnessTicks} min={1} max={600}
                  issues={issues} path={path + '.freshnessTicks'}
                  onChange={(value) => update((target) => {
                    if (value === undefined) delete target.freshnessTicks; else target.freshnessTicks = value;
                  })} />
              )}
            </div>
            <InlineIssues issues={issues} prefix={path} />
            <button type="button" className="btn mt-4 min-h-11 text-arena-hot"
              onClick={() => {
                const nextActive = entries.find(([candidate]) => candidate !== id)?.[0] ?? '';
                mutate((next) => { delete next.playbook.conditions[id]; });
                setActiveId(nextActive);
              }}>Remove trigger</button>
          </article>
        );
      })() : <p className="t-meta">Add a trigger when a play needs a specific moment.</p>}
    </div>
  );
}

function CustodyFlowStep({ label, value, className }: {
  label: string;
  value: string;
  className?: string;
}) {
  return (
    <span className={clsx('min-w-0 flex-1 rounded-sm border border-arena-edge bg-arena-deep/35 p-2', className)}>
      <span className="lab block">{label}</span>
      <span className="t-micro mt-1 block truncate">{humanizeToken(value)}</span>
    </span>
  );
}

function CustodyEditor({ draft, issues, mutate }: {
  draft: SheetDraft;
  issues: SheetIssue[];
  mutate: Mutate;
}) {
  const policies = draft.playbook.custodyPolicies;
  const policyIds = policies.map((policy) => policy.custodyId);
  const roles = draft.playbook.roles.map((role) => role.roleId);
  const zones = draft.layout.zones.map((zone) => zone.zoneId);
  const routes = draft.layout.routes.map((route) => route.routeId);
  const anchors = draft.layout.anchors.map((anchor) => anchor.anchorId);
  const predicateIds = Object.keys(draft.playbook.conditions);
  const [activeId, setActiveId] = useState(policyIds[0] ?? '');
  const activeIndex = Math.max(0, policies.findIndex((policy) => policy.custodyId === activeId));
  const policy = policies[activeIndex];
  const mutatePolicy = (change: (target: CustodyPolicy) => void) =>
    mutate((next) => change(next.playbook.custodyPolicies[activeIndex]));
  const fetchers = policy ? Object.entries(draft.playbook.doctrines.roles)
    .filter(([, doctrine]) => doctrine.modes.some((mode) => mode.fetch === policy.custodyId))
    .map(([role]) => role) : [];

  useEffect(() => {
    if (policy && policy.custodyId !== activeId) setActiveId(policy.custodyId);
  }, [activeId, policy]);

  const add = () => {
    const custodyId = uniqueId('core-plan', policyIds);
    mutate((next) => next.playbook.custodyPolicies.push({
      custodyId,
      sourceWells: ['north', 'centre', 'south'],
      pickupReservationTicks: 8,
      transferTimeoutTicks: 8,
      deliveryTimeoutTicks: 120,
      accidentalPickup: 'deliver',
      dropRecovery: 'nearest-authorized',
      pickupSelection: 'nearest-authorized',
      unreachableFallback: 'guard',
      safeConversion: 'always',
    }));
    setActiveId(custodyId);
  };

  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-3 lg:grid-cols-[240px_minmax(0,1fr)]">
      <aside>
        <button type="button" className="btn btn-primary min-h-11 w-full" onClick={add}>＋ New Core plan</button>
        <div className="mt-2 flex gap-2 overflow-x-auto lg:flex-col">
          {policies.map((entry, index) => {
            const consumers = Object.values(draft.playbook.doctrines.roles)
              .filter((doctrine) => doctrine.modes.some((mode) => mode.fetch === entry.custodyId)).length;
            return (
              <button key={entry.custodyId} type="button" aria-selected={policy === entry}
                className={clsx(
                  'min-w-[210px] rounded-sm border p-3 text-left lg:min-w-0',
                  policy === entry ? 'border-arena-material bg-arena-material/10' : 'border-arena-edge',
                )} onClick={() => setActiveId(entry.custodyId)}>
                <span className="type-display block truncate text-[16px]">{humanizeToken(entry.custodyId)}</span>
                <span className="t-micro mt-1 block">{consumers} fetch assignment{consumers === 1 ? '' : 's'}</span>
                {issues.some((issue) => issue.path.startsWith('custodyPolicies.' + index)) &&
                  <span className="pill mt-2 text-arena-hot">Needs attention</span>}
              </button>
            );
          })}
        </div>
      </aside>
      {policy ? (
        <article className="min-w-0" data-sheet-path={'custodyPolicies.' + activeIndex}>
          <div className="rounded-sm border border-arena-material/35 bg-[linear-gradient(120deg,rgba(210,110,45,.13),transparent)] p-3">
            <p className="lab text-arena-material">Core journey</p>
            <div className="mt-2 flex items-stretch gap-1">
              <CustodyFlowStep label="Claim" value={policy.pickupSelection ?? 'carrier-preference'} />
              <span className="self-center text-arena-material">→</span>
              <CustodyFlowStep label="Carry" value={policy.accidentalPickup} />
              <span className="self-center text-arena-material">→</span>
              <CustodyFlowStep label="Drop" value={policy.dropRecovery} />
              <span className="self-center text-arena-material">→</span>
              <CustodyFlowStep label="Blocked" value={policy.unreachableFallback} />
            </div>
            <p className="t-micro mt-2">
              Fetchers: {fetchers.length ? fetchers.map(humanizeToken).join(' · ') : 'none yet — add a Fetch play to an assignment'}
            </p>
          </div>

          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <TextField label="Plan name" value={policy.custodyId} issues={issues}
              path={'custodyPolicies.' + activeIndex + '.custodyId'}
              onChange={(value) => {
                const nextId = assignmentIdFromName(value);
                if (!nextId || nextId === policy.custodyId) return;
                const previous = policy.custodyId;
                mutate((next) => {
                  next.playbook.custodyPolicies[activeIndex].custodyId = nextId;
                  for (const doctrine of Object.values(next.playbook.doctrines.roles)) {
                    if (doctrine.custody === previous) doctrine.custody = nextId;
                    doctrine.modes.forEach((mode) => {
                      if (mode.fetch === previous) mode.fetch = nextId;
                    });
                  }
                });
                setActiveId(nextId);
              }} />
            <SegmentedField label="Ordinary pickup priority" value={policy.pickupSelection ?? 'carrier-preference'}
              values={['carrier-preference', 'nearest-authorized']}
              onChange={(value) => mutatePolicy((target) => { target.pickupSelection = value; })} />
          </div>
          <ChoiceGrid label="Cores born at these Wells" values={['north', 'centre', 'south']}
            selected={policy.sourceWells} issues={issues}
            path={'custodyPolicies.' + activeIndex + '.sourceWells'}
            onChange={(value) => mutatePolicy((target) => { target.sourceWells = value; })} />

          <div className="mt-3 grid gap-3 sm:grid-cols-3">
            <SegmentedField label="Accidental pickup" value={policy.accidentalPickup}
              values={['transfer', 'deliver', 'drop-safe']}
              onChange={(value) => mutatePolicy((target) => { target.accidentalPickup = value; })} />
            <SegmentedField label="If dropped" value={policy.dropRecovery}
              values={['same-carrier', 'nearest-authorized', 'guard-until-safe']}
              onChange={(value) => mutatePolicy((target) => { target.dropRecovery = value; })} />
            <SegmentedField label="If delivery is blocked" value={policy.unreachableFallback}
              values={['hold', 'guard', 'alternate-core', 'regroup']}
              onChange={(value) => mutatePolicy((target) => { target.unreachableFallback = value; })} />
          </div>

          <div className="mt-3">
            <ConditionField label="Safe to bank when" value={policy.safeConversion} predicates={predicateIds}
              issues={issues} path={'custodyPolicies.' + activeIndex + '.safeConversion'}
              onChange={(value) => mutatePolicy((target) => { target.safeConversion = value; })} />
          </div>

          <details className="mt-3 rounded-sm border border-arena-edge bg-arena-deep/20">
            <summary className="flex min-h-11 cursor-pointer items-center px-3 t-body">
              Routes, passes and recovery <span className="t-micro ml-auto">advanced Core play</span>
            </summary>
            <div className="border-t border-arena-edge p-3">
              <div className="grid gap-2 sm:grid-cols-3">
                <NumberField label="pickup reservation" value={policy.pickupReservationTicks} min={1} max={120}
                  onChange={(value) => mutatePolicy((target) => { target.pickupReservationTicks = value; })} />
                <NumberField label="transfer timeout" value={policy.transferTimeoutTicks} min={1} max={120}
                  onChange={(value) => mutatePolicy((target) => { target.transferTimeoutTicks = value; })} />
                <NumberField label="delivery timeout" value={policy.deliveryTimeoutTicks} min={1} max={1200}
                  onChange={(value) => mutatePolicy((target) => { target.deliveryTimeoutTicks = value; })} />
              </div>
              <SegmentedField label="Forward pass" value={policy.forwardPass ?? 'none'}
                values={['none', 'relay-catcher']}
                onChange={(value) => mutatePolicy((target) => { target.forwardPass = value; })} />
              <fieldset className="mt-3 rounded-sm border border-arena-edge p-3">
                <legend className="lab px-1">Delivery routes by pickup zone</legend>
                {(policy.deliveryRoutes ?? []).map((entry, index) => (
                  <div key={index} className="mb-2 grid gap-2 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
                    <SelectField label="Pickup zone" value={entry.zone} values={zones}
                      onChange={(zone) => mutatePolicy((target) => { target.deliveryRoutes![index].zone = zone; })} />
                    <SelectField label="Delivery route" value={entry.route} values={routes}
                      onChange={(route) => mutatePolicy((target) => { target.deliveryRoutes![index].route = route; })} />
                    <button type="button" className="btn min-h-11" onClick={() =>
                      mutatePolicy((target) => {
                        target.deliveryRoutes = target.deliveryRoutes?.filter((_, at) => at !== index);
                        if (!target.deliveryRoutes?.length) delete target.deliveryRoutes;
                      })}>Remove</button>
                  </div>
                ))}
                <button type="button" className="btn min-h-11"
                  disabled={!zones.length || !routes.length || (policy.deliveryRoutes?.length ?? 0) >= 8}
                  onClick={() => mutatePolicy((target) => {
                    target.deliveryRoutes = [...(target.deliveryRoutes ?? []), {
                      zone: zones[0] ?? '', route: routes[0] ?? '',
                    }];
                  })}>＋ Route rule</button>
              </fieldset>
              <fieldset className="mt-3 rounded-sm border border-arena-edge p-3">
                <legend className="lab px-1">Bait drop</legend>
                {policy.baitDrop ? (
                  <div className="grid gap-2 sm:grid-cols-2">
                    <SelectField label="Drop zone" value={policy.baitDrop.zone} values={zones}
                      onChange={(zone) => mutatePolicy((target) => { target.baitDrop!.zone = zone; })} />
                    <ConditionField label="Reclaim when" value={policy.baitDrop.reclaim} predicates={predicateIds}
                      onChange={(reclaim) => mutatePolicy((target) => { target.baitDrop!.reclaim = reclaim; })} />
                    <button type="button" className="btn min-h-11 text-arena-hot"
                      onClick={() => mutatePolicy((target) => { delete target.baitDrop; })}>Remove bait play</button>
                  </div>
                ) : (
                  <button type="button" className="btn min-h-11" disabled={!zones.length}
                    onClick={() => mutatePolicy((target) => {
                      target.baitDrop = { zone: zones[0] ?? '', reclaim: 'always' };
                    })}>＋ Add bait play</button>
                )}
              </fieldset>
              {policy.emergencyRecoveryZones && (
                <fieldset className="mt-3 rounded-sm border border-arena-edge p-3">
                  <legend className="lab px-1">Emergency recovery</legend>
                  <ChoiceGrid label="Recovery zones" values={zones} selected={policy.emergencyRecoveryZones}
                    onChange={(value) => mutatePolicy((target) => { target.emergencyRecoveryZones = value; })} />
                  <ChoiceGrid label="Authorized assignments" values={roles}
                    selected={policy.emergencyRecoveryRoles ?? []}
                    onChange={(value) => mutatePolicy((target) => { target.emergencyRecoveryRoles = value; })} />
                  <ConditionField label="Emergency when" value={policy.emergencyRecovery ?? ''}
                    predicates={predicateIds}
                    onChange={(value) => mutatePolicy((target) => { target.emergencyRecovery = value; })} />
                  <div className="mt-2 grid gap-2 sm:grid-cols-3">
                    <NumberField label="Pickup radius" value={policy.emergencyPickupRadius ?? 1} min={1} max={4}
                      onChange={(value) => mutatePolicy((target) => { target.emergencyPickupRadius = value; })} />
                    <SelectField label="Disposition" value={policy.emergencyRecoveryDisposition ?? 'deliver'}
                      values={['deliver', 'displace']}
                      onChange={(value) => mutatePolicy((target) => { target.emergencyRecoveryDisposition = value; })} />
                    {policy.emergencyRecoveryDisposition === 'displace' &&
                      <SelectField label="Displace to" value={policy.emergencyDisplacementTarget ?? ''}
                        values={anchors}
                        onChange={(value) => mutatePolicy((target) => { target.emergencyDisplacementTarget = value; })} />}
                  </div>
                </fieldset>
              )}
            </div>
          </details>
          <InlineIssues issues={issues} prefix={'custodyPolicies.' + activeIndex} />
          {policies.length > 1 && (
            <button type="button" className="btn mt-3 min-h-11 text-arena-hot"
              onClick={() => {
                const nextId = policies.find((_, index) => index !== activeIndex)?.custodyId ?? '';
                mutate((next) => { next.playbook.custodyPolicies.splice(activeIndex, 1); });
                setActiveId(nextId);
              }}>Remove Core plan</button>
          )}
        </article>
      ) : <p className="t-meta">Create a Core plan before assigning Fetch duty.</p>}
    </div>
  );
}

function CoordinationEditor({ draft, issues, mutate }: {
  draft: SheetDraft;
  issues: SheetIssue[];
  mutate: Mutate;
}) {
  const focus = draft.playbook.doctrines.focus;
  const cohorts = draft.playbook.doctrines.cohorts;
  const focusConsumers = countModeReferences(draft, 'focus');
  const cohortConsumers = countModeReferences(draft, 'cohort');
  return (
    <div className="grid min-w-0 grid-cols-[minmax(0,1fr)] gap-4 lg:grid-cols-2">
      <section>
        <div className="flex items-end gap-2">
          <div className="mr-auto">
            <p className="lab text-arena-material">Focus scopes</p>
            <p className="t-micro mt-1">One job only: cap attackers on the same target.</p>
          </div>
          <button type="button" className="btn min-h-11" onClick={() => mutate((next) => {
            const id = uniqueId('focus', Object.keys(next.playbook.doctrines.focus));
            next.playbook.doctrines.focus[id] = { maximumAttackers: 2 };
          })}>＋ Focus</button>
        </div>
        <div className="mt-2 space-y-2">
          {Object.entries(focus).map(([id, definition]) => (
            <article key={id} className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3"
              data-sheet-path={'doctrines.focus.' + id}>
              <div className="grid gap-2 sm:grid-cols-[1fr_140px_auto] sm:items-end">
                <TextField label="Scope name" value={id} onChange={(value) => {
                  const nextId = assignmentIdFromName(value);
                  if (nextId) mutate((next) => renameSharedReference(next.playbook, 'focus', id, nextId));
                }} />
                <NumberField label="Maximum attackers" value={definition.maximumAttackers} min={1} max={8}
                  onChange={(value) => mutate((next) => {
                    next.playbook.doctrines.focus[id].maximumAttackers = value;
                  })} />
                <button type="button" className="btn min-h-11 text-arena-hot"
                  onClick={() => mutate((next) => { delete next.playbook.doctrines.focus[id]; })}>Remove</button>
              </div>
              <p className="t-micro mt-2">{focusConsumers.get(id) ?? 0} play consumers · shared names require at least two</p>
              <InlineIssues issues={issues} prefix={'doctrines.focus.' + id} />
            </article>
          ))}
        </div>
      </section>
      <section>
        <div className="flex items-end gap-2">
          <div className="mr-auto">
            <p className="lab text-arena-material">Adaptive cohorts</p>
            <p className="t-micro mt-1">One job only: admit the nearest N bodies to a shared destination.</p>
          </div>
          <button type="button" className="btn min-h-11" onClick={() => mutate((next) => {
            const id = uniqueId('cohort', Object.keys(next.playbook.doctrines.cohorts));
            next.playbook.doctrines.cohorts[id] = { take: 2, select: 'nearest' };
          })}>＋ Cohort</button>
        </div>
        <div className="mt-2 space-y-2">
          {Object.entries(cohorts).map(([id, definition]) => (
            <article key={id} className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3"
              data-sheet-path={'doctrines.cohorts.' + id}>
              <div className="grid gap-2 sm:grid-cols-[1fr_140px_auto] sm:items-end">
                <TextField label="Cohort name" value={id} onChange={(value) => {
                  const nextId = assignmentIdFromName(value);
                  if (nextId) mutate((next) => renameSharedReference(next.playbook, 'cohort', id, nextId));
                }} />
                <NumberField label="Nearest bodies" value={definition.take} min={1} max={8}
                  onChange={(value) => mutate((next) => {
                    next.playbook.doctrines.cohorts[id].take = value;
                  })} />
                <button type="button" className="btn min-h-11 text-arena-hot"
                  onClick={() => mutate((next) => { delete next.playbook.doctrines.cohorts[id]; })}>Remove</button>
              </div>
              <p className="t-micro mt-2">{cohortConsumers.get(id) ?? 0} play consumers · membership is recalculated every tick</p>
              <InlineIssues issues={issues} prefix={'doctrines.cohorts.' + id} />
            </article>
          ))}
        </div>
      </section>
    </div>
  );
}

function MemoryEditor({ draft, issues, mutate }: {
  draft: SheetDraft;
  issues: SheetIssue[];
  mutate: Mutate;
}) {
  type MemoryKey = 'enemyUnavailableTicks' | 'lastSeenEnemyTicks' | 'securedCoreTicks' | 'objectiveProgressTicks';
  const fields: Array<[MemoryKey, string, string]> = [
    ['enemyUnavailableTicks', 'Enemy unavailable', 'How long a known disabled enemy remains unavailable.'],
    ['lastSeenEnemyTicks', 'Last seen enemy', 'How long sightings inform pursuit and threat reads.'],
    ['securedCoreTicks', 'Secured Core', 'How long a banked or controlled Core remains tactically relevant.'],
    ['objectiveProgressTicks', 'No progress alarm', 'When the team considers objective progress stalled.'],
  ];
  return (
    <div>
      <p className="t-meta max-w-3xl">These clocks tune team knowledge. They never create a play; triggers decide what that knowledge means.</p>
      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        {fields.map(([key, label, description]) => (
          <label key={key} className="rounded-sm border border-arena-edge bg-arena-deep/25 p-3"
            data-sheet-path={'memory.' + key}>
            <span className="lab text-arena-material">{label}</span>
            <span className="t-micro mt-1 block">{description}</span>
            <span className="mt-3 flex items-center gap-2">
              <input type="range" className="min-w-0 flex-1" min={1} max={600}
                value={draft.playbook.memory[key]}
                onChange={(event) => mutate((next) => {
                  next.playbook.memory[key] = Number(event.target.value);
                })} />
              <span className="pill min-w-16 justify-center">{draft.playbook.memory[key]} t</span>
            </span>
            <FieldIssues issues={issues} path={'memory.' + key} />
          </label>
        ))}
      </div>
    </div>
  );
}

function RepairEditor({ repair, onChange }: {
  repair: RepairBlock | undefined;
  onChange: (repair: RepairBlock | undefined) => void;
}) {
  if (!repair) return (
    <button type="button" className="btn mt-3 min-h-11"
      onClick={() => onChange({
        prefer: ['carrier', 'weakest', 'any'],
        maximumProvidersPerTarget: 1,
        minimumProviderSeparation: 2,
        reserveHealthPercent: 30,
        survivalFallback: 'evade',
      })}>＋ Make this assignment a repair provider</button>
  );
  return (
    <details className="mt-3 rounded-sm border border-arena-edge">
      <summary className="flex min-h-11 cursor-pointer items-center px-3 t-body">Repair priorities</summary>
      <div className="border-t border-arena-edge p-3">
        <ChoiceGrid label="Repair target order" values={['weakest', 'carrier', 'medic', 'focus', 'anchor', 'any']}
          selected={repair.prefer} onChange={(prefer) => onChange({ ...repair, prefer })} />
        <div className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
          <NumberField label="Providers per target" value={repair.maximumProvidersPerTarget} min={1} max={8}
            onChange={(value) => onChange({ ...repair, maximumProvidersPerTarget: value })} />
          <NumberField label="Provider separation" value={repair.minimumProviderSeparation} min={0} max={8}
            onChange={(value) => onChange({ ...repair, minimumProviderSeparation: value })} />
          <NumberField label="Reserve health %" value={repair.reserveHealthPercent} min={0} max={100}
            onChange={(value) => onChange({ ...repair, reserveHealthPercent: value })} />
          <SelectField label="Survival fallback" value={repair.survivalFallback}
            values={['evade', 'regroup', 'hold', 'self-defense']}
            onChange={(value) => onChange({ ...repair, survivalFallback: value })} />
        </div>
        <button type="button" className="btn mt-3 min-h-11 text-arena-hot" onClick={() => onChange(undefined)}>
          Remove repair duty
        </button>
      </div>
    </details>
  );
}

function ChoiceCard({ label, active, activeLabel, inactiveLabel, onChange }: {
  label: string;
  active: boolean;
  activeLabel: string;
  inactiveLabel: string;
  onChange: (value: boolean) => void;
}) {
  return (
    <button type="button" aria-pressed={active}
      className={clsx(
        'min-h-16 rounded-sm border p-3 text-left',
        active ? 'border-arena-material bg-arena-material/10' : 'border-arena-edge bg-arena-deep/20',
      )} onClick={() => onChange(!active)}>
      <span className="lab block">{label}</span>
      <span className="t-body mt-1 block">{active ? activeLabel : inactiveLabel}</span>
    </button>
  );
}

function SegmentedField({ label, value, values, emptyLabel, onChange }: {
  label: string;
  value: string;
  values: string[];
  emptyLabel?: string;
  onChange: (value: string) => void;
}) {
  return (
    <fieldset>
      <legend className="t-micro mb-1.5">{label}</legend>
      <div className="flex flex-wrap gap-1">
        {values.map((entry) => (
          <button key={entry || '__empty'} type="button" aria-pressed={value === entry}
            className={clsx(
              'min-h-11 flex-1 rounded-sm border px-2 t-micro',
              value === entry
                ? 'border-arena-material bg-arena-material/15 text-arena-text'
                : 'border-arena-edge bg-arena-deep/20 text-arena-dim',
            )} onClick={() => onChange(entry)}>
            {entry ? humanizeToken(entry) : emptyLabel ?? 'None'}
          </button>
        ))}
      </div>
    </fieldset>
  );
}

function TokenField({ label, value, values, path, issues, onChange }: {
  label: string;
  value: string;
  values: string[];
  path?: string;
  issues?: SheetIssue[];
  onChange: (value: string) => void;
}) {
  return (
    <fieldset className="mt-3" data-sheet-path={path}>
      <legend className="t-micro mb-1.5">{label}</legend>
      <div className="flex flex-wrap gap-1.5">
        {values.map((entry) => (
          <button key={entry} type="button" aria-pressed={value === entry}
            className={clsx(
              'min-h-11 rounded-full border px-3 t-micro',
              value === entry
                ? 'border-arena-material bg-arena-material/15 text-arena-text'
                : 'border-arena-edge bg-arena-deep/20 text-arena-dim',
            )} onClick={() => onChange(entry)}>{humanizeToken(entry)}</button>
        ))}
      </div>
      {issues && path && <FieldIssues issues={issues} path={path} />}
    </fieldset>
  );
}

function MapPlotter({ catalog, layout, issues, tracks, trackLabel, onChange }: {
  catalog: TacticalSheetCatalog;
  layout: LayoutDocument;
  issues: SheetIssue[];
  tracks: UnitTrack[];
  trackLabel: string | null;
  onChange: (layout: LayoutDocument) => void;
}) {
  const svg = useRef<SVGSVGElement>(null);
  const ui = readUiState();
  const [kind, setKind] = useState<PlotKind>(ui.kind);
  const [side, setSide] = useState<Side>(ui.side);
  const [selectedId, setSelectedId] = useState(ui.selectedId);
  const [drag, setDrag] = useState<DragState | null>(null);
  const [zonePreview, setZonePreview] = useState<[Point, Point] | null>(null);
  const routes = layout.routes ?? [];
  const zones = layout.zones ?? [];
  const anchors = layout.anchors ?? [];
  const selectedRoute = routes.find((entry) => entry.routeId === selectedId) ?? routes[0];
  const selectedZone = zones.find((entry) => entry.zoneId === selectedId) ?? zones[0];
  const selectedAnchor = anchors.find((entry) => entry.anchorId === selectedId) ?? anchors[0];
  const selected = kind === 'route' ? selectedRoute?.routeId
    : kind === 'zone' ? selectedZone?.zoneId : selectedAnchor?.anchorId;

  useEffect(() => {
    localStorage.setItem(UI_STATE_KEY, JSON.stringify({ kind, side, selectedId: selected ?? '' }));
  }, [kind, selected, side]);

  const commit = (change: (next: LayoutDocument) => void) => {
    const next = structuredClone(layout);
    change(next);
    onChange(next);
  };
  const sourcePoint = (event: ReactPointerEvent<SVGSVGElement>): Point | null => {
    const bounds = svg.current?.getBoundingClientRect();
    if (!bounds) return null;
    let x = Math.floor((event.clientX - bounds.left) / bounds.width * catalog.map.width);
    let y = Math.floor((event.clientY - bounds.top) / bounds.height * catalog.map.height);
    x = clamp(x, 0, catalog.map.width - 1);
    y = clamp(y, 0, catalog.map.height - 1);
    return side === 'east' ? [catalog.map.width - 1 - x, catalog.map.height - 1 - y] : [x, y];
  };
  const pointerDown = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (event.button !== 0 || side === 'east') return;
    const point = sourcePoint(event);
    if (!point) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    if (kind === 'zone' && selectedZone) {
      setDrag({ kind: 'zone', id: selectedZone.zoneId, index: -1 });
      setZonePreview([point, point]);
    } else if (kind === 'route' && selectedRoute) {
      commit((next) => next.routes.find((entry) => entry.routeId === selectedRoute.routeId)!.waypoints.push(point));
      setDrag({ kind: 'route', id: selectedRoute.routeId, index: selectedRoute.waypoints.length });
    } else if (kind === 'anchor' && selectedAnchor) {
      commit((next) => { next.anchors.find((entry) => entry.anchorId === selectedAnchor.anchorId)!.position = point; });
      setDrag({ kind: 'anchor', id: selectedAnchor.anchorId, index: 0 });
    }
  };
  const pointerMove = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (!drag) return;
    const point = sourcePoint(event);
    if (!point) return;
    if (drag.kind === 'zone' && zonePreview) setZonePreview([zonePreview[0], point]);
    if (drag.kind === 'route') commit((next) => {
      next.routes.find((entry) => entry.routeId === drag.id)!.waypoints[drag.index] = point;
    });
    if (drag.kind === 'anchor') commit((next) => {
      next.anchors.find((entry) => entry.anchorId === drag.id)!.position = point;
    });
  };
  const pointerUp = (event: ReactPointerEvent<SVGSVGElement>) => {
    if (drag?.kind === 'zone' && zonePreview) {
      const [[ax, ay], [bx, by]] = zonePreview;
      commit((next) => {
        next.zones.find((entry) => entry.zoneId === drag.id)!.rect = [
          Math.min(ax, bx), Math.min(ay, by), Math.max(ax, bx), Math.max(ay, by),
        ];
      });
    }
    if (event.currentTarget.hasPointerCapture(event.pointerId))
      event.currentTarget.releasePointerCapture(event.pointerId);
    setDrag(null);
    setZonePreview(null);
  };
  const nudgeSelected = (event: ReactKeyboardEvent<SVGSVGElement>) => {
    const delta: Partial<Record<string, Point>> = {
      ArrowLeft: [-1, 0],
      ArrowRight: [1, 0],
      ArrowUp: [0, -1],
      ArrowDown: [0, 1],
    };
    const movement = delta[event.key];
    if (!movement || side === 'east') return;
    event.preventDefault();
    const move = ([x, y]: Point): Point => [
      clamp(x + movement[0], 0, catalog.map.width - 1),
      clamp(y + movement[1], 0, catalog.map.height - 1),
    ];
    commit((next) => {
      if (kind === 'route' && selectedRoute) {
        const route = next.routes.find((entry) => entry.routeId === selectedRoute.routeId);
        if (route?.waypoints.length) route.waypoints[route.waypoints.length - 1] = move(route.waypoints.at(-1)!);
      } else if (kind === 'anchor' && selectedAnchor) {
        const anchor = next.anchors.find((entry) => entry.anchorId === selectedAnchor.anchorId);
        if (anchor) anchor.position = move(anchor.position);
      } else if (kind === 'zone' && selectedZone) {
        const zone = next.zones.find((entry) => entry.zoneId === selectedZone.zoneId);
        if (!zone) return;
        const [x0, y0, x1, y1] = zone.rect;
        const dx = movement[0] < 0 ? Math.max(movement[0], -x0)
          : Math.min(movement[0], catalog.map.width - 1 - x1);
        const dy = movement[1] < 0 ? Math.max(movement[1], -y0)
          : Math.min(movement[1], catalog.map.height - 1 - y1);
        zone.rect = [x0 + dx, y0 + dy, x1 + dx, y1 + dy];
      }
    });
  };
  const startHandleDrag = (
    event: ReactPointerEvent<SVGCircleElement>,
    value: DragState,
  ) => {
    event.stopPropagation();
    svg.current?.setPointerCapture(event.pointerId);
    setDrag(value);
  };
  const transform = (point: Point): Point => side === 'east'
    ? [catalog.map.width - 1 - point[0], catalog.map.height - 1 - point[1]]
    : point;
  const eastBinding = layout.bindings.find((binding) => binding.ownReactorSide === 'east');
  const previewRoute = selectedRoute && side === 'east'
    ? eastBinding?.routeAliases?.[selectedRoute.routeId] ?? selectedRoute.routeId
    : selectedRoute?.routeId;

  return (
    <section className="panel min-w-0 pad" data-sheet-path="layout">
      <div className="flex flex-wrap items-start gap-2">
        <div className="mr-auto">
          <p className="lab">Map plotting</p>
          <h2 className="type-display mt-1 text-[21px]">Draw tactics on the hosted map</h2>
          <p className="t-meta mt-1">Drag waypoints, zone rectangles and anchor pins. Actual tracks from selected practice sit behind the authored geometry.</p>
        </div>
        <div className="flex rounded-sm border border-arena-edge p-1" aria-label="Binding preview">
          {(['west', 'east'] as const).map((value) => (
            <button key={value} type="button" className={clsx('btn min-h-11', side === value && 'btn-on')}
              onClick={() => setSide(value)}>{value} side</button>
          ))}
        </div>
      </div>
      <div className="mt-3 grid min-w-0 grid-cols-[minmax(0,1fr)] gap-3 lg:grid-cols-[220px_minmax(0,1fr)]">
        <div className="order-2 min-w-0 lg:order-1">
          <div className="grid grid-cols-3 gap-1">
            {(['route', 'zone', 'anchor'] as const).map((value) => (
              <button key={value} type="button" className={clsx('btn min-h-11', kind === value && 'btn-on')}
                onClick={() => {
                  setKind(value);
                  setSelectedId(value === 'route' ? routes[0]?.routeId ?? ''
                    : value === 'zone' ? zones[0]?.zoneId ?? '' : anchors[0]?.anchorId ?? '');
                }}>{value}</button>
            ))}
          </div>
          <div className="mt-2 flex gap-1 overflow-x-auto rounded-sm border border-arena-edge p-1 lg:block lg:max-h-[310px] lg:overflow-y-auto">
            {(kind === 'route' ? routes : kind === 'zone' ? zones : anchors).map((entry) => {
              const id = kind === 'route' ? (entry as LayoutRoute).routeId
                : kind === 'zone' ? (entry as LayoutZone).zoneId : (entry as LayoutAnchor).anchorId;
              return <button key={id} type="button"
                className={clsx(
                  'min-h-11 shrink-0 rounded-sm px-3 text-left t-micro lg:mb-1 lg:w-full',
                  selected === id
                    ? 'bg-arena-ok/15 text-arena-text'
                    : 'bg-arena-deep/25 text-arena-dim',
                )}
                onClick={() => setSelectedId(id)}>{humanizeToken(id)}</button>;
            })}
          </div>
          <div className="mt-2 flex flex-col gap-2">
            <button type="button" className="btn min-h-11" onClick={() => {
              const existing = kind === 'route' ? routes.map((entry) => entry.routeId)
                : kind === 'zone' ? zones.map((entry) => entry.zoneId) : anchors.map((entry) => entry.anchorId);
              const id = uniqueId(kind, existing);
              commit((next) => {
                if (kind === 'route') next.routes.push({ routeId: id, corridorWidth: 2, waypoints: [[2, 2]] });
                else if (kind === 'zone') next.zones.push({ zoneId: id, rect: [1, 1, 3, 3] });
                else next.anchors.push({ anchorId: id, position: [2, 2] });
              });
              setSelectedId(id);
            }}>Add {kind}</button>
            {selected && <button type="button" className="btn min-h-11 text-arena-hot" onClick={() => commit((next) => {
              if (kind === 'route') next.routes = next.routes.filter((entry) => entry.routeId !== selected);
              else if (kind === 'zone') next.zones = next.zones.filter((entry) => entry.zoneId !== selected);
              else next.anchors = next.anchors.filter((entry) => entry.anchorId !== selected);
              setSelectedId('');
            })}>Remove selected</button>}
            {kind === 'route' && selectedRoute && <>
              <NumberField label="local reflow width" value={selectedRoute.corridorWidth} min={0} max={8}
                onChange={(value) => commit((next) => { next.routes.find((entry) => entry.routeId === selectedRoute.routeId)!.corridorWidth = value; })} />
              <button type="button" className="btn min-h-11" disabled={selectedRoute.waypoints.length === 0}
                onClick={() => commit((next) => { next.routes.find((entry) => entry.routeId === selectedRoute.routeId)!.waypoints.pop(); })}>
                Undo waypoint
              </button>
            </>}
          </div>
        </div>
        <div className="order-1 min-w-0 lg:order-2">
          <div className="mb-2 flex items-center gap-2">
            <span className="lab text-arena-material">{humanizeToken(kind)}</span>
            <span className="type-display truncate text-[16px]">{humanizeToken(selected ?? 'none selected')}</span>
            <span className="pill ml-auto">{side} view</span>
          </div>
          <div className="overflow-hidden rounded-sm border border-arena-edge bg-[#070b0f]">
            <svg
              ref={svg}
              viewBox={`0 0 ${catalog.map.width} ${catalog.map.height}`}
              preserveAspectRatio="xMidYMid meet"
              className="block w-full touch-none select-none"
              style={{ aspectRatio: `${catalog.map.width} / ${catalog.map.height}` }}
              role="img"
              tabIndex={0}
              aria-label={`${side} binding tactical map editor. Arrow keys move the selected ${kind}.`}
              onPointerDown={pointerDown}
              onPointerMove={pointerMove}
              onPointerUp={pointerUp}
              onPointerCancel={pointerUp}
              onKeyDown={nudgeSelected}
            >
              {catalog.map.tileRows.flatMap((row, y) => [...row].map((tile, x) => (
                <rect key={`${x}:${y}`} x={x} y={y} width={1} height={1}
                  fill={tile === '#' ? '#35404a' : (x + y) % 2 ? '#0c151c' : '#101a22'}
                  stroke="#17232c" strokeWidth={0.035} />
              )))}
              {catalog.map.regions.flatMap((region) => region.tiles.map((point) => {
                const [x, y] = transform([point.x, point.y]);
                const fill = region.id.includes('reactor') ? '#f0b94f55'
                  : region.id.includes('well') ? '#a78bfa3d'
                    : region.id.includes('home-west') ? '#22d3ee1f'
                      : region.id.includes('home-east') ? '#fb71851f' : '#64748b1f';
                return <rect key={`${region.id}:${x}:${y}`} x={x + 0.12} y={y + 0.12} width={0.76} height={0.76}
                  rx={0.14} fill={fill} />;
              }))}
              {zones.map((zone) => {
                const [a, b] = zonePreview && zone.zoneId === selectedZone?.zoneId
                  ? zonePreview : [[zone.rect[0], zone.rect[1]], [zone.rect[2], zone.rect[3]]] as [Point, Point];
                const p0 = transform(a); const p1 = transform(b);
                const x = Math.min(p0[0], p1[0]); const y = Math.min(p0[1], p1[1]);
                return <rect key={zone.zoneId} x={x} y={y}
                  width={Math.abs(p1[0] - p0[0]) + 1} height={Math.abs(p1[1] - p0[1]) + 1}
                  fill={zone.zoneId === selectedZone?.zoneId && kind === 'zone' ? '#f0b94f32' : '#64748b12'}
                  stroke={zone.zoneId === selectedZone?.zoneId && kind === 'zone' ? '#f0c965' : '#64748b55'}
                  strokeWidth={zone.zoneId === selectedZone?.zoneId && kind === 'zone' ? 0.16 : 0.07} />;
              })}
              {tracks.map((track) => {
                const points = track.points.map((point) => transform([point.x, point.y]));
                if (points.length < 2) return null;
                const color = track.teamId === 0 ? '#4de7ff' : '#ff806f';
                return <polyline key={`track:${track.key}`} data-unit-track={track.key}
                  points={points.map(([x, y]) => `${x + 0.5},${y + 0.5}`).join(' ')}
                  fill="none" stroke={color} strokeWidth={0.13} strokeOpacity={0.42}
                  strokeLinecap="round" strokeLinejoin="round" />;
              })}
              {routes.map((route) => {
                const points = route.waypoints.map(transform);
                const selectedLine = side === 'east'
                  ? route.routeId === previewRoute
                  : route.routeId === selectedRoute?.routeId;
                return <g key={route.routeId} opacity={kind === 'route' && !selectedLine ? 0.26 : 1}>
                  <polyline points={points.map(([x, y]) => `${x + 0.5},${y + 0.5}`).join(' ')}
                    fill="none" stroke="#030609" strokeWidth={0.52} />
                  <polyline points={points.map(([x, y]) => `${x + 0.5},${y + 0.5}`).join(' ')}
                    fill="none" stroke={selectedLine ? '#39d7f2' : '#78909c'} strokeWidth={selectedLine ? 0.22 : 0.11}
                    strokeDasharray={selectedLine ? undefined : '0.35 0.25'} />
                </g>;
              })}
              {anchors.map((anchor) => {
                const [x, y] = transform(anchor.position);
                return <g key={anchor.anchorId}>
                  <circle cx={x + 0.5} cy={y + 0.5} r={anchor.anchorId === selectedAnchor?.anchorId && kind === 'anchor' ? 0.38 : 0.25}
                    fill="#f472b6" stroke="#ffe4f2" strokeWidth={0.09} />
                </g>;
              })}
              {side === 'west' && kind === 'route' && selectedRoute?.waypoints.map((point, index) => {
                const [x, y] = transform(point);
                return <circle key={index} cx={x + 0.5} cy={y + 0.5} r={0.32}
                  fill="#39d7f2" stroke="#e6fbff" strokeWidth={0.11}
                  onPointerDown={(event) => startHandleDrag(event, { kind: 'route', id: selectedRoute.routeId, index })} />;
              })}
              {side === 'west' && kind === 'anchor' && selectedAnchor && (() => {
                const [x, y] = transform(selectedAnchor.position);
                return <circle cx={x + 0.5} cy={y + 0.5} r={0.55} fill="transparent"
                  onPointerDown={(event) => startHandleDrag(event, { kind: 'anchor', id: selectedAnchor.anchorId, index: 0 })} />;
              })()}
              {catalog.map.spawnAnchors.map((spawn) => {
                const [x, y] = transform([spawn.position.x, spawn.position.y]);
                return <path key={spawn.id} d={`M ${x + 0.18} ${y + 0.18} L ${x + 0.82} ${y + 0.5} L ${x + 0.18} ${y + 0.82} Z`}
                  fill={spawn.id.startsWith('team-0-') ? '#22d3ee' : '#fb7185'} opacity={0.9} />;
              })}
            </svg>
          </div>
          <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 t-micro">
            <span>Tap empty map · add point</span>
            <span>Pink · anchor</span>
            <span>Drag · zone rectangle</span>
            <span>Arrow keys · nudge selected geometry</span>
            {trackLabel && <span>Cyan/coral · actual tracks · {trackLabel}</span>}
            {!trackLabel && <span>select recent practice to compare actual tracks</span>}
            {side === 'east' && <span>rotated preview{previewRoute ? ` · ${previewRoute}` : ''}</span>}
            {side === 'east' && <span>switch west to edit geometry</span>}
          </div>
          <InlineIssues issues={issues} prefix="layout" />
        </div>
      </div>
    </section>
  );
}

function FightEditor({ fight, label, onChange }: {
  fight: FightBlock | undefined;
  label: string;
  onChange: (fight: FightBlock | undefined) => void;
}) {
  const enabled = fight !== undefined;
  const patch = (section: keyof FightBlock | null, key: string, value: unknown) => {
    const next = structuredClone(fight ?? {}) as FightBlock;
    if (section === null) {
      if (value === undefined || value === '') delete next[key];
      else next[key] = value;
    } else {
      const target = { ...object(next[section]) };
      if (value === undefined || value === '') delete target[key];
      else target[key] = value;
      if (Object.keys(target).length === 0) delete next[section];
      else next[section] = target;
    }
    onChange(next);
  };
  return (
    <details className="mt-3 rounded-sm border border-arena-edge p-3">
      <summary className="flex min-h-11 cursor-pointer list-none items-center gap-2 t-micro">
        {label}<span className="pill ml-auto">{enabled ? 'custom' : 'inherit/default'}</span>
      </summary>
      <div className="mt-2">
        {!enabled ? (
          <button type="button" className="btn min-h-11" onClick={() => onChange({})}>Customize fight</button>
        ) : <>
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            <OptionalSelect label="loose Core priority" value={string(fight.collect)} values={['yield', 'first']}
              onChange={(value) => patch(null, 'collect', value)} />
            <OptionalSelect label="heal priority" value={string(fight.heal)} values={['yield', 'first']}
              onChange={(value) => patch(null, 'heal', value)} />
            <OptionalNumber label="targets · lone radius" value={numeric(object(fight.targets).lone)} min={0} max={8}
              onChange={(value) => patch('targets', 'lone', value)} />
            <OptionalNumber label="engage · within" value={numeric(object(fight.engage).within)} min={0} max={12}
              onChange={(value) => patch('engage', 'within', value)} />
            <OptionalNumber label="engage · killable ticks" value={numeric(object(fight.engage).killableTicks)} min={0} max={60}
              onChange={(value) => patch('engage', 'killableTicks', value)} />
            <OptionalSelect label="engage · from" value={string(object(fight.engage).from)} values={['behind']}
              onChange={(value) => patch('engage', 'from', value)} />
            <OptionalNumber label="engage · position ticks" value={numeric(object(fight.engage).positionTicks)} min={1} max={64}
              onChange={(value) => patch('engage', 'positionTicks', value)} />
            <OptionalSelect label="engage · else" value={string(object(fight.engage).else)} values={['strike', 'breakOff']}
              onChange={(value) => patch('engage', 'else', value)} />
            <OptionalNumber label="chase · leash" value={numeric(object(fight.chase).leash)} min={0} max={16}
              onChange={(value) => patch('chase', 'leash', value)} />
            <OptionalNumber label="chase · persist" value={numeric(object(fight.chase).persistTicks)} min={1} max={120}
              onChange={(value) => patch('chase', 'persistTicks', value)} />
            <OptionalNumber label="chase · execute health" value={numeric(object(fight.chase).executeBelowHealth)} min={0} max={8}
              onChange={(value) => patch('chase', 'executeBelowHealth', value)} />
            <OptionalNumber label="break off · threats" value={numeric(object(fight.breakOff).threats)} min={0} max={8}
              onChange={(value) => patch('breakOff', 'threats', value)} />
            <OptionalNumber label="break off · health" value={numeric(object(fight.breakOff).health)} min={0} max={8}
              onChange={(value) => patch('breakOff', 'health', value)} />
            <OptionalNumber label="break off · within" value={numeric(object(fight.breakOff).within)} min={2} max={16}
              onChange={(value) => patch('breakOff', 'within', value)} />
            <OptionalNumber label="break off · memory" value={numeric(object(fight.breakOff).memoryTicks)} min={1} max={120}
              onChange={(value) => patch('breakOff', 'memoryTicks', value)} />
            <OptionalNumber label="break off · recover" value={numeric(object(fight.breakOff).recoverTicks)} min={4} max={120}
              onChange={(value) => patch('breakOff', 'recoverTicks', value)} />
            <OptionalNumber label="defense · radius" value={numeric(object(fight.defense).radius)} min={0} max={16}
              onChange={(value) => patch('defense', 'radius', value)} />
            <OptionalSelect label="defense · return" value={booleanString(object(fight.defense).return)} values={['true', 'false']}
              onChange={(value) => patch('defense', 'return', value === '' ? undefined : value === 'true')} />
          </div>
          <ChoiceGrid label="target preference · ordered by selection" values={['carrier', 'weakest', 'anchor', 'closest', 'strongest-threat', 'freshest']}
            selected={stringArray(object(fight.targets).prefer)}
            onChange={(value) => patch('targets', 'prefer', value.length ? value : undefined)} />
          <button type="button" className="btn mt-3 min-h-11" onClick={() => onChange(undefined)}>Use inherited/default fight</button>
        </>}
      </div>
    </details>
  );
}

function ConditionField({ label, value, predicates, issues, path, onChange }: {
  label: string;
  value: string;
  predicates: string[];
  issues?: SheetIssue[];
  path?: string;
  onChange: (value: string) => void;
}) {
  const [connector, setConnector] = useState<'and' | 'or'>('and');
  const listId = useId();
  return (
    <div className="rounded-sm border border-arena-edge bg-arena-deep/20 p-2" data-sheet-path={path}>
      <span className="lab block">{label}</span>
      <p className={clsx('t-body mt-1 min-h-5', value ? 'text-arena-text' : 'text-arena-dim')}>
        {value ? conditionExpressionSummary(value) : 'No trigger selected'}
      </p>
      <div className="mt-2 flex gap-1">
        <div className="flex shrink-0 rounded-sm border border-arena-edge p-0.5" aria-label={`${label} connector`}>
          {(['and', 'or'] as const).map((value) => (
            <button key={value} type="button" aria-pressed={connector === value}
              className={clsx('min-h-11 rounded-sm px-2 t-micro', connector === value && 'bg-arena-material/15 text-arena-material')}
              onClick={() => setConnector(value)}>{value.toUpperCase()}</button>
          ))}
        </div>
        <select aria-label={`Add predicate to ${label}`} className="field min-h-11 min-w-0 flex-1 px-2 py-1 text-xs"
          value="" onChange={(event) => {
            if (event.target.value) onChange(`${value ? `${value} ${connector} ` : ''}${event.target.value}`);
          }}>
          <option value="">add trigger…</option>
          {predicates.map((id) => <option key={id} value={id}>{humanizeToken(id)}</option>)}
        </select>
        {value && <button type="button" className="btn min-h-11" aria-label={`Clear ${label}`}
          onClick={() => onChange('')}>Clear</button>}
      </div>
      <details className="mt-1">
        <summary className="flex min-h-11 cursor-pointer items-center t-micro">Advanced expression</summary>
        <input className="field mt-1 min-h-11 font-mono text-xs" value={value}
          list={listId} onChange={(event) => onChange(event.target.value)} />
        <datalist id={listId}>{predicates.map((id) => <option key={id} value={id} />)}</datalist>
      </details>
      {issues && path && <FieldIssues issues={issues} path={path} />}
    </div>
  );
}

function IssueSummary({ issues, onNavigate }: {
  issues: SheetIssue[];
  onNavigate: (issue: SheetIssue) => void;
}) {
  const errors = issues.filter((issue) => issue.severity === 'error');
  const warnings = issues.filter((issue) => issue.severity === 'warning');
  if (!issues.length) return null;
  return (
    <section className="panel pad" aria-live="polite">
      <p className="lab">Draft checks · {errors.length} errors · {warnings.length} warnings</p>
      <ul className="mt-2 max-h-40 overflow-y-auto pl-4 text-xs">
        {issues.map((issue, index) => (
          <li
            key={`${issue.path}:${index}`}
            data-sheet-issue={issue.severity}
            className={issue.severity === 'error' ? 'text-arena-hot' : 'text-arena-material'}
          >
            <button type="button" className="min-h-11 text-left underline decoration-current/40 underline-offset-2"
              onClick={() => onNavigate(issue)}>
              <code>{issue.path}</code> — {issue.message}
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}

function InlineIssues({ issues, prefix }: { issues: SheetIssue[]; prefix: string }) {
  const selected = issues.filter((issue) => issue.path.startsWith(prefix));
  if (!selected.length) return null;
  return <ul data-sheet-path={prefix} role="alert"
    className="mt-2 rounded-sm border border-arena-hot/25 bg-arena-hot/5 p-2 text-xs">
    {selected.map((issue, index) => <li key={index} className={issue.severity === 'error' ? 'text-arena-hot' : 'text-arena-material'}>
      {issue.message}
    </li>)}
  </ul>;
}

function FieldIssues({ issues, path }: { issues: SheetIssue[]; path: string }) {
  const selected = issues.filter((issue) => issue.path === path);
  if (selected.length === 0) return null;
  return <span role="alert" className="mt-1 block text-[11px] leading-snug">
    {selected.map((issue, index) => <span key={`${issue.message}:${index}`} className={clsx(
      'block', issue.severity === 'error' ? 'text-arena-hot' : 'text-arena-material',
    )}>{issue.message}</span>)}
  </span>;
}

function ChoiceGrid({ label, values, selected, issues, path, onChange }: {
  label: string;
  values: string[];
  selected: string[];
  issues?: SheetIssue[];
  path?: string;
  onChange: (selected: string[]) => void;
}) {
  return (
    <fieldset className="mt-3" data-sheet-path={path}>
      <legend className="t-micro">{label}</legend>
      <div className="mt-1.5 flex flex-wrap gap-1.5">
        {values.map((value) => {
          const active = selected.includes(value);
          return <button key={value} type="button" aria-pressed={active}
            className={clsx(
              'min-h-11 rounded-full border px-3 py-1 t-micro transition',
              active
                ? 'border-arena-material bg-arena-material/15 text-arena-text'
                : 'border-arena-edge bg-arena-deep/20 text-arena-dim',
            )}
            onClick={() => onChange(active
              ? selected.filter((entry) => entry !== value)
              : [...selected, value])}>
            {humanizeToken(value)}
          </button>;
        })}
      </div>
      {issues && path && <FieldIssues issues={issues} path={path} />}
    </fieldset>
  );
}


function TextField({ label, value, issues, path, onChange }: {
  label: string;
  value: string;
  issues?: SheetIssue[];
  path?: string;
  onChange: (value: string) => void;
}) {
  return <label data-sheet-path={path}><span className="t-micro mb-1 block">{label}</span>
    <input className="field min-h-11" value={value} onChange={(event) => onChange(event.target.value)} />
    {issues && path && <FieldIssues issues={issues} path={path} />}
  </label>;
}

function SelectField({ label, value, values, issues, path, onChange }: {
  label: string;
  value: string;
  values: string[];
  issues?: SheetIssue[];
  path?: string;
  onChange: (value: string) => void;
}) {
  return <label data-sheet-path={path}><span className="t-micro mb-1 block">{label}</span>
    <select className="field min-h-11" value={value} onChange={(event) => onChange(event.target.value)}>
      {values.map((entry) => <option key={entry} value={entry}>{humanizeToken(entry || 'none')}</option>)}
    </select>
    {issues && path && <FieldIssues issues={issues} path={path} />}
  </label>;
}

function OptionalSelect({ label, value, values, onChange }: {
  label: string;
  value: string;
  values: string[];
  onChange: (value: string | undefined) => void;
}) {
  return <label><span className="t-micro mb-1 block">{label}</span>
    <select className="field min-h-11" value={value} onChange={(event) => onChange(event.target.value || undefined)}>
      <option value="">inherit / unset</option>
      {values.map((entry) => <option key={entry}>{humanizeToken(entry)}</option>)}
    </select>
  </label>;
}

function NumberField({ label, value, min, max, issues, path, onChange }: {
  label: string;
  value: number;
  min: number;
  max: number;
  issues?: SheetIssue[];
  path?: string;
  onChange: (value: number) => void;
}) {
  return <label data-sheet-path={path}><span className="t-micro mb-1 block">{label}</span>
    <input type="number" inputMode="numeric" className="field min-h-11" value={value} min={min} max={max}
      onChange={(event) => onChange(Number(event.target.value))} />
    {issues && path && <FieldIssues issues={issues} path={path} />}
  </label>;
}

function OptionalNumber({ label, value, min, max, issues, path, onChange }: {
  label: string;
  value: number | undefined;
  min: number;
  max: number;
  issues?: SheetIssue[];
  path?: string;
  onChange: (value: number | undefined) => void;
}) {
  return <label data-sheet-path={path}><span className="t-micro mb-1 block">{label}</span>
    <input type="number" inputMode="numeric" className="field min-h-11" value={value ?? ''} min={min} max={max}
      placeholder="unset" onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))} />
    {issues && path && <FieldIssues issues={issues} path={path} />}
  </label>;
}


function fromServer(sheet: TacticalSheet): SheetDraft {
  return parseDraft(
    sheet.name,
    sheet.id,
    sheet.revision,
    sheet.playbookJson,
    sheet.layoutJson,
    sheet.entrant.ladderOptedIn,
  );
}

function humanizeToken(value: string): string {
  return value
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function commandGlyph(verb: TacticalVerb): string {
  switch (verb) {
    case 'patrol': return '↻';
    case 'intercept': return '⌖';
    case 'assault': return '⚔';
    case 'recover': return '✚';
    case 'fetch': return '⬡';
    case 'follow': return '➜';
  }
}

function upgradeGlyph(upgrade: string): string {
  switch (upgrade) {
    case 'damage': return '✦';
    case 'vision': return '◉';
    case 'reach': return '↗';
    case 'vitality': return '♥';
    default: return '·';
  }
}

function modeCommandSummary(mode: TacticalMode): string {
  switch (verbOf(mode)) {
    case 'patrol':
      return `Sweep ${destinationLabel(mode)} and hold the route.`;
    case 'intercept':
      return `${humanizeToken(mode.intercept ?? 'enemy-carriers')}${mode.from ? ` from ${humanizeToken(mode.from)}` : ''}.`;
    case 'assault':
      return `Push ${destinationLabel(mode)} and take the fight there.`;
    case 'recover':
      return 'Break contact for the automatic health and beacon window.';
    case 'fetch':
      return `Claim an eligible Core under ${humanizeToken(mode.fetch ?? 'the Core plan')}.`;
    case 'follow':
      return `${humanizeToken(mode.posture ?? 'trail')} ${humanizeToken(mode.follow ?? 'the leader')} wherever that body goes.`;
    default:
      return 'Hold the safe fallback.';
  }
}

function destinationLabel(mode: TacticalMode): string {
  const destination = destinationOf(mode);
  return destination ? humanizeToken(destination.id) : 'the assigned ground';
}

function predicateSentence(predicate: {
  fact: string;
  operator: string;
  value: number;
  subject?: string;
  zone?: string;
}): string {
  if (predicate.fact === 'always') return 'always';
  const subject = predicate.subject && predicate.zone
    ? `${humanizeToken(predicate.subject)} in ${humanizeToken(predicate.zone)}`
    : predicate.subject
      ? humanizeToken(predicate.subject)
      : predicate.zone
        ? humanizeToken(predicate.zone)
        : null;
  const comparison = predicate.operator === 'at-least' ? 'is at least'
    : predicate.operator === 'at-most' ? 'is at most'
      : predicate.operator === 'equals' ? 'equals'
        : humanizeToken(predicate.operator).toLowerCase();
  return `${subject ? `${subject} · ` : ''}${humanizeToken(predicate.fact)} ${comparison} ${predicate.value}`;
}

function conditionExpressionSummary(value: string): string {
  return value
    .split(/\s+(and|or)\s+/)
    .filter(Boolean)
    .map((part) => part === 'and' || part === 'or' ? part.toUpperCase() : humanizeToken(part))
    .join('  ');
}

function uniqueId(prefix: string, existing: string[]) {
  let index = 1;
  while (existing.includes(`${prefix}-${index}`)) index++;
  return `${prefix}-${index}`;
}

function object(value: unknown): JsonObject {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as JsonObject : {};
}

function string(value: unknown) {
  return typeof value === 'string' ? value : '';
}

function numeric(value: unknown) {
  return typeof value === 'number' ? value : undefined;
}

function booleanString(value: unknown) {
  return typeof value === 'boolean' ? String(value) : '';
}

function stringArray(value: unknown) {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string') : [];
}

function rewriteConditionReferences(
  playbook: SheetDraft['playbook'],
  previous: string,
  nextId: string,
) {
  const rewrite = (value: string | undefined) => value?.split(' ').map((token) =>
    token === previous ? nextId : token).join(' ');
  for (const doctrine of Object.values(playbook.doctrines.roles)) {
    doctrine.modes.forEach((mode) => {
      if (mode.while) mode.while = rewrite(mode.while);
      if (mode.until) mode.until = rewrite(mode.until);
    });
  }
  playbook.custodyPolicies.forEach((policy) => {
    policy.safeConversion = rewrite(policy.safeConversion) ?? policy.safeConversion;
    if (policy.baitDrop)
      policy.baitDrop.reclaim = rewrite(policy.baitDrop.reclaim) ?? policy.baitDrop.reclaim;
    if (policy.emergencyRecovery)
      policy.emergencyRecovery = rewrite(policy.emergencyRecovery);
  });
}

function countModeReferences(draft: SheetDraft, key: 'focus' | 'cohort'): Map<string, number> {
  const result = new Map<string, number>();
  for (const doctrine of Object.values(draft.playbook.doctrines.roles)) {
    doctrine.modes.forEach((mode) => {
      const value = mode[key];
      if (typeof value === 'string') result.set(value, (result.get(value) ?? 0) + 1);
    });
  }
  return result;
}

function renameSharedReference(
  playbook: SheetDraft['playbook'],
  key: 'focus' | 'cohort',
  previous: string,
  nextId: string,
) {
  if (previous === nextId) return;
  if (key === 'focus') {
    playbook.doctrines.focus = Object.fromEntries(
      Object.entries(playbook.doctrines.focus).map(([id, value]) =>
        [id === previous ? nextId : id, value]),
    );
  } else {
    playbook.doctrines.cohorts = Object.fromEntries(
      Object.entries(playbook.doctrines.cohorts).map(([id, value]) =>
        [id === previous ? nextId : id, value]),
    );
  }
  for (const doctrine of Object.values(playbook.doctrines.roles)) {
    doctrine.modes.forEach((mode) => {
      if (mode[key] === previous) mode[key] = nextId;
    });
  }
}

function clamp(value: number, minimum: number, maximum: number) {
  return Math.min(maximum, Math.max(minimum, value));
}

function readUiState(): { kind: PlotKind; side: Side; selectedId: string } {
  try {
    const value = JSON.parse(localStorage.getItem(UI_STATE_KEY) ?? '{}') as Partial<{
      kind: PlotKind; side: Side; selectedId: string;
    }>;
    return {
      kind: ['route', 'zone', 'anchor'].includes(value.kind ?? '') ? value.kind! : 'route',
      side: value.side === 'east' ? 'east' : 'west',
      selectedId: value.selectedId ?? '',
    };
  } catch {
    return { kind: 'route', side: 'west', selectedId: '' };
  }
}

const EDITOR_SECTIONS: ReadonlyArray<{
  id: EditorSection;
  label: string;
  matches: (path: string) => boolean;
}> = [
  { id: 'composition', label: '1 Roster', matches: (path) => path === 'composition' || path.startsWith('composition.') || path.startsWith('roles') },
  { id: 'doctrines', label: '2 Battle plan', matches: (path) => path.startsWith('doctrines') || path.startsWith('conditions') || path.startsWith('custodyPolicies') || path.startsWith('memory') },
  { id: 'map', label: '3 Tactics map', matches: (path) => path.startsWith('layout') || path.startsWith('playbook.layout') },
];

function sectionForIssue(path: string): EditorSection {
  return EDITOR_SECTIONS.find((entry) => entry.matches(path))?.id ?? 'doctrines';
}

function doctrineToolForIssue(path: string): DoctrineTool {
  if (path.startsWith('conditions')) return 'conditions';
  if (path.startsWith('custodyPolicies')) return 'custody';
  if (path.startsWith('doctrines.focus') || path.startsWith('doctrines.cohorts')) return 'coordination';
  if (path.startsWith('memory')) return 'memory';
  return null;
}

function scrollToIssue(path: string) {
  const doctrineId = path.startsWith('doctrines.roles.') ? path.split('.')[2] : null;
  const predicateId = path.startsWith('conditions.') ? path.split('.')[1] : null;
  const assignmentIndex = path.startsWith('roles.') ? path.split('.')[1] : null;
  const selector = assignmentIndex
    ? [...document.querySelectorAll<HTMLButtonElement>('[data-assignment-select]')]
      .find((button) => button.dataset.assignmentSelect === assignmentIndex)
    : doctrineId
      ? [...document.querySelectorAll<HTMLButtonElement>('[data-doctrine-select]')]
        .find((button) => button.dataset.doctrineSelect === doctrineId)
      : predicateId
        ? [...document.querySelectorAll<HTMLButtonElement>('[data-predicate-select]')]
          .find((button) => button.dataset.predicateSelect === predicateId)
        : null;
  if (selector && selector.getAttribute('aria-selected') !== 'true') {
    selector.click();
    window.setTimeout(() => scrollToIssue(path), 0);
    return;
  }
  const fields = [...document.querySelectorAll<HTMLElement>('[data-sheet-path]')];
  const target = fields.find((field) => field.dataset.sheetPath === path)
    ?? fields
      .filter((field) => path.startsWith(field.dataset.sheetPath ?? ''))
      .sort((a, b) => (b.dataset.sheetPath?.length ?? 0) - (a.dataset.sheetPath?.length ?? 0))[0];
  target?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  target?.querySelector<HTMLElement>('input, select, textarea, button')?.focus({ preventScroll: true });
}

type Mutate = (change: (next: SheetDraft) => void) => void;
interface DragState { kind: PlotKind; id: string; index: number }
