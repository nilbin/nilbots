import type * as THREE from 'three';
import type {
  ReplayArcRelayFact,
  ReplayArcRelayModeState,
} from '../../replayModel';

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
  smoke: THREE.MeshStandardMaterial;
  disc: THREE.Mesh;
  ring: THREE.Mesh;
  torus: THREE.Mesh;
  capsule: THREE.Mesh;
  /** Historical-only vertical marker; authored painters intentionally avoid it. */
  beam: THREE.Mesh;
  segments: THREE.Mesh[];
  panels: THREE.Mesh[];
  particles: THREE.Mesh[];
  markers: THREE.Mesh[];
}

export interface ArcSignaturePaintInput {
  rig: ArcSignatureEffectRig;
  signature: ArcSignature;
  points: THREE.Vector3[];
  owner: THREE.Vector3 | undefined;
  target: THREE.Vector3 | undefined;
  time: number;
  phase: ArcSignatureWebGLPhase;
  persistentPropReady: boolean;
  reducedMotion: boolean;
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
