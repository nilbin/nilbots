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
type DeliveryProcessedCueId =
  | 'attack-windup'
  | 'projectile'
  | 'impact'
  | 'destroyed'
  | 'level-up'
  | 'arc-bank'
  | 'arc-pulse'
  | 'arc-handoff'
  | 'arrival';

interface SoundEffectCueMix {
  gain: number;
  voiceLimit: number;
  /** Maximum absolute stereo pan applied to an edge-of-map event. */
  panWidth: number;
  /** Whether this cue enters the shared arena reflection send. */
  roomSend: boolean;
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
    deliveryProcessing: {
      policy: string;
      cues: Record<
        DeliveryProcessedCueId,
        {
          sourceCueId?: 'attack-fizzle';
          channelTransform:
            | 'side-to-dual-mono'
            | 'mid-to-dual-mono'
            | 'preserve-stereo';
          sourceStartSeconds: number;
          playbackRate: number;
          playbackRateEnd?: number;
          durationSeconds: number;
          lowPassHz: number;
          fadeInSeconds: number;
          fadeOutSeconds: number;
          targetSamplePeak: number;
        }
      >;
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
    silentOutcomeCueIds: ['attack-cancel'];
    cueAliases: {
      'attack-fizzle': 'projectile';
      'arc-steal': 'arc-pickup';
    };
    tickMix: {
      coalesceNonProjectileCues: true;
      projectileReleasesPerTick: 4;
      projectileStaggerTicks: 0.04;
      suppressWindupUnderForegroundEvents: true;
      projectileWithConcreteOutcomes: 'layer-per-release-up-to-limit';
    };
  };
  cues: Record<SoundEffectCueId, string>;
  integrity: Record<
    SoundEffectCueId,
    {
      sourceSha256: string;
      outputSha256: string;
      bytes: number;
      bitrate: number;
      deliveryMetrics: {
        durationSeconds: number;
        samplePeakDbfs: number;
        rmsDbfs: number;
        dominantFrequencyHz: number;
        interchannelCorrelation: number;
        sourceInterchannelCorrelation?: number;
        spectralCentroidHz: number;
        energyAbove4kPercent: number;
        energyAbove8kPercent: number;
        energyAbove12kPercent: number;
        maximumWindowAbove4kPercent: number;
        maximumWindowAbove4kAtSeconds: number;
        maximumWindowAbove4kRmsDbfs: number;
        effectiveSamplePeakDbfs: number;
        effectiveRmsDbfs: number;
        effectiveMaximumWindowAbove4kRmsDbfs: number;
      };
    }
  >;
}

export interface SoundEffectPack {
  id: SoundEffectPackId;
  label: string;
  cues: Record<SoundEffectCueId, string>;
  mix: {
    roomSend: number;
    cues: Record<SoundEffectCueId, SoundEffectCueMix>;
  };
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
    manifest.version !== 8 ||
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
    !validDeliveryProcessing(
      manifest.provenance.deliveryProcessing?.cues,
    ) ||
    manifest.provenance.rightsStatus !== 'rights-cleared' ||
    manifest.provenance.license?.termsUrl !==
      'https://sfxengine.com/terms-of-service' ||
    manifest.provenance.license.permittedUse !==
      'personal-and-commercial-projects' ||
    manifest.provenance.license.standaloneRedistribution !== 'prohibited' ||
    manifest.provenance.shipApproval !== 'approved' ||
    JSON.stringify(manifest.behavior?.silentOutcomeCueIds) !==
      JSON.stringify(['attack-cancel']) ||
    manifest.behavior?.cueAliases?.['attack-fizzle'] !== 'projectile' ||
    manifest.behavior?.cueAliases?.['arc-steal'] !== 'arc-pickup' ||
    manifest.behavior?.tickMix?.coalesceNonProjectileCues !== true ||
    manifest.behavior.tickMix.projectileReleasesPerTick !== 4 ||
    manifest.behavior.tickMix.projectileStaggerTicks !== 0.04 ||
    manifest.behavior.tickMix.suppressWindupUnderForegroundEvents !== true ||
    manifest.behavior.tickMix.projectileWithConcreteOutcomes !==
      'layer-per-release-up-to-limit' ||
    !isUnitGain(manifest.mix?.roomSend) ||
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
  assertExactKeys(
    manifest.integrity,
    SOUND_EFFECT_CUE_IDS,
    manifestPath,
    'integrity records',
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
        !isUnitGain(mix.panWidth) ||
        typeof mix.roomSend !== 'boolean' ||
        !validIntegrity(integrity)
      ) {
        throw new Error(
          `Sound-effect pack '${manifest.id}' has invalid metadata for '${cue}'.`,
        );
      }
      return [cue, url];
    }),
  ) as Record<SoundEffectCueId, string>;

  return {
    id: manifest.id,
    label: manifest.label,
    cues,
    mix: manifest.mix,
  };
}

function validDeliveryProcessing(
  cues:
    | SoundEffectManifest['provenance']['deliveryProcessing']['cues']
    | undefined,
): boolean {
  if (!cues) return false;
  if (
    JSON.stringify(Object.keys(cues).sort()) !==
    JSON.stringify(
      [
        'arc-bank',
        'arc-handoff',
        'arc-pulse',
        'arrival',
        'attack-windup',
        'destroyed',
        'impact',
        'level-up',
        'projectile',
      ],
    )
  ) {
    return false;
  }
  return Object.entries(cues).every(
    ([id, cue]) =>
      (cue.sourceCueId === undefined ||
        (id === 'projectile' && cue.sourceCueId === 'attack-fizzle')) &&
      ['side-to-dual-mono', 'mid-to-dual-mono', 'preserve-stereo'].includes(
        cue.channelTransform,
      ) &&
      Number.isFinite(cue.sourceStartSeconds) &&
      cue.sourceStartSeconds >= 0 &&
      cue.sourceStartSeconds <= 1 &&
      Number.isFinite(cue.playbackRate) &&
      cue.playbackRate >= 0.1 &&
      cue.playbackRate <= 1.5 &&
      (cue.playbackRateEnd === undefined ||
        (Number.isFinite(cue.playbackRateEnd) &&
          cue.playbackRateEnd >= 0.1 &&
          cue.playbackRateEnd <= 1.5)) &&
      Number.isFinite(cue.durationSeconds) &&
      cue.durationSeconds > 0 &&
      cue.durationSeconds <= 4 &&
      Number.isFinite(cue.lowPassHz) &&
      cue.lowPassHz >= 500 &&
      cue.lowPassHz <= 4_500 &&
      isUnitGain(cue.targetSamplePeak) &&
      cue.targetSamplePeak <= 0.55 &&
      Number.isFinite(cue.fadeInSeconds) &&
      cue.fadeInSeconds >= 0.01 &&
      Number.isFinite(cue.fadeOutSeconds) &&
      cue.fadeOutSeconds >= 0.05 &&
      cue.fadeOutSeconds <= 0.5,
  );
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
        deliveryMetrics: {
          durationSeconds: number;
          samplePeakDbfs: number;
          rmsDbfs: number;
          dominantFrequencyHz: number;
          interchannelCorrelation: number;
          spectralCentroidHz: number;
          energyAbove4kPercent: number;
          energyAbove8kPercent: number;
          energyAbove12kPercent: number;
          maximumWindowAbove4kPercent: number;
          maximumWindowAbove4kAtSeconds: number;
          maximumWindowAbove4kRmsDbfs: number;
          effectiveSamplePeakDbfs: number;
          effectiveRmsDbfs: number;
          effectiveMaximumWindowAbove4kRmsDbfs: number;
        };
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
      value.bitrate > 0 &&
      Number.isFinite(value.deliveryMetrics?.durationSeconds) &&
      value.deliveryMetrics.durationSeconds > 0 &&
      Number.isFinite(value.deliveryMetrics.samplePeakDbfs) &&
      Number.isFinite(value.deliveryMetrics.rmsDbfs) &&
      Number.isFinite(value.deliveryMetrics.dominantFrequencyHz) &&
      Number.isFinite(value.deliveryMetrics.interchannelCorrelation) &&
      Number.isFinite(value.deliveryMetrics.spectralCentroidHz) &&
      Number.isFinite(value.deliveryMetrics.energyAbove4kPercent) &&
      value.deliveryMetrics.energyAbove4kPercent <= 3 &&
      Number.isFinite(value.deliveryMetrics.energyAbove8kPercent) &&
      Number.isFinite(value.deliveryMetrics.energyAbove12kPercent) &&
      value.deliveryMetrics.energyAbove12kPercent <= 0.05 &&
      Number.isFinite(value.deliveryMetrics.maximumWindowAbove4kPercent) &&
      Number.isFinite(value.deliveryMetrics.maximumWindowAbove4kAtSeconds) &&
      Number.isFinite(value.deliveryMetrics.maximumWindowAbove4kRmsDbfs) &&
      Number.isFinite(value.deliveryMetrics.effectiveSamplePeakDbfs) &&
      value.deliveryMetrics.effectiveSamplePeakDbfs <= -9 &&
      Number.isFinite(value.deliveryMetrics.effectiveRmsDbfs) &&
      value.deliveryMetrics.effectiveRmsDbfs <= -25 &&
      Number.isFinite(
        value.deliveryMetrics.effectiveMaximumWindowAbove4kRmsDbfs,
      ) &&
      (value.deliveryMetrics.maximumWindowAbove4kPercent <= 10 ||
        value.deliveryMetrics.effectiveMaximumWindowAbove4kRmsDbfs <= -28),
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
