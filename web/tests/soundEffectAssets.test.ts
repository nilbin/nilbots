import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';

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
  'impact',
  'level-up',
  'projectile',
];

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
    effectiveSamplePeakDbfs: number;
    effectiveRmsDbfs: number;
    effectiveMaximumWindowAbove4kRmsDbfs: number;
  };
}

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
      deliveryEncoding: {
        encoder: string;
        canonicalization: string;
      };
      deliveryProcessing: {
        policy: string;
        cues: Record<
          string,
          {
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
        coalesceSameCue: boolean;
        suppressWindupUnderForegroundEvents: boolean;
        suppressBeamUnderConcreteOutcomes: string[];
      };
    };
    cues: Record<string, string>;
    integrity: Record<string, Integrity>;
  };

  assert.equal(manifest.version, 5);
  assert.equal(manifest.id, 'astral-relay');
  assert.equal(manifest.approval, 'approved');
  assert.equal(manifest.format, 'aac-lc-m4a');
  assert.equal(manifest.sampleRate, 48_000);
  assert.equal(manifest.channels, 2);
  assert.deepEqual(manifest.provenance, {
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
      '567aafb292054d28136020ea784105bb6a888cc4279fafa4984e340e014064f5',
    deliveryEncoding: {
      encoder: 'macos-afconvert',
      canonicalization: 'zero-iso-bmff-creation-and-modification-times',
    },
    deliveryProcessing: {
      policy:
        'Deterministic low-pass, endpoint, and peak treatment for frequent or ' +
        'spectrally sharp cues; no synthesized layer and no provider request.',
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
          channelTransform: 'mid-to-dual-mono',
          sourceStartSeconds: 0.005,
          playbackRate: 0.16,
          durationSeconds: 0.16,
          lowPassHz: 750,
          fadeInSeconds: 0.025,
          fadeOutSeconds: 0.08,
          targetSamplePeak: 0.24,
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
        destroyed: {
          channelTransform: 'preserve-stereo',
          sourceStartSeconds: 0,
          playbackRate: 1,
          durationSeconds: 2,
          lowPassHz: 2_800,
          fadeInSeconds: 0.08,
          fadeOutSeconds: 0.45,
          targetSamplePeak: 0.5,
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
    rightsStatus: 'rights-cleared',
    license: {
      termsUrl: 'https://sfxengine.com/terms-of-service',
      checkedAt: '2026-08-11',
      permittedUse: 'personal-and-commercial-projects',
      standaloneRedistribution: 'prohibited',
    },
    shipApproval: 'approved',
    sourceComposition: {
      providerGeneratedCues: 13,
      locallyDerivedCues: 3,
      locallySafetyMasteredCues: 5,
    },
  });
  const generationRecord = readFileSync(
    join(repoRoot, manifest.provenance.generationRecord),
  );
  assert.equal(
    sha256(generationRecord),
    manifest.provenance.generationRecordSha256,
  );

  assert.deepEqual(Object.keys(manifest.cues).sort(), expectedCues);
  assert.deepEqual(Object.keys(manifest.mix.cues).sort(), expectedCues);
  assert.deepEqual(manifest.behavior, {
    silentOutcomeCueIds: ['attack-fizzle', 'attack-cancel'],
    cueAliases: { 'arc-steal': 'arc-pickup' },
    tickMix: {
      coalesceSameCue: true,
      suppressWindupUnderForegroundEvents: true,
      suppressBeamUnderConcreteOutcomes: ['impact', 'destroyed', 'deflect'],
    },
  });
  assert.equal(manifest.mix.roomSend, 0.06);
  assert.ok(manifest.mix.roomSend <= 0.08, 'provider tails get only a light room send');
  assert.deepEqual(
    Object.fromEntries(
      ['impact', 'destroyed', 'level-up', 'arc-bank', 'arc-pulse', 'arc-handoff', 'arrival'].map(
        (cue) => [cue, manifest.mix.cues[cue]],
      ),
    ),
    {
      impact: { gain: 0.32, voiceLimit: 1, panWidth: 0.5, roomSend: false },
      destroyed: { gain: 0.32, voiceLimit: 1, panWidth: 0.45, roomSend: false },
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
    assert.ok(
      bytes.length >= (cue === 'attack-windup' || cue === 'projectile' ? 2_500 : 8_000),
      `${cue} is unexpectedly small`,
    );
    assert.ok(bytes.length <= 90_000, `${cue} is unexpectedly large`);
    assert.ok(mix.gain >= 0 && mix.gain <= 0.55, `${cue} gain is unsafe`);
    assert.ok(
      Number.isInteger(mix.voiceLimit) && mix.voiceLimit >= 1 && mix.voiceLimit <= 3,
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
    assert.equal(mix.voiceLimit, 1);
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
  assert.ok(
    manifest.integrity['attack-windup']!.deliveryMetrics!
      .sourceInterchannelCorrelation! <= -0.99,
    'the wind-up delivery must explicitly correct its anti-phase source',
  );

  for (const cue of [
    'impact',
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
    'runtime delivery must contain only the thirteen one-shots',
  );
  assert.ok(totalBytes < 500_000, `runtime audio is ${totalBytes} bytes`);
});

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}
