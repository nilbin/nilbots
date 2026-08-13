import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import test from 'node:test';

const repository = resolve(import.meta.dirname, '..', '..');
const audit = JSON.parse(
  readFileSync(
    join(
      repository,
      'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1/audit.json',
    ),
    'utf8',
  ),
);

test('Longshot and objective review tier preserves its promotion gate', () => {
  assert.equal(audit.status, 'owner-review');
  assert.deepEqual(
    audit.candidates.map((candidate: { id: string }) => candidate.id),
    ['longshot', 'survey-flare', 'team-reactor'],
  );
  assert.deepEqual(
    audit.rejected.map((candidate: { id: string }) => candidate.id),
    ['core-well'],
  );
  assert.equal(audit.providerSpend.correctiveBatchCredits, 80);
  assert.equal(audit.providerSpend.balanceAfter, 1100);
  assert.equal(audit.providerSpend.autoRigging, false);

  const longshot = audit.candidates.find(
    (candidate: { id: string }) => candidate.id === 'longshot',
  );
  const liveLongshot = readFileSync(
    join(repository, 'web/src/assets/class-looks/arc-longshot/model.glb'),
  );
  assert.notEqual(sha256(liveLongshot), longshot.artifact.sha256);
  assert.equal(
    existsSync(
      join(repository, 'web/src/assets/objective-models/arc-core-well/model.glb'),
    ),
    false,
  );
  assert.equal(
    existsSync(
      join(repository, 'web/src/assets/signature-models/arc-survey-flare/model.glb'),
    ),
    false,
  );
  assert.equal(
    existsSync(
      join(repository, 'web/src/assets/objective-models/arc-team-reactor/model.glb'),
    ),
    false,
  );
});

test('review-only Survey Flare and Team Reactor staging manifests match the audited tier', () => {
  for (const [candidateId, runtimeId, expectedKind, manifestFile] of [
    ['survey-flare', 'arc-survey-flare', 'signature', 'arc-survey-flare.model3d.json'],
    ['team-reactor', 'arc-team-reactor', 'objective', 'arc-team-reactor.model3d.json'],
  ] as const) {
    const candidate = audit.candidates.find(
      (entry: { id: string }) => entry.id === candidateId,
    );
    assert.ok(candidate, candidateId);
    const reviewed = readFileSync(join(repository, candidate.artifact.file));
    const manifest = JSON.parse(
      readFileSync(
        join(
          repository,
          'art/class-models/runtime-tiers/arc-relay/longshot-objectives-review-v1',
          manifestFile,
        ),
        'utf8',
      ),
    );

    assert.equal(reviewed.length, candidate.artifact.bytes, candidateId);
    assert.equal(sha256(reviewed), candidate.artifact.sha256, candidateId);
    assert.equal(manifest.id, runtimeId, candidateId);
    assert.equal(manifest.kind, expectedKind, candidateId);
    assert.equal(manifest.facing, '+x', candidateId);
    assert.equal(manifest.up, '+y', candidateId);
    assert.equal(manifest.source.sourceSha256, candidate.artifact.sha256, candidateId);
    assert.equal(manifest.source.taskId, candidate.taskId, candidateId);
    assert.equal(manifest.ledger.sha256, candidate.artifact.sha256, candidateId);
  }
});

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}
