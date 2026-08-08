import {
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type PointerEvent as ReactPointerEvent,
} from 'react';
import { useNavigate } from 'react-router-dom';
import clsx from 'clsx';
import ClassIcon from '../../components/ClassIcon';
import type { TacticalSheet, TacticalSheetCatalog } from '../api';
import {
  useDeleteTacticalSheet,
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
  doctrineOrderIds,
  freshMode,
  loadLocalDraft,
  normalizeEscorts,
  parseDraft,
  pinDraft,
  predicateSubjectKind,
  saveLocalDraft,
  validateDraft,
  verbOf,
  type CustodyPolicy,
  type Doctrine,
  type FightBlock,
  type JsonObject,
  type LayoutAnchor,
  type LayoutDocument,
  type LayoutRoute,
  type LayoutZone,
  type Point,
  type SheetDraft,
  type SheetIssue,
  type TacticalMode,
  type TacticalVerb,
} from './sheetAuthoring';

interface TacticalSheetEditorProps {
  catalog: TacticalSheetCatalog;
}

type PlotKind = 'route' | 'zone' | 'anchor';
type Side = 'west' | 'east';

export default function TacticalSheetEditor({ catalog }: TacticalSheetEditorProps) {
  const navigate = useNavigate();
  const sheetsQuery = useTacticalSheets(true);
  const [activeId, setActiveId] = useState<string | null>(null);
  const sheetQuery = useTacticalSheet(activeId);
  const [draft, setDraft] = useState<SheetDraft | null>(null);
  const [layoutHash, setLayoutHash] = useState<string | null>(null);
  const [stockId, setStockId] = useState(catalog.stockOpponents[0]?.id ?? '');
  const [seed, setSeed] = useState('104729');
  const [importError, setImportError] = useState<string | null>(null);
  const [restored, setRestored] = useState(false);
  const save = useSaveTacticalSheet();
  const remove = useDeleteTacticalSheet();
  const trial = useTrialTacticalSheet();

  useEffect(() => {
    let live = true;
    void loadLocalDraft().then((local) => {
      if (!live) return;
      setDraft(local ?? parseDraft(
        'Untitled tactical sheet',
        null,
        null,
        catalog.templatePlaybookJson,
        catalog.templateLayoutJson,
      ));
      // Keep an unsaved local draft authoritative on restore. Selecting its
      // server row explicitly is what replaces it with the saved revision.
      setActiveId(null);
      setRestored(local !== null);
    }).catch(() => {
      if (!live) return;
      setDraft(parseDraft(
        'Untitled tactical sheet', null, null,
        catalog.templatePlaybookJson, catalog.templateLayoutJson,
      ));
    });
    return () => { live = false; };
  }, [catalog.templateLayoutJson, catalog.templatePlaybookJson]);

  useEffect(() => {
    if (!sheetQuery.data) return;
    setDraft(fromServer(sheetQuery.data));
    setRestored(false);
  }, [sheetQuery.data]);

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

  const issues = useMemo(
    () => draft ? validateDraft(draft, catalog, layoutHash) : [],
    [catalog, draft, layoutHash],
  );
  const blocking = issues.filter((issue) =>
    issue.severity === 'error' && issue.path !== 'playbook.layout.sha256');

  const startNew = () => {
    setActiveId(null);
    setDraft(parseDraft(
      'Untitled tactical sheet', null, null,
      catalog.templatePlaybookJson, catalog.templateLayoutJson,
    ));
    setRestored(false);
    resetMutations();
  };

  const saveDraft = async (asCopy = false): Promise<TacticalSheet> => {
    if (!draft) throw new Error('No draft is open.');
    const prepared = await pinDraft(draft);
    const targetId = asCopy ? null : prepared.draft.sheetId;
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
    setActiveId(saved.id);
    setDraft(fromServer(saved));
    setLayoutHash(prepared.layoutSha256);
    setRestored(false);
    return saved;
  };

  const runTrial = async () => {
    const saved = await saveDraft(false);
    const parsedSeed = seed.trim() === '' ? null : Number(seed);
    const match = await trial.mutateAsync({
      sheetId: saved.id,
      body: {
        stockSheetId: stockId,
        seed: Number.isSafeInteger(parsedSeed) ? parsedSeed : null,
      },
    });
    navigate(`/matches/${match.id}`);
  };

  const deleteDraft = async () => {
    if (!draft?.sheetId) return;
    if (!window.confirm(`Delete “${draft.name}”? Ranked-history sheets cannot be deleted.`)) return;
    await remove.mutateAsync(draft.sheetId);
    startNew();
  };

  const exportDraft = async () => {
    if (!draft) return;
    const prepared = await pinDraft(draft);
    setDraft(prepared.draft);
    download(`${safeName(draft.playbook.playbookId)}.playbook.json`, prepared.playbookJson);
    download(`${safeName(draft.layout.layoutId)}.layout.json`, prepared.layoutJson);
  };

  const importFiles = async (files: FileList | null) => {
    if (!files?.length || !draft) return;
    setImportError(null);
    try {
      const next = structuredClone(draft);
      for (const file of [...files]) {
        const source = await file.text();
        const value = JSON.parse(source) as JsonObject;
        if (value.schema === 'arc-relay-tactical-playbook-v1')
          next.playbook = value as unknown as SheetDraft['playbook'];
        else if (value.schema === 'arc-relay-tactical-layout-v1')
          next.layout = value as unknown as SheetDraft['layout'];
        else throw new Error(`${file.name} is not a current tactical playbook or layout.`);
      }
      setDraft(next);
    } catch (cause) {
      setImportError(cause instanceof Error ? cause.message : 'Import failed.');
    }
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
  };

  if (!draft) return <section className="panel pad"><p className="t-meta">Restoring local draft…</p></section>;

  return (
    <div className="grid min-w-0 gap-4 xl:grid-cols-[250px_minmax(0,1fr)]">
      <aside className="panel h-fit xl:sticky xl:top-4">
        <div className="pad flex items-center gap-2 border-b border-arena-edge">
          <div className="mr-auto">
            <p className="lab">Saved sheets</p>
            <p className="t-micro mt-1">Server revisions</p>
          </div>
          <button type="button" className="btn min-h-11" onClick={startNew}>New</button>
        </div>
        {restored && (
          <p className="mx-2 mt-2 rounded-sm border border-amber-400/35 bg-amber-300/10 p-2 t-micro text-amber-100">
            Restored the last local draft from this device.
          </p>
        )}
        <div className="max-h-[360px] overflow-y-auto p-2">
          {sheetsQuery.isPending && <p className="t-micro p-2">Loading…</p>}
          {sheetsQuery.isError && <p className="t-micro p-2 text-red-300">Could not load saved sheets.</p>}
          {sheetsQuery.data?.map((sheet) => (
            <button
              key={sheet.id}
              type="button"
              className={clsx(
                'mb-1 min-h-12 w-full rounded-sm border px-2.5 py-2 text-left',
                draft.sheetId === sheet.id
                  ? 'border-cyan-300/60 bg-cyan-300/10'
                  : 'border-transparent active:border-arena-edge',
              )}
              onClick={() => {
                setActiveId(sheet.id);
                resetMutations();
              }}
            >
              <span className="t-body block truncate">{sheet.name}</span>
              <span className="t-micro block">r{sheet.revision} · {sheet.contentHash.slice(0, 10)}</span>
            </button>
          ))}
          {sheetsQuery.data?.length === 0 && (
            <p className="t-meta p-2">Your first save creates a sheet entrant.</p>
          )}
        </div>
        <div className="border-t border-arena-edge p-2">
          <p className="t-micro px-1 pb-2">Local draft autosaves in this browser.</p>
          <label className="btn min-h-11 w-full cursor-pointer justify-center">
            Import JSON pair
            <input
              className="sr-only"
              type="file"
              accept="application/json,.json"
              multiple
              onChange={(event) => void importFiles(event.target.files)}
            />
          </label>
          <button type="button" className="btn mt-2 min-h-11 w-full" onClick={() => void exportDraft()}>
            Export pinned pair
          </button>
          {layoutHash && <p className="mt-2 break-all px-1 font-mono text-[10px] text-arena-material">layout sha256 {layoutHash}</p>}
          {importError && <p className="t-micro mt-2 text-red-300">{importError}</p>}
        </div>
      </aside>

      <main className="flex min-w-0 flex-col gap-4">
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
                className="btn btn-on min-h-11 flex-1"
                disabled={save.isPending || blocking.length > 0}
                onClick={() => void saveDraft(false)}
              >
                {save.isPending ? 'Compiling…' : draft.sheetId ? `Save r${(draft.revision ?? 0) + 1}` : 'Save & enter ladder'}
              </button>
              {draft.sheetId && (
                <button type="button" className="btn min-h-11" disabled={save.isPending}
                  onClick={() => void saveDraft(true)}>Save copy</button>
              )}
              {draft.sheetId && (
                <button type="button" className="btn min-h-11 text-red-200" disabled={remove.isPending}
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
            Enter this sheet in the ranked ladder when saved
            <span className="t-micro ml-auto">on by default</span>
          </label>
          {(save.isError || remove.isError) && (
            <p className="t-micro mt-2 text-red-300">{(save.error ?? remove.error)?.message}</p>
          )}
          {save.isSuccess && <p className="t-micro mt-2 text-emerald-300">Compiled, hash-pinned and saved on the server.</p>}
        </section>

        <IssueSummary issues={issues} />
        <CompositionEditor draft={draft} catalog={catalog} mutate={mutate} />
        <DoctrineEditor draft={draft} issues={issues} mutate={mutate} />
        <PredicateEditor draft={draft} issues={issues} mutate={mutate} />
        <CustodyEditor draft={draft} issues={issues} mutate={mutate} />
        <MapPlotter catalog={catalog} layout={draft.layout}
          onChange={(layout) => mutate((next) => { next.layout = layout; })} />

        <section className="panel pad border-cyan-300/30">
          <div className="flex flex-wrap items-start gap-3">
            <div className="mr-auto max-w-2xl">
              <p className="lab text-cyan-200">Edit → run → watch</p>
              <h2 className="type-display mt-1 text-[22px]">Trial the current draft</h2>
              <p className="t-meta mt-1">
                One tap saves through the shared compiler, runs against the chosen tracked stock sheet,
                and opens the completed match in the replay viewer.
              </p>
            </div>
            <span className="pill">{catalog.map.id}</span>
          </div>
          <div className="mt-3 grid gap-3 sm:grid-cols-[minmax(0,1fr)_150px_auto] sm:items-end">
            <label>
              <span className="t-micro mb-1 block">Stock opponent</span>
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
            <button type="button" className="btn btn-on min-h-11"
              disabled={blocking.length > 0 || save.isPending || trial.isPending || !stockId}
              onClick={() => void runTrial()}>
              {trial.isPending ? 'Running…' : 'Save & watch trial'}
            </button>
          </div>
          {catalog.stockOpponents.find((stock) => stock.id === stockId) && (
            <p className="t-micro mt-2">
              {catalog.stockOpponents.find((stock) => stock.id === stockId)!.description}
            </p>
          )}
          {trial.isError && <p className="t-micro mt-2 text-red-300">{trial.error.message}</p>}
        </section>
      </main>
    </div>
  );
}

function CompositionEditor({ draft, catalog, mutate }: {
  draft: SheetDraft;
  catalog: TacticalSheetCatalog;
  mutate: Mutate;
}) {
  const counts = new Map<string, number>();
  draft.playbook.composition.forEach((id) => counts.set(id, (counts.get(id) ?? 0) + 1));
  return (
    <section className="panel pad">
      <div className="flex flex-wrap items-end gap-2">
        <div className="mr-auto">
          <p className="lab">Composition</p>
          <p className="t-micro mt-1">Eight unlocked classes · two copies maximum</p>
        </div>
        <div className="flex gap-1">
          {draft.playbook.composition.map((classId, index) => (
            <ClassIcon key={index} classId={classId} label={`${index + 1}. ${classId}`} size={28} />
          ))}
        </div>
      </div>
      <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-8">
        {Array.from({ length: catalog.slotCount }, (_, index) => {
          const current = draft.playbook.composition[index] ?? '';
          return (
            <label key={index}>
              <span className="t-micro mb-1 block">slot {index + 1}</span>
              <select className="field min-h-11 px-2" value={current}
                onChange={(event) => mutate((next) => {
                  next.playbook.composition[index] = event.target.value;
                })}>
                {catalog.classes.map((entry) => (
                  <option key={entry.id} value={entry.id}
                    disabled={!entry.unlocked || (entry.id !== current
                      && (counts.get(entry.id) ?? 0) >= catalog.maximumCopiesPerClass)}>
                    {entry.name}{entry.unlocked ? '' : ' · locked'}
                  </option>
                ))}
              </select>
            </label>
          );
        })}
      </div>
    </section>
  );
}

function DoctrineEditor({ draft, issues, mutate }: {
  draft: SheetDraft;
  issues: SheetIssue[];
  mutate: Mutate;
}) {
  const doctrines = Object.entries(draft.playbook.doctrines ?? {});
  const roleIds = draft.playbook.roles.map((role) => role.roleId);
  const custodyIds = draft.playbook.custodyPolicies.map((policy) => policy.custodyId);
  const routeIds = draft.layout.routes.map((route) => route.routeId);
  const anchorIds = draft.layout.anchors.map((anchor) => anchor.anchorId);
  const zoneIds = draft.layout.zones.map((zone) => zone.zoneId);
  const orderIds = doctrineOrderIds(draft.playbook);
  const predicateIds = Object.keys(draft.playbook.authoring.predicates ?? {});

  const updateDoctrine = (id: string, change: (doctrine: Doctrine) => void) =>
    mutate((next) => change(next.playbook.doctrines[id]));
  const renameDoctrine = (id: string, nextId: string) => mutate((next) => {
    const entries = Object.entries(next.playbook.doctrines);
    next.playbook.doctrines = Object.fromEntries(entries.map(([key, value]) =>
      key === id ? [nextId, value] : [key, value]));
  });

  return (
    <section className="panel pad">
      <div className="flex flex-wrap items-start gap-2">
        <div className="mr-auto">
          <p className="lab">Doctrines</p>
          <h2 className="type-display mt-1 text-[21px]">What each role does</h2>
          <p className="t-meta mt-1">Modes are evaluated top to bottom; the last mode is the safe floor.</p>
        </div>
        <button type="button" className="btn min-h-11" onClick={() => mutate((next) => {
          const id = uniqueId('doctrine', Object.keys(next.playbook.doctrines));
          next.playbook.doctrines[id] = {
            role: roleIds.find((role) => !Object.values(next.playbook.doctrines)
              .some((doctrine) => doctrine.role === role)) ?? roleIds[0] ?? '',
            custody: custodyIds[0] ?? '',
            conceal: true,
            modes: [{ patrol: routeIds[0] ?? 'traffic' }],
          };
        })}>Add doctrine</button>
      </div>
      <div className="mt-3 flex flex-col gap-3">
        {doctrines.map(([id, doctrine]) => (
          <details key={id} className="rounded-sm border border-arena-edge bg-arena-deep/25" open>
            <summary className="flex min-h-12 cursor-pointer list-none items-center gap-2 px-3 py-2">
              <span className="type-display text-[18px]">{id}</span>
              <span className="pill">{doctrine.role}</span>
              <span className="t-micro ml-auto">{doctrine.modes?.length ?? 0} modes</span>
            </summary>
            <div className="border-t border-arena-edge p-3">
              <div className="grid gap-2 sm:grid-cols-3">
                <TextField label="doctrine id" value={id} onChange={(value) => renameDoctrine(id, value)} />
                <SelectField label="role" value={doctrine.role} values={roleIds}
                  onChange={(value) => updateDoctrine(id, (next) => { next.role = value; })} />
                <SelectField label="custody" value={doctrine.custody} values={custodyIds}
                  onChange={(value) => updateDoctrine(id, (next) => { next.custody = value; })} />
              </div>
              <div className="mt-2 flex flex-wrap items-center gap-3">
                <Check label="concealment micro" checked={doctrine.conceal ?? true}
                  onChange={(value) => updateDoctrine(id, (next) => { next.conceal = value; })} />
                <button type="button" className="btn ml-auto min-h-11 text-red-200"
                  onClick={() => mutate((next) => { delete next.playbook.doctrines[id]; })}>
                  Remove doctrine
                </button>
              </div>
              <fieldset className="mt-3 rounded-sm border border-arena-edge p-3">
                <legend className="t-micro px-1">Break a mode to collect loose Cores in</legend>
                <div className="grid grid-cols-2 gap-1 sm:grid-cols-3 lg:grid-cols-5">
                  {zoneIds.map((zone) => {
                    const selected = typeof doctrine.collect === 'string'
                      ? [doctrine.collect] : doctrine.collect ?? [];
                    return <Check key={zone} label={zone} checked={selected.includes(zone)} onChange={(checked) =>
                      updateDoctrine(id, (next) => {
                        const values = typeof next.collect === 'string' ? [next.collect] : next.collect ?? [];
                        next.collect = checked ? [...values, zone] : values.filter((value) => value !== zone);
                      })} />;
                  })}
                </div>
              </fieldset>
              <FightEditor fight={doctrine.fight} label="Doctrine fight style"
                onChange={(fight) => updateDoctrine(id, (next) => { next.fight = fight; })} />
              <div className="mt-3 flex flex-col gap-2">
                {(doctrine.modes ?? []).map((mode, index) => (
                  <ModeEditor
                    key={index}
                    doctrineId={id}
                    doctrine={doctrine}
                    mode={mode}
                    index={index}
                    routeIds={routeIds}
                    anchorIds={anchorIds}
                    roleIds={roleIds}
                    orderIds={orderIds}
                    predicateIds={predicateIds}
                    issues={issues}
                    onChange={(nextMode) => updateDoctrine(id, (next) => { next.modes[index] = nextMode; })}
                    onMove={(delta) => updateDoctrine(id, (next) => {
                      const [moving] = next.modes.splice(index, 1);
                      next.modes.splice(index + delta, 0, moving);
                    })}
                    onRemove={() => updateDoctrine(id, (next) => { next.modes.splice(index, 1); })}
                    makeRecruitable={(role) => mutate((next) => {
                      const target = Object.values(next.playbook.doctrines).find((value) => value.role === role);
                      if (!target || target.modes.some((value) => verbOf(value) === 'muster')) return;
                      target.modes.splice(Math.max(0, target.modes.length - 1), 0, { muster: 'escort' });
                    })}
                  />
                ))}
                <button type="button" className="btn min-h-11 self-start" disabled={doctrine.modes.length >= 8}
                  onClick={() => updateDoctrine(id, (next) => {
                    const floor = next.modes.pop();
                    next.modes.push(freshMode('intercept', routeIds, anchorIds));
                    if (floor) next.modes.push(floor);
                  })}>Add priority mode</button>
              </div>
              <InlineIssues issues={issues} prefix={`doctrines.${id}`} />
            </div>
          </details>
        ))}
      </div>
    </section>
  );
}

function ModeEditor({
  doctrineId,
  doctrine,
  mode,
  index,
  routeIds,
  anchorIds,
  roleIds,
  orderIds,
  predicateIds,
  issues,
  onChange,
  onMove,
  onRemove,
  makeRecruitable,
}: {
  doctrineId: string;
  doctrine: Doctrine;
  mode: TacticalMode;
  index: number;
  routeIds: string[];
  anchorIds: string[];
  roleIds: string[];
  orderIds: string[];
  predicateIds: string[];
  issues: SheetIssue[];
  onChange: (mode: TacticalMode) => void;
  onMove: (delta: number) => void;
  onRemove: () => void;
  makeRecruitable: (role: string) => void;
}) {
  const verb = verbOf(mode) ?? 'patrol';
  const floor = index === doctrine.modes.length - 1;
  const set = (patch: Partial<TacticalMode>) => onChange({ ...mode, ...patch });
  const changeVerb = (nextVerb: TacticalVerb) => {
    const next = freshMode(nextVerb, routeIds, anchorIds);
    if (!floor && !['recover', 'muster'].includes(nextVerb)) {
      next.while = mode.while ?? predicateIds[0] ?? '';
      next.until = mode.until ?? predicateIds[1] ?? predicateIds[0] ?? '';
    }
    if (mode.fight) next.fight = mode.fight;
    onChange(next);
  };
  const escorts = normalizeEscorts(mode.escort);
  const issuePrefix = `doctrines.${doctrineId}.modes.${index}`;

  return (
    <article className={clsx('rounded-sm border p-3', floor ? 'border-emerald-400/40 bg-emerald-400/5' : 'border-arena-edge')}>
      <div className="flex flex-wrap items-end gap-2">
        <span className="type-display self-center text-[18px]">{index + 1}</span>
        <SelectField label={floor ? 'floor verb' : 'verb'} value={verb} values={[...TACTICAL_VERBS]}
          onChange={(value) => changeVerb(value as TacticalVerb)} />
        <div className="ml-auto flex gap-1">
          <button type="button" aria-label="Move mode up" className="btn min-h-11 min-w-11"
            disabled={index === 0} onClick={() => onMove(-1)}>↑</button>
          <button type="button" aria-label="Move mode down" className="btn min-h-11 min-w-11"
            disabled={floor} onClick={() => onMove(1)}>↓</button>
          <button type="button" className="btn min-h-11" disabled={doctrine.modes.length <= 1}
            onClick={onRemove}>Remove</button>
        </div>
      </div>

      <div className="mt-3 grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
        {verb === 'patrol' && (
          <SelectField label="patrol route" value={mode.patrol ?? ''} values={[...routeIds, 'traffic']}
            onChange={(value) => set({ patrol: value })} />
        )}
        {verb === 'intercept' && <>
          <SelectField label="intercept" value={mode.intercept ?? 'enemy-carriers'}
            values={['enemy-carriers', 'inbound']} onChange={(value) => set({ intercept: value })} />
          <SelectField label="stage from anchor" value={mode.from ?? ''} values={['', ...anchorIds]}
            onChange={(value) => set({ from: value || undefined })} />
          <OptionalNumber label="patience ticks · inert compatibility" value={mode.patienceTicks} min={2} max={120}
            onChange={(value) => set({ patienceTicks: value })} />
        </>}
        {verb === 'assault' && (
          <SelectField label="assault route" value={mode.assault ?? ''} values={routeIds}
            onChange={(value) => set({ assault: value })} />
        )}
        {verb === 'recover' && <ReadOnlyField label="recovery" value="automatic health/beacon window" />}
        {verb === 'muster' && (
          <SelectField label="answer call" value={mode.muster ?? 'escort'} values={['escort', ...orderIds]}
            onChange={(value) => set({ muster: value })} />
        )}
        {verb === 'squad' && <ReadOnlyField label="fallback" value="ordinary squad-plane job" />}
        {!floor && verb !== 'recover' && verb !== 'muster' && <>
          <ConditionField label="while" value={mode.while ?? ''} predicates={predicateIds}
            onChange={(value) => set({ while: value })} />
          <ConditionField label="until" value={mode.until ?? ''} predicates={predicateIds}
            onChange={(value) => set({ until: value || undefined })} />
        </>}
      </div>

      {verb === 'assault' && (
        <fieldset className="mt-3 rounded-sm border border-arena-edge p-3">
          <legend className="t-micro px-1">Escort calls</legend>
          <div className="flex flex-col gap-2">
            {escorts.map((escort, escortIndex) => (
              <div key={escortIndex} className="grid gap-2 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
                <SelectField label="recruit role" value={escort.role}
                  values={roleIds.filter((role) => role !== doctrine.role)}
                  onChange={(role) => {
                    const next = [...escorts]; next[escortIndex] = { ...escort, role }; set({ escort: next });
                  }} />
                <SelectField label="posture" value={escort.posture ?? 'trail'} values={['trail', 'screen']}
                  onChange={(posture) => {
                    const next = [...escorts]; next[escortIndex] = { ...escort, posture }; set({ escort: next });
                  }} />
                <button type="button" className="btn min-h-11" onClick={() =>
                  set({ escort: escorts.filter((_, position) => position !== escortIndex) })}>Remove</button>
              </div>
            ))}
          </div>
          <button type="button" className="btn mt-2 min-h-11" disabled={escorts.length >= 8}
            onClick={() => set({ escort: [...escorts, { role: roleIds.find((role) => role !== doctrine.role) ?? '', posture: 'trail' }] })}>
            Add escort
          </button>
          {escorts.map((escort) => ({
            role: escort.role,
            recruitable: false,
          })).filter(({ role }) => issues.some((issue) => issue.path.startsWith(issuePrefix)
            && issue.message.includes(`'${role}' is not recruitable`))).map(({ role }) => (
              <button key={role} type="button" className="btn ml-2 mt-2 min-h-11 border-amber-300/50"
                onClick={() => makeRecruitable(role)}>Add muster to {role}</button>
            ))}
        </fieldset>
      )}
      <FightEditor fight={mode.fight} label="Mode fight override" onChange={(fight) => set({ fight })} />
      <InlineIssues issues={issues} prefix={issuePrefix} />
    </article>
  );
}

function PredicateEditor({ draft, issues, mutate }: {
  draft: SheetDraft;
  issues: SheetIssue[];
  mutate: Mutate;
}) {
  const predicates = draft.playbook.authoring.predicates ?? {};
  const roles = draft.playbook.roles.map((role) => role.roleId);
  const groups = draft.playbook.groups.map((group) => group.groupId);
  const zones = draft.layout.zones.map((zone) => zone.zoneId);
  const orders = doctrineOrderIds(draft.playbook);
  const rename = (id: string, nextId: string) => mutate((next) => {
    const current = next.playbook.authoring.predicates;
    next.playbook.authoring.predicates = Object.fromEntries(Object.entries(current)
      .map(([key, value]) => key === id ? [nextId, value] : [key, value]));
  });
  return (
    <section className="panel pad">
      <details>
        <summary className="min-h-11 cursor-pointer lab">Conditions · {Object.keys(predicates).length} named facts</summary>
        <p className="t-meta mt-2">Modes combine these names with lowercase <code>and</code>/<code>or</code>. Facts reveal only the subject they require.</p>
        <div className="mt-3 flex flex-col gap-2">
          {Object.entries(predicates).map(([id, predicate]) => {
            const kind = predicateSubjectKind(predicate.fact);
            const options = kind === 'zone' ? zones
              : kind === 'group' ? groups
                : kind === 'role' ? roles
                  : kind === 'well' ? ['north', 'centre', 'south']
                    : kind === 'order' ? orders : [];
            return (
              <article key={id} className="rounded-sm border border-arena-edge p-3">
                <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-5">
                  <TextField label="predicate id" value={id} onChange={(value) => rename(id, value)} />
                  <SelectField label="fact" value={predicate.fact} values={[...CONDITION_FACTS]}
                    onChange={(fact) => mutate((next) => {
                      const target = next.playbook.authoring.predicates[id];
                      target.fact = fact;
                      delete target.subject;
                      delete target.zone;
                      delete target.freshnessTicks;
                    })} />
                  <SelectField label="operator" value={predicate.operator} values={[...CONDITION_OPERATORS]}
                    onChange={(operator) => mutate((next) => { next.playbook.authoring.predicates[id].operator = operator; })} />
                  <NumberField label="value" value={predicate.value} min={0} max={100000}
                    onChange={(value) => mutate((next) => { next.playbook.authoring.predicates[id].value = value; })} />
                  {kind && kind !== 'group-zone' && <SelectField label={kind} value={kind === 'zone' ? predicate.zone ?? '' : predicate.subject ?? ''}
                    values={options} onChange={(value) => mutate((next) => {
                      const target = next.playbook.authoring.predicates[id];
                      if (kind === 'zone') target.zone = value;
                      else target.subject = value;
                    })} />}
                  {kind === 'group-zone' && <>
                    <SelectField label="group" value={predicate.subject ?? ''} values={groups}
                      onChange={(value) => mutate((next) => { next.playbook.authoring.predicates[id].subject = value; })} />
                    <SelectField label="zone" value={predicate.zone ?? ''} values={zones}
                      onChange={(value) => mutate((next) => { next.playbook.authoring.predicates[id].zone = value; })} />
                  </>}
                </div>
                <div className="mt-2 flex flex-wrap items-end gap-2">
                  {(predicate.fact === 'remembered-enemies-in-zone' || predicate.fact === 'secured-cores') && (
                    <OptionalNumber label="freshness ticks" value={predicate.freshnessTicks} min={1} max={600}
                      onChange={(value) => mutate((next) => {
                        const target = next.playbook.authoring.predicates[id];
                        if (value === undefined) delete target.freshnessTicks;
                        else target.freshnessTicks = value;
                      })} />
                  )}
                  <button type="button" className="btn ml-auto min-h-11 text-red-200"
                    onClick={() => mutate((next) => { delete next.playbook.authoring.predicates[id]; })}>Remove</button>
                </div>
                <InlineIssues issues={issues} prefix={`authoring.predicates.${id}`} />
              </article>
            );
          })}
        </div>
        <button type="button" className="btn mt-3 min-h-11" onClick={() => mutate((next) => {
          const id = uniqueId('condition', Object.keys(next.playbook.authoring.predicates));
          next.playbook.authoring.predicates[id] = { fact: 'always', operator: 'equals', value: 1 };
        })}>Add condition fact</button>
      </details>
    </section>
  );
}

function CustodyEditor({ draft, issues, mutate }: {
  draft: SheetDraft;
  issues: SheetIssue[];
  mutate: Mutate;
}) {
  const roles = draft.playbook.roles.map((role) => role.roleId);
  const groups = draft.playbook.groups.map((group) => group.groupId);
  const zones = draft.layout.zones.map((zone) => zone.zoneId);
  const routes = draft.layout.routes.map((route) => route.routeId);
  const predicates = Object.keys(draft.playbook.authoring.predicates ?? {});
  const update = (index: number, change: (policy: CustodyPolicy) => void) => mutate((next) =>
    change(next.playbook.custodyPolicies[index]));
  return (
    <section className="panel pad">
      <details>
        <summary className="min-h-11 cursor-pointer lab">Core custody · {draft.playbook.custodyPolicies.length} policies</summary>
        <p className="t-meta mt-2">Who may carry, how drops recover, and which plotted routes bring a Core home.</p>
        <div className="mt-3 flex flex-col gap-3">
          {draft.playbook.custodyPolicies.map((policy, index) => (
            <article key={`${policy.custodyId}:${index}`} className="rounded-sm border border-arena-edge p-3">
              <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                <TextField label="custody id" value={policy.custodyId}
                  onChange={(value) => update(index, (next) => { next.custodyId = value; })} />
                <NumberField label="pickup reservation" value={policy.pickupReservationTicks} min={1} max={120}
                  onChange={(value) => update(index, (next) => { next.pickupReservationTicks = value; })} />
                <NumberField label="transfer timeout" value={policy.transferTimeoutTicks} min={1} max={120}
                  onChange={(value) => update(index, (next) => { next.transferTimeoutTicks = value; })} />
                <NumberField label="delivery timeout" value={policy.deliveryTimeoutTicks} min={1} max={1200}
                  onChange={(value) => update(index, (next) => { next.deliveryTimeoutTicks = value; })} />
                <SelectField label="accidental pickup" value={policy.accidentalPickup}
                  values={['transfer', 'deliver', 'drop-safe']}
                  onChange={(value) => update(index, (next) => { next.accidentalPickup = value; })} />
                <SelectField label="drop recovery" value={policy.dropRecovery}
                  values={['same-carrier', 'nearest-authorized', 'guard-until-safe']}
                  onChange={(value) => update(index, (next) => { next.dropRecovery = value; })} />
                <SelectField label="unreachable" value={policy.unreachableFallback}
                  values={['hold', 'guard', 'alternate-core', 'regroup']}
                  onChange={(value) => update(index, (next) => { next.unreachableFallback = value; })} />
                <SelectField label="forward pass" value={policy.forwardPass ?? 'none'}
                  values={['none', 'relay-catcher']}
                  onChange={(value) => update(index, (next) => { next.forwardPass = value; })} />
              </div>
              <ChoiceGrid label="authorized carrier roles" values={roles} selected={policy.authorizedCarrierRoles}
                onChange={(selected) => update(index, (next) => { next.authorizedCarrierRoles = selected; })} />
              <ChoiceGrid label="escort groups" values={groups} selected={policy.escortGroups}
                onChange={(selected) => update(index, (next) => { next.escortGroups = selected; })} />
              <ChoiceGrid label="source Wells" values={['north', 'centre', 'south']} selected={policy.sourceWells}
                onChange={(selected) => update(index, (next) => { next.sourceWells = selected; })} />
              <fieldset className="mt-3 rounded-sm border border-arena-edge p-3">
                <legend className="t-micro px-1">Delivery geography</legend>
                {(policy.deliveryRoutes ?? []).map((entry, routeIndex) => (
                  <div key={routeIndex} className="mb-2 grid gap-2 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
                    <SelectField label="Core lifted in zone" value={entry.zone} values={zones}
                      onChange={(value) => update(index, (next) => { next.deliveryRoutes![routeIndex].zone = value; })} />
                    <SelectField label="walks route" value={entry.route} values={routes}
                      onChange={(value) => update(index, (next) => { next.deliveryRoutes![routeIndex].route = value; })} />
                    <button type="button" className="btn min-h-11" onClick={() => update(index, (next) => {
                      next.deliveryRoutes = next.deliveryRoutes?.filter((_, position) => position !== routeIndex);
                    })}>Remove</button>
                  </div>
                ))}
                <button type="button" className="btn min-h-11" disabled={(policy.deliveryRoutes?.length ?? 0) >= 8}
                  onClick={() => update(index, (next) => {
                    next.deliveryRoutes = [...(next.deliveryRoutes ?? []), { zone: zones[0] ?? '', route: routes[0] ?? '' }];
                  })}>Add delivery route</button>
              </fieldset>
              <fieldset className="mt-3 rounded-sm border border-arena-edge p-3">
                <legend className="t-micro px-1">Optional bait drop</legend>
                {policy.baitDrop ? <>
                  <SelectField label="trap zone" value={policy.baitDrop.zone} values={zones}
                    onChange={(value) => update(index, (next) => { next.baitDrop!.zone = value; })} />
                  <ChoiceGrid label="reclaim when all" values={predicates}
                    selected={baitPredicateIds(policy.baitDrop, draft.playbook.authoring)}
                    onChange={(selected) => update(index, (next) => {
                      next.baitDrop!.reclaimAll = directConditionGroups(
                        selected,
                        draft.playbook.authoring.predicates,
                      );
                      delete next.baitDrop!.reclaimConditionSetId;
                    })} />
                  <button type="button" className="btn min-h-11" onClick={() => update(index, (next) => { delete next.baitDrop; })}>
                    Remove bait drop
                  </button>
                </> : (
                  <button type="button" className="btn min-h-11" disabled={predicates.length === 0}
                    onClick={() => update(index, (next) => {
                    next.baitDrop = {
                      zone: zones[0] ?? '',
                      reclaimAll: directConditionGroups(
                        predicates.slice(0, 1),
                        draft.playbook.authoring.predicates,
                      ),
                    };
                  })}>Add bait drop</button>
                )}
              </fieldset>
              <InlineIssues issues={issues} prefix={`custodyPolicies.${index}`} />
            </article>
          ))}
        </div>
      </details>
    </section>
  );
}

function MapPlotter({ catalog, layout, onChange }: {
  catalog: TacticalSheetCatalog;
  layout: LayoutDocument;
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
    <section className="panel pad">
      <div className="flex flex-wrap items-start gap-2">
        <div className="mr-auto">
          <p className="lab">Map plotting</p>
          <h2 className="type-display mt-1 text-[21px]">Draw tactics on the hosted map</h2>
          <p className="t-meta mt-1">Drag waypoints, zone rectangles and anchor pins. Coordinates never need typing.</p>
        </div>
        <div className="flex rounded-sm border border-arena-edge p-1" aria-label="Binding preview">
          {(['west', 'east'] as const).map((value) => (
            <button key={value} type="button" className={clsx('btn min-h-11', side === value && 'btn-on')}
              onClick={() => setSide(value)}>{value} side</button>
          ))}
        </div>
      </div>
      <div className="mt-3 grid gap-3 lg:grid-cols-[220px_minmax(0,1fr)]">
        <div>
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
          <div className="mt-2 max-h-[310px] overflow-y-auto rounded-sm border border-arena-edge p-1">
            {(kind === 'route' ? routes : kind === 'zone' ? zones : anchors).map((entry) => {
              const id = kind === 'route' ? (entry as LayoutRoute).routeId
                : kind === 'zone' ? (entry as LayoutZone).zoneId : (entry as LayoutAnchor).anchorId;
              return <button key={id} type="button"
                className={clsx('mb-1 min-h-11 w-full rounded-sm px-2 text-left t-micro', selected === id && 'bg-cyan-300/15 text-cyan-100')}
                onClick={() => setSelectedId(id)}>{id}</button>;
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
            {selected && <button type="button" className="btn min-h-11 text-red-200" onClick={() => commit((next) => {
              if (kind === 'route') next.routes = next.routes.filter((entry) => entry.routeId !== selected);
              else if (kind === 'zone') next.zones = next.zones.filter((entry) => entry.zoneId !== selected);
              else next.anchors = next.anchors.filter((entry) => entry.anchorId !== selected);
              setSelectedId('');
            })}>Remove selected</button>}
            {kind === 'route' && selectedRoute && <>
              <NumberField label="corridor width" value={selectedRoute.corridorWidth} min={0} max={12}
                onChange={(value) => commit((next) => { next.routes.find((entry) => entry.routeId === selectedRoute.routeId)!.corridorWidth = value; })} />
              <button type="button" className="btn min-h-11" disabled={selectedRoute.waypoints.length === 0}
                onClick={() => commit((next) => { next.routes.find((entry) => entry.routeId === selectedRoute.routeId)!.waypoints.pop(); })}>
                Undo waypoint
              </button>
            </>}
          </div>
        </div>
        <div>
          <div className="overflow-hidden rounded-sm border border-arena-edge bg-[#070b0f]">
            <svg
              ref={svg}
              viewBox={`0 0 ${catalog.map.width} ${catalog.map.height}`}
              preserveAspectRatio="xMidYMid meet"
              className="block w-full touch-none select-none"
              style={{ aspectRatio: `${catalog.map.width} / ${catalog.map.height}` }}
              role="img"
              aria-label={`${side} binding tactical map editor`}
              onPointerDown={pointerDown}
              onPointerMove={pointerMove}
              onPointerUp={pointerUp}
              onPointerCancel={pointerUp}
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
            <span className="text-cyan-200">tap empty map · add point</span>
            <span className="text-pink-200">pink · anchor</span>
            <span className="text-amber-200">drag · zone rectangle</span>
            {side === 'east' && <span>rotated preview{previewRoute ? ` · ${previewRoute}` : ''}</span>}
            {side === 'east' && <span>switch west to edit geometry</span>}
          </div>
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
          <ChoiceGrid label="target preference · ordered by selection" values={['carrier', 'weakest', 'closest', 'strongest-threat', 'freshest']}
            selected={stringArray(object(fight.targets).prefer)}
            onChange={(value) => patch('targets', 'prefer', value.length ? value : undefined)} />
          <button type="button" className="btn mt-3 min-h-11" onClick={() => onChange(undefined)}>Use inherited/default fight</button>
        </>}
      </div>
    </details>
  );
}

function ConditionField({ label, value, predicates, onChange }: {
  label: string;
  value: string;
  predicates: string[];
  onChange: (value: string) => void;
}) {
  const [connector, setConnector] = useState<'and' | 'or'>('and');
  const listId = useId();
  return (
    <label className="block">
      <span className="t-micro mb-1 block">{label}</span>
      <input className="field min-h-11 font-mono text-xs" value={value}
        list={listId} onChange={(event) => onChange(event.target.value)} />
      <datalist id={listId}>{predicates.map((id) => <option key={id} value={id} />)}</datalist>
      <div className="mt-1 flex gap-1">
        <select aria-label={`${label} connector`} className="field min-h-11 w-[78px] px-1 py-1 text-xs"
          value={connector} onChange={(event) => setConnector(event.target.value as 'and' | 'or')}>
          <option>and</option><option>or</option>
        </select>
        <select aria-label={`Add predicate to ${label}`} className="field min-h-11 min-w-0 flex-1 px-1 py-1 text-xs"
          value="" onChange={(event) => {
            if (event.target.value) onChange(`${value ? `${value} ${connector} ` : ''}${event.target.value}`);
          }}>
          <option value="">add named fact…</option>
          {predicates.map((id) => <option key={id} value={id}>{id}</option>)}
        </select>
      </div>
    </label>
  );
}

function IssueSummary({ issues }: { issues: SheetIssue[] }) {
  const errors = issues.filter((issue) => issue.severity === 'error');
  const warnings = issues.filter((issue) => issue.severity === 'warning');
  if (!issues.length) return (
    <section className="rounded-sm border border-emerald-400/35 bg-emerald-400/5 px-3 py-2 t-body text-emerald-200">
      Client checks pass. Save still runs the authoritative shared compiler.
    </section>
  );
  return (
    <section className="panel pad" aria-live="polite">
      <p className="lab">Draft checks · {errors.length} errors · {warnings.length} warnings</p>
      <ul className="mt-2 max-h-40 overflow-y-auto pl-4 text-xs">
        {issues.map((issue, index) => (
          <li
            key={`${issue.path}:${index}`}
            data-sheet-issue={issue.severity}
            className={issue.severity === 'error' ? 'text-red-300' : 'text-amber-200'}
          >
            <code>{issue.path}</code> — {issue.message}
          </li>
        ))}
      </ul>
    </section>
  );
}

function InlineIssues({ issues, prefix }: { issues: SheetIssue[]; prefix: string }) {
  const selected = issues.filter((issue) => issue.path.startsWith(prefix));
  if (!selected.length) return null;
  return <ul className="mt-2 rounded-sm border border-red-400/25 bg-red-400/5 p-2 text-xs">
    {selected.map((issue, index) => <li key={index} className={issue.severity === 'error' ? 'text-red-300' : 'text-amber-200'}>
      {issue.message}
    </li>)}
  </ul>;
}

function ChoiceGrid({ label, values, selected, onChange }: {
  label: string;
  values: string[];
  selected: string[];
  onChange: (selected: string[]) => void;
}) {
  return (
    <fieldset className="mt-3">
      <legend className="t-micro">{label}</legend>
      <div className="mt-1 flex flex-wrap gap-x-3 gap-y-1">
        {values.map((value) => <Check key={value} label={value} checked={selected.includes(value)}
          onChange={(checked) => onChange(checked
            ? [...selected, value] : selected.filter((entry) => entry !== value))} />)}
      </div>
    </fieldset>
  );
}

function Check({ label, checked, onChange }: {
  label: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  return <label className="flex min-h-11 items-center gap-2 t-micro">
    <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
    {label}
  </label>;
}

function TextField({ label, value, onChange }: {
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return <label><span className="t-micro mb-1 block">{label}</span>
    <input className="field min-h-11" value={value} onChange={(event) => onChange(event.target.value)} />
  </label>;
}

function SelectField({ label, value, values, onChange }: {
  label: string;
  value: string;
  values: string[];
  onChange: (value: string) => void;
}) {
  return <label><span className="t-micro mb-1 block">{label}</span>
    <select className="field min-h-11" value={value} onChange={(event) => onChange(event.target.value)}>
      {values.map((entry) => <option key={entry} value={entry}>{entry || 'none'}</option>)}
    </select>
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
      {values.map((entry) => <option key={entry}>{entry}</option>)}
    </select>
  </label>;
}

function NumberField({ label, value, min, max, onChange }: {
  label: string;
  value: number;
  min: number;
  max: number;
  onChange: (value: number) => void;
}) {
  return <label><span className="t-micro mb-1 block">{label}</span>
    <input type="number" inputMode="numeric" className="field min-h-11" value={value} min={min} max={max}
      onChange={(event) => onChange(Number(event.target.value))} />
  </label>;
}

function OptionalNumber({ label, value, min, max, onChange }: {
  label: string;
  value: number | undefined;
  min: number;
  max: number;
  onChange: (value: number | undefined) => void;
}) {
  return <label><span className="t-micro mb-1 block">{label}</span>
    <input type="number" inputMode="numeric" className="field min-h-11" value={value ?? ''} min={min} max={max}
      placeholder="unset" onChange={(event) => onChange(event.target.value === '' ? undefined : Number(event.target.value))} />
  </label>;
}

function ReadOnlyField({ label, value }: { label: string; value: string }) {
  return <div><span className="t-micro mb-1 block">{label}</span>
    <div className="field flex min-h-11 items-center text-arena-material">{value}</div>
  </div>;
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

function download(name: string, source: string) {
  const url = URL.createObjectURL(new Blob([source], { type: 'application/json' }));
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = name;
  anchor.click();
  URL.revokeObjectURL(url);
}

function safeName(value: string) {
  return value.trim().replace(/[^a-z0-9-]+/gi, '-').replace(/^-|-$/g, '') || 'sheet';
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

function directConditionGroups(
  predicateIds: string[],
  predicates: Record<string, PredicateShape>,
): unknown[] {
  return [{
    all: predicateIds.map((id) => structuredClone(predicates[id])).filter(Boolean),
  }];
}

function baitPredicateIds(
  bait: CustodyPolicy['baitDrop'],
  authoring: SheetDraft['playbook']['authoring'],
): string[] {
  if (!bait) return [];
  const first = Array.isArray(bait.reclaimAll) ? object(bait.reclaimAll[0]) : {};
  const leaves = Array.isArray(first.all) ? first.all : [];
  return leaves.flatMap((leaf) => {
    const canonical = JSON.stringify(leaf);
    const found = Object.entries(authoring.predicates)
      .find(([, predicate]) => JSON.stringify(predicate) === canonical);
    return found ? [found[0]] : [];
  });
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

type Mutate = (change: (next: SheetDraft) => void) => void;
interface DragState { kind: PlotKind; id: string; index: number }
type PredicateShape = SheetDraft['playbook']['authoring']['predicates'][string];
