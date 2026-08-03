export type ArenaRenderProfileId = 'full' | 'mobile';

export interface ArenaRenderProfile {
  id: ArenaRenderProfileId;
  activeFramesPerSecond: number;
  idleFramesPerSecond: number;
  webglMaxPixelRatio: number;
  canvasMaxPixelRatio: number;
  shadowMapSize: number;
  powerPreference: WebGLPowerPreference;
}

export interface ArenaRenderEnvironment {
  coarsePointer: boolean;
  maxTouchPoints: number;
  viewportWidth: number;
  viewportHeight: number;
  override?: string | null;
}

/** The desktop/reference path, also used by `?render-profile=full` A/B evidence. */
export const FULL_ARENA_RENDER_PROFILE: ArenaRenderProfile = Object.freeze({
  id: 'full',
  activeFramesPerSecond: 60,
  idleFramesPerSecond: 60,
  webglMaxPixelRatio: 2,
  canvasMaxPixelRatio: Number.POSITIVE_INFINITY,
  shadowMapSize: 2048,
  powerPreference: 'high-performance',
});

/**
 * Sustained phone-watch budget.
 *
 * A replay is watched for minutes, not inspected for one frame. Thirty presentation
 * frames preserve causal interpolation while halving scene submission; DPR 1.5 keeps a
 * 48 px bot comfortably above one physical pixel per authored detail without paying the
 * 4x/9x fill-rate tax of DPR 2/3. A 1024 shadow map is still larger than the landscape
 * phone's arena height and costs one quarter of the former 2048 map per update.
 */
export const MOBILE_ARENA_RENDER_PROFILE: ArenaRenderProfile = Object.freeze({
  id: 'mobile',
  activeFramesPerSecond: 30,
  idleFramesPerSecond: 12,
  webglMaxPixelRatio: 1.5,
  canvasMaxPixelRatio: 1.5,
  shadowMapSize: 1024,
  powerPreference: 'low-power',
});

/** Select the power profile without user-agent inference. */
export function selectArenaRenderProfile(
  environment: ArenaRenderEnvironment,
): ArenaRenderProfile {
  if (environment.override === 'full') return FULL_ARENA_RENDER_PROFILE;
  if (environment.override === 'mobile') return MOBILE_ARENA_RENDER_PROFILE;

  const shortEdge = Math.min(environment.viewportWidth, environment.viewportHeight);
  const phoneLikeTouchViewport = environment.maxTouchPoints > 0 && shortEdge <= 1024;
  return environment.coarsePointer || phoneLikeTouchViewport
    ? MOBILE_ARENA_RENDER_PROFILE
    : FULL_ARENA_RENDER_PROFILE;
}

/** Resolve the current browser once when a viewer mounts. */
export function currentArenaRenderProfile(owner: Window = window): ArenaRenderProfile {
  return selectArenaRenderProfile({
    coarsePointer: owner.matchMedia('(pointer: coarse)').matches,
    maxTouchPoints: owner.navigator.maxTouchPoints,
    viewportWidth: owner.innerWidth,
    viewportHeight: owner.innerHeight,
    override: new URLSearchParams(owner.location.search).get('render-profile'),
  });
}

/**
 * Whether a timestamp is due under a capped animation loop.
 *
 * The one-millisecond tolerance prevents a nominal 60 Hz clock (`16.666…`) from missing
 * every second 30 Hz frame because of timer rounding and falling to 20 fps.
 */
export function arenaFrameDue(
  stamp: number,
  previousStamp: number | null,
  framesPerSecond: number,
): boolean {
  return arenaPresentedFrameStamp(stamp, previousStamp, framesPerSecond) !== null;
}

/**
 * Return the cadence anchor for a due frame, or null when this display refresh is early.
 *
 * Anchoring to the ideal cadence instead of the latest (slightly late) callback prevents
 * timer jitter from accumulating. Without that correction a requested 30 fps settled at
 * roughly 26 fps in mobile WebKit because every late frame moved the next deadline too.
 */
export function arenaPresentedFrameStamp(
  stamp: number,
  previousStamp: number | null,
  framesPerSecond: number,
): number | null {
  if (previousStamp === null) return stamp;
  const interval = 1000 / framesPerSecond;
  const elapsed = stamp - previousStamp;
  if (elapsed < interval - 1) return null;
  const elapsedIntervals = Math.max(1, Math.floor((elapsed + 1) / interval));
  return previousStamp + elapsedIntervals * interval;
}

/** Conservative fill-work proxy: screen color pass plus one full shadow-map pass. */
export function arenaWeightedPixelsPerSecond(
  cssWidth: number,
  cssHeight: number,
  profile: ArenaRenderProfile,
  devicePixelRatio: number,
): number {
  const ratio = Math.min(devicePixelRatio, profile.webglMaxPixelRatio);
  return profile.activeFramesPerSecond * (
    cssWidth * cssHeight * ratio * ratio + profile.shadowMapSize * profile.shadowMapSize
  );
}
