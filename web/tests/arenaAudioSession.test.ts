import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { ArenaAudioSession } from '../src/audio/ArenaAudioSession.ts';

const originalAudioContext = globalThis.AudioContext;
const originalNavigator = Object.getOwnPropertyDescriptor(
  globalThis,
  'navigator',
);

test.beforeEach(() => {
  FakeAudioContext.instances = [];
  Object.defineProperty(globalThis, 'AudioContext', {
    configurable: true,
    value: FakeAudioContext,
  });
  Object.defineProperty(globalThis, 'navigator', {
    configurable: true,
    value: { audioSession: { type: 'ambient' } },
  });
});

test.afterEach(() => {
  Object.defineProperty(globalThis, 'AudioContext', {
    configurable: true,
    value: originalAudioContext,
  });
  if (originalNavigator) {
    Object.defineProperty(globalThis, 'navigator', originalNavigator);
  } else {
    delete (globalThis as { navigator?: Navigator }).navigator;
  }
});

test('the arena graph is lazy, shared, and feeds both buses through one limiter', () => {
  const session = new ArenaAudioSession();
  assert.equal(FakeAudioContext.instances.length, 0);

  const graph = session.ensureGraph();
  const context = FakeAudioContext.instances[0]!;
  assert.equal(FakeAudioContext.instances.length, 1);
  assert.equal(
    (globalThis.navigator as Navigator & { audioSession: { type: string } })
      .audioSession.type,
    'playback',
  );
  assert.strictEqual(session.ensureGraph(), graph);
  assert.equal(FakeAudioContext.instances.length, 1);

  assert.strictEqual(context.effects.connections[0], context.limiter);
  assert.strictEqual(context.music.connections[0], context.limiter);
  assert.strictEqual(context.limiter.connections[0], context.destination);
  assert.deepEqual(
    {
      threshold: context.limiter.threshold.value,
      knee: context.limiter.knee.value,
      ratio: context.limiter.ratio.value,
      attack: context.limiter.attack.value,
      release: context.limiter.release.value,
    },
    {
      threshold: -4,
      knee: 3,
      ratio: 14,
      attack: 0.002,
      release: 0.11,
    },
  );
});

test('only the session owner resumes and closes the context', async () => {
  const session = new ArenaAudioSession();
  const graph = await session.resume();
  const context = FakeAudioContext.instances[0]!;

  assert.strictEqual(graph.context, context);
  assert.equal(context.resumeCalls, 1);
  assert.equal(context.state, 'running');

  await session.dispose();
  await session.dispose();
  assert.equal(context.closeCalls, 1);
  assert.equal(context.state, 'closed');
  assert.equal(context.effects.disconnectCalls, 1);
  assert.equal(context.music.disconnectCalls, 1);
  assert.equal(context.limiter.disconnectCalls, 1);
  assert.throws(() => session.ensureGraph(), /disposed/);
});

test('an effect cleanup/setup replay retains the session until the final owner leaves', async () => {
  const session = new ArenaAudioSession();
  const firstRelease = session.retainOwner();
  const graph = session.ensureGraph();
  const context = FakeAudioContext.instances[0]!;

  // React StrictMode performs this cleanup/setup sequence synchronously.
  firstRelease();
  const finalRelease = session.retainOwner();
  await Promise.resolve();

  assert.strictEqual(session.ensureGraph(), graph);
  assert.equal(context.closeCalls, 0);

  finalRelease();
  await Promise.resolve();
  assert.equal(context.closeCalls, 1);
  assert.throws(() => session.ensureGraph(), /disposed/);
});

test('the compatibility audio hook leases its fallback session', async () => {
  const source = await readFile(
    new URL('../src/audio/useReplayAudio.ts', import.meta.url),
    'utf8',
  );
  assert.match(source, /ownsSession \? audioSession\.retainOwner\(\) : undefined/);
  assert.doesNotMatch(source, /ownsSession\).*audioSession\.dispose/);
});

test('live completion keeps the audio owner mounted through the final replay fetch', async () => {
  const [queries, playback] = await Promise.all([
    readFile(new URL('../src/site/queries.ts', import.meta.url), 'utf8'),
    readFile(new URL('../src/playback.ts', import.meta.url), 'utf8'),
  ]);

  assert.match(
    queries,
    /queryKey: \['match', matchId \?\? '', 'replay', complete\]/,
  );
  assert.match(queries, /placeholderData: \(previous\) => previous/);
  assert.match(
    playback,
    /endedNaturally && timeRef\.current < tickCount/,
  );
});

class FakeAudioParam {
  value = 0;
}

class FakeAudioNode {
  readonly connections: FakeAudioNode[] = [];
  disconnectCalls = 0;

  connect<T extends FakeAudioNode>(destination: T): T {
    this.connections.push(destination);
    return destination;
  }

  disconnect(): void {
    this.disconnectCalls += 1;
  }
}

class FakeGainNode extends FakeAudioNode {
  readonly gain = new FakeAudioParam();
}

class FakeCompressorNode extends FakeAudioNode {
  readonly threshold = new FakeAudioParam();
  readonly knee = new FakeAudioParam();
  readonly ratio = new FakeAudioParam();
  readonly attack = new FakeAudioParam();
  readonly release = new FakeAudioParam();
}

class FakeAudioContext {
  static instances: FakeAudioContext[] = [];

  readonly destination = new FakeAudioNode();
  readonly effects = new FakeGainNode();
  readonly music = new FakeGainNode();
  readonly limiter = new FakeCompressorNode();
  state: AudioContextState = 'suspended';
  resumeCalls = 0;
  closeCalls = 0;
  private gainCount = 0;

  constructor(_options: AudioContextOptions) {
    FakeAudioContext.instances.push(this);
  }

  createGain(): GainNode {
    const node = this.gainCount++ === 0 ? this.effects : this.music;
    return node as unknown as GainNode;
  }

  createDynamicsCompressor(): DynamicsCompressorNode {
    return this.limiter as unknown as DynamicsCompressorNode;
  }

  async resume(): Promise<void> {
    this.resumeCalls += 1;
    this.state = 'running';
  }

  async close(): Promise<void> {
    this.closeCalls += 1;
    this.state = 'closed';
  }
}
