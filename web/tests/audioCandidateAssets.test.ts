import assert from 'node:assert/strict';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import test from 'node:test';

const root = join(
  import.meta.dirname,
  '..',
  'src',
  'assets',
  'audio',
  'candidates',
);
const expectedCues = ['destroyed', 'impact', 'projectile', 'unlock'];

test('audio review candidates have complete matched AAC packages', () => {
  const directories = readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort();
  assert.deepEqual(directories, [
    'aegis-systems',
    'aurora-core',
    'nilbots-signature',
    'obsidian-foundry',
  ]);

  let totalBytes = 0;
  for (const id of directories) {
    const directory = join(root, id);
    const manifest = JSON.parse(
      readFileSync(join(directory, 'manifest.json'), 'utf8'),
    ) as {
      version: number;
      id: string;
      reviewOnly: boolean;
      format: string;
      sampleRate: number;
      channels: number;
      cues: Record<string, string>;
    };
    assert.equal(manifest.version, 1);
    assert.equal(manifest.id, id);
    assert.equal(manifest.reviewOnly, true);
    assert.equal(manifest.format, 'aac-lc-m4a');
    assert.equal(manifest.sampleRate, 48_000);
    assert.equal(manifest.channels, 2);
    assert.deepEqual(Object.keys(manifest.cues).sort(), expectedCues);

    for (const cue of expectedCues) {
      assert.equal(manifest.cues[cue], `${cue}.m4a`);
      const bytes = readFileSync(join(directory, manifest.cues[cue]));
      assert.equal(bytes.toString('ascii', 4, 8), 'ftyp');
      assert.ok(bytes.length >= 8_000, `${id}/${cue} is unexpectedly small`);
      assert.ok(bytes.length <= 90_000, `${id}/${cue} is unexpectedly large`);
      totalBytes += statSync(join(directory, manifest.cues[cue])).size;
    }
  }
  assert.ok(totalBytes < 700_000, `review audio is ${totalBytes} bytes`);
});
