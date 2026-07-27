#!/usr/bin/env node

import { execFileSync } from 'node:child_process';
import {
  glob,
  lstat,
  readFile,
  readdir,
  realpath,
} from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const SCORE_STATES = Object.freeze([
  'sparse',
  'tension',
  'pursuit',
  'combat',
  'climax',
  'resolve',
]);

const STATE_RANK = Object.freeze(
  Object.fromEntries(SCORE_STATES.map((state, index) => [state, index])),
);
const RUN_LENGTHS = Symbol('runLengths');
const GLOB_MAGIC = /[*?[\]{}]/;
const DEFAULT_OPTIONS = Object.freeze({
  ticksPerSecond: 5,
  shortSeconds: 10,
  mediumSeconds: 30,
  phraseBarBpm: 120,
  phraseBarBeats: 4,
});
const SCRIPT_DIRECTORY = path.dirname(fileURLToPath(import.meta.url));
const REPOSITORY_ROOT = path.resolve(SCRIPT_DIRECTORY, '..');
const WEB_DIRECTORY = path.join(REPOSITORY_ROOT, 'web');
const HARNESS_ENTRY = path.join(
  WEB_DIRECTORY,
  'tests',
  '.harness',
  'harness.entry.js',
);

const {
  buildAdaptiveTimeline,
  loadReplayJson,
  loadReplayObject,
} = await loadAuditRuntime();

/**
 * Expand explicit replay files, directories, and shell-style glob patterns.
 * Directories are searched recursively for files named replay.json.
 */
export async function collectReplayFiles(inputs, cwd = process.cwd()) {
  if (!Array.isArray(inputs) || inputs.length === 0) {
    throw new Error('Provide at least one replay file, directory, or glob.');
  }

  const discovered = new Map();
  for (const input of inputs) {
    if (typeof input !== 'string' || input.length === 0) {
      throw new Error('Replay inputs must be non-empty paths or glob patterns.');
    }
    const matches = [];
    if (GLOB_MAGIC.test(input)) {
      for await (const match of glob(input, { cwd })) {
        matches.push(path.resolve(cwd, match));
      }
    } else {
      matches.push(path.resolve(cwd, input));
    }
    if (matches.length === 0) {
      throw new Error(`Replay input matched no files: ${input}`);
    }

    for (const match of matches.sort()) {
      const info = await lstat(match).catch((reason) => {
        throw new Error(
          `Could not inspect replay input ${match}: ${errorMessage(reason)}`,
        );
      });
      if (info.isDirectory()) {
        for (const file of await replayFilesBelow(match)) {
          discovered.set(await realpath(file), file);
        }
      } else if (info.isFile()) {
        discovered.set(await realpath(match), match);
      } else {
        throw new Error(
          `Replay input is not a regular file or directory: ${match}`,
        );
      }
    }
  }

  const files = [...discovered.values()].sort();
  if (files.length === 0) {
    throw new Error(
      'No replay.json files were found below the supplied directories.',
    );
  }
  return files;
}

export async function auditReplayPaths(inputs, options = {}) {
  const cwd =
    options.cwd === undefined ? process.cwd() : path.resolve(options.cwd);
  const files = await collectReplayFiles(inputs, cwd);
  const entries = await Promise.all(
    files.map(async (file) => {
      let replay;
      try {
        replay = loadReplayJson(await readFile(file, 'utf8')).replay;
      } catch (reason) {
        throw new Error(
          `Could not decode replay ${file}: ${errorMessage(reason)}`,
        );
      }
      const relative = path.relative(cwd, file);
      return {
        path: relative.length > 0 ? relative : path.basename(file),
        replay,
      };
    }),
  );
  return auditReplayDocuments(entries, options);
}

/**
 * Audit parsed replay wire documents or already-normalized ReplayModels.
 * Each entry is `{ path, replay }`.
 */
export function auditReplayDocuments(entries, options = {}) {
  const resolved = resolveOptions(options);
  if (!Array.isArray(entries) || entries.length === 0) {
    throw new Error('At least one replay document is required.');
  }

  const replays = entries.map((entry) => {
    if (
      entry === null ||
      typeof entry !== 'object' ||
      typeof entry.path !== 'string'
    ) {
      throw new Error(
        'Replay entries must have a string path and replay document.',
      );
    }

    let timeline;
    try {
      const replay = normalizeReplayForAudit(entry.replay);
      timeline = buildAdaptiveTimeline(replay);
    } catch (reason) {
      throw new Error(
        `Could not direct soundtrack for ${entry.path}: ${errorMessage(reason)}`,
      );
    }
    return summarizeFrames(
      entry.path,
      timeline.frames,
      resolved.ticksPerSecond,
      resolved.phraseBarTicks,
    );
  });

  const buckets = [
    {
      id: 'short',
      label: `≤${formatNumber(resolved.shortSeconds)}s`,
      replays: replays.filter(
        (replay) => replay.durationSeconds <= resolved.shortSeconds,
      ),
    },
    {
      id: 'medium',
      label:
        `>${formatNumber(resolved.shortSeconds)}–` +
        `${formatNumber(resolved.mediumSeconds)}s`,
      replays: replays.filter(
        (replay) =>
          replay.durationSeconds > resolved.shortSeconds &&
          replay.durationSeconds <= resolved.mediumSeconds,
      ),
    },
    {
      id: 'long',
      label: `>${formatNumber(resolved.mediumSeconds)}s`,
      replays: replays.filter(
        (replay) => replay.durationSeconds > resolved.mediumSeconds,
      ),
    },
  ];

  const tickCounts = replays.map((replay) => replay.tickCount).sort(numberSort);
  return {
    schemaVersion: 2,
    options: {
      ticksPerSecond: resolved.ticksPerSecond,
      shortSeconds: resolved.shortSeconds,
      mediumSeconds: resolved.mediumSeconds,
      phraseBarBpm: resolved.phraseBarBpm,
      phraseBarBeats: resolved.phraseBarBeats,
      phraseBarTicks: resolved.phraseBarTicks,
    },
    replayCount: replays.length,
    tickRange: {
      minimum: tickCounts[0],
      median: median(tickCounts),
      maximum: tickCounts.at(-1),
    },
    overall: summarizeGroup(replays, resolved.phraseBarTicks),
    buckets: buckets.map(({ id, label, replays: bucketReplays }) => ({
      id,
      label,
      ...summarizeGroup(bucketReplays, resolved.phraseBarTicks),
    })),
    replays,
  };
}

export function formatAuditText(report) {
  const { ticksPerSecond, phraseBarTicks } = report.options;
  const lines = [
    `Soundtrack director audit: ${report.replayCount} replay${report.replayCount === 1 ? '' : 's'} @ ${formatNumber(ticksPerSecond)} TPS`,
    `Tick range min/median/max: ${formatNumber(report.tickRange.minimum)} / ${formatNumber(report.tickRange.median)} / ${formatNumber(report.tickRange.maximum)}`,
    `Overall occupancy: ${formatOccupancy(report.overall.stateOccupancy)}`,
    `One-bar runs (≥${formatNumber(phraseBarTicks)} ticks): ${formatPhraseRuns(report.overall.stateRuns)}`,
    `Non-acute climax: ${formatNonAcuteClimax(report.overall, ticksPerSecond)}`,
  ];

  for (const bucket of report.buckets) {
    if (bucket.replayCount === 0) {
      lines.push(`${bucket.id} (${bucket.label}): 0 replays`);
      continue;
    }
    const maxDwell = bucket.maxDwell;
    const maximumDominance = bucket.dominance.maximum;
    lines.push(
      `${bucket.id} (${bucket.label}): ${bucket.replayCount} replays, ` +
        `${formatNumber(bucket.averageTicks, 1)} ticks / ` +
        `${formatNumber(bucket.averageSeconds, 2)}s avg`,
      `  occupancy: ${formatOccupancy(bucket.stateOccupancy)}`,
      `  transitions: ${bucket.transitionCount} total / ` +
        `${formatNumber(bucket.transitionsPerReplay, 1)} per replay; ` +
        `skipped-rank leaps: ${bucket.skippedRankLeapCount}`,
      `  one-bar runs: ${formatPhraseRuns(bucket.stateRuns)}`,
      `  max dwell: ${maxDwell.ticks} ticks / ` +
        `${formatNumber(maxDwell.seconds, 2)}s ${maxDwell.state} ` +
        `(${maxDwell.path}); avg replay maximum ` +
        `${formatNumber(bucket.averageMaxDwellTicks, 1)} ticks`,
      `  dominance: ${formatNumber(bucket.dominance.averagePercent, 1)}% avg; ` +
        `${formatNumber(maximumDominance.percent, 1)}% ` +
        `${maximumDominance.state} (${maximumDominance.path})`,
      `  non-acute climax: ${formatNonAcuteClimax(bucket, ticksPerSecond)}`,
    );
  }
  return lines.join('\n');
}

async function loadAuditRuntime() {
  try {
    execFileSync(
      process.platform === 'win32' ? 'npm.cmd' : 'npm',
      ['run', 'harness', '--silent'],
      {
        cwd: WEB_DIRECTORY,
        encoding: 'utf8',
        stdio: ['ignore', 'pipe', 'pipe'],
      },
    );
  } catch (reason) {
    const detail =
      reason !== null &&
      typeof reason === 'object' &&
      'stderr' in reason &&
      typeof reason.stderr === 'string'
        ? reason.stderr.trim()
        : errorMessage(reason);
    throw new Error(
      'Could not build the soundtrack audit runtime. Run ' +
        '`npm ci --prefix web && npm run harness --prefix web`.' +
        (detail.length > 0 ? ` ${detail}` : ''),
    );
  }

  let runtime;
  try {
    runtime = await import(
      `${pathToFileURL(HARNESS_ENTRY).href}?soundtrack-audit=${Date.now()}`
    );
  } catch (reason) {
    throw new Error(
      `Could not load the Vite soundtrack audit runtime: ${errorMessage(reason)}`,
    );
  }

  for (const name of [
    'buildAdaptiveTimeline',
    'loadReplayJson',
    'loadReplayObject',
  ]) {
    if (typeof runtime[name] !== 'function') {
      throw new Error(
        `Vite soundtrack audit runtime is missing export "${name}".`,
      );
    }
  }
  return runtime;
}

function normalizeReplayForAudit(replay) {
  if (isReplayModel(replay)) return replay;
  if (typeof replay === 'string') return loadReplayJson(replay).replay;
  return loadReplayObject(replay).replay;
}

function isReplayModel(value) {
  return (
    value !== null &&
    typeof value === 'object' &&
    (value.sourceVersion === 1 || value.sourceVersion === 2) &&
    value.contract !== null &&
    typeof value.contract === 'object' &&
    Array.isArray(value.ticks)
  );
}

async function replayFilesBelow(directory) {
  const files = [];
  const entries = await readdir(directory, { withFileTypes: true });
  for (const entry of entries.sort((left, right) =>
    left.name.localeCompare(right.name),
  )) {
    const candidate = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...(await replayFilesBelow(candidate)));
    } else if (entry.isFile() && entry.name === 'replay.json') {
      files.push(candidate);
    }
  }
  return files;
}

function summarizeFrames(
  replayPath,
  frames,
  ticksPerSecond,
  phraseBarTicks,
) {
  const stateTicks = Object.fromEntries(SCORE_STATES.map((state) => [state, 0]));
  const runLengths = Object.fromEntries(
    SCORE_STATES.map((state) => [state, []]),
  );
  let transitionCount = 0;
  let skippedRankLeapCount = 0;
  let runState = null;
  let runTicks = 0;
  let maxDwellState = null;
  let maxDwellTicks = 0;
  let nonAcuteClimaxTicks = 0;
  let nonAcuteClimaxRunTicks = 0;
  let maxNonAcuteClimaxRunTicks = 0;

  for (const frame of frames) {
    if (!(frame.state in stateTicks)) {
      throw new Error(
        `Unknown soundtrack state "${frame.state}" in ${replayPath}.`,
      );
    }
    stateTicks[frame.state] += 1;
    if (frame.state === runState) {
      runTicks += 1;
    } else {
      if (runState !== null) runLengths[runState].push(runTicks);
      if (runTicks > maxDwellTicks) {
        maxDwellTicks = runTicks;
        maxDwellState = runState;
      }
      if (runState !== null) {
        transitionCount += 1;
        if (Math.abs(STATE_RANK[frame.state] - STATE_RANK[runState]) > 1) {
          skippedRankLeapCount += 1;
        }
      }
      runState = frame.state;
      runTicks = 1;
    }

    const nonAcuteClimax =
      frame.state === 'climax' &&
      frame.features.objectiveImminence === 0 &&
      frame.features.acuteThreat < 0.5 &&
      !frame.features.overtime;
    if (nonAcuteClimax) {
      nonAcuteClimaxTicks += 1;
      nonAcuteClimaxRunTicks += 1;
      maxNonAcuteClimaxRunTicks = Math.max(
        maxNonAcuteClimaxRunTicks,
        nonAcuteClimaxRunTicks,
      );
    } else {
      nonAcuteClimaxRunTicks = 0;
    }
  }
  if (runState !== null) runLengths[runState].push(runTicks);
  if (runTicks > maxDwellTicks) {
    maxDwellTicks = runTicks;
    maxDwellState = runState;
  }

  let dominantState = null;
  let dominantTicks = 0;
  for (const state of SCORE_STATES) {
    if (stateTicks[state] > dominantTicks) {
      dominantState = state;
      dominantTicks = stateTicks[state];
    }
  }

  const summary = {
    path: replayPath,
    tickCount: frames.length,
    durationSeconds: round(frames.length / ticksPerSecond, 4),
    stateTicks,
    stateRuns: summarizeStateRuns(runLengths, phraseBarTicks),
    transitionCount,
    skippedRankLeapCount,
    maxDwell: {
      state: maxDwellState,
      ticks: maxDwellTicks,
      seconds: round(maxDwellTicks / ticksPerSecond, 4),
    },
    dominance: {
      state: dominantState,
      ticks: dominantTicks,
      percent: percentage(dominantTicks, frames.length),
    },
    nonAcuteClimaxTicks,
    maxNonAcuteClimaxRun: {
      ticks: maxNonAcuteClimaxRunTicks,
      seconds: round(maxNonAcuteClimaxRunTicks / ticksPerSecond, 4),
    },
  };
  summary[RUN_LENGTHS] = runLengths;
  return summary;
}

function summarizeGroup(replays, phraseBarTicks) {
  const totalTicks = replays.reduce(
    (total, replay) => total + replay.tickCount,
    0,
  );
  const stateTicks = Object.fromEntries(
    SCORE_STATES.map((state) => [
      state,
      replays.reduce((total, replay) => total + replay.stateTicks[state], 0),
    ]),
  );
  const emptyExtreme = { state: null, ticks: 0, seconds: 0, path: null };
  const maxDwellReplay = maximumBy(
    replays,
    (replay) => replay.maxDwell.ticks,
  );
  const maximumDominanceReplay = maximumBy(
    replays,
    (replay) => replay.dominance.percent,
  );
  const maxNonAcuteClimaxReplay = maximumBy(
    replays,
    (replay) => replay.maxNonAcuteClimaxRun.ticks,
  );
  const groupRunLengths = Object.fromEntries(
    SCORE_STATES.map((state) => [
      state,
      replays.flatMap((replay) => replay[RUN_LENGTHS][state]),
    ]),
  );
  return {
    replayCount: replays.length,
    totalTicks,
    totalSeconds: round(
      replays.reduce((total, replay) => total + replay.durationSeconds, 0),
      4,
    ),
    averageTicks: average(replays.map((replay) => replay.tickCount)),
    averageSeconds: average(
      replays.map((replay) => replay.durationSeconds),
    ),
    stateOccupancy: Object.fromEntries(
      SCORE_STATES.map((state) => [
        state,
        {
          ticks: stateTicks[state],
          percent: percentage(stateTicks[state], totalTicks),
        },
      ]),
    ),
    stateRuns: summarizeStateRuns(groupRunLengths, phraseBarTicks),
    transitionCount: replays.reduce(
      (total, replay) => total + replay.transitionCount,
      0,
    ),
    transitionsPerReplay: average(
      replays.map((replay) => replay.transitionCount),
    ),
    skippedRankLeapCount: replays.reduce(
      (total, replay) => total + replay.skippedRankLeapCount,
      0,
    ),
    averageMaxDwellTicks: average(
      replays.map((replay) => replay.maxDwell.ticks),
    ),
    maxDwell:
      maxDwellReplay === null
        ? emptyExtreme
        : {
            ...maxDwellReplay.maxDwell,
            path: maxDwellReplay.path,
          },
    dominance: {
      averagePercent: average(
        replays.map((replay) => replay.dominance.percent),
      ),
      maximum:
        maximumDominanceReplay === null
          ? { state: null, ticks: 0, percent: 0, path: null }
          : {
              ...maximumDominanceReplay.dominance,
              path: maximumDominanceReplay.path,
            },
    },
    nonAcuteClimaxTicks: replays.reduce(
      (total, replay) => total + replay.nonAcuteClimaxTicks,
      0,
    ),
    maxNonAcuteClimaxRun:
      maxNonAcuteClimaxReplay === null ||
      maxNonAcuteClimaxReplay.maxNonAcuteClimaxRun.ticks === 0
        ? { ticks: 0, seconds: 0, path: null }
        : {
            ...maxNonAcuteClimaxReplay.maxNonAcuteClimaxRun,
            path: maxNonAcuteClimaxReplay.path,
          },
  };
}

function resolveOptions(options) {
  const ticksPerSecond = positiveNumber(
    options.ticksPerSecond,
    DEFAULT_OPTIONS.ticksPerSecond,
    'ticksPerSecond',
  );
  const shortSeconds = positiveNumber(
    options.shortSeconds,
    DEFAULT_OPTIONS.shortSeconds,
    'shortSeconds',
  );
  const mediumSeconds = positiveNumber(
    options.mediumSeconds,
    DEFAULT_OPTIONS.mediumSeconds,
    'mediumSeconds',
  );
  const phraseBarBpm = positiveNumber(
    options.phraseBarBpm,
    DEFAULT_OPTIONS.phraseBarBpm,
    'phraseBarBpm',
  );
  const phraseBarBeats = positiveInteger(
    options.phraseBarBeats,
    DEFAULT_OPTIONS.phraseBarBeats,
    'phraseBarBeats',
  );
  if (mediumSeconds <= shortSeconds) {
    throw new Error('mediumSeconds must be greater than shortSeconds.');
  }
  const phraseBarTicks =
    ticksPerSecond * (60 / phraseBarBpm) * phraseBarBeats;
  return {
    ticksPerSecond,
    shortSeconds,
    mediumSeconds,
    phraseBarBpm,
    phraseBarBeats,
    phraseBarTicks,
  };
}

function summarizeStateRuns(runLengths, phraseBarTicks) {
  return Object.fromEntries(
    SCORE_STATES.map((state) => {
      const lengths = [...runLengths[state]].sort(numberSort);
      const runsReachingOneBar = lengths.filter(
        (ticks) => ticks >= phraseBarTicks,
      ).length;
      return [
        state,
        {
          runCount: lengths.length,
          phraseBarTicks,
          runsReachingOneBar,
          percentReachingOneBar: percentage(
            runsReachingOneBar,
            lengths.length,
          ),
          minimumTicks: lengths[0] ?? 0,
          medianTicks: median(lengths),
          maximumTicks: lengths.at(-1) ?? 0,
        },
      ];
    }),
  );
}

function positiveNumber(value, fallback, name) {
  const resolved = value === undefined ? fallback : Number(value);
  if (!Number.isFinite(resolved) || resolved <= 0) {
    throw new Error(`${name} must be a positive number.`);
  }
  return resolved;
}

function positiveInteger(value, fallback, name) {
  const resolved = value === undefined ? fallback : Number(value);
  if (!Number.isSafeInteger(resolved) || resolved <= 0) {
    throw new Error(`${name} must be a positive integer.`);
  }
  return resolved;
}

function average(values) {
  if (values.length === 0) return 0;
  return round(
    values.reduce((total, value) => total + value, 0) / values.length,
    4,
  );
}

function median(sortedValues) {
  if (sortedValues.length === 0) return 0;
  const middle = Math.floor(sortedValues.length / 2);
  if (sortedValues.length % 2 === 1) return sortedValues[middle];
  return (sortedValues[middle - 1] + sortedValues[middle]) / 2;
}

function maximumBy(values, score) {
  let maximum = null;
  for (const value of values) {
    if (maximum === null || score(value) > score(maximum)) maximum = value;
  }
  return maximum;
}

function percentage(numerator, denominator) {
  return denominator === 0 ? 0 : round((numerator / denominator) * 100, 4);
}

function round(value, digits) {
  const scale = 10 ** digits;
  return Math.round((value + Number.EPSILON) * scale) / scale;
}

function numberSort(left, right) {
  return left - right;
}

function formatOccupancy(occupancy) {
  return SCORE_STATES.map(
    (state) => `${state} ${formatNumber(occupancy[state].percent, 1)}%`,
  ).join(', ');
}

function formatPhraseRuns(stateRuns) {
  return SCORE_STATES.map((state) => {
    const runs = stateRuns[state];
    return (
      `${state} ${runs.runsReachingOneBar}/${runs.runCount}` +
      ` (median ${formatNumber(runs.medianTicks)}t)`
    );
  }).join(', ');
}

function formatNonAcuteClimax(summary, ticksPerSecond) {
  const maximum = summary.maxNonAcuteClimaxRun;
  if (maximum.ticks === 0) {
    return `${summary.nonAcuteClimaxTicks} ticks; max run 0`;
  }
  const location = maximum.path === undefined ? '' : ` (${maximum.path})`;
  return (
    `${summary.nonAcuteClimaxTicks} ticks; max run ${maximum.ticks} ticks / ` +
    `${formatNumber(maximum.ticks / ticksPerSecond, 2)}s${location}`
  );
}

function formatNumber(value, digits) {
  return digits === undefined
    ? String(value)
    : Number(value).toFixed(digits).replace(/\.?0+$/, '');
}

function errorMessage(reason) {
  return reason instanceof Error ? reason.message : String(reason);
}

export function usage() {
  return `Usage:
  node scripts/audit_soundtrack_director.mjs [options] <replay|directory|glob>...

Options:
  --json                    Emit machine-readable JSON.
  --tps NUMBER              Presentation ticks per second (default: 5).
  --bpm NUMBER              Phrase tempo in beats per minute (default: 120).
  --beats-per-bar NUMBER    Beats in one phrase bar (default: 4).
  --short-seconds NUMBER    Upper bound for the short bucket (default: 10).
  --medium-seconds NUMBER   Upper bound for the medium bucket (default: 30).
  -h, --help                Show this help.

Directories are searched recursively for replay.json. Quote glob patterns to
let this script expand them consistently. The command reads replay data only.`;
}

export function parseCliArguments(argv) {
  const inputs = [];
  const options = {};
  let json = false;
  let positionalOnly = false;
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (positionalOnly) {
      inputs.push(argument);
    } else if (argument === '--') {
      positionalOnly = true;
    } else if (argument === '--json') {
      json = true;
    } else if (argument === '--help' || argument === '-h') {
      return { help: true, inputs, options, json };
    } else if (
      argument === '--tps' ||
      argument === '--bpm' ||
      argument === '--beats-per-bar' ||
      argument === '--short-seconds' ||
      argument === '--medium-seconds'
    ) {
      const value = argv[index + 1];
      if (value === undefined) throw new Error(`${argument} requires a value.`);
      index += 1;
      const key =
        argument === '--tps'
          ? 'ticksPerSecond'
          : argument === '--bpm'
            ? 'phraseBarBpm'
            : argument === '--beats-per-bar'
              ? 'phraseBarBeats'
              : argument === '--short-seconds'
                ? 'shortSeconds'
                : 'mediumSeconds';
      options[key] = value;
    } else if (argument.startsWith('--tps=')) {
      options.ticksPerSecond = argument.slice('--tps='.length);
    } else if (argument.startsWith('--bpm=')) {
      options.phraseBarBpm = argument.slice('--bpm='.length);
    } else if (argument.startsWith('--beats-per-bar=')) {
      options.phraseBarBeats = argument.slice('--beats-per-bar='.length);
    } else if (argument.startsWith('--short-seconds=')) {
      options.shortSeconds = argument.slice('--short-seconds='.length);
    } else if (argument.startsWith('--medium-seconds=')) {
      options.mediumSeconds = argument.slice('--medium-seconds='.length);
    } else if (argument.startsWith('-')) {
      throw new Error(`Unknown option: ${argument}`);
    } else {
      inputs.push(argument);
    }
  }
  return { help: false, inputs, options, json };
}

async function main(argv) {
  const parsed = parseCliArguments(argv);
  if (parsed.help) {
    process.stdout.write(`${usage()}\n`);
    return;
  }
  const report = await auditReplayPaths(parsed.inputs, parsed.options);
  process.stdout.write(
    parsed.json
      ? `${JSON.stringify(report, null, 2)}\n`
      : `${formatAuditText(report)}\n`,
  );
}

const invokedPath =
  process.argv[1] === undefined
    ? null
    : pathToFileURL(path.resolve(process.argv[1])).href;
if (invokedPath === import.meta.url) {
  main(process.argv.slice(2)).catch((reason) => {
    process.stderr.write(
      `soundtrack director audit: ${errorMessage(reason)}\n`,
    );
    process.exitCode = 1;
  });
}
