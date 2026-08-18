import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { TacticalSheetCatalog } from '../api';
import { ErrorState, LoadingState } from '../components/StateView';
import { errorMessage } from '../errorMessage';
import {
  useSaveTacticalSheet, useTacticalSheet, useTrialTacticalSheet,
} from '../queries';
import { IssueSummary } from './IssueSummary';
import { Modal } from './Modal';
import { PhaseStrip } from './PhaseStrip';
import { BotChip } from './BotChip';
import { RECIPES, instantiateRecipe, recipeById, type RecipeHints } from './recipes';
import { groupClause, summarize } from './summary';
import { TacticsMapCanvas, type GeometryEdit, type MapSelectRef } from './TacticsMapCanvas';
import {
  blankDocument, expectedGeometry, freshLocalId, identifierFromName,
  parseDocument, signatureMarkGeometry, stringifyDocument, validateDocument,
  sheetIssueFromCompilerError,
  type Build, type SheetIssue, type SquadBot, type Tactic, type TacticsDraft,
} from './tacticsDocument';
import { loadLocalDraft, useSheetDraft } from './useSheetDraft';
import { useMapTask, type MapTask } from './useMapTask';
import {
  CoresStep, FinishStep, GroupsStep, PhaseStep, ReleaseStep, ReviewStep,
  StartStep, type StepProps,
} from './WizardSteps';
import { BUILDS, POSTURES } from './tacticsDocument';
import {
  WIZARD_STEPS, createGate, stepDone, type WizardState, type WizardStepId,
} from './wizardState';

const BOT_NAMES = [
  'Vex', 'Solder', 'Gantry', 'Flux', 'Ratchet', 'Ember', 'Piston', 'Cinder',
  'Anvil', 'Dynamo', 'Socket', 'Bellows', 'Ingot', 'Crucible', 'Tinder', 'Brazier',
];

/**
 * The Tactics editor: rail (the Sheet), map (the editor), drawer (the
 * questions). A Sheet is an ordered list of whole-team Tactics; the wizard
 * asks Start → Groups → Stage → Release → Act → Cores → Finish → Review and
 * reopening a Tactic reopens those same answers.
 */
export default function TacticsEditorPage({ catalog, initialSheetId }: {
  catalog: TacticalSheetCatalog;
  initialSheetId: string | null;
}) {
  const navigate = useNavigate();
  const sheetQuery = useTacticalSheet(initialSheetId);
  const save = useSaveTacticalSheet();
  const trial = useTrialTacticalSheet();
  const { draft, mutate, beginGesture, undo, redo, replaceDraft } =
    useSheetDraft(null);
  const [wizard, setWizard] = useState<WizardState | null>(null);
  const { task, start: startTask, end: endTask } = useMapTask();
  const [selectedTacticId, setSelectedTacticId] = useState<string | null>(null);
  const [showAll, setShowAll] = useState(false);
  const [zoom, setZoom] = useState<1 | 1.5 | 2>(1);
  const [squadOpen, setSquadOpen] = useState(false);
  const [recipeOpen, setRecipeOpen] = useState(false);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [serverIssues, setServerIssues] = useState<SheetIssue[]>([]);
  const [notice, setNotice] = useState<string | null>(null);
  const bootstrapped = useRef(false);

  // Bootstrap the draft: a fresh sheet immediately; an existing one when its
  // server copy arrives, preferring this device's newer unsent local work.
  useEffect(() => {
    if (bootstrapped.current || draft) return;
    if (!initialSheetId) {
      bootstrapped.current = true;
      replaceDraft({
        localId: freshLocalId('local'),
        name: 'New Sheet',
        sheetId: null,
        revision: null,
        enterLadder: true,
        document: blankDocument(catalog),
      });
      return;
    }
    if (!sheetQuery.data) return;
    bootstrapped.current = true;
    const server = sheetQuery.data;
    void loadLocalDraft(`sheet-${server.id}`).then((stored) => {
      const serverDraft: TacticsDraft = {
        localId: `sheet-${server.id}`,
        name: server.name,
        sheetId: server.id,
        revision: server.revision,
        enterLadder: server.entrant.ladderOptedIn,
        document: parseDocument(server.tacticsJson),
      };
      if (stored && stored.draft.revision === server.revision
        && JSON.stringify(stored.draft.document) !== JSON.stringify(serverDraft.document)) {
        setNotice('Restored this device’s newer draft.');
        replaceDraft({ ...stored.draft, enterLadder: serverDraft.enterLadder });
      } else {
        replaceDraft(serverDraft);
      }
    });
  }, [catalog, draft, initialSheetId, replaceDraft, sheetQuery.data]);

  const clientIssues = useMemo(() => {
    if (!draft) return [];
    return validateDocument(viewDocument(draft, wizard), catalog);
  }, [catalog, draft, wizard]);
  const issues = [...serverIssues, ...clientIssues];
  const blocking = clientIssues.some((issue) => issue.severity === 'error')
    || wizard !== null;

  const changeWizard = useCallback((apply: (tactic: Tactic) => void) => {
    setWizard((current) => {
      if (!current) return current;
      const tactic = structuredClone(current.tactic);
      apply(tactic);
      return { ...current, tactic };
    });
  }, []);

  // Undo/redo and task escape hatches, with a real dependency array.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement;
      if (target.tagName === 'INPUT' || target.tagName === 'SELECT' || target.tagName === 'TEXTAREA') return;
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'z') {
        event.preventDefault();
        if (event.shiftKey) redo();
        else undo();
      } else if (event.key === 'Escape' && task) {
        endTask();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [endTask, redo, task, undo]);

  if (initialSheetId && sheetQuery.isError) {
    return <ErrorState error={sheetQuery.error} onRetry={() => void sheetQuery.refetch()} />;
  }
  if (!draft) return <LoadingState label="Opening the sheet…" />;

  const document = viewDocument(draft, wizard);
  const editingTacticId = wizard?.tactic.id ?? null;

  const commitWizard = () => {
    if (!wizard) return;
    const finished = wizard.tactic;
    mutate((next) => {
      next.document.squad = document.squad;
      if (wizard.editing && wizard.originalId) {
        const index = next.document.tactics.findIndex((value) => value.id === wizard.originalId);
        if (index >= 0) next.document.tactics[index] = finished;
      } else {
        next.document.tactics.push(finished);
      }
    });
    setWizard(null);
    endTask();
    setSelectedTacticId(finished.id);
    setNotice(wizard.editing ? 'Tactic updated.' : `“${finished.name}” added to the Sheet.`);
  };

  const openWizard = (tactic: Tactic, hints: RecipeHints, editing: boolean) => {
    setWizard({
      tactic: structuredClone(tactic),
      hints,
      step: editing ? 'review' : 'start',
      editing,
      originalId: editing ? tactic.id : null,
      armedSlot: null,
    });
    setSelectedTacticId(null);
    setServerIssues([]);
  };

  const stepProps: StepProps = {
    catalog,
    document,
    wizard: wizard!,
    task,
    change: changeWizard,
    arm: (slot) => setWizard((current) => current && { ...current, armedSlot: slot }),
    startPhaseTask: (groupId, phase) => {
      const group = wizard?.tactic.groups.find((value) => value.id === groupId);
      if (!group || !wizard) return;
      const geo = phase === 'stage' ? group.stage : group.act;
      const draw = drawKindFor(geo.kind);
      if (!draw) return;
      startTask({
        kind: 'phase', tacticId: wizard.tactic.id, groupId, phase, draw,
        hint: wizard.hints.groups.get(groupId) ?? null,
        needed: group.slots.length,
      });
    },
    startSigTask: (groupId, phase, classId) => {
      if (!wizard) return;
      const geometry = signatureMarkGeometry(catalog, classId);
      if (!geometry) return;
      startTask({
        kind: 'sig', tacticId: wizard.tactic.id, groupId, phase, classId,
        draw: geometry.kind === 'points' ? 'posts' : 'route',
        max: geometry.max,
      });
    },
    startWatchTask: (which) => {
      if (!wizard) return;
      startTask({ kind: 'watch', tacticId: wizard.tactic.id, which });
    },
    endTask,
  };

  const onTilePicked = (x: number, y: number) => {
    if (!task || !wizard) return;
    changeWizard((tactic) => {
      if (task.kind === 'phase') {
        const group = tactic.groups.find((value) => value.id === task.groupId);
        if (!group) return;
        const phase = task.phase === 'stage' ? group.stage : group.act;
        if (task.draw === 'posts') {
          phase.posts = phase.posts ?? [];
          const existing = phase.posts.findIndex((post) => post.at[0] === x && post.at[1] === y);
          if (existing >= 0) phase.posts.splice(existing, 1);
          else if (phase.posts.length < Math.max(1, group.slots.length)) {
            phase.posts.push({ at: [x, y], facing: x < document.mapWidth / 2 ? 'e' : 'w' });
          }
        } else if (task.draw === 'route') {
          phase.path = [...(phase.path ?? []), [x, y]];
        } else if (task.draw === 'point') {
          phase.point = [x, y];
        }
      } else if (task.kind === 'sig') {
        const group = tactic.groups.find((value) => value.id === task.groupId);
        if (!group) return;
        group.sigs = group.sigs ?? {};
        group.sigs[task.phase] = group.sigs[task.phase] ?? {};
        const store = group.sigs[task.phase]!;
        const geometry = signatureMarkGeometry(catalog, task.classId)!;
        const mark = store[task.classId];
        const points = mark ? ('points' in mark ? mark.points : mark.line) : [];
        const existing = points.findIndex((point) => point[0] === x && point[1] === y);
        if (existing >= 0) points.splice(existing, 1);
        else if (points.length < task.max) points.push([x, y]);
        store[task.classId] = geometry.kind === 'points'
          ? { points } : { line: points };
      }
    });
    // Point tasks and full mark/post sets complete themselves.
    if (task.kind === 'phase' && task.draw === 'point') endTask();
    if (task.kind === 'phase' && task.draw === 'posts') {
      const group = wizard.tactic.groups.find((value) => value.id === task.groupId);
      const placed = (group && task.phase === 'stage' ? group.stage.posts?.length ?? 0 : 0) + 1;
      if (group && placed >= Math.max(1, group.slots.length)) endTask();
    }
  };

  const onRectDrawn = (rect: [number, number, number, number]) => {
    if (!task) return;
    changeWizard((tactic) => {
      if (task.kind === 'watch') {
        const conds = task.which === 'start' ? tactic.start.conds : tactic.release.conds;
        if (conds?.enemiesAt) conds.enemiesAt.zone = { rect };
      } else if (task.kind === 'phase') {
        const group = tactic.groups.find((value) => value.id === task.groupId);
        if (!group) return;
        (task.phase === 'stage' ? group.stage : group.act).rect = rect;
      }
    });
    endTask();
  };

  const onGeometryLive = (edit: GeometryEdit) => {
    if (wizard && wizard.tactic.id === edit.tacticId) {
      changeWizard(edit.apply);
    }
  };

  const onSelect = (ref: MapSelectRef) => {
    if (wizard && wizard.tactic.id === ref.tacticId) {
      const step: WizardStepId = ref.part === 'group' ? 'stage'
        : ref.part === 'cores' ? 'cores'
          : ref.part === 'finish' ? 'finish'
            : ref.part;
      setWizard({ ...wizard, step });
      return;
    }
    setSelectedTacticId(ref.tacticId);
  };

  const saveSheet = () => {
    setServerIssues([]);
    const trimmed = draft.name.trim();
    const documentToSave: typeof draft.document = {
      ...draft.document,
      sheetId: identifierFromName(trimmed, 'sheet'),
    };
    save.mutate({
      sheetId: draft.sheetId ?? undefined,
      body: {
        name: trimmed || 'Untitled sheet',
        expectedRevision: draft.sheetId ? draft.revision : null,
        tacticsJson: stringifyDocument(documentToSave),
        enterLadder: draft.enterLadder,
      },
    }, {
      onSuccess: (saved) => {
        replaceDraft({
          localId: `sheet-${saved.id}`,
          name: saved.name,
          sheetId: saved.id,
          revision: saved.revision,
          enterLadder: saved.entrant.ladderOptedIn,
          document: parseDocument(saved.tacticsJson),
        });
        setNotice(`Saved · revision ${saved.revision}.`);
        if (!draft.sheetId) navigate(`/entrants/sheets/${saved.id}/edit`, { replace: true });
      },
      onError: (cause) => {
        setServerIssues([sheetIssueFromCompilerError(
          errorMessage(cause, 'The server rejected this sheet.'))]);
      },
    });
  };

  const practice = () => {
    if (!draft.sheetId) {
      setNotice('Save the sheet first, then practice.');
      return;
    }
    trial.mutate({
      sheetId: draft.sheetId,
      body: { stockSheetId: catalog.stockOpponents[0]?.id ?? 'tempered-standard-v2', seed: null },
    }, {
      onSuccess: (match) => navigate(`/matches/${match.id}`),
      onError: (cause) => setNotice(errorMessage(cause, 'Practice could not start.')),
    });
  };

  const gate = wizard ? createGate(wizard, document) : null;
  const selected = selectedTacticId
    ? draft.document.tactics.find((value) => value.id === selectedTacticId) ?? null
    : null;
  const botName = (slot: number) => document.squad[slot]?.name ?? null;

  return (
    <div className="grid gap-3 lg:grid-cols-[280px_minmax(0,1fr)_360px]">
      {/* ------------------------------------------------------- the rail --- */}
      <section className="flex flex-col gap-3">
        <div className="panel pad">
          <div className="mb-2 flex items-center gap-2">
            <span className="lab">The Sheet</span>
            <span className="pill">{document.tactics.length} tactic{document.tactics.length === 1 ? '' : 's'}</span>
            <span className="grow" />
            <button type="button" className="btn btn-primary" disabled={blocking || save.isPending}
              onClick={saveSheet} data-testid="save-sheet">
              {save.isPending ? 'Saving…' : draft.sheetId ? 'Save' : 'Save sheet'}
            </button>
          </div>
          <input className="field mb-2 w-full" value={draft.name} aria-label="Sheet name"
            onChange={(event) => mutate((next) => { next.name = event.target.value; })} />
          <label className="t-micro mb-2 flex items-center gap-2 text-arena-dim">
            <input type="checkbox" checked={draft.enterLadder}
              onChange={(event) => mutate((next) => { next.enterLadder = event.target.checked; })} />
            Field for ranked Season play
          </label>
          <div className="flex gap-2">
            <button type="button" className="btn btn-secondary grow" disabled={wizard !== null}
              onClick={() => setRecipeOpen(true)} data-testid="create-tactic">
              ＋ Create Tactic
            </button>
            <button type="button" className="btn" disabled={!draft.sheetId || trial.isPending}
              onClick={practice}>
              {trial.isPending ? 'Starting…' : 'Practice'}
            </button>
          </div>
          {notice && <p className="t-micro mt-2 text-arena-material" role="status">{notice}</p>}
        </div>
        <IssueSummary issues={issues} />
        <div className="flex flex-col gap-2" data-testid="tactic-list">
          {document.tactics.length === 0 && !wizard && (
            <div className="panel pad border-dashed t-meta">
              No Tactics yet. The Sheet runs top to bottom — every Tactic moves all eight bots.
            </div>
          )}
          {draft.document.tactics.map((tactic, index) => (
            <TacticCard key={tactic.id} tactic={tactic} index={index}
              total={draft.document.tactics.length}
              selected={tactic.id === selectedTacticId && !wizard}
              confirmDelete={confirmDeleteId === tactic.id}
              botName={botName}
              onSelect={() => {
                if (wizard) return;
                setSelectedTacticId(tactic.id === selectedTacticId ? null : tactic.id);
                setConfirmDeleteId(null);
              }}
              onMove={(delta) => mutate((next) => {
                const from = next.document.tactics.findIndex((value) => value.id === tactic.id);
                const to = from + delta;
                if (to < 0 || to >= next.document.tactics.length) return;
                const [moved] = next.document.tactics.splice(from, 1);
                next.document.tactics.splice(to, 0, moved);
              })}
              onEdit={() => openWizard(tactic, { groups: new Map(), targetSizes: new Map() }, true)}
              onDelete={() => {
                if (confirmDeleteId !== tactic.id) {
                  setConfirmDeleteId(tactic.id);
                  return;
                }
                mutate((next) => {
                  next.document.tactics = next.document.tactics.filter((value) => value.id !== tactic.id);
                });
                setConfirmDeleteId(null);
                if (selectedTacticId === tactic.id) setSelectedTacticId(null);
              }} />
          ))}
        </div>
        <SquadRail catalog={catalog} squad={document.squad}
          onOpen={() => setSquadOpen(true)} />
      </section>

      {/* -------------------------------------------------------- the map --- */}
      <section className="flex min-w-0 flex-col gap-2">
        <div className="flex items-center gap-2">
          <span className="lab">Tapline — your side is west</span>
          <span className="grow" />
          <label className="t-micro flex cursor-pointer items-center gap-1.5 text-arena-dim">
            <input type="checkbox" checked={showAll}
              onChange={(event) => setShowAll(event.target.checked)} />
            Show every Tactic
          </label>
          <span className="flex gap-0.5 rounded-sm border border-arena-edge p-0.5">
            {([1, 1.5, 2] as const).map((level) => (
              <button key={level} type="button"
                className={`btn !min-h-7 !border-0 ${zoom === level ? 'btn-on' : ''}`}
                onClick={() => setZoom(level)}>
                {level === 1 ? 'Fit' : `${level}×`}
              </button>
            ))}
          </span>
        </div>
        <div className={`overflow-auto rounded-sm border border-arena-edge bg-[#06090c] ${zoom !== 1 ? 'max-h-[80vh]' : ''}`}>
          <div style={{ width: `${zoom * 100}%` }}>
            <TacticsMapCanvas
              catalog={catalog}
              document={document}
              editingTacticId={editingTacticId}
              selectedTacticId={selectedTacticId}
              showAll={showAll}
              task={task}
              hints={wizard?.hints.groups ?? null}
              onTilePicked={onTilePicked}
              onRectDrawn={onRectDrawn}
              onGeometryLive={onGeometryLive}
              onGestureStart={beginGesture}
              onGestureEnd={() => undefined}
              onSelect={onSelect}
            />
          </div>
        </div>
        {task && <TaskBanner task={task} wizard={wizard} onDone={endTask}
          onUndoPoint={() => changeWizard((tactic) => {
            if (task.kind === 'phase') {
              const group = tactic.groups.find((value) => value.id === task.groupId);
              const phase = group && (task.phase === 'stage' ? group.stage : group.act);
              if (!phase) return;
              if (task.draw === 'posts') phase.posts?.pop();
              if (task.draw === 'route') phase.path?.pop();
            } else if (task.kind === 'sig') {
              const group = tactic.groups.find((value) => value.id === task.groupId);
              const mark = group?.sigs?.[task.phase]?.[task.classId];
              if (mark) ('points' in mark ? mark.points : mark.line).pop();
            }
          })} />}
      </section>

      {/* ----------------------------------------------------- the drawer --- */}
      <section className="flex flex-col gap-2" data-testid="drawer">
        {wizard ? (
          <>
            <div className="flex flex-wrap gap-1" data-testid="wizard-steps">
              {WIZARD_STEPS.map((step) => (
                <button key={step.id} type="button"
                  className={`btn !min-h-7 ${wizard.step === step.id ? 'btn-on' : ''}`}
                  data-done={stepDone(wizard, step.id)}
                  onClick={() => { setWizard({ ...wizard, step: step.id }); endTask(); }}>
                  {stepDone(wizard, step.id) ? '· ' : ''}{step.label}
                </button>
              ))}
            </div>
            <div className="flex items-center gap-2">
              <h2 className="type-display text-[18px]">{wizard.tactic.name}</h2>
              <span className="pill">{recipeById(wizard.tactic.recipe).label}</span>
              <span className="grow" />
              <button type="button" className="btn" onClick={() => {
                setWizard(null);
                endTask();
                if (wizard.editing) setSelectedTacticId(wizard.originalId);
              }}>Cancel</button>
            </div>
            <div className="flex flex-col gap-2.5 overflow-y-auto pr-1" style={{ maxHeight: '72vh' }}>
              {wizard.step === 'start' && <StartStep {...stepProps} />}
              {wizard.step === 'groups' && <GroupsStep {...stepProps} />}
              {wizard.step === 'stage' && <PhaseStep {...stepProps} phase="stage" />}
              {wizard.step === 'release' && <ReleaseStep {...stepProps} />}
              {wizard.step === 'act' && <PhaseStep {...stepProps} phase="act" />}
              {wizard.step === 'cores' && <CoresStep {...stepProps} />}
              {wizard.step === 'finish' && <FinishStep {...stepProps} />}
              {wizard.step === 'review' && <ReviewStep {...stepProps} />}
            </div>
            <div className="flex items-center gap-2 border-t border-arena-edge pt-2">
              <button type="button" className="btn"
                disabled={wizard.step === WIZARD_STEPS[0].id}
                onClick={() => stepBy(-1)}>‹ Back</button>
              <span className="t-micro grow text-arena-hot">
                {wizard.step === 'review' && gate ? gate : ''}
              </span>
              {wizard.step === 'review'
                ? <button type="button" className="btn btn-primary" disabled={gate !== null}
                  onClick={commitWizard} data-testid="commit-tactic">
                  {wizard.editing ? 'Save changes' : 'Create Tactic'}
                </button>
                : <button type="button" className="btn btn-secondary" onClick={() => stepBy(1)}
                  data-testid="wizard-next">Next ›</button>}
            </div>
          </>
        ) : selected ? (
          <TacticInspector tactic={selected} botName={botName}
            catalog={catalog} document={document}
            onClose={() => setSelectedTacticId(null)}
            onEdit={() => openWizard(selected, { groups: new Map(), targetSizes: new Map() }, true)} />
        ) : (
          <DrawerIntro />
        )}
      </section>

      {squadOpen && (
        <SquadModal catalog={catalog} squad={document.squad}
          onClose={() => setSquadOpen(false)}
          onChange={(squad) => mutate((next) => { next.document.squad = squad; })} />
      )}
      {recipeOpen && (
        <Modal onClose={() => setRecipeOpen(false)} wide>
          <div className="mb-2 flex items-center gap-2">
            <h2 className="type-display text-[16px]">New Tactic</h2>
            <span className="t-meta">Recipes prefill the same structure — they are shortcuts, not systems.</span>
            <span className="grow" />
            <button type="button" className="btn" onClick={() => setRecipeOpen(false)}>✕</button>
          </div>
          <div className="grid gap-2 sm:grid-cols-2" data-testid="recipe-chooser">
            {RECIPES.map((recipe) => (
              <button key={recipe.id} type="button" className="choice-card text-left"
                data-testid={`recipe-${recipe.id}`}
                onClick={() => {
                  const { tactic, hints } = instantiateRecipe(recipe, draft.document);
                  setRecipeOpen(false);
                  openWizard(tactic, hints, false);
                }}>
                <span className="flex items-center gap-2">
                  <span className="text-arena-material">{recipe.icon}</span>
                  <span className="text-[13px] font-semibold">{recipe.label}</span>
                </span>
                <span className="mt-0.5 block t-micro text-arena-dim">{recipe.tagline}</span>
                <span className="mt-1 flex flex-wrap gap-1">
                  {recipe.groups.map((group) => (
                    <span key={group.name} className="pill !text-[9px]">{group.name}{group.size ? ` · ${group.size}` : ''}</span>
                  ))}
                </span>
              </button>
            ))}
          </div>
        </Modal>
      )}
    </div>
  );

  function stepBy(delta: number) {
    if (!wizard) return;
    const index = WIZARD_STEPS.findIndex((step) => step.id === wizard.step);
    const next = WIZARD_STEPS[Math.min(WIZARD_STEPS.length - 1, Math.max(0, index + delta))];
    setWizard({ ...wizard, step: next.id });
    endTask();
  }
}

/** The wizard's working tactic spliced into the document, so the map and the
 * validators always see what the player currently means. */
function viewDocument(draft: TacticsDraft, wizard: WizardState | null) {
  if (!wizard) return draft.document;
  const document = structuredClone(draft.document);
  if (wizard.editing && wizard.originalId) {
    const index = document.tactics.findIndex((value) => value.id === wizard.originalId);
    if (index >= 0) document.tactics[index] = wizard.tactic;
    else document.tactics.push(wizard.tactic);
  } else {
    document.tactics.push(wizard.tactic);
  }
  return document;
}

function drawKindFor(kind: Tactic['groups'][number]['stage']['kind']) {
  const expected = expectedGeometry(kind);
  return expected === 'posts' ? 'posts'
    : expected === 'rect' ? 'rect'
      : expected === 'path' ? 'route'
        : expected === 'point' ? 'point'
          : null;
}

function TacticCard({ tactic, index, total, selected, confirmDelete, botName, onSelect, onMove, onEdit, onDelete }: {
  tactic: Tactic;
  index: number;
  total: number;
  selected: boolean;
  confirmDelete: boolean;
  botName: (slot: number) => string | null;
  onSelect: () => void;
  onMove: (delta: number) => void;
  onEdit: () => void;
  onDelete: () => void;
}) {
  return (
    <article
      className={`flex cursor-pointer gap-2 rounded-sm border p-2 transition-colors ${selected
        ? 'border-arena-material/70 bg-arena-deep/60'
        : 'border-arena-edge bg-arena-deep/30 hover:border-arena-dim'}`}
      onClick={onSelect}
      data-testid={`tactic-card-${index + 1}`}
    >
      <span className="val pt-0.5">{index + 1}</span>
      <div className="min-w-0 grow">
        <div className="flex items-center gap-1.5">
          <span className="truncate text-[13px] font-semibold">{tactic.name}</span>
          <span className="pill !text-[9px]">{recipeById(tactic.recipe).label}</span>
        </div>
        <PhaseStrip tactic={tactic} />
        <p className="t-micro mt-1 text-arena-dim">{summarize(tactic, botName)}</p>
      </div>
      <div className="flex flex-col gap-0.5" onClick={(event) => event.stopPropagation()} role="presentation">
        <button type="button" className="btn !min-h-6 !px-1.5" title="Move up" disabled={index === 0}
          onClick={() => onMove(-1)}>▲</button>
        <button type="button" className="btn !min-h-6 !px-1.5" title="Move down" disabled={index === total - 1}
          onClick={() => onMove(1)}>▼</button>
        <button type="button" className="btn !min-h-6 !px-1.5" title="Edit" onClick={onEdit}>✎</button>
        <button type="button" className={`btn !min-h-6 !px-1.5 ${confirmDelete ? 'text-arena-hot' : ''}`}
          title={confirmDelete ? 'Confirm delete' : 'Delete'} onClick={onDelete}>
          {confirmDelete ? '✓?' : '✕'}
        </button>
      </div>
    </article>
  );
}

function TacticInspector({ tactic, botName, catalog, document, onClose, onEdit }: {
  tactic: Tactic;
  botName: (slot: number) => string | null;
  catalog: TacticalSheetCatalog;
  document: ReturnType<typeof viewDocument>;
  onClose: () => void;
  onEdit: () => void;
}) {
  return <>
    <div className="flex items-center gap-2">
      <h2 className="type-display text-[18px]">{tactic.name}</h2>
      <span className="pill">{recipeById(tactic.recipe).label}</span>
      <span className="grow" />
      <button type="button" className="btn" onClick={onClose}>✕</button>
    </div>
    <div className="panel-feature pad">
      <div className="lab mb-1.5">The whole operation</div>
      <p className="t-body leading-relaxed">{summarize(tactic, botName)}</p>
      <PhaseStrip tactic={tactic} big />
    </div>
    {tactic.groups.map((group) => (
      <div key={group.id} className="panel pad">
        <div className="mb-1 flex items-center gap-2">
          <b className="t-body">{group.name}</b>
          <span className="val">{group.slots.length} bots</span>
          <span className="grow" />
          <span className="pill">
            {POSTURES.find((value) => value.id === group.stage.posture)?.label}
            {group.act.kind !== 'none' && ` → ${POSTURES.find((value) => value.id === group.act.posture)?.label}`}
          </span>
        </div>
        <p className="t-meta">{groupClause(group, true)}.</p>
        <div className="mt-1.5 flex flex-wrap gap-1">
          {group.slots.map((slot) => document.squad[slot] && (
            <BotChip key={slot} bot={document.squad[slot]} catalog={catalog} />
          ))}
        </div>
      </div>
    ))}
    <button type="button" className="btn btn-secondary" onClick={onEdit} data-testid="edit-tactic">
      Edit this Tactic
    </button>
    <p className="t-micro text-arena-dim">Editing reopens the same answers you gave — the map overlays stay live.</p>
  </>;
}

function DrawerIntro() {
  return <>
    <div className="panel-feature pad">
      <div className="lab mb-1.5">A Sheet is a plan</div>
      <p className="t-body leading-relaxed">
        An ordered list of whole-team <b>Tactics</b>. Each one splits your eight bots into
        temporary <b>Groups</b>, stages them on the map, releases them together, and finishes
        on outcomes you choose.
      </p>
    </div>
    <div className="panel pad">
      <div className="lab mb-1.5">Start here</div>
      <ol className="t-meta list-decimal space-y-1 pl-5">
        <li>Fill the eight squad slots on the left.</li>
        <li>Hit <b>Create Tactic</b> and pick a recipe.</li>
        <li>Draw where each Group stands, straight on the map.</li>
      </ol>
    </div>
    <p className="t-micro text-arena-dim">The map is the editor — the drawer only asks the questions.</p>
  </>;
}

function TaskBanner({ task, wizard, onDone, onUndoPoint }: {
  task: MapTask;
  wizard: WizardState | null;
  onDone: () => void;
  onUndoPoint: () => void;
}) {
  const group = task.kind !== 'watch' && wizard
    ? wizard.tactic.groups.find((value) => value.id === task.groupId)
    : null;
  const what = task.kind === 'sig'
    ? `Placing ${group?.name ?? 'the group'}’s signature marks — ${task.draw === 'route' ? 'click the lane points in order' : `click up to ${task.max} tiles`}`
    : task.kind === 'watch'
      ? task.which === 'start'
        ? 'Drag the box this Tactic watches to Start — it applies once enemies gather inside'
        : 'Drag the box the Release watches — enemies are counted inside it'
      : {
        posts: `Placing ${group?.name ?? ''} posts — click ${group?.slots.length || 'a few'} tiles`,
        rect: `Drag a box for ${group?.name ?? ''}’s ${task.phase === 'stage' ? 'staging area' : 'target area'}`,
        route: `Click waypoints for ${group?.name ?? ''}’s ${task.phase === 'stage' ? 'line' : 'route'}, in travel order`,
        point: `Click ${group?.name ?? ''}’s ${task.phase === 'stage' ? 'approach point' : 'destination'}`,
      }[task.draw];
  const undoable = (task.kind === 'phase' && (task.draw === 'route' || task.draw === 'posts'))
    || task.kind === 'sig';
  return (
    <div className="flex items-center gap-2 rounded-sm border border-arena-material/50 bg-arena-deep/70 px-2 py-1.5"
      data-testid="task-banner" role="status">
      <span className="t-micro">{what}</span>
      {task.kind === 'phase' && task.hint && <span className="t-micro text-arena-dim">· {task.hint.text}</span>}
      <span className="grow" />
      {undoable && <button type="button" className="btn" onClick={onUndoPoint}>Undo</button>}
      <button type="button" className="btn btn-secondary" onClick={onDone}>Done</button>
    </div>
  );
}

function SquadRail({ catalog, squad, onOpen }: {
  catalog: TacticalSheetCatalog;
  squad: SquadBot[];
  onOpen: () => void;
}) {
  return (
    <div className="panel pad" data-testid="squad-rail">
      <div className="mb-1.5 flex items-center gap-2">
        <span className="lab">Squad</span>
        <span className="val">{squad.length}/8</span>
        <span className="grow" />
        <button type="button" className="btn" onClick={onOpen} data-testid="open-squad">Pick eight</button>
      </div>
      <div className="grid grid-cols-2 gap-1">
        {Array.from({ length: 8 }, (_, slot) => {
          const bot = squad[slot];
          if (!bot) {
            return <button key={slot} type="button" onClick={onOpen}
              className="min-h-9 rounded-sm border border-dashed border-arena-edge t-micro text-arena-dim">
              ＋ Slot {slot + 1}
            </button>;
          }
          const entry = catalog.classes.find((value) => value.id === bot.class);
          return <button key={slot} type="button" onClick={onOpen}
            className="flex min-h-9 flex-col items-start rounded-sm border border-arena-edge px-1.5 py-1 text-left">
            <span className="t-micro font-semibold">{bot.name}</span>
            <span className="text-[10px] text-arena-dim">{entry?.name ?? bot.class} · {BUILDS.find((value) => value.id === bot.build)?.label}</span>
          </button>;
        })}
      </div>
      <p className="t-micro mt-1.5 text-arena-dim">Click a slot to pick its class and build preset.</p>
    </div>
  );
}

function SquadModal({ catalog, squad, onClose, onChange }: {
  catalog: TacticalSheetCatalog;
  squad: SquadBot[];
  onClose: () => void;
  onChange: (squad: SquadBot[]) => void;
}) {
  const [selected, setSelected] = useState<number | null>(null);
  const filled = squad.length;
  const makeBot = (slot: number, classId: string): SquadBot => {
    const used = new Set(squad.map((bot) => bot.name));
    const name = BOT_NAMES.find((candidate) => !used.has(candidate)) ?? `Unit ${slot + 1}`;
    return { slot, class: classId, build: 'balanced', name };
  };
  const normalize = (bots: SquadBot[]) =>
    bots.map((bot, index) => ({ ...bot, slot: index }));
  const pickClass = (classId: string) => {
    if (selected !== null && squad[selected]) {
      onChange(normalize(squad.map((bot, index) =>
        index === selected ? { ...bot, class: classId } : bot)));
      setSelected(null);
      return;
    }
    if (filled >= 8) return;
    onChange(normalize([...squad, makeBot(filled, classId)]));
  };
  const counts = new Map<string, number>();
  for (const bot of squad) counts.set(bot.class, (counts.get(bot.class) ?? 0) + 1);
  return (
    <Modal onClose={onClose} wide>
      <div className="mb-2 flex items-center gap-2">
        <h2 className="type-display text-[16px]">Squad — pick eight</h2>
        <span className="val">{filled}/8</span>
        <span className="grow" />
        <button type="button" className="btn btn-primary" onClick={onClose} data-testid="squad-done">Done</button>
      </div>
      <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-4" data-testid="class-grid">
        {catalog.classes.map((entry) => {
          const capped = (counts.get(entry.id) ?? 0) >= catalog.maximumCopiesPerClass
            && selected === null;
          return (
            <button key={entry.id} type="button" className="choice-card text-left"
              disabled={!entry.unlocked || (capped && filled >= 8)}
              data-testid={`class-${entry.id}`}
              onClick={() => pickClass(entry.id)}>
              <span className="block text-[12px] font-semibold">
                {entry.name}{!entry.unlocked && ' 🔒'}
              </span>
              <span className="block t-micro text-arena-dim">{entry.signatureName}</span>
            </button>
          );
        })}
      </div>
      <div className="mt-3">
        <div className="lab mb-1">
          Your eight — a class click fills the next free slot{selected !== null ? `, or swaps slot ${selected + 1}` : ''}
        </div>
        <div className="flex flex-col gap-1">
          {squad.map((bot, index) => {
            const entry = catalog.classes.find((value) => value.id === bot.class);
            return (
              <div key={index}
                className={`flex min-h-9 items-center gap-2 rounded-sm border px-2 ${selected === index ? 'border-arena-material' : 'border-arena-edge'}`}>
                <button type="button" className="grow text-left"
                  onClick={() => setSelected(selected === index ? null : index)}>
                  <span className="t-micro font-semibold">{bot.name}</span>
                  <span className="t-micro text-arena-dim"> · {entry?.name ?? bot.class}</span>
                </button>
                <input className="field !min-h-7 w-24 !py-0 t-micro" value={bot.name}
                  aria-label="Bot name" maxLength={24}
                  onChange={(event) => onChange(normalize(squad.map((value, i) =>
                    i === index ? { ...value, name: event.target.value } : value)))} />
                <button type="button" className="pill"
                  title="Tap to cycle the build preset"
                  onClick={() => {
                    const order: Build[] = ['balanced', 'durable', 'aggressive', 'scout'];
                    const next = order[(order.indexOf(bot.build) + 1) % order.length];
                    onChange(normalize(squad.map((value, i) =>
                      i === index ? { ...value, build: next } : value)));
                  }}>
                  {BUILDS.find((value) => value.id === bot.build)?.label}
                </button>
                <button type="button" className="btn !min-h-7" title="Remove bot"
                  onClick={() => {
                    onChange(normalize(squad.filter((_, i) => i !== index)));
                    setSelected(null);
                  }}>✕</button>
              </div>
            );
          })}
          {filled === 0 && (
            <p className="t-micro text-arena-dim">New bots arrive <b>Balanced</b>. Select one to swap its
              class, tap the build tag to cycle it, or rename it in place.</p>
          )}
        </div>
      </div>
    </Modal>
  );
}
