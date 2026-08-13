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
  strikeAimsAt,
  strikeSlashesAt,
} from '../../src/render/interpolate';
export { frontlineCaptureVisual } from '../../src/render/frontlineCaptureVisual';
export {
  ARENA_MARGIN_TILES,
  MINIMUM_BOT_RENDER_PIXELS,
  ArenaCamera,
  arenaViewport,
  directorFrameAt,
  directorMinSpan,
  directorShotHoldTicks,
  directorShotInterruptsAt,
  focusFrame,
  focusPointsAt,
  frameEscapes,
  fullArenaFrame,
  reviewFocusFrame,
  selectedUnitPointAt,
  selectionFollowFrame,
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
  SOUND_EFFECT_CUE_IDS,
  soundEffectPack,
} from '../../src/audio/soundEffects';
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
  ACTOR_OCCLUSION_MASK_RENDER_ORDER,
  createActorOcclusionMaskMaterial,
} from '../../src/render3d/occlusionLayers';
export {
  BOT_BODY_RENDER_ORDER,
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
  arcSignatureBodyPose,
  ARC_SIGNATURE_BODY_RELEASE_TICKS,
  createArcModelMotionRig,
  releaseArcSignatureBodyPose,
} from '../../src/render3d/arcModelMotion';
export {
  ARC_SIGNATURE_WEBGL_PAINTERS,
  ARC_SIGNATURE_PROP_SCALE,
  CONSTRUCT_STATUS_BIB_FALLBACK_HEIGHT,
  CONSTRUCT_STATUS_BIB_GAP,
  PERSISTENT_CONSTRUCT_RENDER_ORDER,
  SENTINEL_YAW_SETTLE_TICKS,
  arcSignatureVisualPhase,
  buildArcRelayEffects,
  constructStatusBibHeight,
  configurePersistentConstructRendering,
  latestObservedSignatureYaw,
  sentinelYawTarget,
  surveyFlareFlightEndpoints,
} from '../../src/render3d/arcRelayEffects';
export { orientStatusBib } from '../../src/render3d/statusBib';
export {
  ARC_SIGNATURE_PRESENTATION_ID,
  arcSignaturePresentationDefinitions,
  resolveArcSignaturePresentation,
} from '../../src/render/arcSignaturePresentation';
export {
  arcSignatureAudioDefinitionIds,
  resolveArcSignatureAudioBinding,
} from '../../src/render/arcSignatureAudio';
export {
  buildArcVeterancy,
  LEVEL_UP_BEAT_TICKS,
} from '../../src/replayArcVeterancy';
