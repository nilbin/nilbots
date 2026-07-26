export type AudioCueId = 'projectile' | 'impact' | 'destroyed' | 'unlock';
export type AudioCandidateId =
  | 'aegis-systems'
  | 'obsidian-foundry'
  | 'aurora-core'
  | 'nilbots-signature';

interface AudioCandidateManifest {
  version: number;
  id: AudioCandidateId;
  number: string;
  label: string;
  kicker: string;
  reviewOnly: boolean;
  format: string;
  sampleRate: number;
  channels: number;
  cues: Record<AudioCueId, string>;
}

export interface AudioCandidate {
  id: AudioCandidateId;
  number: string;
  label: string;
  kicker: string;
  cues: Record<AudioCueId, string>;
}

const manifests = import.meta.glob<AudioCandidateManifest>(
  '../assets/audio/candidates/*/manifest.json',
  { eager: true, import: 'default' },
);
const assetUrls = import.meta.glob<string>(
  '../assets/audio/candidates/*/*.m4a',
  { eager: true, query: '?url', import: 'default' },
);

export const audioCandidates: readonly AudioCandidate[] = Object.entries(manifests)
  .map(([manifestPath, manifest]) => buildCandidate(manifestPath, manifest))
  .sort((left, right) => left.number.localeCompare(right.number));

export function audioCandidate(id: AudioCandidateId): AudioCandidate {
  const candidate = audioCandidates.find((item) => item.id === id);
  if (!candidate) throw new Error(`Unknown audio candidate '${id}'.`);
  return candidate;
}

function buildCandidate(
  manifestPath: string,
  manifest: AudioCandidateManifest,
): AudioCandidate {
  if (
    manifest.version !== 1 ||
    manifest.reviewOnly !== true ||
    manifest.format !== 'aac-lc-m4a' ||
    manifest.sampleRate !== 48_000 ||
    manifest.channels !== 2
  ) {
    throw new Error(`Invalid runtime audio manifest '${manifestPath}'.`);
  }
  const directory = manifestPath.slice(0, manifestPath.lastIndexOf('/'));
  const cues = Object.fromEntries(
    (['projectile', 'impact', 'destroyed', 'unlock'] as const).map((cue) => {
      const filename = manifest.cues[cue];
      const url = assetUrls[`${directory}/${filename}`];
      if (!url) {
        throw new Error(
          `Audio candidate '${manifest.id}' references missing cue '${filename}'.`,
        );
      }
      return [cue, url];
    }),
  ) as Record<AudioCueId, string>;

  return {
    id: manifest.id,
    number: manifest.number,
    label: manifest.label,
    kicker: manifest.kicker,
    cues,
  };
}
