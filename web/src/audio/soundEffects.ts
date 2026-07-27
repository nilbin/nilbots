export type SoundEffectCueId = 'projectile' | 'impact' | 'destroyed';
export type SoundEffectPackId = 'obsidian-foundry';

interface SoundEffectManifest {
  version: number;
  id: SoundEffectPackId;
  label: string;
  approval: 'approved';
  format: string;
  sampleRate: number;
  channels: number;
  provenance: {
    generatedBy: string;
    rightsStatus: 'rights-cleared';
    shipApproval: 'approved';
  };
  cues: Record<SoundEffectCueId, string>;
}

export interface SoundEffectPack {
  id: SoundEffectPackId;
  label: string;
  cues: Record<SoundEffectCueId, string>;
}

const manifests = import.meta.glob<SoundEffectManifest>(
  '../assets/audio/effects/*/manifest.json',
  { eager: true, import: 'default' },
);
const assetUrls = import.meta.glob<string>(
  '../assets/audio/effects/*/*.m4a',
  { eager: true, query: '?url', import: 'default' },
);

export const DEFAULT_SOUND_EFFECT_PACK_ID: SoundEffectPackId =
  'obsidian-foundry';
export const soundEffectPack = loadSoundEffectPack();

function loadSoundEffectPack(): SoundEffectPack {
  const entries = Object.entries(manifests);
  if (entries.length !== 1) {
    throw new Error(
      `Expected exactly one approved runtime sound-effect pack, found ${entries.length}.`,
    );
  }
  const [manifestPath, manifest] = entries[0]!;
  if (
    manifest.version !== 1 ||
    manifest.id !== DEFAULT_SOUND_EFFECT_PACK_ID ||
    manifest.approval !== 'approved' ||
    manifest.format !== 'aac-lc-m4a' ||
    manifest.sampleRate !== 48_000 ||
    manifest.channels !== 2 ||
    manifest.provenance?.generatedBy !==
      'scripts/generate-audio-v2-candidates.mjs' ||
    manifest.provenance.rightsStatus !== 'rights-cleared' ||
    manifest.provenance.shipApproval !== 'approved'
  ) {
    throw new Error(`Invalid runtime sound-effect manifest '${manifestPath}'.`);
  }
  const directory = manifestPath.slice(0, manifestPath.lastIndexOf('/'));
  const cues = Object.fromEntries(
    (['projectile', 'impact', 'destroyed'] as const).map((cue) => {
      const filename = manifest.cues[cue];
      const url = assetUrls[`${directory}/${filename}`];
      if (!url) {
        throw new Error(
          `Sound-effect pack '${manifest.id}' references missing cue '${filename}'.`,
        );
      }
      return [cue, url];
    }),
  ) as Record<SoundEffectCueId, string>;

  return {
    id: manifest.id,
    label: manifest.label,
    cues,
  };
}
