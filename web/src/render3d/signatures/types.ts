import type * as THREE from 'three';
import type {
  ReplayArcRelayFact,
  ReplayArcRelayModeState,
} from '../../replayModel';
import type { ArenaTheme } from '../../render/arenaThemes';

export type ArcSignature =
  ReplayArcRelayModeState['visibleSignatures'][number];

export type ArcSignatureWebGLPhase =
  | 'tell'
  | 'active'
  | 'channel'
  | 'in-flight';

/**
 * One pooled primitive rig. Painters may reveal and transform these meshes,
 * but must never allocate geometry or material from their per-frame paint.
 */
export interface ArcSignatureEffectRig {
  group: THREE.Group;
  main: THREE.MeshBasicMaterial;
  fill: THREE.MeshBasicMaterial;
  solid: THREE.MeshStandardMaterial;
  /** Dark backing for object-led silhouettes that need readable panel seams. */
  inset: THREE.MeshStandardMaterial;
  smoke: THREE.MeshStandardMaterial;
  disc: THREE.Mesh;
  ring: THREE.Mesh;
  rings: THREE.Mesh[];
  torus: THREE.Mesh;
  toruses: THREE.Mesh[];
  capsule: THREE.Mesh;
  /** Larger matte action body for signatures whose read is an object, not a spark. */
  payload: THREE.Mesh;
  /** Distinct heavy payload used by impacts/deployments that need a machined silhouette. */
  pod: THREE.Mesh;
  /** Matte spool/winch body for tether and exchange silhouettes. */
  spool: THREE.Mesh;
  /** Low-profile mechanical hub for pressure fields and deployment sockets. */
  hub: THREE.Mesh;
  /** Low-poly shell used for smoke and other broad soft silhouettes. */
  cloud: THREE.Mesh;
  /** Matte destination/impact plate, distinct from transparent energy floor cues. */
  plate: THREE.Mesh;
  /** Historical-only vertical marker; authored painters intentionally avoid it. */
  beam: THREE.Mesh;
  segments: THREE.Mesh[];
  panels: THREE.Mesh[];
  frames: THREE.Mesh[];
  slabs: THREE.Mesh[];
  particles: THREE.Mesh[];
  wisps: THREE.Mesh[];
  markers: THREE.Mesh[];
  posts: THREE.Mesh[];
  /** Broad vertical fins/arrows that remain legible at the strategic camera. */
  fins: THREE.Mesh[];
  /** Solid bars for broad ribbons, lanes, arrows and crosshairs at overview scale. */
  bars: THREE.Mesh[];
  /** Narrow dark bars used as panel seams and mechanical rails. */
  insets: THREE.Mesh[];
  /** Heavy matte cable that remains visible without additive glare. */
  cable: THREE.Mesh;
}

export interface ArcSignaturePaintInput {
  rig: ArcSignatureEffectRig;
  signature: ArcSignature;
  points: THREE.Vector3[];
  owner: THREE.Vector3 | undefined;
  target: THREE.Vector3 | undefined;
  /** Exact public Core flight sample for Arc Toss; absent for every other signature. */
  coreFlight: {
    source: THREE.Vector3;
    destination: THREE.Vector3;
    progress: number;
  } | undefined;
  /** Replay-owned arena theme; object painters may borrow its material language. */
  arenaTheme: ArenaTheme;
  time: number;
  phase: ArcSignatureWebGLPhase;
  persistentPropReady: boolean;
  reducedMotion: boolean;
  /** Fraction inside the current public phase, sampled only from replay boundaries. */
  phaseProgress: number;
  /** First tick at which this exact public phase was visible for the operation. */
  phaseStartedTick: number;
  /** Deterministic age inside the current public phase. */
  phaseAge: number;
  /** Age inside the current tick; useful for a deterministic deployment settle. */
  tickFraction: number;
}

export type ArcSignatureOutcomeFact = Extract<
  ReplayArcRelayFact,
  { operationId: string }
>;

export interface ArcSignatureOutcomePaintInput {
  rig: ArcSignatureEffectRig;
  signatureId: string;
  priorSignature: ArcSignature | null;
  facts: readonly ArcSignatureOutcomeFact[];
  owner: THREE.Vector3 | undefined;
  /** Replay-owned arena theme, identical to the one used by the static board. */
  arenaTheme: ArenaTheme;
  time: number;
  fraction: number;
  reducedMotion: boolean;
}

export interface ArcSignatureWebGLPainter {
  signatureId: string;
  paint(input: ArcSignaturePaintInput): void;
  paintOutcome?(input: ArcSignatureOutcomePaintInput): void;
}

export function defineArcSignatureWebGLPainter(
  painter: ArcSignatureWebGLPainter,
): ArcSignatureWebGLPainter {
  return Object.freeze(painter);
}
