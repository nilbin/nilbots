import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';
import {
  SOUND_EFFECT_CUE_IDS,
  soundEffectPack,
} from './.harness/harness.entry.js';

const repoRoot = join(import.meta.dirname, '..', '..');
const root = join(repoRoot, 'web', 'src', 'assets', 'audio', 'effects');
const expectedCues = [
  'arc-bank',
  'arc-birth',
  'arc-drop',
  'arc-handoff',
  'arc-pickup',
  'arc-pulse',
  'arrival',
  'attack-windup',
  'deflect',
  'destroyed',
  'healing',
  'impact',
  'level-up',
  'projectile',
  'signature-arc-toss',
  'signature-construct',
  'signature-exchange',
  'signature-falling-star',
  'signature-kinetic',
  'signature-mason',
  'signature-motion',
  'signature-null',
  'signature-rail-charge',
  'signature-rail-release',
  'signature-repair',
  'signature-sentinel-deploy',
  'signature-sentinel-fire',
  'signature-smoke',
  'signature-survey',
  'signature-target',
  'signature-tether',
  'signature-trip-deploy',
  'signature-trip-trigger',
];
const signatureCues = expectedCues.filter((cue) =>
  cue.startsWith('signature-'),
);

interface Integrity {
  sourceSha256: string;
  outputSha256: string;
  bytes: number;
  bitrate: number;
  deliveryMetrics?: {
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
    maximumHighBandWindowRmsDbfs: number;
    effectiveSamplePeakDbfs: number;
    effectiveRmsDbfs: number;
    effectiveMaximumWindowAbove4kRmsDbfs: number;
    effectiveMaximumHighBandWindowRmsDbfs: number;
  };
}

test('the production sound-effect loader accepts the checked-in pack', () => {
  assert.equal(soundEffectPack.id, 'astral-relay');
  assert.deepEqual([...SOUND_EFFECT_CUE_IDS].sort(), expectedCues);
  assert.deepEqual(Object.keys(soundEffectPack.cues).sort(), expectedCues);
  assert.deepEqual(Object.keys(soundEffectPack.mix.cues).sort(), expectedCues);
});

test('the focused signature review covers every shipped identity at runtime gain', () => {
  const page = readFileSync(
    join(repoRoot, 'art', 'audio', 'sound-lab', 'signatures-v2.html'),
    'utf8',
  );
  const review = readFileSync(
    join(repoRoot, 'art', 'audio', 'sound-lab', 'signatures-v2.js'),
    'utf8',
  );
  const primaryCues = [...review.matchAll(/primaryCue: '([^']+)'/g)].map(
    (match) => match[1],
  );

  assert.equal(primaryCues.length, 16);
  assert.equal(new Set(primaryCues).size, 16);
  assert.ok(primaryCues.every((cue) => signatureCues.includes(cue)));
  for (const cue of signatureCues) {
    assert.match(review, new RegExp(`'${cue}'`), `${cue} needs a review control`);
  }
  assert.match(review, /const defaultSignatureGain = 0\.16/);
  assert.match(review, /\.\/runtime-audio\//);
  assert.doesNotMatch(page, /autoplay/i);
  assert.doesNotMatch(review, /ambient|ambience/i);
});

test('the approved Astral Relay runtime pack is complete, restrained, and traceable', () => {
  const directories = readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();
  assert.deepEqual(directories, ['astral-relay']);

  const directory = join(root, 'astral-relay');
  const manifest = JSON.parse(
    readFileSync(join(directory, 'manifest.json'), 'utf8'),
  ) as {
    version: number;
    id: string;
    approval: string;
    format: string;
    sampleRate: number;
    channels: number;
    provenance: {
      provider: string;
      generatedBy: string[];
      exportedBy: string;
      generationRecord: string;
      generationRecordSha256: string;
      signatureGenerationRecord: string;
      signatureGenerationRecordSha256: string;
      signatureGenerationAddendum: string;
      signatureGenerationAddendumSha256: string;
      deliveryEncoding: {
        encoder: string;
        canonicalization: string;
      };
      deliveryProcessing: {
        policy: string;
        cues: Record<
          string,
          {
            sourceCueId?: string;
            channelTransform: string;
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
      signatureDeliveryProcessing: {
        policy: string;
        cues: Record<
          string,
          {
            sourceCueId: string;
            sourceOutputSha256: string;
            channelTransform: string;
            sourceStartSeconds: number;
            playbackRate: number;
            durationSeconds: number;
            lowPassHz: number;
            fadeInSeconds: number;
            fadeOutSeconds: number;
            targetSamplePeak: number;
          }
        >;
      };
      rightsStatus: string;
      license: {
        termsUrl: string;
        checkedAt: string;
        permittedUse: string;
        standaloneRedistribution: string;
      };
      shipApproval: string;
      sourceComposition: Record<string, number>;
    };
    mix: {
      roomSend: number;
      cues: Record<
        string,
        {
          gain: number;
          voiceLimit: number;
          panWidth: number;
          roomSend: boolean;
        }
      >;
    };
    behavior: {
      silentOutcomeCueIds: string[];
      cueAliases: Record<string, string>;
      tickMix: {
        coalesceNonProjectileCues: boolean;
        projectileReleasesPerTick: number;
        projectileStaggerTicks: number;
        suppressWindupUnderForegroundEvents: boolean;
        projectileWithConcreteOutcomes: string;
      };
      crowdedMixAudit: {
        method: string;
        sampleRate: number;
        projectileVoices: number;
        projectileStaggerTicks: number;
        presentationTicksPerSecond: number;
        projectileStaggerSeconds: number;
        fixedCueIds: string[];
        coreCueIds: string[];
        signatureCueIds: string[];
        signatureVoices: number;
        signaturePairCount: number;
        variantCount: number;
        maximumApproximateTruePeakDbtp: number;
        worstVariant: { coreCueId: string; signatureCueIds: string[] };
        ceilingDbtp: number;
        limiterAttenuationModeled: boolean;
        roomSendModeled: boolean;
      };
    };
    cues: Record<string, string>;
    integrity: Record<string, Integrity>;
  };

  assert.equal(manifest.version, 13);
  assert.equal(manifest.id, 'astral-relay');
  assert.equal(manifest.approval, 'approved');
  assert.equal(manifest.format, 'aac-lc-m4a');
  assert.equal(manifest.sampleRate, 48_000);
  assert.equal(manifest.channels, 2);
  assert.deepEqual(
    {
      ...manifest.provenance,
      signatureGenerationRecordSha256: '<hash>',
      signatureGenerationAddendumSha256: '<hash>',
      signatureDeliveryProcessing: '<checked separately>',
    },
    {
    provider: 'sfx-engine',
    generatedBy: [
      '.claude/skills/sfx-engine/scripts/generate-pack.mjs',
      'scripts/derive-audio-v5-revisions.mjs',
      'scripts/master-audio-v5-energy.mjs',
      'scripts/build-audio-v5-candidates.mjs',
    ],
    exportedBy: 'scripts/export-runtime-sound-effects.mjs',
    generationRecord: 'art/audio/sound-lab/v5/sfx-engine-generation.json',
    generationRecordSha256:
      '84aed4d0ecc95cdfad2bfa02f3305a8a902a50961531738816e501d4ec9de768',
    signatureGenerationRecord:
      'art/audio/sound-lab/v5/signature-sfx-engine-generation.json',
    signatureGenerationRecordSha256: '<hash>',
    signatureGenerationAddendum:
      'art/audio/sound-lab/v5/signature-v2-sfx-engine-generation.json',
    signatureGenerationAddendumSha256: '<hash>',
    deliveryEncoding: {
      encoder: 'macos-afconvert',
      canonicalization: 'zero-iso-bmff-creation-and-modification-times',
    },
    deliveryProcessing: {
      policy:
        'Deterministic hash-pinned source selection, resampling, low-pass, ' +
        'endpoint, and peak treatment for frequent or spectrally sharp cues; ' +
        'no synthesized layer and no provider request.',
      cues: {
        'attack-windup': {
          channelTransform: 'side-to-dual-mono',
          sourceStartSeconds: 0.1,
          playbackRate: 0.16,
          durationSeconds: 0.24,
          lowPassHz: 800,
          fadeInSeconds: 0.045,
          fadeOutSeconds: 0.09,
          targetSamplePeak: 0.24,
        },
        projectile: {
          sourceCueId: 'attack-fizzle',
          channelTransform: 'mid-to-dual-mono',
          sourceStartSeconds: 0,
          playbackRate: 0.65,
          durationSeconds: 0.14,
          lowPassHz: 900,
          fadeInSeconds: 0.01,
          fadeOutSeconds: 0.06,
          targetSamplePeak: 0.2,
        },
        impact: {
          channelTransform: 'preserve-stereo',
          sourceStartSeconds: 0,
          playbackRate: 1,
          durationSeconds: 1,
          lowPassHz: 3_600,
          fadeInSeconds: 0.02,
          fadeOutSeconds: 0.14,
          targetSamplePeak: 0.45,
        },
        healing: {
          channelTransform: 'preserve-stereo',
          sourceStartSeconds: 0,
          playbackRate: 1,
          durationSeconds: 0.8,
          lowPassHz: 900,
          fadeInSeconds: 0.025,
          fadeOutSeconds: 0.2,
          targetSamplePeak: 0.38,
        },
        destroyed: {
          channelTransform: 'preserve-stereo',
          sourceStartSeconds: 0.68,
          playbackRate: 1.25,
          durationSeconds: 0.7,
          lowPassHz: 2_200,
          fadeInSeconds: 0.01,
          fadeOutSeconds: 0.22,
          targetSamplePeak: 0.44,
        },
        'level-up': {
          channelTransform: 'preserve-stereo',
          sourceStartSeconds: 0,
          playbackRate: 1,
          durationSeconds: 1,
          lowPassHz: 3_500,
          fadeInSeconds: 0.12,
          fadeOutSeconds: 0.18,
          targetSamplePeak: 0.42,
        },
        'arc-bank': {
          channelTransform: 'preserve-stereo',
          sourceStartSeconds: 0,
          playbackRate: 1,
          durationSeconds: 2,
          lowPassHz: 4_500,
          fadeInSeconds: 0.04,
          fadeOutSeconds: 0.5,
          targetSamplePeak: 0.55,
        },
        'arc-pulse': {
          channelTransform: 'preserve-stereo',
          sourceStartSeconds: 0,
          playbackRate: 1,
          durationSeconds: 4,
          lowPassHz: 3_000,
          fadeInSeconds: 0.12,
          fadeOutSeconds: 0.4,
          targetSamplePeak: 0.55,
        },
        'arc-handoff': {
          channelTransform: 'preserve-stereo',
          sourceStartSeconds: 0,
          playbackRate: 1,
          durationSeconds: 2,
          lowPassHz: 3_200,
          fadeInSeconds: 0.04,
          fadeOutSeconds: 0.5,
          targetSamplePeak: 0.5,
        },
        arrival: {
          channelTransform: 'preserve-stereo',
          sourceStartSeconds: 0,
          playbackRate: 1,
          durationSeconds: 2,
          lowPassHz: 2_800,
          fadeInSeconds: 0.45,
          fadeOutSeconds: 0.08,
          targetSamplePeak: 0.5,
        },
      },
    },
    signatureDeliveryProcessing: '<checked separately>',
    rightsStatus: 'rights-cleared',
    license: {
      termsUrl: 'https://sfxengine.com/terms-of-service',
      checkedAt: '2026-08-14',
      permittedUse: 'personal-and-commercial-projects',
      standaloneRedistribution: 'prohibited',
    },
    shipApproval: 'approved',
    sourceComposition: {
      providerGeneratedCues: 14,
      locallyDerivedCues: 3,
      locallySafetyMasteredCues: 5,
    },
    },
  );
  const generationRecord = readFileSync(
    join(repoRoot, manifest.provenance.generationRecord),
  );
  assert.equal(
    sha256(generationRecord),
    manifest.provenance.generationRecordSha256,
  );
  const signatureGenerationRecord = readFileSync(
    join(repoRoot, manifest.provenance.signatureGenerationRecord),
  );
  assert.equal(
    sha256(signatureGenerationRecord),
    manifest.provenance.signatureGenerationRecordSha256,
  );
  const signatureGenerationAddendum = readFileSync(
    join(repoRoot, manifest.provenance.signatureGenerationAddendum),
  );
  assert.equal(
    sha256(signatureGenerationAddendum),
    manifest.provenance.signatureGenerationAddendumSha256,
  );
  const signatureProvenance = JSON.parse(
    signatureGenerationRecord.toString('utf8'),
  ) as {
    batch: { maximumProviderJobs: number; automaticRerolls: boolean };
    cues: Array<{
      id: string;
      attempts?: Array<{ result: { status: string; outputSha256: string } }>;
      result: { status: string; sourceSha256: string; outputSha256: string };
    }>;
    deliveries: Array<{ id: string; outputSha256: string }>;
  };
  assert.equal(signatureProvenance.batch.maximumProviderJobs, 6);
  assert.equal(signatureProvenance.batch.automaticRerolls, false);
  assert.equal(signatureProvenance.cues.length, 6);
  assert.ok(
    signatureProvenance.cues.every(
      (cue) =>
        cue.result.status === 'generated' &&
        /^[a-f0-9]{64}$/.test(cue.result.sourceSha256) &&
        /^[a-f0-9]{64}$/.test(cue.result.outputSha256),
    ),
  );
  assert.deepEqual(
    signatureProvenance.deliveries.map((delivery) => delivery.id).sort(),
    signatureCues.filter(
      (cue) =>
        ![
          'signature-arc-toss',
          'signature-falling-star',
          'signature-mason',
          'signature-rail-charge',
          'signature-rail-release',
          'signature-repair',
          'signature-sentinel-deploy',
          'signature-sentinel-fire',
          'signature-trip-deploy',
          'signature-trip-trigger',
        ].includes(cue),
    ),
  );
  const signatureAddendumProvenance = JSON.parse(
    signatureGenerationAddendum.toString('utf8'),
  ) as typeof signatureProvenance & { batch: typeof signatureProvenance.batch & { ambience: boolean } };
  assert.equal(signatureAddendumProvenance.batch.maximumProviderJobs, 10);
  assert.equal(signatureAddendumProvenance.batch.automaticRerolls, false);
  assert.equal(signatureAddendumProvenance.batch.ambience, false);
  assert.equal(signatureAddendumProvenance.cues.length, 8);
  assert.equal(
    signatureAddendumProvenance.cues.reduce(
      (total, cue) => total + 1 + (cue.attempts?.length ?? 0),
      0,
    ),
    10,
  );
  assert.deepEqual(
    signatureAddendumProvenance.cues
      .filter((cue) => (cue.attempts?.length ?? 0) > 0)
      .map((cue) => [cue.id, cue.attempts?.length]),
    [
      ['signature-v2-source-mason', 1],
      ['signature-v2-source-trip-node', 1],
    ],
  );
  assert.ok(
    signatureAddendumProvenance.cues.every(
      (cue) =>
        cue.result.status === 'generated' &&
        /^[a-f0-9]{64}$/.test(cue.result.sourceSha256) &&
        /^[a-f0-9]{64}$/.test(cue.result.outputSha256),
    ),
  );
  assert.deepEqual(
    signatureAddendumProvenance.deliveries
      .map((delivery) => delivery.id)
      .sort(),
    signatureCues.filter(
      (cue) =>
        cue.startsWith('signature-arc-') ||
        cue.startsWith('signature-falling-') ||
        cue.startsWith('signature-mason') ||
        cue.startsWith('signature-rail-') ||
        cue.startsWith('signature-repair') ||
        cue.startsWith('signature-sentinel-') ||
        cue.startsWith('signature-trip-'),
    ),
  );
  assert.deepEqual(
    Object.keys(manifest.provenance.signatureDeliveryProcessing.cues).sort(),
    signatureCues,
  );
  assert.match(
    manifest.provenance.signatureDeliveryProcessing.policy,
    /no synthesized layer and no provider request/i,
  );

  assert.deepEqual(Object.keys(manifest.cues).sort(), expectedCues);
  assert.deepEqual(Object.keys(manifest.mix.cues).sort(), expectedCues);
  const { crowdedMixAudit, ...behavior } = manifest.behavior;
  assert.deepEqual(behavior, {
    silentOutcomeCueIds: ['attack-cancel'],
    cueAliases: {
      'attack-fizzle': 'projectile',
      'arc-steal': 'arc-pickup',
    },
    tickMix: {
      coalesceNonProjectileCues: true,
      projectileReleasesPerTick: 4,
      projectileStaggerTicks: 0.04,
      suppressWindupUnderForegroundEvents: true,
      projectileWithConcreteOutcomes: 'layer-per-release-up-to-limit',
    },
  });
  assert.match(crowdedMixAudit.method, /decoded AAC deliveries/);
  assert.deepEqual(
    {
      ...crowdedMixAudit,
      maximumApproximateTruePeakDbtp: '<checked below>',
      worstVariant: '<valid member combination>',
    },
    {
      method:
        'Four-times windowed-sinc true-peak estimate over decoded AAC deliveries ' +
        'at exact runtime gains; dry effects bus, centered sources.',
      sampleRate: 48_000,
      projectileVoices: 4,
      projectileStaggerTicks: 0.04,
      presentationTicksPerSecond: 2.5,
      projectileStaggerSeconds: 0.016,
      fixedCueIds: ['impact', 'destroyed', 'level-up'],
      coreCueIds: ['arc-pickup', 'arc-drop', 'arc-handoff'],
      signatureCueIds: [
        'signature-motion',
        'signature-construct',
        'signature-tether',
        'signature-survey',
        'signature-null',
        'signature-smoke',
        'signature-exchange',
        'signature-target',
        'signature-kinetic',
        'signature-mason',
        'signature-repair',
        'signature-falling-star',
        'signature-trip-deploy',
        'signature-trip-trigger',
        'signature-arc-toss',
        'signature-rail-charge',
        'signature-rail-release',
        'signature-sentinel-deploy',
        'signature-sentinel-fire',
      ],
      signatureVoices: 2,
      signaturePairCount: 171,
      variantCount: 513,
      maximumApproximateTruePeakDbtp: '<checked below>',
      worstVariant: '<valid member combination>',
      ceilingDbtp: -6,
      limiterAttenuationModeled: false,
      roomSendModeled: false,
    },
  );
  assert.ok(crowdedMixAudit.maximumApproximateTruePeakDbtp <= -6);
  assert.ok(crowdedMixAudit.coreCueIds.includes(crowdedMixAudit.worstVariant.coreCueId));
  assert.equal(crowdedMixAudit.worstVariant.signatureCueIds.length, 2);
  assert.ok(
    crowdedMixAudit.worstVariant.signatureCueIds.every((cue) =>
      signatureCues.includes(cue),
    ),
  );
  assert.equal(manifest.mix.roomSend, 0.06);
  assert.ok(manifest.mix.roomSend <= 0.08, 'provider tails get only a light room send');
  assert.deepEqual(
    Object.fromEntries(
      ['impact', 'healing', 'destroyed', 'level-up', 'arc-bank', 'arc-pulse', 'arc-handoff', 'arrival'].map(
        (cue) => [cue, manifest.mix.cues[cue]],
      ),
    ),
    {
      impact: { gain: 0.32, voiceLimit: 1, panWidth: 0.5, roomSend: false },
      healing: { gain: 0.28, voiceLimit: 2, panWidth: 0.55, roomSend: true },
      destroyed: { gain: 0.3, voiceLimit: 1, panWidth: 0.4, roomSend: false },
      'level-up': { gain: 0.32, voiceLimit: 1, panWidth: 0.4, roomSend: false },
      'arc-bank': { gain: 0.44, voiceLimit: 1, panWidth: 0.55, roomSend: true },
      'arc-pulse': { gain: 0.32, voiceLimit: 1, panWidth: 0.55, roomSend: true },
      'arc-handoff': { gain: 0.3, voiceLimit: 1, panWidth: 0.55, roomSend: false },
      arrival: { gain: 0.28, voiceLimit: 1, panWidth: 0.45, roomSend: false },
    },
  );

  let totalBytes = 0;
  for (const cue of expectedCues) {
    assert.equal(manifest.cues[cue], `${cue}.m4a`);
    const bytes = readFileSync(join(directory, manifest.cues[cue]!));
    const integrity = manifest.integrity[cue]!;
    const mix = manifest.mix.cues[cue]!;
    assert.equal(bytes.toString('ascii', 4, 8), 'ftyp');
    assert.equal(bytes.length, integrity.bytes);
    assert.equal(sha256(bytes), integrity.outputSha256);
    assert.match(integrity.sourceSha256, /^[a-f0-9]{64}$/);
    assert.equal(integrity.bitrate, 192_000);
    const minimumBytes =
      cue === 'attack-windup' || cue === 'projectile'
        ? 2_500
        : cue.startsWith('signature-')
          ? 4_500
          : 8_000;
    assert.ok(bytes.length >= minimumBytes, `${cue} is unexpectedly small`);
    assert.ok(bytes.length <= 90_000, `${cue} is unexpectedly large`);
    assert.ok(mix.gain >= 0 && mix.gain <= 0.55, `${cue} gain is unsafe`);
    assert.ok(
      Number.isInteger(mix.voiceLimit) &&
        mix.voiceLimit >= 1 &&
        mix.voiceLimit <= (cue === 'projectile' ? 4 : 3),
      `${cue} voice limit is unsafe`,
    );
    assert.ok(mix.panWidth >= 0 && mix.panWidth <= 0.7, `${cue} pan is unsafe`);
    assert.equal(typeof mix.roomSend, 'boolean');
    const metrics = integrity.deliveryMetrics;
    assert.ok(metrics, `${cue} lacks its runtime delivery audit`);
    assert.ok(metrics.durationSeconds > 0 && metrics.durationSeconds <= 4);
    assert.ok(
      metrics.spectralCentroidHz <= 3_700,
      `${cue} spectral centre is unexpectedly sharp`,
    );
    assert.ok(
      metrics.energyAbove4kPercent <= 3,
      `${cue} carries too much upper-mid energy`,
    );
    assert.ok(
      metrics.energyAbove12kPercent <= 0.05,
      `${cue} carries too much extreme high-frequency energy`,
    );
    assert.ok(
      metrics.maximumWindowAbove4kPercent <= 80,
      `${cue} contains an overly concentrated bright window`,
    );
    assert.ok(
      metrics.maximumWindowAbove4kPercent <= 10 ||
        metrics.effectiveMaximumWindowAbove4kRmsDbfs <= -28,
      `${cue} contains an audible concentrated bright window`,
    );
    assert.ok(
      metrics.effectiveSamplePeakDbfs <= -9,
      `${cue} is too hot after its runtime gain`,
    );
    assert.ok(
      metrics.effectiveRmsDbfs <= -25,
      `${cue} carries too much sustained runtime energy`,
    );
    totalBytes += bytes.length;
  }

  for (const cue of ['attack-windup', 'projectile'] as const) {
    const mix = manifest.mix.cues[cue]!;
    const metrics = manifest.integrity[cue]!.deliveryMetrics!;
    assert.equal(mix.voiceLimit, cue === 'projectile' ? 4 : 1);
    assert.equal(mix.roomSend, false);
    assert.ok(mix.panWidth <= 0.2);
    assert.ok(mix.gain <= 0.2);
    assert.ok(metrics.durationSeconds <= 0.24);
    assert.ok(
      metrics.dominantFrequencyHz <= 700,
      `${cue} has crept out of the low-mid pitch band`,
    );
    assert.ok(metrics.interchannelCorrelation >= 0.999);
    assert.ok(
      metrics.effectiveSamplePeakDbfs <= -20,
      `${cue} is too hot after its runtime gain`,
    );
    assert.ok(
      metrics.effectiveRmsDbfs <= -30,
      `${cue} carries too much sustained runtime energy`,
    );
  }
  assert.deepEqual(manifest.mix.cues.projectile, {
    gain: 0.16,
    voiceLimit: 4,
    panWidth: 0.16,
    roomSend: false,
  });
  assert.ok(
    manifest.integrity.projectile!.deliveryMetrics!.effectiveSamplePeakDbfs <=
      -28,
    'the newly layered common projectile cue must stay well below outcomes',
  );

  for (const cue of signatureCues) {
    const mix = manifest.mix.cues[cue]!;
    const metrics = manifest.integrity[cue]!.deliveryMetrics!;
    const processing =
      manifest.provenance.signatureDeliveryProcessing.cues[cue]!;
    assert.deepEqual(mix, {
      gain: 0.16,
      voiceLimit: 1,
      panWidth: 0.45,
      roomSend: false,
    });
    assert.match(processing.sourceCueId, /^signature-(?:v2-)?source-/);
    assert.match(processing.sourceOutputSha256, /^[a-f0-9]{64}$/);
    assert.ok(
      ['mid-to-dual-mono', 'side-to-dual-mono'].includes(
        processing.channelTransform,
      ),
    );
    assert.ok(processing.durationSeconds <= 0.7);
    assert.ok(processing.lowPassHz <= 1_000);
    assert.ok(processing.targetSamplePeak <= 0.22);
    assert.ok(metrics.durationSeconds <= 0.7);
    assert.ok(metrics.spectralCentroidHz <= 1_000);
    assert.ok(metrics.energyAbove4kPercent <= 0.5);
    assert.ok(metrics.energyAbove8kPercent <= 0.05);
    assert.ok(metrics.energyAbove12kPercent <= 0.01);
    assert.ok(metrics.interchannelCorrelation >= 0.999);
    assert.ok(metrics.effectiveSamplePeakDbfs <= -22);
    assert.ok(metrics.effectiveRmsDbfs <= -34);
    assert.ok(
      metrics.effectiveMaximumHighBandWindowRmsDbfs <= -42,
      `${cue} has an audible concentrated high-band window`,
    );
  }
  assert.ok(
    manifest.integrity.projectile!.deliveryMetrics!.effectiveSamplePeakDbfs +
      20 * Math.log10(manifest.mix.cues.projectile!.voiceLimit) <=
      -15,
    'even a phase-aligned four-release stack must retain conservative peak headroom',
  );
  const healingMetrics = manifest.integrity.healing!.deliveryMetrics!;
  assert.equal(healingMetrics.durationSeconds, 0.8);
  assert.ok(healingMetrics.dominantFrequencyHz <= 500);
  assert.ok(healingMetrics.spectralCentroidHz <= 1_200);
  assert.ok(
    healingMetrics.effectiveSamplePeakDbfs +
      20 * Math.log10(manifest.mix.cues.healing!.voiceLimit) <=
      -11,
    'two overlapping healing tails must retain conservative peak headroom',
  );
  assert.ok(
    manifest.integrity['attack-windup']!.deliveryMetrics!
      .sourceInterchannelCorrelation! <= -0.99,
    'the wind-up delivery must explicitly correct its anti-phase source',
  );

  for (const cue of [
    'impact',
    'healing',
    'destroyed',
    'level-up',
    'arc-bank',
    'arc-pulse',
    'arc-handoff',
    'arrival',
  ]) {
    const metrics = manifest.integrity[cue]!.deliveryMetrics!;
    assert.ok(
      metrics.energyAbove4kPercent <= 3,
      `${cue} comfort treatment no longer controls upper-mid energy`,
    );
    assert.ok(
      metrics.energyAbove12kPercent <= 0.05,
      `${cue} comfort treatment no longer controls extreme highs`,
    );
  }

  assert.deepEqual(Object.keys(manifest.integrity).sort(), expectedCues);
  assert.deepEqual(
    readdirSync(directory).sort(),
    ['manifest.json', ...expectedCues.map((cue) => `${cue}.m4a`)].sort(),
    'runtime delivery must contain only the twenty-three approved one-shots',
  );
  assert.ok(totalBytes < 500_000, `runtime audio is ${totalBytes} bytes`);
});

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}
