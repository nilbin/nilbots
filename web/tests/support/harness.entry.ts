/**
 * Renderer entry for golden-frame tests.
 *
 * `arenaThemes` uses `import.meta.glob`, which only exists inside Vite — so the renderer
 * cannot be imported by bare Node. This entry is built through Vite's SSR pipeline, which
 * resolves the globs, producing a bundle a test can import directly.
 */
export { drawArena } from '../../src/render/drawArena';
export {
  arrivalsAt,
  boltsAt,
  posesAt,
  spentBoltsAt,
} from '../../src/render/interpolate';
export { frontlineCaptureVisual } from '../../src/render/frontlineCaptureVisual';
export {
  ARENA_MARGIN_TILES,
  ArenaCamera,
  arenaViewport,
  directorMinSpan,
  directorShotHoldTicks,
  focusFrame,
  focusPointsAt,
  frameEscapes,
  fullArenaFrame,
  strategicOverviewFrame,
} from '../../src/render/arenaCamera';
export { arcOriginAccent, createPresenter } from '../../src/replayPresentation';
export {
  parsePlayRoleTag,
  playAwarenessTimeline,
  playForUnit,
  playRoleSummary,
  playsAt,
} from '../../src/presentation/playAwareness';
export { defaultPlaybackSpeed } from '../../src/playback';
export {
  DESKTOP_ARENA_RENDER_PROFILE,
  FULL_ARENA_RENDER_PROFILE,
  MOBILE_ARENA_RENDER_PROFILE,
  arenaWeightedPixelsPerSecond,
  createArenaFramePacer,
  selectArenaRenderProfile,
  takeArenaFrame,
} from '../../src/render/arenaRenderProfile';
export {
  classFamilyForForm,
  fallbackProjectileLookIdForForm,
  fallbackLookIdForForm,
  stanceFormForUnit,
  stanceKindForForm,
  unitAccent,
  unitEmplacedLook,
  unitLook,
  unitProjectileLook,
  unitStanceLook,
} from '../../src/render/unitPresentation';
export {
  applyTeamAccentToSvg,
  classIconLook,
  presentationBotLook,
} from '../../src/render/arenaThemes';
export {
  volleyArrowOutline,
  volleyLanes,
  volleysAt,
} from '../../src/render/volley';
export {
  buildAdaptiveTimeline,
  sampleAdaptiveTimeline,
} from '../../src/soundtrack/director';
export { buildRetrospectiveMusicPlan } from '../../src/soundtrack/planner';
export {
  loadReplayJson,
  loadReplayObject,
} from '../../src/replayIngress';
export {
  buildActors,
  installMobileModel,
  signedTravelByActor,
} from '../../src/render3d/arenaActors';
export {
  buildArena,
  WALL_OPEN_EDGE_INSET,
} from '../../src/render3d/arenaScene';
export { buildOverlays } from '../../src/render3d/arenaOverlays';
export {
  isGenuineLookModel,
  lookModel,
  lookModelSource,
  modelSpec,
  signatureModel,
} from '../../src/render3d/lookModel';
export {
  arcMotionFrame,
  createArcModelMotionRig,
} from '../../src/render3d/arcModelMotion';
export {
  ARC_SIGNATURE_STYLES,
  ARC_SIGNATURE_PROP_SCALE,
  arcSignatureVisualPhase,
  latestObservedSignatureYaw,
} from '../../src/render3d/arcRelayEffects';
export { default as LabsPanel } from '../../src/site/components/LabsPanel';
export {
  arenaModeParticipantsReady,
  ArenaActionProvider,
  defaultChallengeContextRole,
  resolveChallengeParticipants,
} from '../../src/site/components/ArenaAction';
