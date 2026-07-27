import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile, stat } from 'node:fs/promises';
import test from 'node:test';
import {
  lowestLatencyAdaptiveRoute,
  validateSoundtrackCatalog,
  validateSoundtrackManifest,
} from '../src/soundtrack/manifest.ts';

const soundtrackRoot = new URL('../public/soundtracks/', import.meta.url);

test('the generated soundtrack catalog satisfies the runtime contract', async () => {
  const catalog = validateSoundtrackCatalog(
    JSON.parse(await readFile(new URL('index.json', soundtrackRoot), 'utf8')),
  );
  assert.ok(catalog.tracks.length > 0);

  for (const entry of catalog.tracks) {
    const manifestUrl = new URL(entry.manifest, soundtrackRoot);
    const manifest = validateSoundtrackManifest(
      JSON.parse(await readFile(manifestUrl, 'utf8')),
      manifestUrl,
    );
    assert.equal(manifest.id, entry.id);

    assert.ok(
      manifest.assets[manifest.build.analysis],
      `${entry.id} does not inventory its analysis report`,
    );
    for (const [assetPath, asset] of Object.entries(manifest.assets)) {
      const fileUrl = new URL(assetPath, manifestUrl);
      assert.equal((await stat(fileUrl)).size, asset.bytes);
      const hash = createHash('sha256')
        .update(await readFile(fileUrl))
        .digest('hex');
      assert.equal(hash, asset.sha256, `${assetPath} hash drifted`);
    }
  }
});

test('Neon Protocol adaptive cuts use full-bar overlaps', async () => {
  const manifest = await manifestDocument('neon-protocol');
  const adaptiveCuts = manifest.transitions.filter(
    (transition) =>
      transition.timing === 'next-quantum' &&
      transition.crossfadeBars > 0,
  );

  assert.ok(adaptiveCuts.length > 0);
  assert.ok(
    adaptiveCuts.every((transition) => transition.crossfadeBars === 1),
  );
});

test('optional staged seams and continuous cues are integrity checked', async () => {
  const manifest = await defaultManifestDocument();
  const source = manifest.sections.find(
    (section) => section.barCount > 1 && Object.keys(section.files).length > 0,
  );
  assert.ok(source);
  manifest.adaptiveSeam = {
    strategy: 'staged',
    retreatBars: 1,
    overlapBars: 0.25,
    riseBars: 1,
    curve: 'linear',
  };
  manifest.retrospectiveCue = {
    id: 'validator-runway',
    startBar: source.startBar,
    barCount: source.barCount,
    anchorBar: 1,
    durationSeconds: source.durationSeconds,
    files: structuredClone(source.files),
  };
  manifest.straightThroughCue = {
    id: 'validator-straight',
    startBar: source.startBar,
    barCount: source.barCount,
    durationSeconds: source.durationSeconds,
    file: Object.values(source.files)[0],
  };

  assert.doesNotThrow(() => validateSoundtrackManifest(manifest));

  const unknownSeamKey = structuredClone(manifest);
  unknownSeamKey.adaptiveSeam.unreviewedMode = true;
  assert.throws(
    () => validateSoundtrackManifest(unknownSeamKey),
    /malformed adaptive seam metadata/,
  );

  const undeclaredCueAsset = structuredClone(manifest);
  undeclaredCueAsset.retrospectiveCue.files[
    Object.keys(undeclaredCueAsset.retrospectiveCue.files)[0]
  ] = 'retrospective-cues/missing.m4a';
  assert.throws(
    () => validateSoundtrackManifest(undeclaredCueAsset),
    /retrospective cue has an invalid stem asset/,
  );

  const undeclaredPremix = structuredClone(manifest);
  undeclaredPremix.straightThroughCue.file =
    'straight-through/missing.m4a';
  assert.throws(
    () => validateSoundtrackManifest(undeclaredPremix),
    /straight-through cue has an invalid premix asset/,
  );

  const offGridPremix = structuredClone(manifest);
  offGridPremix.straightThroughCue.durationSeconds += 0.5;
  assert.throws(
    () => validateSoundtrackManifest(offGridPremix),
    /straight-through cue does not match the source grid/,
  );
});

test('build provenance and manifest URL must agree on one content version', async () => {
  const manifest = await defaultManifestDocument();
  const catalog = JSON.parse(
    await readFile(new URL('index.json', soundtrackRoot), 'utf8'),
  );
  const entry = catalog.tracks.find(
    (candidate) => candidate.id === catalog.defaultId,
  );
  assert.ok(entry);
  const manifestUrl = new URL(entry.manifest, soundtrackRoot);

  assert.throws(
    () =>
      validateSoundtrackManifest(
        {
          ...structuredClone(manifest),
          build: {
            ...manifest.build,
            version: manifest.build.version.replace(/^v1-/, 'v2-'),
          },
        },
        manifestUrl,
      ),
    /Malformed soundtrack build provenance/,
  );
  assert.throws(
    () =>
      validateSoundtrackManifest(
        manifest,
        new URL(
          `../v1-0000000000000000/manifest.json`,
          manifestUrl,
        ),
      ),
    /manifest URL does not match its content version/,
  );
});

test('the build analysis must be an integrity-checked declared asset', async () => {
  const manifest = await defaultManifestDocument();
  delete manifest.assets[manifest.build.analysis];

  assert.throws(
    () => validateSoundtrackManifest(manifest),
    /build analysis is not a declared asset/,
  );
});

test('a reviewed hold can continue through its intrinsic loop', async () => {
  const manifest = await defaultManifestDocument();
  const sections = new Map(
    manifest.sections.map((section) => [section.id, section]),
  );
  const hold = manifest.sections.find((section) => {
    if (section.role !== 'hold') return false;
    const sameState = manifest.transitions.filter(
      (transition) =>
        transition.from === section.id &&
        sections.get(transition.to)?.classification ===
          section.classification,
    );
    return (
      sameState.length > 0 &&
      sameState.every(
        (transition) => sections.get(transition.to)?.role === 'stinger',
      )
    );
  });
  assert.ok(hold, 'fixture needs a hold whose only same-state edge is a stinger');

  manifest.transitions = manifest.transitions.filter(
    (transition) =>
      transition.from !== hold.id ||
      sections.get(transition.to)?.classification !== hold.classification,
  );

  assert.doesNotThrow(() => validateSoundtrackManifest(manifest));
});

test('a stinger cannot satisfy a finite cue continuation', async () => {
  const manifest = await defaultManifestDocument();
  const sections = new Map(
    manifest.sections.map((section) => [section.id, section]),
  );
  const finite = manifest.sections.find((section) => {
    if (section.role !== 'bridge') return false;
    return manifest.transitions.some(
      (transition) =>
        transition.from === section.id &&
        transition.timing === 'section-end' &&
        sections.get(transition.to)?.role !== 'stinger' &&
        sections.get(transition.to)?.classification ===
          section.classification,
    );
  });
  assert.ok(finite, 'fixture needs a finite cue with an ordinary continuation');
  const continuations = manifest.transitions.filter(
    (transition) =>
      transition.from === finite.id &&
      sections.get(transition.to)?.role !== 'stinger' &&
      sections.get(transition.to)?.classification === finite.classification,
  );
  assert.ok(continuations.length > 0);
  const original = continuations[0];
  const stingerTemplate = manifest.sections.find(
    (section) => section.role === 'stinger',
  );
  assert.ok(stingerTemplate);
  const stingerId = 'validator-only-stinger';
  manifest.sections.push({
    ...structuredClone(stingerTemplate),
    id: stingerId,
    label: 'Validator-only stinger',
    classification: finite.classification,
  });
  manifest.transitions = manifest.transitions.filter(
    (transition) => !continuations.includes(transition),
  );
  manifest.transitions.push(
    { ...original, to: stingerId },
    {
      ...original,
      from: stingerId,
      to: original.to,
      timing: 'section-end',
    },
  );

  assert.throws(
    () => validateSoundtrackManifest(manifest),
    new RegExp(
      `Section "${finite.id}" has no executable non-stinger same-state continuation`,
    ),
  );
});

test('lowest-latency routing prefers a bounded bridge over a slow direct edge', () => {
  const slowDirect = routeTransition('source', 'target', 'section-end');
  const manifest = routeManifest([
    slowDirect,
    routeTransition('source', 'bridge', 'next-quantum'),
    routeTransition('bridge', 'target', 'section-end'),
  ]);

  const direct = lowestLatencyAdaptiveRoute(
    routeManifest([slowDirect]),
    'source',
    'tension',
  );
  const route = lowestLatencyAdaptiveRoute(manifest, 'source', 'tension');

  assert.equal(direct?.bars, 4);
  assert.deepEqual(route, {
    bars: 2,
    path: ['source', 'bridge', 'target'],
  });
});

test('cycles and unarmed stingers cannot satisfy adaptive reachability', () => {
  const manifest = routeManifest([
    routeTransition('source', 'bridge', 'next-quantum'),
    routeTransition('bridge', 'source', 'section-end'),
    routeTransition('source', 'sting', 'next-quantum'),
    routeTransition('sting', 'target', 'next-quantum'),
  ]);

  assert.equal(
    lowestLatencyAdaptiveRoute(manifest, 'source', 'tension'),
    null,
  );
});

function routeManifest(transitions) {
  return {
    sections: [
      {
        id: 'source',
        classification: 'sparse',
        role: 'hold',
        barCount: 4,
        repeat: { minimumBars: 8 },
      },
      {
        id: 'bridge',
        classification: 'sparse',
        role: 'bridge',
        barCount: 1,
      },
      {
        id: 'sting',
        classification: 'tension',
        role: 'stinger',
        barCount: 1,
      },
      {
        id: 'target',
        classification: 'tension',
        role: 'hold',
        barCount: 4,
        repeat: { minimumBars: 8 },
      },
    ],
    transitions,
  };
}

function routeTransition(from, to, timing) {
  return {
    from,
    to,
    timing,
    quantizeBars: 1,
    crossfadeBars: 0,
    weight: 1,
  };
}

async function defaultManifestDocument() {
  return manifestDocument();
}

async function manifestDocument(id) {
  const catalog = JSON.parse(
    await readFile(new URL('index.json', soundtrackRoot), 'utf8'),
  );
  const entry = catalog.tracks.find((candidate) =>
    id === undefined
      ? candidate.id === catalog.defaultId
      : candidate.id === id,
  );
  assert.ok(entry);
  return JSON.parse(
    await readFile(new URL(entry.manifest, soundtrackRoot), 'utf8'),
  );
}
