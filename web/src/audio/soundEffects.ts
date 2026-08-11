export const SOUND_EFFECT_CUE_IDS = [
  'attack-windup',
  'projectile',
  'impact',
  'destroyed',
  'level-up',
  'arc-birth',
  'arc-pickup',
  'arc-drop',
  'arc-bank',
  'arc-pulse',
  'arc-handoff',
  'deflect',
  'arrival',
] as const;

export type SoundEffectCueId = (typeof SOUND_EFFECT_CUE_IDS)[number];
export type SoundEffectPackId = 'astral-relay';

interface SoundEffectCueMix {
  gain: number;
  voiceLimit: number;
}

interface SoundEffectManifest {
  version: number;
  id: SoundEffectPackId;
  label: string;
  approval: 'approved';
  format: string;
  sampleRate: number;
  channels: number;
  provenance: {
    provider: 'sfx-engine';
    generatedBy: string[];
    exportedBy: string;
    generationRecord: string;
    generationRecordSha256: string;
    deliveryEncoding: {
      encoder: 'macos-afconvert';
      canonicalization: 'zero-iso-bmff-creation-and-modification-times';
    };
    rightsStatus: 'rights-cleared';
    license: {
      termsUrl: string;
      checkedAt: string;
      permittedUse: 'personal-and-commercial-projects';
      standaloneRedistribution: 'prohibited';
    };
    shipApproval: 'approved';
  };
  mix: {
    roomSend: number;
    cues: Record<SoundEffectCueId, SoundEffectCueMix>;
  };
  behavior: {
    silentOutcomeCueIds: ['attack-fizzle', 'attack-cancel'];
    cueAliases: { 'arc-steal': 'arc-pickup' };
  };
  cues: Record<SoundEffectCueId, string>;
  ambience: {
    id: string;
    sourceCueId: string;
    file: string;
    loop: boolean;
    durationSeconds: number;
    gain: number;
    mapIds: string[];
  };
  integrity: Record<
    string,
    {
      sourceSha256: string;
      outputSha256: string;
      bytes: number;
      bitrate: number;
    }
  >;
}

export interface SoundEffectAmbience {
  id: string;
  url: string;
  loop: boolean;
  durationSeconds: number;
  gain: number;
  mapIds: readonly string[];
}

export interface SoundEffectPack {
  id: SoundEffectPackId;
  label: string;
  cues: Record<SoundEffectCueId, string>;
  mix: {
    roomSend: number;
    cues: Record<SoundEffectCueId, SoundEffectCueMix>;
  };
  ambience: SoundEffectAmbience;
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
  'astral-relay';
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
    manifest.version !== 2 ||
    manifest.id !== DEFAULT_SOUND_EFFECT_PACK_ID ||
    manifest.approval !== 'approved' ||
    manifest.format !== 'aac-lc-m4a' ||
    manifest.sampleRate !== 48_000 ||
    manifest.channels !== 2 ||
    manifest.provenance?.provider !== 'sfx-engine' ||
    !manifest.provenance.generatedBy?.includes(
      '.claude/skills/sfx-engine/scripts/generate-pack.mjs',
    ) ||
    manifest.provenance.exportedBy !==
      'scripts/export-runtime-sound-effects.mjs' ||
    manifest.provenance.deliveryEncoding?.encoder !== 'macos-afconvert' ||
    manifest.provenance.deliveryEncoding.canonicalization !==
      'zero-iso-bmff-creation-and-modification-times' ||
    manifest.provenance.rightsStatus !== 'rights-cleared' ||
    manifest.provenance.license?.termsUrl !==
      'https://sfxengine.com/terms-of-service' ||
    manifest.provenance.license.permittedUse !==
      'personal-and-commercial-projects' ||
    manifest.provenance.license.standaloneRedistribution !== 'prohibited' ||
    manifest.provenance.shipApproval !== 'approved' ||
    JSON.stringify(manifest.behavior?.silentOutcomeCueIds) !==
      JSON.stringify(['attack-fizzle', 'attack-cancel']) ||
    manifest.behavior?.cueAliases?.['arc-steal'] !== 'arc-pickup' ||
    !isUnitGain(manifest.mix?.roomSend) ||
    !isUnitGain(manifest.ambience?.gain) ||
    manifest.ambience?.loop !== true ||
    manifest.ambience.durationSeconds <= 0 ||
    manifest.ambience.mapIds.length === 0 ||
    !/^[a-f0-9]{64}$/.test(manifest.provenance.generationRecordSha256)
  ) {
    throw new Error(`Invalid runtime sound-effect manifest '${manifestPath}'.`);
  }
  assertExactKeys(manifest.cues, SOUND_EFFECT_CUE_IDS, manifestPath, 'cues');
  assertExactKeys(
    manifest.mix.cues,
    SOUND_EFFECT_CUE_IDS,
    manifestPath,
    'cue mix',
  );

  const directory = manifestPath.slice(0, manifestPath.lastIndexOf('/'));
  const cues = Object.fromEntries(
    SOUND_EFFECT_CUE_IDS.map((cue) => {
      const filename = manifest.cues[cue];
      const url = assetUrls[`${directory}/${filename}`];
      const mix = manifest.mix.cues[cue];
      const integrity = manifest.integrity?.[cue];
      if (!url) {
        throw new Error(
          `Sound-effect pack '${manifest.id}' references missing cue '${filename}'.`,
        );
      }
      if (
        !isUnitGain(mix?.gain) ||
        !Number.isInteger(mix.voiceLimit) ||
        mix.voiceLimit < 1 ||
        !validIntegrity(integrity)
      ) {
        throw new Error(
          `Sound-effect pack '${manifest.id}' has invalid metadata for '${cue}'.`,
        );
      }
      return [cue, url];
    }),
  ) as Record<SoundEffectCueId, string>;

  const ambienceUrl = assetUrls[`${directory}/${manifest.ambience.file}`];
  if (
    !ambienceUrl ||
    !validIntegrity(manifest.integrity?.[manifest.ambience.sourceCueId])
  ) {
    throw new Error(
      `Sound-effect pack '${manifest.id}' references invalid ambience '${manifest.ambience.file}'.`,
    );
  }

  return {
    id: manifest.id,
    label: manifest.label,
    cues,
    mix: manifest.mix,
    ambience: {
      id: manifest.ambience.id,
      url: ambienceUrl,
      loop: manifest.ambience.loop,
      durationSeconds: manifest.ambience.durationSeconds,
      gain: manifest.ambience.gain,
      mapIds: manifest.ambience.mapIds,
    },
  };
}

function isUnitGain(value: number | undefined): value is number {
  return (
    typeof value === 'number' &&
    Number.isFinite(value) &&
    value >= 0 &&
    value <= 1
  );
}

function validIntegrity(
  value:
    | {
        sourceSha256: string;
        outputSha256: string;
        bytes: number;
        bitrate: number;
      }
    | undefined,
): boolean {
  return Boolean(
    value &&
      /^[a-f0-9]{64}$/.test(value.sourceSha256) &&
      /^[a-f0-9]{64}$/.test(value.outputSha256) &&
      Number.isInteger(value.bytes) &&
      value.bytes > 0 &&
      Number.isInteger(value.bitrate) &&
      value.bitrate > 0,
  );
}

function assertExactKeys(
  value: Record<string, unknown>,
  expected: readonly string[],
  manifestPath: string,
  label: string,
): void {
  const actual = Object.keys(value).sort();
  const wanted = [...expected].sort();
  if (
    actual.length !== wanted.length ||
    actual.some((key, index) => key !== wanted[index])
  ) {
    throw new Error(
      `Runtime sound-effect manifest '${manifestPath}' has invalid ${label}.`,
    );
  }
}
