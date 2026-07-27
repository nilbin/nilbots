export type AdaptiveScoreState =
  | 'sparse'
  | 'tension'
  | 'pursuit'
  | 'combat'
  | 'climax'
  | 'resolve';

export type SoundtrackSectionRole = 'hold' | 'bridge' | 'stinger' | 'resolve';

export type SoundtrackTrigger =
  | 'contact'
  | 'shot'
  | 'damage'
  | 'destruction'
  | 'overtime'
  | 'resolve';

export interface SoundtrackTriggerEvent {
  type: SoundtrackTrigger;
  sourceTick: number;
}

export interface StemResponse {
  /** Intensity where this stem starts to become audible. */
  minimum: number;
  /** Intensity where this stem reaches its configured gain. */
  full: number;
}

export interface SoundtrackStem {
  id: string;
  label: string;
  role: string;
  /** Gain at full response. Source-stem balance is preserved unless overridden. */
  gainDb: number;
  response: StemResponse;
}

export interface SoundtrackLoop {
  boundarySimilarity: number;
  sourceBoundarySimilarity: number;
  /** True only when the compiler altered the delivered assets, not merely analyzed them. */
  rendered: true;
  crossfadeSeconds: number;
  /**
   * The compiler blends the natural continuation after the section end into
   * the matching section head while preserving the exact bar-grid duration.
   */
  strategy: 'rendered-head-crossfade';
  curve: 'equal-power' | 'linear';
  crossfadeFrames: number;
  continuationFrames: number;
  seamJumpDbfs: number;
  blendPeakDbfs: number;
  packHeadPeakDbfs: number;
  headroomTreatment: 'none' | 'linear-blend-fallback';
  approvalStatus: 'analysis-reviewed' | 'auditioned';
  auditionRequired: boolean;
}

export interface SoundtrackRepeatPolicy {
  /** Complete musical bars before a same-state hold may rotate. */
  minimumBars: number;
}

export interface SoundtrackSection {
  id: string;
  label: string;
  classification: AdaptiveScoreState;
  role: SoundtrackSectionRole;
  startBar: number;
  barCount: number;
  durationSeconds: number;
  energy: number;
  loopable: boolean;
  loop?: SoundtrackLoop;
  repeat?: SoundtrackRepeatPolicy;
  /** Minimum time before this authored stinger may be armed again. */
  cooldownSeconds?: number;
  /** Stem ids map to paths relative to the manifest. Silent stems may be omitted. */
  files: Record<string, string>;
  stemGainsDb?: Record<string, number>;
}

export interface SoundtrackTransition {
  from: string;
  to: string;
  /** Natural source edits wait for the section end; approved adaptive cuts may jump sooner. */
  timing: 'next-quantum' | 'section-end';
  /** Start a transition only on this many-bar boundary. */
  quantizeBars: number;
  /** Duration of the equal-power overlap. */
  crossfadeBars: number;
  weight: number;
}

export interface SoundtrackAsset {
  sha256: string;
  bytes: number;
}

export interface SoundtrackAdaptiveSeam {
  strategy: 'staged';
  /** Bars spent withdrawing high-energy stems before the edit point. */
  retreatBars: number;
  /** Brief full-mix handoff at the edit point. */
  overlapBars: number;
  /** Bars spent restoring the incoming section's high-energy stems. */
  riseBars: number;
  curve: 'linear';
}

/**
 * One source-contiguous cue used when a completed replay can be planned around
 * its known primary highlight. `anchorBar` is the musical peak marker relative
 * to the start of this cue.
 */
export interface SoundtrackRetrospectiveCue {
  id: string;
  startBar: number;
  barCount: number;
  anchorBar: number;
  durationSeconds: number;
  /** Stem ids map to paths relative to the manifest. */
  files: Record<string, string>;
}

/**
 * Premixed source-contiguous cue for straight-through playback.
 * Its encoded balance is authoritative; runtime stem processing is bypassed.
 */
export interface SoundtrackStraightThroughCue {
  id: string;
  startBar: number;
  barCount: number;
  durationSeconds: number;
  file: string;
}

export interface SoundtrackProvenance {
  sourceTool: string;
  rightsStatus: 'user-supplied-unverified' | 'rights-cleared';
  shipApproval: 'pending' | 'approved';
}

export interface SoundtrackBuild {
  /** Immutable directory name containing this manifest. */
  version: string;
  pipelineVersion: number;
  sourceSha256: string;
  configSha256: string;
  encoder: {
    name: string;
    version: string;
    codec: string;
    bitrateKbps: number;
  };
  /** Analysis asset path relative to this manifest. */
  analysis: string;
}

export interface SoundtrackManifest {
  schemaVersion: 1;
  id: string;
  title: string;
  provenance: SoundtrackProvenance;
  bpm: number;
  beatsPerBar: number;
  sampleRate: number;
  /** Musical bar one starts here; source files may contain an exported preroll. */
  gridOriginFrame: number;
  barFrames: number;
  sourceEndFrame: number;
  segmentBars: number;
  durationSeconds: number;
  masterGainDb: number;
  adaptiveLatencyBudgetBars: {
    gameplay: number;
    resolve: number;
  };
  /** Optional slower treatment for non-contiguous live/fallback edits. */
  adaptiveSeam?: SoundtrackAdaptiveSeam;
  /** Optional continuous cue for whole-replay narrative planning. */
  retrospectiveCue?: SoundtrackRetrospectiveCue;
  /** Optional premixed cue for straight-through playback. */
  straightThroughCue?: SoundtrackStraightThroughCue;
  entrySection: string;
  stems: SoundtrackStem[];
  sections: SoundtrackSection[];
  transitions: SoundtrackTransition[];
  assets: Record<string, SoundtrackAsset>;
  build: SoundtrackBuild;
}

export interface SoundtrackCatalogEntry {
  id: string;
  title: string;
  /** Path relative to the catalog. */
  manifest: string;
}

export interface SoundtrackCatalog {
  schemaVersion: 1;
  defaultId: string;
  tracks: SoundtrackCatalogEntry[];
}

export type SoundtrackStatus =
  | 'unavailable'
  | 'off'
  | 'armed'
  | 'loading'
  | 'playing'
  | 'paused'
  | 'error';

export type SoundtrackPlaybackMode = 'adaptive' | 'straight';
