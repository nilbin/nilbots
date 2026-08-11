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
    sourceInterchannelCorrelation: number;
    effectiveSamplePeakDbfs: number;
    effectiveRmsDbfs: number;
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
    ambience: {
      id: string;
      sourceCueId: string;
      file: string;
      loop: boolean;
      durationSeconds: number;
      gain: number;
      mapIds: string[];
    };
    integrity: Record<string, Integrity>;
  };

  assert.equal(manifest.version, 4);
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
        'Short down-pitched dual-mono low-mid combat markers with soft endpoints; ' +
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
          channelTransform: 'mid-to-dual-mono',
          sourceStartSeconds: 0.005,
          playbackRate: 0.16,
          durationSeconds: 0.16,
          lowPassHz: 750,
          fadeInSeconds: 0.025,
          fadeOutSeconds: 0.08,
          targetSamplePeak: 0.24,
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
  const generation = JSON.parse(generationRecord.toString('utf8')) as {
    cues: Array<{
      id: string;
      result: {
        metrics: {
          energy?: {
            integratedLufs: number;
            maxMomentaryLufs: number;
          };
        };
      };
    }>;
  };

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
    totalBytes += bytes.length;
  }

  for (const cue of ['attack-windup', 'projectile'] as const) {
    const mix = manifest.mix.cues[cue]!;
    const metrics = manifest.integrity[cue]!.deliveryMetrics;
    assert.ok(metrics, `${cue} lacks its delivery-shaping audit`);
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
      .sourceInterchannelCorrelation <= -0.99,
    'the wind-up delivery must explicitly correct its anti-phase source',
  );

  assert.deepEqual(manifest.ambience, {
    id: 'ion-veil',
    sourceCueId: 'arena-ambience-ion-veil',
    file: 'arena-ambience-ion-veil.m4a',
    loop: true,
    durationSeconds: 30,
    gain: 0.08,
    mapIds: ['arc-relay-tapline-01'],
  });
  const ambience = readFileSync(join(directory, manifest.ambience.file));
  const ambienceIntegrity = manifest.integrity[manifest.ambience.sourceCueId]!;
  assert.equal(ambience.toString('ascii', 4, 8), 'ftyp');
  assert.equal(ambience.length, ambienceIntegrity.bytes);
  assert.equal(sha256(ambience), ambienceIntegrity.outputSha256);
  assert.match(ambienceIntegrity.sourceSha256, /^[a-f0-9]{64}$/);
  assert.equal(ambienceIntegrity.bitrate, 160_000);
  assert.ok(ambience.length >= 500_000 && ambience.length <= 700_000);
  assert.ok(manifest.ambience.gain <= 0.08, 'Ion veil remains an almost subliminal bed');
  const sourceAmbience = generation.cues.find(
    (cue) => cue.id === manifest.ambience.sourceCueId,
  );
  const sourceEnergy = sourceAmbience?.result.metrics.energy;
  assert.ok(sourceEnergy, 'selected ambience is missing its loudness audit');
  const gainDb = 20 * Math.log10(manifest.ambience.gain);
  assert.ok(
    sourceEnergy.integratedLufs + gainDb <= -40,
    'runtime ambience must remain at or below -40 LUFS integrated',
  );
  assert.ok(
    sourceEnergy.maxMomentaryLufs + gainDb <= -35.5,
    'runtime ambience must remain below -35.5 LUFS maximum momentary',
  );
  totalBytes += ambience.length;

  assert.deepEqual(
    Object.keys(manifest.integrity).sort(),
    [...expectedCues, 'arena-ambience-ion-veil'].sort(),
  );
  assert.ok(totalBytes < 1_200_000, `runtime audio is ${totalBytes} bytes`);
});

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}
