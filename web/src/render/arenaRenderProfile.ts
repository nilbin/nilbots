export type ArenaRenderProfileId = 'full' | 'desktop' | 'mobile';

export interface ArenaRenderProfile {
  id: ArenaRenderProfileId;
  presentationRateLimited: boolean;
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

/** The unrestricted historical path, retained only for `?render-profile=full` evidence. */
export const FULL_ARENA_RENDER_PROFILE: ArenaRenderProfile = Object.freeze({
  id: 'full',
  presentationRateLimited: false,
  activeFramesPerSecond: 60,
  idleFramesPerSecond: 60,
  webglMaxPixelRatio: 2,
  canvasMaxPixelRatio: Number.POSITIVE_INFINITY,
  shadowMapSize: 2048,
  powerPreference: 'high-performance',
});

/**
 * Sustained desktop-watch budget.
 *
 * Desktop keeps the reference resolution and shadow fidelity. It only stops submitting
 * duplicate work above 60 Hz, lets paused micro-life breathe at 12 Hz, and avoids forcing
 * a discrete GPU when the browser's default adapter can render the match comfortably.
 */
export const DESKTOP_ARENA_RENDER_PROFILE: ArenaRenderProfile = Object.freeze({
  id: 'desktop',
  presentationRateLimited: true,
  activeFramesPerSecond: 60,
  idleFramesPerSecond: 12,
  webglMaxPixelRatio: 2,
  canvasMaxPixelRatio: 2,
  shadowMapSize: 2048,
  powerPreference: 'default',
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
  presentationRateLimited: true,
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
  if (environment.override === 'desktop') return DESKTOP_ARENA_RENDER_PROFILE;
  if (environment.override === 'mobile') return MOBILE_ARENA_RENDER_PROFILE;

  const shortEdge = Math.min(environment.viewportWidth, environment.viewportHeight);
  const phoneLikeTouchViewport = environment.maxTouchPoints > 0 && shortEdge <= 1024;
  return environment.coarsePointer || phoneLikeTouchViewport
    ? MOBILE_ARENA_RENDER_PROFILE
    : DESKTOP_ARENA_RENDER_PROFILE;
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

export interface ArenaFramePacer {
  lastRefreshStamp: number | null;
  elapsed: number;
  framesPerSecond: number | null;
}

/**
 * Create the mutable clock owned by one animation loop.
 */
export function createArenaFramePacer(): ArenaFramePacer {
  return {
    lastRefreshStamp: null,
    elapsed: 0,
    framesPerSecond: null,
  };
}

/**
 * Consume a display timestamp and report whether this loop should present a frame.
 *
 * This is an elapsed-time accumulator rather than a comparison with the last presented
 * stamp. A one-millisecond early allowance absorbs browser timestamp jitter, but resets
 * its negative debt immediately; otherwise a nominal 60 Hz cap can alternately accept an
 * early callback and reject the next one, settling around 43 fps. Excess time is reduced
 * modulo one interval so a backgrounded tab never tries to replay missed visual frames.
 */
export function takeArenaFrame(
  pacer: ArenaFramePacer,
  stamp: number,
  framesPerSecond: number,
): boolean {
  if (
    pacer.lastRefreshStamp === null ||
    pacer.framesPerSecond !== framesPerSecond
  ) {
    pacer.lastRefreshStamp = stamp;
    pacer.elapsed = 0;
    pacer.framesPerSecond = framesPerSecond;
    return true;
  }

  const interval = 1000 / framesPerSecond;
  const sinceRefresh = Math.max(0, stamp - pacer.lastRefreshStamp);
  pacer.lastRefreshStamp = stamp;
  pacer.elapsed += sinceRefresh;
  if (pacer.elapsed < interval - 1) return false;
  pacer.elapsed = pacer.elapsed < interval ? 0 : pacer.elapsed % interval;
  return true;
}

/** Conservative fill-work proxy: screen color pass plus one full shadow-map pass. */
export function arenaWeightedPixelsPerSecond(
  cssWidth: number,
  cssHeight: number,
  profile: ArenaRenderProfile,
  devicePixelRatio: number,
  displayFramesPerSecond = 60,
): number {
  const ratio = Math.min(devicePixelRatio, profile.webglMaxPixelRatio);
  const presentedFramesPerSecond = profile.presentationRateLimited
    ? Math.min(displayFramesPerSecond, profile.activeFramesPerSecond)
    : displayFramesPerSecond;
  return presentedFramesPerSecond * (
    cssWidth * cssHeight * ratio * ratio + profile.shadowMapSize * profile.shadowMapSize
  );
}
