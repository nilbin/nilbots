import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import type { TacticalSheetCatalog } from '../api';
import { ErrorState, LoadingState } from '../components/StateView';
import { errorMessage } from '../errorMessage';
import {
  useSaveTacticalSheet, useTacticalSheet, useTrialTacticalSheet,
} from '../queries';
import { IssueSummary } from './IssueSummary';
import { Modal } from './Modal';
import { PhaseStrip } from './PhaseStrip';
import {
  MapFirstBehaviorPanel, MapFirstBotPanel, MapFirstGroupToolbar,
  MapFirstGroupTrays, MapFirstTimeline, type MapFirstFocus,
} from './MapFirstChrome';
import { TacticsIcon, TacticsIconButton } from './TacticsIcons';
import {
  materializeSpatialJob, suggestFormationPosts, translatePostSelection,
} from './mapFirstSpatial';
import { classSprite, slotAccent } from './classArt';
import { RECIPES, instantiateRecipe, recipeById, type RecipeHints } from './recipes';
import { possessive, summarize } from './summary';
import {
  TacticsMapCanvas, watchConds, type GeometryEdit, type MapSelectRef,
} from './TacticsMapCanvas';
import {
  blankDocument, effectiveJobResolution, expectedGeometry, followMembership, freshLocalId, freshPhase, groupColor, identifierFromName,
  normalizeEditableDocumentForCatalog, postFacingIssue, reversedWayIn,
  planJobKindChange, signatureMarkGeometry, stringifyDocument, tilePlacementIssue,
  validateDocument, sheetIssueFromCompilerError,
  type Build, type Facing, type JobKind, type SheetIssue, type SquadBot,
  type Tactic, type TacticsDraft,
} from './tacticsDocument';
import { loadLocalDraft, useSheetDraft } from './useSheetDraft';
import { useMapTask, type MapTask } from './useMapTask';
import {
  CoresStep, GroupsStep, PhaseCustodyEditor,
  StartStep, TransitionEditor, type StepProps,
} from './WizardSteps';
import { BUILDS } from './tacticsDocument';
import {
  createGate, type WizardState,
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
  const { draft, mutate, undo, redo, replaceDraft } =
    useSheetDraft(null);
  const [wizard, setWizard] = useState<WizardState | null>(null);
  const wizardRef = useRef<WizardState | null>(null);
  wizardRef.current = wizard;
  const wizardUndoStack = useRef<Tactic[]>([]);
  const wizardRedoStack = useRef<Tactic[]>([]);
  const wizardGesture = useRef<Tactic | null>(null);
  const [wizardHistoryRevision, setWizardHistoryRevision] = useState(0);
  const { task, start: startTask, end: endMapTask } = useMapTask();
  const [selectedTacticId, setSelectedTacticId] = useState<string | null>(null);
  const [showAll, setShowAll] = useState(false);
  const [showAllPhases, setShowAllPhases] = useState(false);
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [selectedSlots, setSelectedSlots] = useState<number[]>([]);
  const [mapFirstPanel, setMapFirstPanel] = useState<MapFirstFocus | null>(null);
  const [showVision, setShowVision] = useState(false);
  const [showAttackRange, setShowAttackRange] = useState(false);
  const [zoom, setZoom] = useState<1 | 1.5 | 2>(1);
  const [squadOpen, setSquadOpen] = useState(false);
  const [recipeOpen, setRecipeOpen] = useState(false);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [serverIssues, setServerIssues] = useState<SheetIssue[]>([]);
  const [notice, setNotice] = useState<string | null>(null);
  const [toasts, setToasts] = useState<{ id: number; text: string; tone: 'ok' | 'hot' }[]>([]);
  const [dragTactic, setDragTactic] = useState<{ id: string; over: number | null; below: boolean }| null>(null);
  const toastSeq = useRef(0);
  const bootstrapped = useRef(false);
  const tacticWorkspaceRef = useRef<HTMLElement>(null);
  const contextDrawerRef = useRef<HTMLElement>(null);

  // The mock's toast rack: transient action feedback, self-clearing.
  const toast = useCallback((text: string, tone: 'ok' | 'hot' = 'ok') => {
    const id = ++toastSeq.current;
    setToasts((current) => [...current.slice(-3), { id, text, tone }]);
    window.setTimeout(() =>
      setToasts((current) => current.filter((value) => value.id !== id)), 2600);
  }, []);

  // A first save navigates to the sheet's own URL, remounting this page; the
  // "Saved" toast rides across in the navigation state.
  const location = useLocation();
  useEffect(() => {
    const state = location.state as { savedRevision?: number } | null;
    if (state?.savedRevision) {
      toast(`Saved · revision ${state.savedRevision}.`);
      navigate(location.pathname, { replace: true, state: null });
    }
  }, [location, navigate, toast]);

  /** On narrow layouts the drawer sits far below the map; arming a draw task
   * brings the map (and its floating banner) back on screen. */
  const revealMap = useCallback(() => {
    if (window.innerWidth >= 1024) return;
    window.document.querySelector('[data-testid="tactics-map"]')
      ?.scrollIntoView({ block: 'start', behavior: 'smooth' });
  }, []);

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
        document: normalizeEditableDocumentForCatalog(
          JSON.parse(server.tacticsJson), catalog),
      };
      // A device draft may predate the current schema: normalize it through
      // the same migration the server copy gets, and never let an unreadable
      // draft block opening the sheet.
      let restored: TacticsDraft | null = null;
      if (stored && stored.draft.revision === server.revision) {
        try {
          const document = normalizeEditableDocumentForCatalog(
            stored.draft.document, catalog);
          if (JSON.stringify(document) !== JSON.stringify(serverDraft.document)) {
            restored = {
              ...stored.draft, document,
              enterLadder: serverDraft.enterLadder,
            };
          }
        } catch {
          restored = null;
        }
      }
      if (restored) {
        setNotice('Restored this device’s newer draft.');
        replaceDraft(restored);
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

  const pushWizardUndo = useCallback((snapshot: Tactic) => {
    wizardUndoStack.current.push(structuredClone(snapshot));
    if (wizardUndoStack.current.length > 80) wizardUndoStack.current.shift();
    wizardRedoStack.current = [];
  }, []);

  /** Ordinary wizard mutation. During a map gesture the gesture owns the
   * single undo snapshot; outside one, every deliberate control change gets
   * its own local history entry. */
  const changeWizard = useCallback((apply: (tactic: Tactic) => void) => {
    setWizard((current) => {
      if (!current) return current;
      const tactic = structuredClone(current.tactic);
      apply(tactic);
      if (JSON.stringify(tactic) === JSON.stringify(current.tactic)) return current;
      if (!wizardGesture.current) pushWizardUndo(current.tactic);
      const next = { ...current, tactic };
      wizardRef.current = next;
      return next;
    });
  }, [pushWizardUndo]);

  const replaceWizardTactic = useCallback((tactic: Tactic) => {
    setWizard((current) => {
      if (!current || JSON.stringify(tactic) === JSON.stringify(current.tactic)) return current;
      pushWizardUndo(current.tactic);
      const next = { ...current, tactic };
      wizardRef.current = next;
      return next;
    });
  }, [pushWizardUndo]);

  /** Live map frames and draw points mutate the working tactic without
   * flooding history. begin/finishWizardGesture wrap them as one undo step. */
  const changeWizardLive = useCallback((apply: (tactic: Tactic) => void) => {
    setWizard((current) => {
      if (!current) return current;
      const tactic = structuredClone(current.tactic);
      apply(tactic);
      if (JSON.stringify(tactic) === JSON.stringify(current.tactic)) return current;
      const next = { ...current, tactic };
      wizardRef.current = next;
      return next;
    });
  }, []);

  const beginWizardGesture = useCallback(() => {
    if (!wizardGesture.current && wizardRef.current) {
      wizardGesture.current = structuredClone(wizardRef.current.tactic);
    }
  }, []);

  const finishWizardGesture = useCallback(() => {
    const before = wizardGesture.current;
    const current = wizardRef.current;
    wizardGesture.current = null;
    if (!before || !current
      || JSON.stringify(before) === JSON.stringify(current.tactic)) return;
    pushWizardUndo(before);
    setWizardHistoryRevision((value) => value + 1);
  }, [pushWizardUndo]);

  const cancelWizardGesture = useCallback(() => {
    const before = wizardGesture.current;
    wizardGesture.current = null;
    if (!before) return;
    setWizard((current) => {
      if (!current) return current;
      const next = { ...current, tactic: before };
      wizardRef.current = next;
      return next;
    });
    setWizardHistoryRevision((value) => value + 1);
  }, []);

  const resetWizardHistory = useCallback(() => {
    wizardUndoStack.current = [];
    wizardRedoStack.current = [];
    wizardGesture.current = null;
    setWizardHistoryRevision((value) => value + 1);
  }, []);

  const undoWizard = useCallback(() => {
    if (wizardGesture.current) {
      cancelWizardGesture();
      return;
    }
    setWizard((current) => {
      const previous = wizardUndoStack.current.pop();
      if (!current || !previous) return current;
      wizardRedoStack.current.push(structuredClone(current.tactic));
      const next = { ...current, tactic: previous };
      wizardRef.current = next;
      return next;
    });
    setWizardHistoryRevision((value) => value + 1);
  }, [cancelWizardGesture]);

  const redoWizard = useCallback(() => {
    if (wizardGesture.current) return;
    setWizard((current) => {
      const next = wizardRedoStack.current.pop();
      if (!current || !next) return current;
      wizardUndoStack.current.push(structuredClone(current.tactic));
      const restored = { ...current, tactic: next };
      wizardRef.current = restored;
      return restored;
    });
    setWizardHistoryRevision((value) => value + 1);
  }, []);

  // Finishing a post way-in drawn from the post OUTWARD flips it into travel
  // order, then commits the entire draw/drag as one local undo step.
  const endTask = useCallback(() => {
    const active = task;
    if (active?.kind === 'job' && active.field === 'via'
      && active.postIndex != null) {
      changeWizardLive((tactic) => {
        const post = tactic.phases[active.phaseIndex]
          ?.jobs[active.groupId]?.posts?.[active.postIndex!];
        if (!post) return;
        const flipped = reversedWayIn(post.via, post.at);
        if (flipped) {
          post.via = flipped;
          toast('Way in flipped to travel order — it ends at the post.');
        }
      });
    }
    if (active?.kind === 'carry') {
      changeWizardLive((tactic) => {
        const custody = tactic.phases[active.phaseIndex]?.custody;
        if (!custody?.via) return;
        const flipped = reversedWayIn(custody.via, [2, 5]);
        if (flipped) {
          custody.via = flipped;
          toast('Way home flipped to travel order — it ends at your reactor.');
        }
      });
    }
    endMapTask();
    // State updates above are queued; defer snapshot comparison until React
    // has committed the final route normalization.
    window.queueMicrotask(finishWizardGesture);
  }, [changeWizardLive, endMapTask, finishWizardGesture, task, toast]);

  const cancelTask = useCallback(() => {
    cancelWizardGesture();
    endMapTask();
  }, [cancelWizardGesture, endMapTask]);

  // Undo/redo and task escape hatches, with a real dependency array.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement;
      if (target.tagName === 'INPUT' || target.tagName === 'SELECT' || target.tagName === 'TEXTAREA') return;
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'z') {
        event.preventDefault();
        if (wizardRef.current) {
          if (event.shiftKey) redoWizard();
          else undoWizard();
        } else if (event.shiftKey) redo();
        else undo();
      } else if (event.key === 'Escape' && task) {
        cancelTask();
      } else if (event.key === 'Escape' && mapFirstPanel) {
        setMapFirstPanel(null);
      } else if (event.key === 'Enter' && task && task.kind !== 'watch'
        && task.draw === 'route') {
        endTask();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [cancelTask, endTask, mapFirstPanel, redo, redoWizard, task, undo, undoWizard]);

  // A modal backdrop used to swallow the first click on another map/timeline
  // target. Listening at the document level closes this drawer before that
  // target's own click runs, so context changes are genuinely one click.
  useEffect(() => {
    if (!mapFirstPanel) return;
    const closeOnOutsidePointer = (event: PointerEvent) => {
      const target = event.target;
      if (!(target instanceof Node) || contextDrawerRef.current?.contains(target)) return;
      if (target instanceof Element
        && target.closest('[data-map-first-context-trigger], [data-select]')) return;
      setMapFirstPanel(null);
      endTask();
    };
    window.document.addEventListener('pointerdown', closeOnOutsidePointer);
    return () => window.document.removeEventListener('pointerdown', closeOnOutsidePointer);
  }, [endTask, mapFirstPanel]);

  if (initialSheetId && sheetQuery.isError) {
    return <ErrorState error={sheetQuery.error} onRetry={() => void sheetQuery.refetch()} />;
  }
  if (!draft) return <LoadingState label="Opening the sheet…" />;

  const document = viewDocument(draft, wizard);
  const editingTacticId = wizard?.tactic.id ?? null;
  const tacticDirty = wizard
    ? !wizard.editing || JSON.stringify(wizard.tactic) !== JSON.stringify(
      draft.document.tactics.find((tactic) => tactic.id === wizard.originalId))
    : false;

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
    setMapFirstPanel(null);
    setSelectedSlots([]);
    endMapTask();
    resetWizardHistory();
    setSelectedTacticId(finished.id);
    toast(wizard.editing ? 'Tactic updated.' : `“${finished.name}” added to the Sheet.`);
  };

  /** Reopening an existing Tactic rebuilds its recipe's draw hints and target
   * sizes by group position, so edits keep the pulses and the distribute
   * weighting the recipe originally suggested. */
  const hintsFor = (tactic: Tactic): RecipeHints => {
    const recipe = recipeById(tactic.recipe);
    const hints: RecipeHints = { groups: new Map(), targetSizes: new Map() };
    recipe.groups.forEach((prefill, index) => {
      const group = tactic.groups[index];
      if (!group) return;
      if (prefill.hint) hints.groups.set(group.id, prefill.hint);
      hints.targetSizes.set(group.id, prefill.size);
    });
    return hints;
  };

  /** One-click prefilled Deliver Core: carrier from the runner classes,
   * escorts and screen auto-assigned, the screen's lane pre-drawn. */
  const addDeliverCore = () => {
    mutate((next) => {
      const { tactic } = instantiateRecipe(recipeById('deliver-core'), next.document, catalog);
      const squad = next.document.squad;
      const runners = ['relay', 'towline'];
      const carrierSlot = squad.find((bot) => runners.includes(bot.class))?.slot
        ?? squad[0]?.slot ?? 0;
      const rest = squad.map((bot) => bot.slot).filter((slot) => slot !== carrierSlot);
      tactic.groups[0].slots = [carrierSlot];
      tactic.groups[1].slots = rest.slice(0, 3);
      tactic.groups[2].slots = rest.slice(3);
      const screenJob = tactic.phases[0].jobs[tactic.groups[2].id];
      if (screenJob) screenJob.rect = [4, 10, 9, 16];
      tactic.delivery.preferredSlots = [carrierSlot];
      next.document.tactics.push(tactic);
    });
    toast('Deliver Core added — it runs whenever a carrier exists.');
  };

  const openWizard = (tactic: Tactic, hints: RecipeHints, editing: boolean) => {
    const assigned = tactic.groups.flatMap((group) => group.slots);
    const uniqueAssigned = new Set(assigned);
    const membershipComplete = document.squad.length === 8
      && assigned.length === 8 && uniqueAssigned.size === 8;
    resetWizardHistory();
    setWizard({
      tactic: structuredClone(tactic),
      hints,
      step: 'phases',
      phaseIndex: 0,
      editing,
      originalId: editing ? tactic.id : null,
      armedSlot: null,
    });
    setSelectedTacticId(null);
    setServerIssues([]);
    setSelectedGroupId(tactic.groups[0]?.id ?? null);
    setSelectedSlots([]);
    setMapFirstPanel(!editing && !membershipComplete ? { kind: 'groups' } : null);
    if (window.innerWidth < 1024) {
      window.setTimeout(() => {
        tacticWorkspaceRef.current?.focus({ preventScroll: true });
        tacticWorkspaceRef.current?.scrollIntoView({ block: 'start', behavior: 'smooth' });
      }, 0);
    }
  };

  const stepProps: StepProps = {
    catalog,
    document,
    wizard: wizard!,
    task,
    change: changeWizard,
    notify: toast,
    arm: (slot) => setWizard((current) => current && { ...current, armedSlot: slot }),
    selectPhase: (index) => setWizard((current) =>
      current && { ...current, phaseIndex: index }),
    startJobTask: (groupId, phaseIndex, field, drawOverride) => {
      const group = wizard?.tactic.groups.find((value) => value.id === groupId);
      if (!group || !wizard) return;
      const job = wizard.tactic.phases[phaseIndex]?.jobs[groupId];
      if (!job) return;
      const draw = field === 'via' ? 'route' : drawOverride ?? drawKindFor(job.kind);
      if (!draw) return;
      beginWizardGesture();
      startTask({
        kind: 'job', tacticId: wizard.tactic.id, groupId, phaseIndex, draw, field,
        // Recipe hints point at first-phase ground; showing one while drawing
        // a later phase would aim the player at the wrong box.
        hint: field === 'via' || phaseIndex > 0
          ? null : wizard.hints.groups.get(groupId) ?? null,
        needed: group.slots.length,
      });
      revealMap();
    },
    startPostViaTask: (groupId, phaseIndex, postIndex) => {
      if (!wizard) return;
      beginWizardGesture();
      startTask({
        kind: 'job', tacticId: wizard.tactic.id, groupId, phaseIndex,
        draw: 'route', field: 'via', postIndex, hint: null,
      });
      revealMap();
    },
    startSigTask: (groupId, phaseIndex, classId) => {
      if (!wizard) return;
      const geometry = signatureMarkGeometry(catalog, classId);
      if (!geometry) return;
      beginWizardGesture();
      startTask({
        kind: 'sig', tacticId: wizard.tactic.id, groupId, phaseIndex, classId,
        draw: geometry.kind === 'points' ? 'posts' : 'route',
        max: geometry.max,
      });
      revealMap();
    },
    startWatchTask: (target) => {
      if (!wizard) return;
      beginWizardGesture();
      startTask({ kind: 'watch', tacticId: wizard.tactic.id, target });
      revealMap();
    },
    startCarryTask: (phaseIndex) => {
      if (!wizard) return;
      beginWizardGesture();
      startTask({
        kind: 'carry', tacticId: wizard.tactic.id, phaseIndex, draw: 'route',
      });
      revealMap();
    },
    endTask,
    addDeliverCore,
    highlightPost: null,
  };

  const onTilePicked = (x: number, y: number) => {
    if (!task || !wizard) return;
    if (task.kind === 'carry') {
      const via = wizard.tactic.phases[task.phaseIndex]?.custody?.via ?? [];
      if (via.length >= 16) {
        toast('That’s the longest way home — 16 points.', 'hot');
        return;
      }
      changeWizardLive((tactic) => {
        const custody = tactic.phases[task.phaseIndex]?.custody;
        if (!custody || custody.plan === 'keep') return;
        custody.via = [...(custody.via ?? []), [x, y]];
      });
      return;
    }
    // Completion checks read the click's actual effect: a click that REMOVED
    // a post or mark must not count as placing one.
    if (task.kind === 'job' && task.draw === 'posts') {
      const group = wizard.tactic.groups.find((value) => value.id === task.groupId);
      const job = effectiveJobResolution(wizard.tactic, task.phaseIndex, task.groupId)?.job;
      if (!group || !job) return;
      const posts = job.posts ?? [];
      const cap = Math.max(1, group.slots.length);
      const removing = posts.some((post) => post.at[0] === x && post.at[1] === y);
      if (!removing && posts.length >= cap) {
        toast('Every bot has a post — drag one to move it.', 'hot');
        return;
      }
      changeWizardLive((tactic) => {
        const owner = tactic.groups.find((value) => value.id === task.groupId);
        const target = materializeSpatialJob(tactic, task.phaseIndex, task.groupId);
        if (!owner || !target) return;
        target.posts = target.posts ?? [];
        // Posts belong to bots by explicit slot; stamp any drawn before that
        // existed so one array never mixes named and positional pairing.
        target.posts.forEach((post, index) => {
          if (post.slot == null) post.slot = owner.slots[index];
        });
        const existing = target.posts.findIndex((post) => post.at[0] === x && post.at[1] === y);
        if (existing >= 0) target.posts.splice(existing, 1);
        else {
          // Default facing prefers the enemy's direction, but never points a
          // fresh post into a wall or spawn ground.
          const order: Facing[] = x < document.mapWidth / 2
            ? ['e', 'n', 's', 'w'] : ['w', 'n', 's', 'e'];
          const facing = order.find((value) =>
            !postFacingIssue(catalog, { at: [x, y], facing: value })) ?? order[0];
          const slot = owner.slots.find((value) =>
            !target.posts!.some((post) => post.slot === value));
          target.posts.push({ at: [x, y], facing, slot });
        }
      });
      if (!removing && posts.length + 1 >= cap) endTask();
      return;
    }
    if (task.kind === 'sig') {
      const job = wizard.tactic.phases[task.phaseIndex]?.jobs[task.groupId];
      const mark = job?.sigs?.[task.classId];
      const points = mark ? ('points' in mark ? mark.points : mark.line) : [];
      const removing = points.some((point) => point[0] === x && point[1] === y);
      if (!removing && points.length >= task.max) {
        toast(`That’s every mark — drag one to move it.`, 'hot');
        return;
      }
      changeWizardLive((tactic) => {
        const target = tactic.phases[task.phaseIndex]?.jobs[task.groupId];
        if (!target) return;
        target.sigs = target.sigs ?? {};
        const geometry = signatureMarkGeometry(catalog, task.classId)!;
        const stored = target.sigs[task.classId];
        const marks = stored ? ('points' in stored ? stored.points : stored.line) : [];
        const existing = marks.findIndex((point) => point[0] === x && point[1] === y);
        if (existing >= 0) marks.splice(existing, 1);
        else marks.push([x, y]);
        target.sigs[task.classId] = geometry.kind === 'points'
          ? { points: marks } : { line: marks };
      });
      if (!removing && points.length + 1 >= task.max) {
        endTask();
        toast(`${task.max}/${task.max} marks placed.`);
      }
      return;
    }
    changeWizardLive((tactic) => {
      if (task.kind !== 'job') return;
      const job = task.draw === 'route'
        ? materializeSpatialJob(tactic, task.phaseIndex, task.groupId)
        : tactic.phases[task.phaseIndex]?.jobs[task.groupId];
      if (!job) return;
      if (task.draw === 'route') {
        if (task.field === 'via') {
          if (task.postIndex != null) {
            const post = job.posts?.[task.postIndex];
            if (post) post.via = [...(post.via ?? []), [x, y]];
          } else {
            job.via = [...(job.via ?? []), [x, y]];
          }
        } else {
          job.path = [...(job.path ?? []), [x, y]];
        }
      } else if (task.draw === 'point') {
        job.point = [x, y];
      }
    });
    if (task.kind === 'job' && task.draw === 'point') endTask();
  };

  const onRectDrawn = (rect: [number, number, number, number]) => {
    if (!task) return;
    changeWizardLive((tactic) => {
      if (task.kind === 'watch') {
        const conds = watchConds(tactic, task.target);
        if (!conds) return;
        if (task.target.row === 'cleared') {
          if (conds.cleared) conds.cleared.zones[task.target.zoneIndex] = { rect };
        } else if (task.target.row === 'notFighting') {
          if (conds.notFighting) conds.notFighting.zones[task.target.zoneIndex] = { rect };
        } else if (conds.enemiesAt) {
          conds.enemiesAt.zones[task.target.zoneIndex] = { rect };
        }
      } else if (task.kind === 'job') {
        const job = tactic.phases[task.phaseIndex]?.jobs[task.groupId];
        if (job) job.rect = rect;
      }
    });
    endTask();
  };

  const onGeometryLive = (edit: GeometryEdit) => {
    if (wizard && wizard.tactic.id === edit.tacticId) {
      changeWizardLive(edit.apply);
    }
  };

  const onSelect = (ref: MapSelectRef, additive = false) => {
    if (wizard && wizard.tactic.id === ref.tacticId) {
      if (ref.part === 'post') {
        const group = wizard.tactic.groups.find((value) => value.id === ref.groupId);
        const effective = effectiveJobResolution(
          wizard.tactic, ref.phaseIndex, ref.groupId)?.job;
        const slot = effective?.posts?.[ref.postIndex]?.slot ?? group?.slots[ref.postIndex];
        setSelectedGroupId(ref.groupId);
        if (slot != null) selectMapFirstBot(ref.groupId, slot, additive, ref.phaseIndex);
        setWizard({ ...wizard, step: 'phases', phaseIndex: ref.phaseIndex });
        return;
      }
      if (ref.part === 'group' || ref.part === 'bot' || ref.part === 'phase') {
        // Clicking an overlay jumps to its phase on the storyboard and
        // scrolls that group's section into view.
        setWizard({ ...wizard, step: 'phases', phaseIndex: ref.phaseIndex });
        if (ref.part === 'group' || ref.part === 'bot') {
          setSelectedGroupId(ref.groupId);
          if (ref.part === 'group') selectMapFirstGroup(ref.groupId);
          if (ref.part === 'bot') {
            selectMapFirstBot(ref.groupId, ref.slot, additive, ref.phaseIndex);
          }
          window.setTimeout(() => {
            window.document.getElementById(`section-${ref.groupId}-${ref.phaseIndex}`)
              ?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
          }, 40);
        }
        return;
      }
      setWizard({ ...wizard, step: ref.part === 'cores' ? 'cores' : 'start' });
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
          document: normalizeEditableDocumentForCatalog(
            JSON.parse(saved.tacticsJson), catalog),
        });
        setNotice(`Saved · revision ${saved.revision}.`);
        toast(`Saved · revision ${saved.revision}.`);
        if (!draft.sheetId) {
          // The first save remounts on its new URL, which would eat the toast
          // — hand the fact across through the navigation state instead.
          navigate(`/entrants/sheets/${saved.id}/edit`, {
            replace: true, state: { savedRevision: saved.revision },
          });
        }
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
  const botName = (slot: number) => document.squad[slot]?.name ?? null;
  const activeMapGroupId = wizard
    ? selectedGroupId && wizard.tactic.groups.some((group) => group.id === selectedGroupId)
      ? selectedGroupId : wizard.tactic.groups[0]?.id ?? null
    : null;
  const focusedBot = mapFirstPanel?.kind === 'bot'
    ? document.squad[mapFirstPanel.slot] : null;
  const focusedBotClass = focusedBot
    ? catalog.classes.find((entry) => entry.id === focusedBot.class) : null;
  const activeMapGroup = wizard?.tactic.groups.find((group) => group.id === activeMapGroupId);
  const activeMapJob = wizard && activeMapGroupId
    ? effectiveJobResolution(wizard.tactic, wizard.phaseIndex, activeMapGroupId)?.job
    : null;
  const needsFirstPlacement = activeMapJob?.kind === 'posts' && activeMapGroup
    ? (activeMapJob.posts?.length ?? 0) < activeMapGroup.slots.length
    : false;

  const focusMapFirst = (focus: MapFirstFocus) => {
    if (!wizard) return;
    let step = wizard.step;
    let phaseIndex = wizard.phaseIndex;
    if (focus.kind === 'start') {
      step = 'start';
    } else if (focus.kind === 'abort') {
      step = 'start';
    } else if (focus.kind === 'cores') {
      step = 'cores';
    } else if (focus.kind === 'groups') {
      step = 'groups';
    } else {
      step = 'phases';
      phaseIndex = focus.phaseIndex;
      if (focus.kind === 'transition') {
        // The complete transition editor opens in the temporary drawer below.
      } else if (focus.kind === 'group' || focus.kind === 'bot') {
        setSelectedGroupId(focus.groupId);
        if (focus.kind === 'bot') setSelectedSlots([focus.slot]);
      }
    }
    setWizard({ ...wizard, step, phaseIndex });
    setMapFirstPanel(focus);
    endTask();
    if (focus.kind === 'start' || focus.kind === 'abort') {
      const id = focus.kind === 'start' ? 'start-controls' : 'abort-controls';
      window.setTimeout(() => window.document.getElementById(id)
        ?.scrollIntoView({ block: 'start', behavior: 'smooth' }), 40);
    }
  };

  const selectMapFirstPhase = (phaseIndex: number) => {
    if (!wizard) return;
    setWizard({ ...wizard, step: 'phases', phaseIndex });
    setMapFirstPanel(null);
    endTask();
  };

  const cancelWizard = () => {
    if (!wizard) return;
    setWizard(null);
    setMapFirstPanel(null);
    setSelectedSlots([]);
    endMapTask();
    resetWizardHistory();
    if (wizard.editing) setSelectedTacticId(wizard.originalId);
    else toast('Draft discarded.');
  };

  const addMapFirstPhase = () => {
    if (!wizard || wizard.tactic.phases.length >= 8) return;
    const nextIndex = wizard.tactic.phases.length;
    changeWizard((next) => {
      const previous = next.phases[next.phases.length - 1];
      if ((previous.continue ?? []).length === 0) previous.continue = [{ required: {} }];
      next.phases.push(freshPhase(next, `Phase ${next.phases.length + 1}`));
      delete next.phases[next.phases.length - 1].continue;
    });
    setWizard((current) => current && { ...current, phaseIndex: nextIndex, step: 'phases' });
    setMapFirstPanel(null);
    endTask();
  };

  const selectMapFirstGroup = (groupId: string) => {
    if (!wizard) return;
    const group = wizard.tactic.groups.find((value) => value.id === groupId);
    setSelectedGroupId(groupId);
    setSelectedSlots(group?.slots ?? []);
    setMapFirstPanel(null);
  };

  const selectMapFirstBot = (
    groupId: string, slot: number, additive: boolean,
    phaseIndex = wizard?.phaseIndex ?? 0,
  ) => {
    if (!wizard) return;
    const next = additive
      ? selectedSlots.includes(slot)
        ? selectedSlots.filter((value) => value !== slot)
        : [...selectedSlots, slot]
      : [slot];
    const loneSlot = next.length === 1 ? next[0] : null;
    const loneGroup = loneSlot == null ? null
      : wizard.tactic.groups.find((group) => group.slots.includes(loneSlot));
    setSelectedGroupId(loneGroup?.id ?? groupId);
    setSelectedSlots(next);
    setMapFirstPanel(loneSlot != null && loneGroup
      ? { kind: 'bot', phaseIndex, groupId: loneGroup.id, slot: loneSlot }
      : null);
  };

  const selectMapFirstSlots = (slots: number[], additive: boolean) => {
    if (!wizard) return;
    const next = (additive
      ? [...new Set([...selectedSlots, ...slots])]
      : slots).sort((left, right) => left - right);
    const groups = wizard.tactic.groups.filter((group) =>
      group.slots.some((slot) => next.includes(slot)));
    if (groups.length === 1) setSelectedGroupId(groups[0].id);
    setSelectedSlots(next);
    if (next.length === 1 && groups.length === 1) {
      setMapFirstPanel({
        kind: 'bot', phaseIndex: wizard.phaseIndex,
        groupId: groups[0].id, slot: next[0],
      });
    } else {
      setMapFirstPanel(null);
    }
  };

  /** A tray-to-tray move uses the same slot-keyed belongings migration as
   * GroupsStep. There is no second membership model in Map-first. */
  const moveWizardBot = (slot: number, targetGroupId: string) => {
    if (!wizard) return;
    const source = wizard.tactic.groups.find((group) => group.slots.includes(slot));
    if (source?.id === targetGroupId) return;
    const tactic = structuredClone(wizard.tactic);
    const before = tactic.groups.map((group) => ({ id: group.id, slots: [...group.slots] }));
    for (const group of tactic.groups) group.slots = group.slots.filter((value) => value !== slot);
    tactic.groups.find((group) => group.id === targetGroupId)?.slots.push(slot);
    const summary = followMembership(tactic, document.squad, before, new Map());
    replaceWizardTactic(tactic);
    if (summary) toast(summary);
    setSelectedGroupId(targetGroupId);
  };

  /** Dragging a Group onto the map is the beginner-friendly formation tool:
   * existing exact Posts move rigidly; other/partial ground becomes a legal
   * ordinary Posts formation centred on the dropped tile. */
  const dropWizardGroup = (groupId: string, at: [number, number]) => {
    if (!wizard) return;
    const phaseIndex = wizard.phaseIndex;
    const currentGroup = wizard.tactic.groups.find((group) => group.id === groupId);
    if (!currentGroup || currentGroup.slots.length === 0) return;
    const tactic = structuredClone(wizard.tactic);
    let snapped = true;
    const group = tactic.groups.find((value) => value.id === groupId);
    let job = materializeSpatialJob(tactic, phaseIndex, groupId);
    if (!group || !job) return;
    if (job.kind !== 'posts') {
      const plan = planJobKindChange(job, 'posts');
      tactic.phases[phaseIndex].jobs[groupId] = structuredClone(plan.next);
      job = tactic.phases[phaseIndex].jobs[groupId];
    }
    job.posts = job.posts ?? [];
    job.posts.forEach((post, index) => {
      if (post.slot == null) post.slot = group.slots[index];
    });
    const complete = group.slots.every((slot) =>
      job.posts?.some((post) => post.slot === slot));
    if (!complete || job.posts.length !== group.slots.length) {
      job.posts = suggestFormationPosts(catalog, group.slots, at, [],
        `${tactic.id}:${phaseIndex}:${groupId}:drop`);
      snapped = job.posts.length === group.slots.length;
    } else {
      const anchorSlot = group.slots[0];
      const anchor = job.posts.find((post) => post.slot === anchorSlot);
      if (!anchor) return;
      const centre: [number, number] = [
        Math.round(job.posts.reduce((sum, post) => sum + post.at[0], 0) / job.posts.length),
        Math.round(job.posts.reduce((sum, post) => sum + post.at[1], 0) / job.posts.length),
      ];
      const anchorDestination: [number, number] = [
        anchor.at[0] + at[0] - centre[0], anchor.at[1] + at[1] - centre[1],
      ];
      const translated = translatePostSelection(
        catalog, job.posts, group.slots, anchorSlot, anchorDestination);
      if (!translated) {
        snapped = false;
      } else {
        for (const post of job.posts) {
          const translatedAt = post.slot == null ? null : translated.positions.get(post.slot);
          if (translatedAt) post.at = translatedAt;
          const translatedFacing = post.slot == null ? null : translated.facings.get(post.slot);
          if (translatedFacing) post.facing = translatedFacing;
        }
      }
    }
    if (snapped) replaceWizardTactic(tactic);
    setSelectedGroupId(groupId);
    setSelectedSlots(currentGroup.slots);
    setMapFirstPanel(null);
    toast(snapped
      ? `${currentGroup.name} placed — drag any selected bot to refine the formation.`
      : `${currentGroup.name} could not fit near that tile.`, snapped ? 'ok' : 'hot');
  };

  /** Dropping an unposted tray bot is real Posts authoring. Preview bots for
   * other job kinds never enter this path and remain non-authoritative. */
  const dropPostBot = (groupId: string, slot: number, [x, y]: [number, number]) => {
    if (!wizard) return;
    const phaseIndex = wizard.phaseIndex;
    const group = wizard.tactic.groups.find((value) => value.id === groupId);
    const job = effectiveJobResolution(wizard.tactic, phaseIndex, groupId)?.job;
    if (!group?.slots.includes(slot) || job?.kind !== 'posts') {
      toast('Choose a Posts assignment before placing an exact bot.', 'hot');
      return;
    }
    const issue = tilePlacementIssue(catalog, [x, y], 'anchor');
    if (issue) {
      toast(`That tile is ${issue}.`, 'hot');
      return;
    }
    if (job.posts?.some((post) => post.slot === slot)) {
      toast('That bot already has a Post — drag it on the map to move it.', 'hot');
      return;
    }
    changeWizard((tactic) => {
      const target = materializeSpatialJob(tactic, phaseIndex, groupId);
      if (!target || target.kind !== 'posts') return;
      target.posts = target.posts ?? [];
      const order: Facing[] = x < document.mapWidth / 2
        ? ['e', 'n', 's', 'w'] : ['w', 'n', 's', 'e'];
      const facing = order.find((value) =>
        !postFacingIssue(catalog, { at: [x, y], facing: value })) ?? order[0];
      target.posts.push({ at: [x, y], facing, slot });
    });
    setSelectedGroupId(groupId);
    toast(`${document.squad[slot]?.name ?? 'Bot'} posted.`);
  };

  return (
    // The map-first workspace grows in normal page flow, so its legend and
    // behavior rail can never paint through the footer.
    <div className={wizard
      ? 'min-h-[calc(100dvh-150px)] min-w-0'
      : 'grid gap-3 lg:min-h-[calc(100dvh-150px)] lg:grid-cols-[260px_minmax(0,1fr)]'}
      data-layout="map-first">
      {/* ------------------------------------------------------- the rail --- */}
      {!wizard && <section className="flex flex-col gap-3 lg:min-h-0 lg:overflow-y-auto lg:pr-0.5"
        data-testid="sheet-overview">
        <div className="panel pad">
          <div className="mb-2 flex items-center gap-2">
            <span className="lab">The Sheet</span>
            <span className="pill">{document.tactics.length} tactic{document.tactics.length === 1 ? '' : 's'}</span>
            <span className="grow" />
            <button type="button" className="btn btn-strong" disabled={blocking || save.isPending}
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
          <button type="button" className="btn btn-primary mb-2 w-full" disabled={wizard !== null}
            onClick={() => setRecipeOpen(true)} data-testid="create-tactic">
            ＋ Create Tactic
          </button>
          <button type="button" className="btn w-full" disabled={!draft.sheetId || trial.isPending}
            onClick={practice}>
            {trial.isPending ? 'Starting…' : 'Practice'}
          </button>
          {notice && <p className="t-micro mt-2 text-arena-material" role="status">{notice}</p>}
        </div>
        <IssueSummary issues={issues} />
        <div className="flex flex-col gap-2" data-testid="tactic-list">
          {document.tactics.length === 0 && !wizard && (
            <div className="panel pad border-dashed t-meta">
              No Tactics yet. An ordered list of whole-team <b>Tactics</b> runs top to bottom;
              every Tactic moves all eight bots.
            </div>
          )}
          {draft.document.tactics.map((tactic, index) => (
            <TacticCard key={tactic.id} tactic={tactic} index={index}
              total={draft.document.tactics.length}
              selected={tactic.id === selectedTacticId && !wizard}
              confirmDelete={confirmDeleteId === tactic.id}
              squadSize={document.squad.length}
              botName={botName}
              onSelect={() => {
                if (wizard) return;
                setConfirmDeleteId(null);
                // Straight into edit mode: the map's phase icons are the
                // navigation, and the paragraph readback lives on Review.
                openWizard(tactic, hintsFor(tactic), true);
              }}
              onMove={(delta) => mutate((next) => {
                const from = next.document.tactics.findIndex((value) => value.id === tactic.id);
                const to = from + delta;
                if (to < 0 || to >= next.document.tactics.length) return;
                const [moved] = next.document.tactics.splice(from, 1);
                next.document.tactics.splice(to, 0, moved);
              })}
              onEdit={() => openWizard(tactic, hintsFor(tactic), true)}
              onDelete={() => {
                if (confirmDeleteId !== tactic.id) {
                  setConfirmDeleteId(tactic.id);
                  // The armed confirm disarms itself, like the mock's.
                  window.setTimeout(() => setConfirmDeleteId((current) =>
                    current === tactic.id ? null : current), 2600);
                  return;
                }
                mutate((next) => {
                  next.document.tactics = next.document.tactics.filter((value) => value.id !== tactic.id);
                });
                setConfirmDeleteId(null);
                if (selectedTacticId === tactic.id) setSelectedTacticId(null);
                toast('Tactic removed.');
              }}
              dropIndicator={dragTactic?.over === index
                ? (dragTactic.below ? 'below' : 'above') : null}
              onDragStart={() => setDragTactic({ id: tactic.id, over: null, below: false })}
              onDragEnd={() => setDragTactic(null)}
              onDragOver={(below) => setDragTactic((current) =>
                current && { ...current, over: index, below })}
              onDrop={(below) => {
                const source = dragTactic?.id;
                setDragTactic(null);
                if (!source || source === tactic.id) return;
                mutate((next) => {
                  const from = next.document.tactics.findIndex((value) => value.id === source);
                  if (from < 0) return;
                  const [moved] = next.document.tactics.splice(from, 1);
                  let to = next.document.tactics.findIndex((value) => value.id === tactic.id);
                  if (to < 0) to = next.document.tactics.length;
                  else if (below) to += 1;
                  next.document.tactics.splice(to, 0, moved);
                });
              }} />
          ))}
        </div>
        <SquadRail catalog={catalog} squad={document.squad}
          onOpen={() => setSquadOpen(true)}
          onAutoFill={document.squad.length < 8 ? () => {
            mutate((next) => {
              next.document.squad = autoFillSquad(catalog, next.document.squad);
            });
            toast('Squad filled out.');
          } : undefined} />
      </section>}

      {/* -------------------------------------------------------- the map --- */}
      <section ref={tacticWorkspaceRef} tabIndex={-1}
        className="flex min-w-0 flex-col gap-2 focus:outline-none lg:min-h-0"
        data-testid="tactic-workspace">
        {wizard && (
          <MapFirstTimeline tactic={wizard.tactic} phaseIndex={wizard.phaseIndex}
            focus={mapFirstPanel} gate={gate} dirty={tacticDirty}
            canUndo={wizardHistoryRevision >= 0 && wizardUndoStack.current.length > 0}
            canRedo={wizardHistoryRevision >= 0 && wizardRedoStack.current.length > 0}
            squadSize={document.squad.length} onFocus={focusMapFirst}
            onSelectPhase={selectMapFirstPhase}
            onUndo={undoWizard} onRedo={redoWizard}
            onAddPhase={addMapFirstPhase} onEditSquad={() => setSquadOpen(true)}
            onCommit={commitWizard} onCancel={cancelWizard} />
        )}
        <div className={wizard
          ? 'grid min-w-0 items-start gap-2 min-[820px]:grid-cols-[168px_minmax(0,1fr)]'
          : 'min-w-0'}>
          {wizard && (
            <MapFirstGroupTrays catalog={catalog} document={document}
              tactic={wizard.tactic} phaseIndex={wizard.phaseIndex}
              selectedGroupId={activeMapGroupId} selectedSlots={selectedSlots}
              onFocus={focusMapFirst} onMoveBot={moveWizardBot}
              onSelectGroup={selectMapFirstGroup} onSelectBot={selectMapFirstBot} />
          )}
          <div className="min-w-0">
            {wizard && activeMapGroupId && (
              <MapFirstGroupToolbar key={`${wizard.tactic.id}:${wizard.phaseIndex}:${activeMapGroupId}`}
                catalog={catalog} document={document}
                tactic={wizard.tactic} phaseIndex={wizard.phaseIndex}
                groupId={activeMapGroupId} change={changeWizard}
                onDrawPosts={() => stepProps.startJobTask(activeMapGroupId, wizard.phaseIndex, undefined, 'posts')}
                onDrawPatrol={() => stepProps.startJobTask(activeMapGroupId, wizard.phaseIndex, undefined, 'route')} />
            )}
            {/* The map and Behavior are one visual workbench. The map remains
                left aligned and viewport-capped; Behavior hugs its right edge
                instead of drifting to the far side of a flexible grid. */}
            <div className="min-[1240px]:flex min-[1240px]:items-start min-[1240px]:gap-2">
              <div className="min-w-0 lg:w-[min(100%,calc((100dvh-340px)*1.148))] min-[1240px]:shrink-0">
                <div className="relative">
                  <div className="absolute right-1.5 top-1.5 z-20 flex max-w-[calc(100%_-_0.75rem)] flex-wrap items-center justify-end gap-1 rounded-sm border border-arena-edge/80 bg-arena-deep/90 p-0.5 shadow-lg backdrop-blur-sm"
                    data-testid="map-layer-controls">
                    <span className="flex gap-0.5">
                      <TacticsIconButton icon="layers" label="Show every Tactic"
                        pressed={showAll} onClick={() => setShowAll((value) => !value)} />
                      <TacticsIconButton icon="transition" label="Show every phase"
                        pressed={showAllPhases} onClick={() => setShowAllPhases((value) => !value)} />
                    </span>
                    <span className="flex gap-0.5 border-l border-arena-edge pl-0.5">
                      <TacticsIconButton icon="vision"
                        label={selectedSlots.length ? 'Selected bot vision' : 'Select a bot first'}
                        pressed={showVision} disabled={selectedSlots.length === 0}
                        onClick={() => setShowVision((value) => !value)} />
                      <TacticsIconButton icon="attack-range"
                        label={selectedSlots.length ? 'Selected bot attack reach' : 'Select a bot first'}
                        pressed={showAttackRange} disabled={selectedSlots.length === 0}
                        onClick={() => setShowAttackRange((value) => !value)} />
                      <TacticsIconButton icon="deselect" label="Clear bot selection"
                        disabled={selectedSlots.length === 0} onClick={() => {
                          setSelectedSlots([]); setMapFirstPanel(null);
                        }} />
                    </span>
                    <span className="flex gap-0.5 border-l border-arena-edge pl-0.5">
                      {([1, 1.5, 2] as const).map((level) => (
                        <button key={level} type="button"
                          className={`btn !min-h-7 !border-0 !px-1.5 ${zoom === level ? 'btn-on' : ''}`}
                          onClick={() => setZoom(level)}>
                          {level === 1 ? 'Fit' : `${level}×`}
                        </button>
                      ))}
                    </span>
                  </div>
                  {!task && needsFirstPlacement && <div
                    className="pointer-events-none absolute left-2 top-2 z-10 max-w-64 rounded-sm border border-arena-material/60 bg-arena-deep/90 px-2 py-1.5 text-[10.5px] text-arena-text"
                    data-testid="group-placement-callout">
                    Drag <b>{activeMapGroup?.name}</b> from the left onto the map to place its formation.
                  </div>}
                  <div className={`overflow-auto rounded-sm border border-arena-edge bg-arena-deep ${zoom !== 1 ? 'max-h-[80vh]' : ''}`}>
                    <div style={{ width: `${zoom * 100}%` }}>
                      <TacticsMapCanvas
                        catalog={catalog} document={document}
                        editingTacticId={editingTacticId}
                        activePhaseIndex={wizard?.phaseIndex ?? null}
                        selectedTacticId={selectedTacticId}
                        showAll={showAll} showAllPhases={showAllPhases}
                        showProjectedBots
                        showAdjacentPhases={false}
                        allowInheritedSpatialEditing
                        selectedSlots={selectedSlots}
                        showVision={showVision}
                        showAttackRange={showAttackRange}
                        task={task} hints={wizard?.hints.groups ?? null}
                        onTilePicked={onTilePicked} onRectDrawn={onRectDrawn}
                        onGeometryLive={onGeometryLive} onGestureStart={beginWizardGesture}
                        onGestureEnd={finishWizardGesture} onSelect={onSelect}
                        onSlotsSelected={selectMapFirstSlots}
                        onClearSelection={() => {
                          setSelectedSlots([]); setMapFirstPanel(null);
                        }}
                        onTaskDone={endTask}
                        onTileRejected={(reason) => toast(reason, 'hot')}
                        onPostsSwapped={() => toast('Swapped posts.')}
                        onPostBotDropped={dropPostBot}
                        onGroupDropped={dropWizardGroup}
                      />
                    </div>
                  </div>
                  {task && <TaskBanner task={task} wizard={wizard} botName={botName}
                    onDone={endTask} onCancel={cancelTask}
                    onUndoPoint={() => changeWizardLive((tactic) => {
                      if (task.kind === 'carry') {
                        tactic.phases[task.phaseIndex]?.custody?.via?.pop();
                      } else if (task.kind === 'job') {
                        const job = tactic.phases[task.phaseIndex]?.jobs[task.groupId];
                        if (!job) return;
                        if (task.draw === 'posts') job.posts?.pop();
                        if (task.draw === 'route') {
                          if (task.field === 'via') {
                            if (task.postIndex != null) job.posts?.[task.postIndex]?.via?.pop();
                            else job.via?.pop();
                          } else {
                            job.path?.pop();
                          }
                        }
                      } else if (task.kind === 'sig') {
                        const mark = tactic.phases[task.phaseIndex]?.jobs[task.groupId]
                          ?.sigs?.[task.classId];
                        if (mark) ('points' in mark ? mark.points : mark.line).pop();
                      }
                    })} />}
                </div>
                <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 t-micro text-arena-dim">
                  <span><span style={{ color: '#22d3ee' }}>●</span> Group post</span>
                  <span><span style={{ color: '#22d3ee' }}>→</span> route</span>
                  <span><span style={{ color: '#c8763c' }}>⚡▭</span> watched area</span>
                  <span><span style={{ color: '#b49cff' }}>◐</span> Wells</span>
                  <span><span style={{ color: '#22d3ee' }}>◆</span> reactors</span>
                </div>
              </div>
              {wizard && activeMapGroupId && (
                <div className="mt-2 min-[1240px]:mt-0 min-[1240px]:w-[232px] min-[1240px]:shrink-0">
                  <MapFirstBehaviorPanel tactic={wizard.tactic} phaseIndex={wizard.phaseIndex}
                    groupId={activeMapGroupId} change={changeWizard} />
                </div>
              )}
            </div>
          </div>
        </div>
      </section>

      {/* ----------------------------------------------------- the drawer --- */}
      {wizard && mapFirstPanel && (
        <aside ref={contextDrawerRef} className={`fixed right-4 top-[14rem] z-40 flex max-h-[calc(100dvh-15rem)] w-[min(400px,calc(100vw-2rem))] flex-col overflow-hidden rounded-sm border border-arena-edge bg-arena-bg shadow-2xl ${mapFirstPanel.kind === 'bot' ? '' : 'bottom-4'}`}
          data-testid="map-first-context-drawer" aria-label="Context editor">
          <div className="flex items-center gap-2 border-b border-arena-edge bg-arena-panel px-2 py-1.5">
            <TacticsIcon name={mapFirstPanel.kind === 'transition' ? 'transition'
              : mapFirstPanel.kind === 'cores' ? 'core'
                : mapFirstPanel.kind === 'start' ? 'start'
                  : mapFirstPanel.kind === 'abort' ? 'abort'
                    : mapFirstPanel.kind === 'groups' ? 'members'
                      : mapFirstPanel.kind === 'bot' ? 'signature' : 'edit'} size={16} />
            <b className="t-body grow">{mapFirstPanel.kind === 'transition'
              ? `${wizard.tactic.phases[mapFirstPanel.phaseIndex]?.name} transition`
              : mapFirstPanel.kind === 'cores' ? 'Core behavior'
                : mapFirstPanel.kind === 'start' ? 'Tactic start'
                  : mapFirstPanel.kind === 'abort' ? 'Tactic-wide abort'
                    : mapFirstPanel.kind === 'groups' ? 'Group membership'
                      : mapFirstPanel.kind === 'phase' ? 'Phase details'
                      : mapFirstPanel.kind === 'bot'
                        ? focusedBot
                          ? `${focusedBot.name} · ${focusedBotClass?.name ?? focusedBot.class}`
                          : 'Bot'
                        : 'Group'}</b>
            <TacticsIconButton icon="close" label="Close context editor"
              onClick={() => { setMapFirstPanel(null); endTask(); }} />
          </div>
          <div className="flex flex-col gap-2 overflow-y-auto p-2">
            {(mapFirstPanel.kind === 'start' || mapFirstPanel.kind === 'abort') && <StartStep {...stepProps} />}
            {mapFirstPanel.kind === 'groups' && <GroupsStep {...stepProps} />}
            {mapFirstPanel.kind === 'transition'
              && <TransitionEditor {...stepProps} phaseIndex={mapFirstPanel.phaseIndex} />}
            {mapFirstPanel.kind === 'phase' && (() => {
              const index = mapFirstPanel.phaseIndex;
              const phase = wizard.tactic.phases[index];
              if (!phase) return null;
              return <div className="rounded-sm border border-arena-edge p-2">
                <label className="lab mb-1 block" htmlFor={`map-phase-name-${phase.id}`}>Phase name</label>
                <input id={`map-phase-name-${phase.id}`} className="field w-full" value={phase.name}
                  onChange={(event) => changeWizard((next) => {
                    next.phases[index].name = event.target.value;
                  })} />
                <p className="t-micro my-2 text-arena-dim">Phase {index + 1} of {wizard.tactic.phases.length}.
                  A blank spatial assignment carries the previous phase until its first map edit.</p>
                {wizard.tactic.phases.length > 1 && <button type="button" className="btn text-arena-hot"
                  onClick={() => {
                    changeWizard((next) => {
                      next.phases.splice(index, 1);
                      const tail = next.phases[next.phases.length - 1];
                      if (tail && (tail.continue ?? []).every((rule) =>
                        Object.keys(rule.required ?? {}).length === 0
                        && Object.keys(rule.triggers ?? {}).length === 0
                        && Object.keys(rule.unless ?? {}).length === 0)) delete tail.continue;
                    });
                    setWizard((current) => current && {
                      ...current, phaseIndex: Math.max(0, index - 1), step: 'phases',
                    });
                    setMapFirstPanel(null);
                    endTask();
                  }}>Delete this phase</button>}
              </div>;
            })()}
            {mapFirstPanel.kind === 'cores' && <>
              <CoresStep {...stepProps} />
              <PhaseCustodyEditor {...stepProps} phaseIndex={wizard.phaseIndex} />
            </>}
            {mapFirstPanel.kind === 'bot' && <MapFirstBotPanel
              catalog={catalog} document={document} tactic={wizard.tactic}
              phaseIndex={mapFirstPanel.phaseIndex} groupId={mapFirstPanel.groupId}
              slot={mapFirstPanel.slot} task={task} change={changeWizard}
              onDrawRoute={(postIndex) => {
                if (postIndex == null) {
                  stepProps.startJobTask(mapFirstPanel.groupId, mapFirstPanel.phaseIndex, 'via');
                } else {
                  stepProps.startPostViaTask(mapFirstPanel.groupId, mapFirstPanel.phaseIndex, postIndex);
                }
                setMapFirstPanel(null);
              }}
              onDrawPatrol={() => {
                stepProps.startJobTask(mapFirstPanel.groupId, mapFirstPanel.phaseIndex, undefined, 'route');
                setMapFirstPanel(null);
              }}
              onDrawSignature={(classId) => {
                stepProps.startSigTask(mapFirstPanel.groupId, mapFirstPanel.phaseIndex, classId);
                setMapFirstPanel(null);
              }}
              onStop={endTask} />}
          </div>
        </aside>
      )}

      <div className="pointer-events-none fixed bottom-4 right-4 z-50 flex w-64 flex-col gap-1.5"
        aria-live="polite">
        {toasts.map((entry) => (
          <div key={entry.id} role="status"
            className={`unlock-toast panel px-3 py-2 t-micro shadow-lg ${entry.tone === 'hot'
              ? 'border-arena-hot/60 text-arena-hot' : 'text-arena-text'}`}>
            {entry.text}
          </div>
        ))}
      </div>

      {squadOpen && (
        <SquadModal catalog={catalog} squad={document.squad}
          onClose={() => setSquadOpen(false)}
          notify={toast}
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
          <div className="grid gap-2 sm:grid-cols-3" data-testid="recipe-chooser">
            {RECIPES.filter((recipe) => recipe.id !== 'deliver-core').map((recipe) => (
              <button key={recipe.id} type="button"
                className="choice-card flex min-h-[108px] flex-col text-left"
                data-testid={`recipe-${recipe.id}`}
                onClick={() => {
                  const { tactic, hints } = instantiateRecipe(recipe, draft.document, catalog);
                  setRecipeOpen(false);
                  openWizard(tactic, hints, false);
                }}>
                <span className="flex items-center gap-2">
                  <span className="text-arena-material">{recipe.icon}</span>
                  <span className="text-[13px] font-semibold">{recipe.label}</span>
                </span>
                <span className="mt-0.5 block t-micro text-arena-dim">{recipe.tagline}</span>
                <span className="mt-auto flex flex-wrap gap-1 pt-1">
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

function drawKindFor(kind: JobKind) {
  const expected = expectedGeometry(kind);
  return expected === 'posts' ? 'posts'
    : expected === 'rect' ? 'rect'
      : expected === 'path' ? 'route'
        : expected === 'point' ? 'point'
          : null;
}

/** Round-robin over unlocked classes, one copy each before seconds, so the
 * result respects the copy cap and lands on a wide composition. */
function autoFillSquad(catalog: TacticalSheetCatalog, squad: SquadBot[]): SquadBot[] {
  const next = [...squad];
  const unlocked = catalog.classes.filter((entry) => entry.unlocked);
  const freshName = (bots: SquadBot[], slot: number) => {
    const used = new Set(bots.map((bot) => bot.name));
    return BOT_NAMES.find((candidate) => !used.has(candidate)) ?? `Unit ${slot + 1}`;
  };
  let round = 0;
  while (next.length < 8 && round < catalog.maximumCopiesPerClass) {
    for (const entry of unlocked) {
      if (next.length >= 8) break;
      const used = next.filter((bot) => bot.class === entry.id).length;
      if (used > round) continue;
      next.push({
        slot: next.length, class: entry.id, build: 'balanced',
        name: freshName(next, next.length),
      });
    }
    round += 1;
  }
  return next.map((bot, index) => ({ ...bot, slot: index }));
}

/** Per-card problems, so a broken Tactic names itself in the list. */
function tacticProblems(tactic: Tactic, squadSize: number): string[] {
  const problems: string[] = [];
  const assigned = new Set(tactic.groups.flatMap((group) => group.slots));
  if (squadSize === 8 && assigned.size !== 8) {
    problems.push(`${assigned.size}/8 bots assigned`);
  }
  if ([...assigned].some((slot) => slot >= squadSize)) {
    problems.push('a squad slot was emptied');
  }
  return problems;
}

function TacticCard({
  tactic, index, total, selected, confirmDelete, squadSize, botName, dropIndicator,
  onSelect, onMove, onEdit, onDelete, onDragStart, onDragEnd, onDragOver, onDrop,
}: {
  tactic: Tactic;
  index: number;
  total: number;
  selected: boolean;
  confirmDelete: boolean;
  squadSize: number;
  botName: (slot: number) => string | null;
  dropIndicator: 'above' | 'below' | null;
  onSelect: () => void;
  onMove: (delta: number) => void;
  onEdit: () => void;
  onDelete: () => void;
  onDragStart: () => void;
  onDragEnd: () => void;
  onDragOver: (below: boolean) => void;
  onDrop: (below: boolean) => void;
}) {
  const problems = tacticProblems(tactic, squadSize);
  const belowHalf = (event: React.DragEvent) => {
    const box = event.currentTarget.getBoundingClientRect();
    return event.clientY > box.top + box.height / 2;
  };
  return (
    <article
      draggable
      onDragStart={(event) => {
        event.dataTransfer.effectAllowed = 'move';
        onDragStart();
      }}
      onDragEnd={onDragEnd}
      onDragOver={(event) => {
        event.preventDefault();
        event.dataTransfer.dropEffect = 'move';
        onDragOver(belowHalf(event));
      }}
      onDrop={(event) => {
        event.preventDefault();
        onDrop(belowHalf(event));
      }}
      className={`flex cursor-pointer gap-2 rounded-sm border p-2 transition-colors ${selected
        ? 'border-arena-material/70 bg-arena-deep/60 shadow-[inset_3px_0_0_var(--color-arena-material)]'
        : 'border-arena-edge bg-arena-deep/30 hover:border-arena-dim'} ${dropIndicator === 'above'
        ? '!shadow-[0_-2px_0_var(--color-arena-material)]'
        : dropIndicator === 'below' ? '!shadow-[0_2px_0_var(--color-arena-material)]' : ''}`}
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
        <div className="mt-1 flex flex-wrap gap-1">
          {tactic.groups.map((group) => (
            <span key={group.id} className="flex items-center gap-1 rounded-full border border-arena-edge px-1.5 py-px text-[9.5px] text-arena-dim">
              <span className="h-1.5 w-1.5 rounded-full" style={{ background: groupColor(tactic, group.id) }} />
              {group.name} · {group.slots.length}
            </span>
          ))}
        </div>
        <p className="t-micro mt-1 line-clamp-3 text-arena-dim">{summarize(tactic, botName)}</p>
        {problems.map((problem) => (
          <p key={problem} className="t-micro mt-0.5 text-arena-hot">⚠ {problem}</p>
        ))}
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

function TaskBanner({ task, wizard, botName, onDone, onCancel, onUndoPoint }: {
  task: MapTask;
  wizard: WizardState | null;
  botName: (slot: number) => string | null;
  onDone: () => void;
  onCancel: () => void;
  onUndoPoint: () => void;
}) {
  const group = (task.kind === 'job' || task.kind === 'sig') && wizard
    ? wizard.tactic.groups.find((value) => value.id === task.groupId)
    : null;
  const job = (task.kind === 'job' || task.kind === 'sig') && wizard
    ? wizard.tactic.phases[task.phaseIndex]?.jobs[task.groupId]
    : null;
  const whose = possessive(group?.name?.trim() || 'the group');
  const what = task.kind === 'carry'
    ? 'Click the carrier’s waypoints home, in travel order — the run ends at your reactor'
    : task.kind === 'sig'
    ? `Placing ${whose} signature marks — ${task.draw === 'route' ? 'click the lane points in order' : `click up to ${task.max} tiles`}`
    : task.kind === 'watch'
      ? task.target.row === 'cleared'
        ? 'Drag the area your team must freshly check and clear'
        : task.target.row === 'notFighting'
          ? 'Drag the area where local fighting must have ended'
        : task.target.scope === 'start'
          ? 'Drag the box this Tactic watches to Start — it applies once enemies gather inside'
          : task.target.scope === 'abort'
            ? 'Drag the box the abort watches — enemies are counted inside it'
            : 'Drag the box this phase watches — enemies are counted inside it'
      : {
        posts: `Placing ${group?.name ?? ''} posts — click ${group?.slots.length || 'a few'} tiles`,
        rect: `Drag a box for ${whose} area`,
        route: task.field === 'via'
          ? task.postIndex != null
            ? `Click ${possessive(
              (group && botName(group.slots[task.postIndex] ?? -1))
                ?? `${group?.name ?? ''} post ${task.postIndex + 1}`,
            )} way in — waypoints in travel order, ending near its post`
            : `Click ${whose} approach waypoints in travel order — they walk this in before the job`
          : `Click waypoints for ${whose} ${job?.kind === 'line' ? 'line' : 'route'}, in travel order`,
        point: `Click ${whose} ${job?.kind === 'approach' ? 'approach point' : 'destination'}`,
      }[task.draw];
  const undoable = (task.kind === 'job' && (task.draw === 'route' || task.draw === 'posts'))
    || task.kind === 'sig' || task.kind === 'carry';
  // Group-accent left rail; forge orange for a phase watch, pale for start,
  // zone teal for a cleared box.
  const accent = task.kind === 'watch'
    ? (task.target.row === 'cleared' ? '#38d9c5'
      : task.target.row === 'notFighting' ? '#f0b35a'
      : task.target.scope === 'start' ? '#e6ddd4' : '#c8763c')
    : task.kind === 'carry' ? '#b49cff'
      : wizard ? groupColor(wizard.tactic, task.groupId) : '#e6ddd4';
  // A live placed/needed readout for the countable tasks.
  let progress: string | null = null;
  if (task.kind === 'job' && task.draw === 'posts' && group) {
    progress = `${job?.posts?.length ?? 0}/${Math.max(1, group.slots.length)}`;
  } else if (task.kind === 'sig' && group) {
    const mark = job?.sigs?.[task.classId];
    const points = mark ? ('points' in mark ? mark.points : mark.line) : [];
    progress = `${points.length}/${task.max}`;
  }
  return (
    // pointer-events pass through everywhere but the buttons, so the banner
    // never blocks drawing on the map rows underneath it.
    <div className="pointer-events-none absolute inset-x-2 top-2 z-10 flex items-center gap-2 rounded-sm border border-arena-edge bg-arena-deep/80 px-2 py-1.5 backdrop-blur"
      style={{ borderLeft: `3px solid ${accent}` }}
      data-testid="task-banner" role="status">
      <span className="t-micro">{what}</span>
      {progress && <span className="val">{progress}</span>}
      {task.kind === 'job' && task.hint && <span className="t-micro text-arena-dim">· {task.hint.text}</span>}
      <span className="grow" />
      {undoable && <button type="button" className="btn pointer-events-auto" onClick={onUndoPoint}>Undo</button>}
      <button type="button" className="btn pointer-events-auto" onClick={onCancel}>Cancel</button>
      <button type="button" className="btn btn-secondary pointer-events-auto" onClick={onDone}>Done</button>
    </div>
  );
}

function SquadRail({ catalog, squad, onOpen, onAutoFill }: {
  catalog: TacticalSheetCatalog;
  squad: SquadBot[];
  onOpen: () => void;
  onAutoFill?: () => void;
}) {
  return (
    <div className="panel pad" data-testid="squad-rail">
      <div className="mb-1.5 flex items-center gap-2">
        <span className="lab">Squad</span>
        <span className="val">{squad.length}/8</span>
        <span className="grow" />
        {onAutoFill && (
          <button type="button" className="btn" onClick={onAutoFill}>Auto-fill</button>
        )}
        <button type="button" className="btn" onClick={onOpen} data-testid="open-squad">Pick eight</button>
      </div>
      <div className="grid grid-cols-2 gap-1.5">
        {Array.from({ length: 8 }, (_, slot) => {
          const bot = squad[slot];
          if (!bot) {
            return <button key={slot} type="button" onClick={onOpen}
              className="min-h-[54px] rounded-sm border border-dashed border-arena-edge t-micro text-arena-dim">
              ＋ Slot {slot + 1}
            </button>;
          }
          const entry = catalog.classes.find((value) => value.id === bot.class);
          const sprite = classSprite(bot.class);
          return <button key={slot} type="button" onClick={onOpen}
            title={entry ? `${entry.name} — ${entry.fantasy}` : bot.class}
            className="relative flex min-h-[54px] items-center gap-2 rounded-sm border border-arena-edge px-2 py-1.5 text-left transition-colors hover:border-arena-edge2">
            <span className="absolute inset-y-1 left-0 w-[3px] rounded-full"
              style={{ background: slotAccent(slot) }} />
            {sprite && <img src={sprite} alt="" className="h-[34px] w-[34px] shrink-0 object-contain drop-shadow" />}
            <span className="min-w-0">
              <span className="block truncate t-micro font-semibold leading-tight">{bot.name}</span>
              <span className="block text-[10.5px] leading-tight text-arena-dim">{entry?.name ?? bot.class}</span>
            </span>
            <span className="absolute right-1 top-1 text-[8.5px] uppercase tracking-widest text-arena-dim">
              {BUILDS.find((value) => value.id === bot.build)?.label}
            </span>
          </button>;
        })}
      </div>
      <p className="t-micro mt-1.5 text-arena-dim">Click a slot to pick its class and build preset.</p>
    </div>
  );
}

function SquadModal({ catalog, squad, onClose, onChange, notify }: {
  catalog: TacticalSheetCatalog;
  squad: SquadBot[];
  onClose: () => void;
  onChange: (squad: SquadBot[]) => void;
  notify?: (text: string, tone?: 'ok' | 'hot') => void;
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
  const counts = new Map<string, number>();
  for (const bot of squad) counts.set(bot.class, (counts.get(bot.class) ?? 0) + 1);
  // The roster rule, derived from the catalog's numbers: at most
  // maximumCopiesPerClass of one class, and the distinct-classes floor
  // leaves a doubling budget (8 slots, 7 distinct → one pair total).
  const duplicateBudget = catalog.slotCount - catalog.minimumDistinctClasses;
  const className = (classId: string) =>
    catalog.classes.find((value) => value.id === classId)?.name ?? classId;
  /** Why picking this class now is illegal — null when it's fine. Evaluates
   * the composition that would result, so swaps account for the freed slot. */
  const blockReason = (classId: string): string | null => {
    const next = new Map(counts);
    if (selected !== null && squad[selected]) {
      const from = squad[selected].class;
      if (from === classId) return null;
      next.set(from, (next.get(from) ?? 1) - 1);
    }
    const mine = next.get(classId) ?? 0;
    if (mine >= catalog.maximumCopiesPerClass) {
      return `${className(classId)} already fields its pair — ${catalog.maximumCopiesPerClass} is the cap.`;
    }
    if (mine >= 1) {
      const duplicatesAfter = [...next.entries()].reduce((sum, [id, count]) =>
        sum + Math.max(0, (id === classId ? count + 1 : count) - 1), 0);
      if (duplicatesAfter > duplicateBudget) {
        const doubled = [...next.entries()]
          .find(([id, count]) => id !== classId && count >= 2)?.[0];
        return doubled
          ? `Only one class may field a pair — ${className(doubled)} already does.`
          : 'Only one class may field a pair.';
      }
    }
    return null;
  };
  const pickClass = (classId: string) => {
    const blocked = blockReason(classId);
    if (blocked) {
      notify?.(blocked, 'hot');
      return;
    }
    if (selected !== null && squad[selected]) {
      onChange(normalize(squad.map((bot, index) =>
        index === selected ? { ...bot, class: classId } : bot)));
      setSelected(null);
      return;
    }
    if (filled >= 8) {
      notify?.('The squad is full — select a bot to swap its class.', 'hot');
      return;
    }
    onChange(normalize([...squad, makeBot(filled, classId)]));
  };
  const autoFill = () => {
    onChange(autoFillSquad(catalog, squad));
    notify?.('Squad filled out.');
  };
  return (
    <Modal onClose={onClose} wide>
      <div className="mb-2 flex items-center gap-2">
        <h2 className="type-display text-[16px]">Squad — pick eight</h2>
        <span className="val">{filled}/8</span>
        <span className="grow" />
        <button type="button" className="btn" onClick={autoFill} disabled={filled >= 8}
          data-testid="squad-autofill">Auto-fill the rest</button>
        <button type="button" className="btn btn-primary" onClick={onClose} data-testid="squad-done">Done</button>
      </div>
      <p className="t-micro mb-1.5 text-arena-dim">
        One class may field a pair — every other class is a single.
      </p>
      <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-4" data-testid="class-grid">
        {catalog.classes.map((entry) => {
          const count = counts.get(entry.id) ?? 0;
          const blocked = entry.unlocked ? blockReason(entry.id) : null;
          const swapping = selected !== null && squad[selected]?.class === entry.id;
          const sprite = classSprite(entry.id);
          return (
            <button key={entry.id} type="button"
              className={`choice-card flex items-center gap-2 text-left ${swapping ? '!border-arena-material' : ''} ${blocked ? 'opacity-45' : ''}`}
              disabled={!entry.unlocked}
              data-testid={`class-${entry.id}`}
              title={blocked ?? entry.signatureName}
              onClick={() => pickClass(entry.id)}>
              {sprite && <img src={sprite} alt=""
                className={`h-8 w-8 shrink-0 object-contain drop-shadow ${entry.unlocked ? '' : 'opacity-40 grayscale'}`} />}
              <span className="min-w-0">
                <span className="block text-[12px] font-semibold leading-tight">
                  {entry.name}{!entry.unlocked && ' 🔒'}
                </span>
                <span className="block t-micro leading-tight text-arena-dim">{entry.fantasy}</span>
              </span>
              {count > 0 && (
                <span className="pill ml-auto shrink-0 !text-[9px] text-arena-text">×{count}</span>
              )}
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
                <button type="button" className="flex grow items-center gap-2 text-left"
                  data-testid={`squad-row-${index}`}
                  onClick={() => setSelected(selected === index ? null : index)}>
                  {classSprite(bot.class) && <img src={classSprite(bot.class)} alt=""
                    className="h-7 w-7 shrink-0 object-contain drop-shadow" />}
                  <span>
                    <span className="t-micro font-semibold">{bot.name}</span>
                    <span className="t-micro text-arena-dim"> · {entry?.name ?? bot.class}</span>
                  </span>
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
      {selected !== null && squad[selected] ? (
        <div className="mt-3">
          <div className="lab mb-1">Build for {squad[selected].name}</div>
          <div className="grid grid-cols-2 gap-1.5 sm:grid-cols-4">
            {BUILDS.map((build) => (
              <button key={build.id} type="button"
                className={`choice-card text-left ${squad[selected]?.build === build.id ? '!border-arena-material' : ''}`}
                onClick={() => onChange(normalize(squad.map((value, i) =>
                  i === selected ? { ...value, build: build.id } : value)))}>
                <span className="block text-[12px] font-semibold">{build.label}</span>
                <span className="block t-micro text-arena-dim">{build.blurb}</span>
              </button>
            ))}
          </div>
        </div>
      ) : filled > 0 && (
        <p className="t-micro mt-3 text-arena-dim">New bots arrive <b>Balanced</b>. Select one to set its
          build here, or tap the build tag on its row to cycle. The upgrade path itself is generated —
          you never see it.</p>
      )}
    </Modal>
  );
}
