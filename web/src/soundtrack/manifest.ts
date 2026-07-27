import type {
  AdaptiveScoreState,
  SoundtrackCatalog,
  SoundtrackCatalogEntry,
  SoundtrackManifest,
  SoundtrackSection,
  SoundtrackTransition,
} from './types';

const SCORE_STATES = new Set<AdaptiveScoreState>([
  'sparse',
  'tension',
  'pursuit',
  'combat',
  'climax',
  'resolve',
]);
const GAMEPLAY_STATES: AdaptiveScoreState[] = [
  'sparse',
  'tension',
  'pursuit',
  'combat',
  'climax',
];
const SECTION_ROLES = new Set(['hold', 'bridge', 'stinger', 'resolve']);
const CONTENT_VERSION = /^v([1-9][0-9]*)-([0-9a-f]{16,64})$/;
const SHA256 = /^[0-9a-f]{64}$/;

export interface LoadedSoundtrack {
  catalog: SoundtrackCatalog;
  entry: SoundtrackCatalogEntry;
  manifest: SoundtrackManifest;
  manifestUrl: URL;
}

export interface AdaptiveSoundtrackRoute {
  bars: number;
  path: string[];
}

export async function loadSoundtrack(
  catalogUrl: URL,
  soundtrackId?: string,
  signal?: AbortSignal,
): Promise<LoadedSoundtrack> {
  const catalog = validateSoundtrackCatalog(
    await fetchJson(catalogUrl, 'no-cache', signal),
  );
  const id = soundtrackId ?? catalog.defaultId;
  const entry = catalog.tracks.find((candidate) => candidate.id === id);
  if (!entry) throw new Error(`Soundtrack "${id}" is not present in the catalog.`);

  const manifestUrl = new URL(entry.manifest, catalogUrl);
  const manifest = validateSoundtrackManifest(
    await fetchJson(manifestUrl, 'force-cache', signal),
    manifestUrl,
  );
  if (manifest.id !== entry.id) {
    throw new Error(
      `Soundtrack catalog id "${entry.id}" does not match manifest id "${manifest.id}".`,
    );
  }
  return { catalog, entry, manifest, manifestUrl };
}

async function fetchJson(
  url: URL,
  cache: RequestCache,
  signal?: AbortSignal,
): Promise<unknown> {
  const response = await fetch(url, { cache, signal });
  if (!response.ok) {
    throw new Error(`Could not load ${url.pathname} (${response.status}).`);
  }
  return response.json();
}

export function validateSoundtrackCatalog(value: unknown): SoundtrackCatalog {
  const catalog = asRecord(value, 'soundtrack catalog');
  if (catalog.schemaVersion !== 1) throw new Error('Unsupported soundtrack catalog.');
  if (!isString(catalog.defaultId) || !Array.isArray(catalog.tracks)) {
    throw new Error('Malformed soundtrack catalog.');
  }
  const tracks = catalog.tracks.map((candidate, index) => {
    const track = asRecord(candidate, `catalog track ${index}`);
    if (
      !isString(track.id) ||
      !isString(track.title) ||
      !isRelativeAssetPath(track.manifest)
    ) {
      throw new Error(`Malformed soundtrack catalog track ${index}.`);
    }
    return { id: track.id, title: track.title, manifest: track.manifest };
  });
  if (
    tracks.length === 0 ||
    !tracks.some((track) => track.id === catalog.defaultId)
  ) {
    throw new Error('Soundtrack catalog has no valid default track.');
  }
  if (new Set(tracks.map((track) => track.id)).size !== tracks.length) {
    throw new Error('Soundtrack catalog contains duplicate track ids.');
  }
  return { schemaVersion: 1, defaultId: catalog.defaultId, tracks };
}

export function validateSoundtrackManifest(
  value: unknown,
  manifestUrl?: URL,
): SoundtrackManifest {
  const manifest = asRecord(value, 'soundtrack manifest');
  if (manifest.schemaVersion !== 1) throw new Error('Unsupported soundtrack manifest.');
  if (
    !isString(manifest.id) ||
    !isString(manifest.title) ||
    !isRecord(manifest.provenance) ||
    !isString(manifest.provenance.sourceTool) ||
    (manifest.provenance.rightsStatus !== 'user-supplied-unverified' &&
      manifest.provenance.rightsStatus !== 'rights-cleared') ||
    (manifest.provenance.shipApproval !== 'pending' &&
      manifest.provenance.shipApproval !== 'approved') ||
    !isPositiveNumber(manifest.bpm) ||
    !isPositiveInteger(manifest.beatsPerBar) ||
    !isPositiveInteger(manifest.sampleRate) ||
    !isNonNegativeInteger(manifest.gridOriginFrame) ||
    !isPositiveInteger(manifest.barFrames) ||
    !isPositiveInteger(manifest.sourceEndFrame) ||
    manifest.sourceEndFrame <= manifest.gridOriginFrame ||
    !isPositiveInteger(manifest.segmentBars) ||
    !isPositiveNumber(manifest.durationSeconds) ||
    !isFiniteNumber(manifest.masterGainDb) ||
    !isRecord(manifest.adaptiveLatencyBudgetBars) ||
    !isPositiveNumber(manifest.adaptiveLatencyBudgetBars.gameplay) ||
    manifest.adaptiveLatencyBudgetBars.gameplay < 0.25 ||
    manifest.adaptiveLatencyBudgetBars.gameplay > 64 ||
    !isPositiveNumber(manifest.adaptiveLatencyBudgetBars.resolve) ||
    manifest.adaptiveLatencyBudgetBars.resolve < 0.25 ||
    manifest.adaptiveLatencyBudgetBars.resolve > 64 ||
    !isString(manifest.entrySection) ||
    !Array.isArray(manifest.stems) ||
    !Array.isArray(manifest.sections) ||
    !Array.isArray(manifest.transitions) ||
    !isRecord(manifest.assets) ||
    !isRecord(manifest.build)
  ) {
    throw new Error('Malformed soundtrack manifest header.');
  }
  const expectedBarFrames =
    (manifest.sampleRate * 60 * manifest.beatsPerBar) / manifest.bpm;
  if (Math.abs(expectedBarFrames - manifest.barFrames) > 1) {
    throw new Error('Soundtrack BPM and sample grid disagree.');
  }
  if (
    manifest.adaptiveSeam !== undefined &&
    (!isRecord(manifest.adaptiveSeam) ||
      !hasOnlyKeys(manifest.adaptiveSeam, [
        'strategy',
        'retreatBars',
        'overlapBars',
        'riseBars',
        'curve',
      ]) ||
      manifest.adaptiveSeam.strategy !== 'staged' ||
      !isFiniteNumber(manifest.adaptiveSeam.retreatBars) ||
      manifest.adaptiveSeam.retreatBars < 0.25 ||
      manifest.adaptiveSeam.retreatBars > 64 ||
      !isFiniteNumber(manifest.adaptiveSeam.overlapBars) ||
      manifest.adaptiveSeam.overlapBars < 0 ||
      manifest.adaptiveSeam.overlapBars > 64 ||
      !isFiniteNumber(manifest.adaptiveSeam.riseBars) ||
      manifest.adaptiveSeam.riseBars < 0.25 ||
      manifest.adaptiveSeam.riseBars > 64 ||
      manifest.adaptiveSeam.overlapBars >
        manifest.adaptiveSeam.retreatBars ||
      manifest.adaptiveSeam.overlapBars >
        manifest.adaptiveSeam.riseBars ||
      manifest.adaptiveSeam.curve !== 'linear')
  ) {
    throw new Error('Soundtrack has malformed adaptive seam metadata.');
  }

  // The pipeline owns detailed validation. Runtime checks the references that
  // would otherwise fail as mysterious fetch or graph errors.
  const typed = manifest as unknown as SoundtrackManifest;
  const versionMatch =
    isString(typed.build.version)
      ? CONTENT_VERSION.exec(typed.build.version)
      : null;
  if (
    !versionMatch ||
    !isPositiveInteger(typed.build.pipelineVersion) ||
    Number(versionMatch[1]) !== typed.build.pipelineVersion ||
    !isString(typed.build.sourceSha256) ||
    !SHA256.test(typed.build.sourceSha256) ||
    !isString(typed.build.configSha256) ||
    !SHA256.test(typed.build.configSha256) ||
    !isRecord(typed.build.encoder) ||
    !isString(typed.build.encoder.name) ||
    !isString(typed.build.encoder.version) ||
    !isString(typed.build.encoder.codec) ||
    !isPositiveInteger(typed.build.encoder.bitrateKbps) ||
    !isRelativeAssetPath(typed.build.analysis)
  ) {
    throw new Error('Malformed soundtrack build provenance.');
  }
  if (
    manifestUrl &&
    manifestVersionFromUrl(manifestUrl) !== typed.build.version
  ) {
    throw new Error(
      'Soundtrack manifest URL does not match its content version.',
    );
  }

  const assetEntries = Object.entries(typed.assets);
  if (assetEntries.length === 0) {
    throw new Error('Soundtrack manifest has no declared assets.');
  }
  for (const [path, asset] of assetEntries) {
    if (
      !isRelativeAssetPath(path) ||
      !isRecord(asset) ||
      !isString(asset.sha256) ||
      !SHA256.test(asset.sha256) ||
      !isPositiveInteger(asset.bytes)
    ) {
      throw new Error(`Soundtrack manifest has an invalid asset "${path}".`);
    }
  }
  if (!typed.assets[typed.build.analysis]) {
    throw new Error('Soundtrack build analysis is not a declared asset.');
  }

  const stemIds = new Set<string>();
  for (const stem of typed.stems) {
    if (
      !isString(stem.id) ||
      !isString(stem.label) ||
      !isString(stem.role) ||
      !isFiniteNumber(stem.gainDb) ||
      !isFiniteNumber(stem.response?.minimum) ||
      !isFiniteNumber(stem.response?.full) ||
      stem.response.minimum < 0 ||
      stem.response.full > 1 ||
      stem.response.minimum > stem.response.full ||
      stemIds.has(stem.id)
    ) {
      throw new Error(`Malformed or duplicate soundtrack stem "${stem.id}".`);
    }
    stemIds.add(stem.id);
  }

  if (typed.retrospectiveCue !== undefined) {
    const cue = typed.retrospectiveCue;
    if (
      !isRecord(cue) ||
      !hasOnlyKeys(cue, [
        'id',
        'startBar',
        'barCount',
        'anchorBar',
        'durationSeconds',
        'files',
      ]) ||
      !isString(cue.id) ||
      !isNonNegativeInteger(cue.startBar) ||
      !isPositiveInteger(cue.barCount) ||
      !isNonNegativeInteger(cue.anchorBar) ||
      cue.anchorBar >= cue.barCount ||
      !isPositiveNumber(cue.durationSeconds) ||
      !isRecord(cue.files)
    ) {
      throw new Error('Soundtrack has malformed retrospective cue metadata.');
    }
    const expectedDuration =
      (cue.barCount * typed.barFrames) / typed.sampleRate;
    const cueEndFrame =
      typed.gridOriginFrame +
      (cue.startBar + cue.barCount) * typed.barFrames;
    if (
      Math.abs(cue.durationSeconds - expectedDuration) >
        1 / typed.sampleRate ||
      cueEndFrame > typed.sourceEndFrame ||
      Object.keys(cue.files).length === 0
    ) {
      throw new Error('Soundtrack retrospective cue does not match the source grid.');
    }
    for (const [stemId, path] of Object.entries(cue.files)) {
      const asset = typed.assets[path];
      if (
        !stemIds.has(stemId) ||
        !isRelativeAssetPath(path) ||
        !asset ||
        !isString(asset.sha256) ||
        !isPositiveInteger(asset.bytes)
      ) {
        throw new Error('Soundtrack retrospective cue has an invalid stem asset.');
      }
    }
  }

  const sectionIds = new Set<string>();
  for (const section of typed.sections) {
    if (
      !isString(section.id) ||
      !isString(section.label) ||
      !SCORE_STATES.has(section.classification) ||
      !SECTION_ROLES.has(section.role) ||
      !isNonNegativeInteger(section.startBar) ||
      !isPositiveInteger(section.barCount) ||
      !isPositiveNumber(section.durationSeconds) ||
      !isFiniteNumber(section.energy) ||
      section.energy < 0 ||
      section.energy > 1 ||
      typeof section.loopable !== 'boolean' ||
      !isRecord(section.files) ||
      sectionIds.has(section.id)
    ) {
      throw new Error(`Malformed or duplicate soundtrack section "${section.id}".`);
    }
    const expectedDuration =
      (section.barCount * typed.barFrames) / typed.sampleRate;
    if (Math.abs(section.durationSeconds - expectedDuration) > 1 / typed.sampleRate) {
      throw new Error(`Section "${section.id}" does not match the musical grid.`);
    }
    for (const [stemId, path] of Object.entries(section.files)) {
      const asset = typed.assets[path];
      if (
        !stemIds.has(stemId) ||
        !isRelativeAssetPath(path) ||
        !asset ||
        !isString(asset.sha256) ||
        !isPositiveInteger(asset.bytes)
      ) {
        throw new Error(`Section "${section.id}" has an invalid stem asset.`);
      }
    }
    if (Object.keys(section.files).length === 0) {
      throw new Error(`Section "${section.id}" has no audible stem assets.`);
    }
    if (
      section.loopable &&
      (section.loop?.rendered !== true ||
        section.loop.strategy !== 'rendered-head-crossfade' ||
        !isPositiveNumber(section.loop.crossfadeSeconds) ||
        section.loop.crossfadeSeconds >= section.durationSeconds / 2 ||
        !isFiniteNumber(section.loop.boundarySimilarity) ||
        section.loop.boundarySimilarity < 0 ||
        section.loop.boundarySimilarity > 1 ||
        !isFiniteNumber(section.loop.sourceBoundarySimilarity) ||
        section.loop.sourceBoundarySimilarity < 0 ||
        section.loop.sourceBoundarySimilarity > 1 ||
        (section.loop.curve !== 'equal-power' &&
          section.loop.curve !== 'linear') ||
        !isPositiveInteger(section.loop.crossfadeFrames) ||
        !isPositiveInteger(section.loop.continuationFrames) ||
        section.loop.crossfadeFrames !== section.loop.continuationFrames ||
        Math.abs(
          section.loop.crossfadeFrames -
            section.loop.crossfadeSeconds * typed.sampleRate,
        ) > 1 ||
        !isFiniteNumber(section.loop.seamJumpDbfs) ||
        !isFiniteNumber(section.loop.blendPeakDbfs) ||
        !isFiniteNumber(section.loop.packHeadPeakDbfs) ||
        section.loop.packHeadPeakDbfs > 0 ||
        (section.loop.headroomTreatment !== 'none' &&
          section.loop.headroomTreatment !== 'linear-blend-fallback') ||
        (section.loop.approvalStatus !== 'analysis-reviewed' &&
          section.loop.approvalStatus !== 'auditioned') ||
        typeof section.loop.auditionRequired !== 'boolean' ||
        section.loop.auditionRequired !==
          (section.loop.approvalStatus !== 'auditioned'))
    ) {
      throw new Error(
        `Loopable section "${section.id}" does not use a supported rendered loop treatment.`,
      );
    }
    if (section.loopable !== (section.role === 'hold')) {
      throw new Error(
        `Section "${section.id}" role does not match its loop treatment.`,
      );
    }
    if (
      (section.classification === 'resolve') !== (section.role === 'resolve')
    ) {
      throw new Error(
        `Section "${section.id}" must pair resolve classification and role.`,
      );
    }
    if (section.repeat !== undefined) {
      if (
        section.role !== 'hold' ||
        !isRecord(section.repeat) ||
        !isPositiveInteger(section.repeat.minimumBars) ||
        section.repeat.minimumBars < section.barCount ||
        section.repeat.minimumBars % section.barCount !== 0
      ) {
        throw new Error(`Section "${section.id}" has invalid repeat metadata.`);
      }
    }
    if (section.cooldownSeconds !== undefined) {
      if (
        section.role !== 'stinger' ||
        !isPositiveNumber(section.cooldownSeconds) ||
        section.cooldownSeconds < section.durationSeconds ||
        section.cooldownSeconds > 3600
      ) {
        throw new Error(`Section "${section.id}" has invalid cooldown metadata.`);
      }
    }
    if (section.stemGainsDb !== undefined) {
      if (!isRecord(section.stemGainsDb)) {
        throw new Error(`Section "${section.id}" has malformed stem gains.`);
      }
      for (const [stemId, gain] of Object.entries(section.stemGainsDb)) {
        if (!stemIds.has(stemId) || !isFiniteNumber(gain)) {
          throw new Error(`Section "${section.id}" has an invalid stem gain.`);
        }
      }
    }
    sectionIds.add(section.id);
  }
  if (!sectionIds.has(typed.entrySection)) {
    throw new Error('Soundtrack entry section does not exist.');
  }
  if (
    typed.sections.find((section) => section.id === typed.entrySection)?.role !==
    'hold'
  ) {
    throw new Error('Soundtrack entry section must be a hold.');
  }
  const transitionPairs = new Set<string>();
  for (const transition of typed.transitions) {
    const pair = `${transition.from}\0${transition.to}`;
    if (
      !sectionIds.has(transition.from) ||
      !sectionIds.has(transition.to) ||
      transition.from === transition.to ||
      transitionPairs.has(pair) ||
      (transition.timing !== 'next-quantum' &&
        transition.timing !== 'section-end') ||
      !isPositiveNumber(transition.quantizeBars) ||
      !isFiniteNumber(transition.crossfadeBars) ||
      transition.crossfadeBars < 0 ||
      transition.crossfadeBars > transition.quantizeBars ||
      !isFiniteNumber(transition.weight) ||
      transition.weight < 0
    ) {
      throw new Error('Soundtrack contains an invalid transition.');
    }
    transitionPairs.add(pair);
  }
  const transitionsBySource = new Map<string, typeof typed.transitions>();
  for (const transition of typed.transitions) {
    const outgoing = transitionsBySource.get(transition.from) ?? [];
    outgoing.push(transition);
    transitionsBySource.set(transition.from, outgoing);
  }
  const sectionsById = new Map(
    typed.sections.map((section) => [section.id, section]),
  );
  for (const state of GAMEPLAY_STATES) {
    if (
      !typed.sections.some(
        (section) =>
          section.role === 'hold' && section.classification === state,
      )
    ) {
      throw new Error(`Soundtrack has no hold for gameplay state "${state}".`);
    }
  }
  for (const section of typed.sections) {
    if (section.classification === 'resolve') continue;
    const outgoing = transitionsBySource.get(section.id) ?? [];
    const ordinarySameState = outgoing.filter((transition) => {
      const destination = sectionsById.get(transition.to);
      return (
        destination?.role !== 'stinger' &&
        destination?.classification === section.classification
      );
    });
    const hasExecutableSameStateContinuation = ordinarySameState.some(
      (transition) =>
        section.role === 'hold'
          ? transition.timing === 'next-quantum'
          : transition.timing === 'section-end',
    );
    const invalidSameStateEdge = ordinarySameState.some(
      (transition) =>
        section.role === 'hold'
          ? transition.timing !== 'next-quantum'
          : transition.timing !== 'section-end',
    );
    if (invalidSameStateEdge) {
      throw new Error(
        `Section "${section.id}" has an incompatible same-state transition.`,
      );
    }
    // A reviewed hold can always continue through its intrinsic rendered
    // loop. Finite cues need an authored ordinary successor before their
    // source buffer ends; trigger-gated stingers can never satisfy that path.
    if (section.role !== 'hold' && !hasExecutableSameStateContinuation) {
      throw new Error(
        `Section "${section.id}" has no executable non-stinger same-state continuation.`,
      );
    }
    const resolveRoute = lowestLatencyAdaptiveRoute(
      typed,
      section.id,
      'resolve',
    );
    if (!resolveRoute) {
      throw new Error(
        `Section "${section.id}" cannot reach a resolution without a stinger.`,
      );
    }
    if (
      resolveRoute.bars >
      typed.adaptiveLatencyBudgetBars.resolve + Number.EPSILON
    ) {
      throw new Error(
        `Section "${section.id}" exceeds the resolution latency budget.`,
      );
    }
    if (section.role !== 'hold') continue;
    for (const target of GAMEPLAY_STATES) {
      if (target === section.classification) continue;
      const route = lowestLatencyAdaptiveRoute(typed, section.id, target);
      if (!route) {
        throw new Error(
          `Hold "${section.id}" cannot reach gameplay state "${target}" without a stinger.`,
        );
      }
      if (
        route.bars >
        typed.adaptiveLatencyBudgetBars.gameplay + Number.EPSILON
      ) {
        throw new Error(
          `Hold "${section.id}" exceeds the "${target}" latency budget.`,
        );
      }
    }
  }
  return typed;
}

export function lowestLatencyAdaptiveRoute(
  manifest: SoundtrackManifest,
  sourceId: string,
  target: AdaptiveScoreState,
): AdaptiveSoundtrackRoute | null {
  const sections = new Map(
    manifest.sections.map((section) => [section.id, section]),
  );
  const source = sections.get(sourceId);
  if (!source) return null;
  const outgoing = new Map<string, SoundtrackTransition[]>();
  for (const transition of manifest.transitions) {
    const edges = outgoing.get(transition.from) ?? [];
    edges.push(transition);
    outgoing.set(transition.from, edges);
  }
  const isTarget = (section: SoundtrackSection): boolean =>
    target === 'resolve'
      ? section.role === 'resolve'
      : section.role === 'hold' && section.classification === target;
  const pending = [{ id: sourceId, bars: 0, hops: 0, path: [sourceId] }];
  const best = new Map<
    string,
    { bars: number; hops: number; pathKey: string }
  >([[sourceId, { bars: 0, hops: 0, pathKey: sourceId }]]);

  while (pending.length > 0) {
    pending.sort(
      (left, right) =>
        left.bars - right.bars ||
        left.hops - right.hops ||
        left.path.join('\0').localeCompare(right.path.join('\0')),
    );
    const current = pending.shift()!;
    const currentKey = current.path.join('\0');
    const known = best.get(current.id);
    if (
      !known ||
      known.bars !== current.bars ||
      known.hops !== current.hops ||
      known.pathKey !== currentKey
    ) {
      continue;
    }
    const currentSection = sections.get(current.id);
    if (!currentSection) continue;
    if (isTarget(currentSection)) {
      return { bars: current.bars, path: current.path };
    }
    const edges = [...(outgoing.get(current.id) ?? [])].sort(
      (left, right) => right.weight - left.weight || left.to.localeCompare(right.to),
    );
    for (const transition of edges) {
      const destination = sections.get(transition.to);
      if (
        !destination ||
        !adaptiveRouteDestinationAllowed(
          destination,
          source.classification,
          target,
        )
      ) {
        continue;
      }
      const next = {
        id: destination.id,
        bars:
          current.bars +
          adaptiveTransitionWaitBars(currentSection, transition),
        hops: current.hops + 1,
        path: [...current.path, destination.id],
      };
      const pathKey = next.path.join('\0');
      const previous = best.get(next.id);
      if (
        previous &&
        (previous.bars < next.bars ||
          (previous.bars === next.bars &&
            (previous.hops < next.hops ||
              (previous.hops === next.hops &&
                previous.pathKey.localeCompare(pathKey) <= 0))))
      ) {
        continue;
      }
      best.set(next.id, {
        bars: next.bars,
        hops: next.hops,
        pathKey,
      });
      pending.push(next);
    }
  }
  return null;
}

function adaptiveTransitionWaitBars(
  source: SoundtrackSection,
  transition: SoundtrackTransition,
): number {
  if (transition.timing === 'next-quantum') return transition.quantizeBars;
  return source.barCount;
}

function adaptiveRouteDestinationAllowed(
  section: SoundtrackSection,
  origin: AdaptiveScoreState,
  target: AdaptiveScoreState,
): boolean {
  if (section.role === 'stinger') return false;
  if (target === 'resolve') return true;
  if (section.role === 'resolve') return false;
  // A completed presentation can seek or restart into any gameplay frame.
  // Resolve is outside the ordinary escalation ladder, but an authored
  // resolve-to-entry edge must still be usable as the first step back in.
  const originRank =
    origin === 'resolve'
      ? GAMEPLAY_STATES.indexOf('sparse')
      : GAMEPLAY_STATES.indexOf(origin);
  const targetRank = GAMEPLAY_STATES.indexOf(target);
  const sectionRank = GAMEPLAY_STATES.indexOf(section.classification);
  if (originRank < 0 || targetRank < 0 || sectionRank < 0) return false;
  return (
    sectionRank >= Math.min(originRank, targetRank) &&
    sectionRank <= Math.max(originRank, targetRank)
  );
}

function asRecord(value: unknown, label: string): Record<string, unknown> {
  if (!isRecord(value)) throw new Error(`Malformed ${label}.`);
  return value;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function hasOnlyKeys(
  value: Record<string, unknown>,
  allowed: readonly string[],
): boolean {
  const keys = new Set(allowed);
  return Object.keys(value).every((key) => keys.has(key));
}

function isString(value: unknown): value is string {
  return typeof value === 'string' && value.length > 0;
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value);
}

function isPositiveNumber(value: unknown): value is number {
  return isFiniteNumber(value) && value > 0;
}

function isPositiveInteger(value: unknown): value is number {
  return isPositiveNumber(value) && Number.isInteger(value);
}

function isNonNegativeInteger(value: unknown): value is number {
  return isFiniteNumber(value) && value >= 0 && Number.isInteger(value);
}

function isRelativeAssetPath(value: unknown): value is string {
  if (
    !isString(value) ||
    value.startsWith('/') ||
    value.includes('\\') ||
    value.includes(':') ||
    value.includes('?') ||
    value.includes('#')
  ) {
    return false;
  }
  try {
    const parts = value.split('/').map((part) => decodeURIComponent(part));
    return !parts.includes('..') && !parts.includes('');
  } catch {
    return false;
  }
}

function manifestVersionFromUrl(url: URL): string | null {
  const parts = url.pathname.split('/').filter((part) => part.length > 0);
  if (parts.length < 2) return null;
  try {
    return decodeURIComponent(parts[parts.length - 2]);
  } catch {
    return null;
  }
}
