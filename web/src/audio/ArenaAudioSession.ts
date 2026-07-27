/**
 * The one Web Audio context owned by a full viewer.
 *
 * Effects and music retain independent mix controls, but meet at one final
 * limiter. The graph is deliberately lazy: constructing a viewer must not
 * create an AudioContext or touch the browser's audio session before a trusted
 * user interaction.
 */
export interface ArenaAudioGraph {
  context: AudioContext;
  effects: GainNode;
  music: GainNode;
}

interface OwnedArenaAudioGraph extends ArenaAudioGraph {
  limiter: DynamicsCompressorNode;
}

export class ArenaAudioSession {
  private graph: OwnedArenaAudioGraph | null = null;
  private disposed = false;
  private ownerCount = 0;

  /**
   * Retain the session for one owner effect.
   *
   * React StrictMode deliberately runs an effect's setup, cleanup, and setup
   * again in development. Releasing on a microtask lets that replacement setup
   * retain the same lazy session before the old lease can close it. A real
   * unmount has no replacement owner, so the final release still disposes the
   * graph immediately after the cleanup pass.
   */
  retainOwner(): () => void {
    if (this.disposed) {
      throw new Error('The arena audio session has been disposed.');
    }
    this.ownerCount += 1;
    let released = false;
    return () => {
      if (released) return;
      released = true;
      queueMicrotask(() => {
        if (this.disposed) return;
        this.ownerCount = Math.max(0, this.ownerCount - 1);
        if (this.ownerCount === 0) void this.dispose();
      });
    };
  }

  /**
   * Create the shared graph synchronously. Call this from the user gesture that
   * unlocks either effects or music, before awaiting asset downloads.
   */
  ensureGraph(): ArenaAudioGraph {
    if (this.disposed) {
      throw new Error('The arena audio session has been disposed.');
    }
    if (this.graph) return this.graph;

    configurePlaybackSession();
    const context = new AudioContext({ latencyHint: 'interactive' });
    const effects = context.createGain();
    const music = context.createGain();
    const limiter = context.createDynamicsCompressor();

    // Preserve the effects mix's existing safety limiter as the final arbiter
    // for the combined effects + score signal.
    limiter.threshold.value = -4;
    limiter.knee.value = 3;
    limiter.ratio.value = 14;
    limiter.attack.value = 0.002;
    limiter.release.value = 0.11;

    effects.connect(limiter);
    music.connect(limiter);
    limiter.connect(context.destination);

    this.graph = { context, effects, music, limiter };
    return this.graph;
  }

  /**
   * Resume the shared context without transferring ownership to the caller.
   */
  async resume(): Promise<ArenaAudioGraph> {
    const graph = this.ensureGraph();
    await graph.context.resume();
    return graph;
  }

  /**
   * Only the viewer that created the session calls this. Effects and soundtrack
   * hooks disconnect their own nodes, but never close the shared context.
   */
  async dispose(): Promise<void> {
    if (this.disposed) return;
    this.disposed = true;
    this.ownerCount = 0;
    const graph = this.graph;
    this.graph = null;
    if (!graph) return;

    graph.effects.disconnect();
    graph.music.disconnect();
    graph.limiter.disconnect();
    if (graph.context.state !== 'closed') await graph.context.close();
  }
}

function configurePlaybackSession(): void {
  // iOS normally subjects Web Audio to the ring/silent switch. A match viewer
  // is media playback, so opt into that session where WebKit exposes it.
  try {
    if (typeof navigator === 'undefined') return;
    const session = (
      navigator as Navigator & { audioSession?: { type: string } }
    ).audioSession;
    if (session) session.type = 'playback';
  } catch {
    // Best effort. Failure here must not take down ordinary Web Audio.
  }
}
