export {
  DEFAULT_MUSIC_DIRECTOR_CONFIG,
  buildAdaptiveTimeline,
  sampleAdaptiveTimeline,
} from './director';

export type {
  AdaptiveMusicFrame,
  AdaptiveMusicKeyframe,
  AdaptiveMusicTimeline,
  MusicDirectorFeatures,
  MusicDirectorOptions,
  MusicMomentumTrend,
  MusicTrigger,
  ResolvedMusicDirectorConfig,
} from './director';

export { buildRetrospectiveMusicPlan } from './planner';

export type {
  AdaptiveMusicHighlight,
  AdaptiveMusicHighlightKind,
  AdaptiveMusicTimelineMode,
  RetrospectiveMusicPlan,
  RetrospectiveMusicPlannerOptions,
} from './planner';

export {
  collectCrossedSoundtrackTriggers,
  createSoundtrackTriggerCursor,
  resetSoundtrackTriggerCursor,
  soundtrackPresentationId,
} from './transport';
