import assert from 'node:assert/strict';
import test from 'node:test';
import {
  SoundtrackEngine,
  straightThroughPauseReasonForTransport,
} from '../src/soundtrack/SoundtrackEngine.ts';

const SAMPLE_RATE = 100;
const BAR_FRAMES = 200;
const BAR_SECONDS = BAR_FRAMES / SAMPLE_RATE;
const SECTION_BARS = 4;
const SECTION_SECONDS = SECTION_BARS * BAR_SECONDS;
const STEM_IDS = ['foundation', 'drums', 'guitar'];

test('straight-through transport policy is non-live 1x only', () => {
  const transport = {
    enabled: true,
    followingLive: false,
    playing: true,
    playResolveTail: false,
    playbackSpeed: 1,
  } as const;
  assert.equal(straightThroughPauseReasonForTransport(transport), null);
  assert.equal(
    straightThroughPauseReasonForTransport({
      ...transport,
      playbackSpeed: 2,
    }),
    'rate',
  );
  assert.equal(
    straightThroughPauseReasonForTransport({
      ...transport,
      playing: false,
    }),
    'manual',
  );
  assert.equal(
    straightThroughPauseReasonForTransport({
      ...transport,
      playing: false,
      playResolveTail: true,
    }),
    'result',
  );
  assert.equal(
    straightThroughPauseReasonForTransport({
      ...transport,
      followingLive: true,
      playbackSpeed: 2,
    }),
    null,
  );
  assert.equal(
    straightThroughPauseReasonForTransport({
      ...transport,
      playbackSpeed: 2,
      enabled: false,
    }),
    null,
  );
});

test(
  'a finite mandatory retarget and target-back always retain a successor',
  { concurrency: false },
  async () => {
    const manifest = finiteRoutingManifest();
    await withEngine(manifest, async ({ context, engine }) => {
      await enterFiniteCue(engine, context);
      advanceToDecision(engine, context);
      await flushAsync();

      assertPending(engine, 'tension-loop', true);

      engine.setDirection(direction('combat'));
      await flushAsync();
      assertPending(engine, 'combat-loop', true);

      // The latest state is held until the two-bar phrase gate, leaving time
      // to retarget the two-bar-quantized combat exit safely.
      engine.setDirection(direction('tension'));
      assert.equal(engine.direction.state, 'combat');
      advanceHorizontalCommit(engine, context);
      await flushAsync();
      const restored = assertPending(engine, 'tension-loop', true);

      context.advanceTo(restored.when + restored.crossfadeSeconds + 0.01);
      assert.equal(activeVoice(engine).section.id, 'tension-loop');
      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);
    });
  },
);

test(
  'a terminal direction replaces an in-flight mandatory exit and stale decode cannot win',
  { concurrency: false },
  async () => {
    const manifest = finiteRoutingManifest();
    await withEngine(manifest, async ({ context, engine }) => {
      context.holdDecode('tension-loop');
      await enterFiniteCue(engine, context);
      advanceToDecision(engine, context);
      await flushAsync();

      assert.equal(engine.pending, null);
      assert.equal(engine.loading?.transition.to, 'tension-loop');
      assert.equal(engine.loading?.mandatory, true);

      engine.setDirection(direction('resolve', 0.9, 0.18));
      await flushAsync();
      const terminal = assertPending(engine, 'resolve', true);

      context.releaseDecode('tension-loop');
      await flushAsync();
      assert.equal(engine.pending?.to.section.id, 'resolve');
      assert.equal(engine.loading, null);

      context.advanceTo(terminal.when + terminal.crossfadeSeconds + 0.01);
      assert.equal(activeVoice(engine).section.id, 'resolve');
    });
  },
);

test(
  'an explicit discontinuity cancels a pending rotation and rearms its loop decision',
  { concurrency: false },
  async () => {
    const manifest = rotationManifest();
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('sparse'));
      const entry = activeVoice(engine);
      assert.equal(entry.section.id, 'loop-a');

      context.advanceTo(entry.decisionTimer.stopTime);
      await flushAsync();
      const firstRotation = assertPending(engine, 'loop-b', false);
      assert.equal(
        firstRotation.when,
        entry.startedAt + 2 * entry.durationSeconds,
      );

      engine.resetForDiscontinuity();

      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);
      assert.ok(
        entry.decisionTimer,
        'the canceled rotation must rearm a timer',
      );
      const rearmedAt = entry.decisionTimer.stopTime;
      assert.ok(rearmedAt > context.currentTime);

      context.advanceTo(rearmedAt);
      await flushAsync();
      const rearmedRotation = assertPending(engine, 'loop-b', false);
      assert.equal(
        (rearmedRotation.when - entry.startedAt) % entry.durationSeconds,
        0,
      );
    });
  },
);

test(
  'same-state variety rotation stays on holds instead of entering a finite bridge',
  { concurrency: false },
  async () => {
    await withEngine(rotationManifest(), async ({ context, engine }) => {
      await engine.start(direction('sparse'));
      const entry = activeVoice(engine);

      context.advanceTo(entry.decisionTimer.stopTime);
      await flushAsync();

      assertPending(engine, 'loop-b', false);
    });
  },
);

test(
  'pause ramps only the soundtrack bus while its shared context keeps running',
  { concurrency: false },
  async () => {
    await withEngine(rotationManifest(), async ({ context, engine }) => {
      const pauseGain = engine.pauseGain.gain;

      await engine.setPaused(true);
      assert.equal(lastTargetCall(pauseGain).value, 0);
      assert.equal(context.state, 'running');
      assert.equal(context.suspendCalls, 0);

      await engine.setPaused(false);
      assert.equal(lastTargetCall(pauseGain).value, 1);
      assert.equal(context.state, 'running');
      assert.equal(context.resumeCalls, 0);

      await engine.dispose();
      assert.equal(context.state, 'running');
      assert.equal(context.closeCalls, 0);
    });
  },
);

test(
  'straight-through mode uses the post-decode replay clock and bypasses the adaptive stem graph',
  { concurrency: false },
  async () => {
    const manifest = straightThroughManifest();
    await withEngine(manifest, async ({ context, engine }) => {
      let replaySeconds = 0;
      context.holdDecode('original-mix');
      const starting = engine.start(
        direction('sparse', 0.15, 0.2),
        {
          straightThrough: {
            getReplaySeconds: () => replaySeconds,
          },
        },
      );
      await flushAsync();
      replaySeconds = 3.2;
      context.releaseDecode('original-mix');
      await starting;

      const playback = engine.straightThrough;
      assert.ok(playback);
      const voice = playback.voice;
      assert.equal(engine.active, null);
      assert.equal(voice.sourceOffsetSeconds, 3.5);
      assert.equal(voice.source.startedOffset, 3.5);
      assert.equal(
        voice.durationSeconds,
        manifest.straightThroughCue.durationSeconds,
      );
      assert.equal(
        voice.source.startedDuration,
        manifest.straightThroughCue.durationSeconds -
          voice.sourceOffsetSeconds,
      );
      assert.ok(Math.abs(voice.startedAt - 0.3) < 1e-9);
      assert.equal(
        voice.sourceOffsetSeconds / (BAR_SECONDS / 4),
        Math.round(voice.sourceOffsetSeconds / (BAR_SECONDS / 4)),
        'the premix starts on a source beat',
      );
      assert.equal(
        context.gainNodes.length,
        3,
        'master, pause, and one premix bus are the only gain nodes',
      );
      assert.equal(engine.title, 'Engine Test');

      const fades = voice.bus.gain.calls.filter(
        (call) => call.method === 'setValueCurveAtTime',
      );
      assert.equal(fades.length, 2);
      const [startFade, endFade] = fades;
      assert.equal(startFade.when, voice.startedAt);
      assert.equal(startFade.duration, BAR_SECONDS / 4);
      assert.equal(startFade.curve[0], 0);
      assert.equal(startFade.curve.at(-1), 1);
      assert.equal(endFade.curve[0], 1);
      assert.equal(endFade.curve.at(-1), 0);
      assert.ok(
        Math.abs(
          endFade.when +
            endFade.duration -
            (voice.startedAt +
              voice.durationSeconds -
              voice.sourceOffsetSeconds),
        ) < 1e-9,
        'the premix fades to silence at its natural source end',
      );

      engine.setDirection(direction('climax', 1, 1), [
        trigger('destruction', 12),
      ]);
      await flushAsync();
      assert.equal(engine.receivedTriggerKeys.size, 0);
      assert.equal(engine.stingerArmedUntil, 0);
      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);
      assert.equal(context.bufferSources.length, 1);
      assert.equal(playback.voice, voice);
    });
  },
);

test(
  'one AAC-frame decoder remainder is accepted while authored durations stay authoritative',
  { concurrency: false },
  async () => {
    const straightManifest = straightThroughManifest();
    await withEngine(
      straightManifest,
      async ({ context, engine }) => {
        context.setDecodedFrameDelta('original-mix', 960);
        await engine.start(direction('sparse'), {
          straightThrough: {
            getReplaySeconds: () => 2.1,
          },
        });

        const voice = engine.straightThrough.voice;
        assert.equal(
          voice.source.buffer.length,
          straightManifest.straightThroughCue.durationSeconds * SAMPLE_RATE +
            960,
        );
        assert.equal(
          voice.durationSeconds,
          straightManifest.straightThroughCue.durationSeconds,
        );
        assert.equal(
          voice.source.startedDuration,
          straightManifest.straightThroughCue.durationSeconds -
            voice.sourceOffsetSeconds,
        );
        assert.equal(
          voice.endTimer.stopTime,
          voice.startedAt +
            straightManifest.straightThroughCue.durationSeconds -
            voice.sourceOffsetSeconds +
            0.002,
        );
      },
    );

    const adaptiveManifest = rotationManifest();
    await withEngine(
      adaptiveManifest,
      async ({ context, engine }) => {
        context.setDecodedFrameDelta('loop-a', 960);
        await engine.start(direction('sparse'));

        const voice = activeVoice(engine);
        assert.equal(voice.durationSeconds, SECTION_SECONDS);
        assert.equal(voice.sources[0].buffer.length, 800 + 960);
        assert.equal(voice.sources[0].loopEnd, SECTION_SECONDS);
      },
    );
  },
);

test(
  'short decodes and padding beyond one AAC frame are rejected',
  { concurrency: false },
  async () => {
    for (const frameDelta of [-1, 1025]) {
      const manifest = straightThroughManifest();
      await withEngine(manifest, async ({ context, engine }) => {
        context.setDecodedFrameDelta('original-mix', frameDelta);
        const expectedFrames =
          manifest.straightThroughCue.durationSeconds * SAMPLE_RATE;
        await assert.rejects(
          engine.start(direction('sparse'), {
            straightThrough: {
              getReplaySeconds: () => 0,
            },
          }),
          new RegExp(
            `decoded to ${expectedFrames + frameDelta} frames; expected ${expectedFrames} plus at most 1024`,
          ),
        );
      });
    }
  },
);

test(
  'straight-through pause, resume, and seek rebuild the premix at replay time',
  { concurrency: false },
  async () => {
    await withEngine(
      straightThroughManifest(),
      async ({ context, engine }) => {
        let replaySeconds = 2.1;
        await engine.start(direction('sparse'), {
          straightThrough: {
            getReplaySeconds: () => replaySeconds,
          },
        });
        const first = engine.straightThrough.voice;
        assert.equal(first.sourceOffsetSeconds, 2.5);

        await engine.setPaused(true);
        const pauseFade = lastCurveCall(engine.pauseGain.gain);
        assert.equal(pauseFade.when, context.currentTime);
        assert.equal(pauseFade.duration, BAR_SECONDS / 4);
        assert.equal(pauseFade.curve[0], 1);
        assert.equal(pauseFade.curve.at(-1), 0);
        context.advanceTo(context.currentTime + 5);
        await engine.setPaused(false);
        const resumed = engine.straightThrough.voice;
        assert.notEqual(resumed, first);
        assert.equal(resumed.sourceOffsetSeconds, 2.5);
        assert.equal(first.stopped, true);
        assert.ok(first.source.stoppedAt !== null);

        replaySeconds = 8.1;
        engine.resetForDiscontinuity();
        await flushAsync();
        const sought = engine.straightThrough.voice;
        assert.notEqual(sought, resumed);
        assert.equal(sought.sourceOffsetSeconds, 8.5);
        assert.equal(sought.source.startedOffset, 8.5);
        assert.equal(resumed.stopped, true);
      },
    );
  },
);

test(
  'straight-through startup and paused seeks are hard-muted before any rebuild can start',
  { concurrency: false },
  async () => {
    await withEngine(
      straightThroughManifest(),
      async ({ context, engine }) => {
        let replaySeconds = 2.1;
        let pauseReason: 'manual' | null = 'manual';
        await engine.start(direction('sparse'), {
          straightThrough: {
            getReplaySeconds: () => replaySeconds,
            getPauseReason: () => pauseReason,
          },
        });

        const initial = engine.straightThrough.voice;
        assert.equal(engine.paused, true);
        assert.equal(engine.pauseGain.gain.value, 0);
        assert.equal(
          engine.pauseGain.gain.calls.some(
            (call) =>
              call.method === 'setValueAtTime' && call.value === 0,
          ),
          true,
        );

        replaySeconds = 8.1;
        engine.resetForDiscontinuity();
        await flushAsync();
        assert.equal(
          engine.straightThrough.voice,
          initial,
          'a paused seek defers its source rebuild until playback resumes',
        );
        assert.equal(engine.pauseGain.gain.value, 0);

        pauseReason = null;
        await engine.setPaused(false);
        const resumed = engine.straightThrough.voice;
        assert.notEqual(resumed, initial);
        assert.equal(resumed.sourceOffsetSeconds, 8.5);
      },
    );
  },
);

test(
  'straight-through rate suspension is immediately silent and resyncs at 1x',
  { concurrency: false },
  async () => {
    await withEngine(
      straightThroughManifest(),
      async ({ engine }) => {
        let replaySeconds = 1.1;
        await engine.start(direction('sparse'), {
          straightThrough: {
            getReplaySeconds: () => replaySeconds,
          },
        });
        const original = engine.straightThrough.voice;

        await engine.setPaused(true, 'rate');
        assert.equal(engine.pauseGain.gain.value, 0);
        assert.equal(
          engine.pauseGain.gain.calls.filter(
            (call) => call.method === 'setValueCurveAtTime',
          ).length,
          0,
          'non-1x playback must not leak through a musical fade',
        );

        replaySeconds = 9.1;
        await engine.setPaused(false);
        const resynced = engine.straightThrough.voice;
        assert.notEqual(resynced, original);
        assert.equal(resynced.sourceOffsetSeconds, 9.5);
        assert.equal(original.stopped, true);
      },
    );
  },
);

test(
  'straight-through result holds one beat before a one-beat fade',
  { concurrency: false },
  async () => {
    await withEngine(
      straightThroughManifest(),
      async ({ context, engine }) => {
        await engine.start(direction('sparse'), {
          straightThrough: {
            getReplaySeconds: () => 1,
          },
        });

        await engine.setPaused(true, 'result');
        const resultFade = lastCurveCall(engine.pauseGain.gain);
        assert.equal(resultFade.when, context.currentTime + BAR_SECONDS / 4);
        assert.equal(resultFade.duration, BAR_SECONDS / 4);
        assert.equal(resultFade.curve[0], 1);
        assert.equal(resultFade.curve.at(-1), 0);

        const hardMutesBeforeRateChange =
          engine.pauseGain.gain.calls.filter(
            (call) =>
              call.method === 'setValueAtTime' && call.value === 0,
          ).length;
        await engine.setPaused(true, 'rate');
        assert.equal(
          engine.pauseGain.gain.calls.filter(
            (call) =>
              call.method === 'setValueAtTime' && call.value === 0,
          ).length,
          hardMutesBeforeRateChange + 1,
          'changing a held result to non-1x must become silent immediately',
        );
      },
    );
  },
);

test(
  'straight-through rebuild cancels the superseded long-lived end timer',
  { concurrency: false },
  async () => {
    await withEngine(
      straightThroughManifest(),
      async ({ context, engine }) => {
        let replaySeconds = 1;
        await engine.start(direction('sparse'), {
          straightThrough: {
            getReplaySeconds: () => replaySeconds,
          },
        });
        const original = engine.straightThrough.voice;
        const originalTimer = original.endTimer;
        assert.ok(originalTimer);
        assert.ok(originalTimer.stopTime > context.currentTime + 40);

        replaySeconds = 6;
        engine.resetForDiscontinuity();
        await flushAsync();

        assert.equal(original.endTimer, null);
        assert.ok(
          originalTimer.stopTime <= context.currentTime + 0.001,
          'the timer stop is replaced immediately instead of surviving to cue end',
        );
        context.advanceTo(context.currentTime + 0.002);
        assert.equal(originalTimer.ended, true);
        assert.equal(originalTimer.disconnected, true);
      },
    );
  },
);

test(
  'requesting a straight control fails clearly when a pack has no premix',
  { concurrency: false },
  async () => {
    await withEngine(directStatesManifest(), async ({ engine }) => {
      await assert.rejects(
        engine.start(direction('sparse'), {
          straightThrough: {
            getReplaySeconds: () => 0,
          },
        }),
        /does not provide a straight-through control cue/,
      );
    });
  },
);

test(
  'a retrospective cue uses the post-decode replay clock and reaches its authored peak without horizontal jumps',
  { concurrency: false },
  async () => {
    const manifest = retrospectiveManifest();
    await withEngine(manifest, async ({ context, engine }) => {
      let replaySeconds = 0;
      context.holdDecode('final-runway');
      const starting = engine.start(
        direction('sparse', 0.28, 0.35),
        {
          retrospective: {
            primaryPeakSeconds: 19.2,
            getReplaySeconds: () => replaySeconds,
          },
        },
      );
      await flushAsync();
      replaySeconds = 3;
      context.releaseDecode('final-runway');
      await starting;

      const runway = activeVoice(engine);
      assert.equal(runway.retrospective, true);
      assert.equal(runway.decisionTimer, null);
      assert.equal(runway.sourceOffsetSeconds, 16);
      assert.ok(
        runway.sources.every((source) => source.startedOffset === 16),
      );
      assert.ok(
        Math.abs(
          runway.sourceOffsetSeconds +
            (19.2 - replaySeconds - runway.startedAt) -
            manifest.retrospectiveCue.anchorBar * BAR_SECONDS,
        ) < 1e-9,
        'the source peak marker must land on the replay highlight',
      );

      engine.setDirection(direction('combat', 0.72, 0.8));
      engine.setDirection(direction('climax', 0.9, 0.92));
      await flushAsync();
      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);
      assert.equal(engine.horizontalTimer, null);
      assert.equal(activeVoice(engine), runway);
    });
  },
);

test(
  'retrospective pause, resume, and seek rebuild the cue at the replay-owned source offset',
  { concurrency: false },
  async () => {
    const manifest = retrospectiveManifest();
    await withEngine(manifest, async ({ context, engine }) => {
      let replaySeconds = 0;
      await engine.start(direction('sparse'), {
        retrospective: {
          primaryPeakSeconds: 19.2,
          getReplaySeconds: () => replaySeconds,
        },
      });
      const first = activeVoice(engine);
      assert.equal(first.sourceOffsetSeconds, 13);

      await engine.setPaused(true);
      context.advanceTo(context.currentTime + 5);
      await engine.setPaused(false);
      const resumed = activeVoice(engine);
      assert.notEqual(resumed, first);
      assert.equal(resumed.sourceOffsetSeconds, 13);
      assert.ok(first.sources.every((source) => source.stoppedAt !== null));

      replaySeconds = 8;
      engine.resetForDiscontinuity();
      await flushAsync();
      const sought = activeVoice(engine);
      assert.notEqual(sought, resumed);
      assert.equal(sought.sourceOffsetSeconds, 21);
      assert.ok(
        sought.sources.every((source) => source.startedOffset === 21),
      );
    });
  },
);

test(
  'retrospective resolution holds the landed peak and fades instead of cutting to an outro',
  { concurrency: false },
  async () => {
    await withEngine(retrospectiveManifest(), async ({ context, engine }) => {
      await engine.start(direction('climax', 0.92, 0.2), {
        retrospective: {
          primaryPeakSeconds: 19.2,
          getReplaySeconds: () => 19.2,
        },
      });
      const runway = activeVoice(engine);

      engine.setDirection(direction('resolve', 0.95, 0.2), [
        trigger('destruction', 96),
      ]);

      assert.equal(engine.pending, null);
      const resolveFade = lastCurveCall(runway.bus.gain);
      assert.equal(
        resolveFade.when,
        Math.max(
          context.currentTime + BAR_SECONDS * 0.5,
          runway.startedAt + BAR_SECONDS,
        ),
      );
      assert.equal(resolveFade.duration, BAR_SECONDS * 1.5);
      assert.equal(resolveFade.curve[0], 1);
      assert.equal(resolveFade.curve.at(-1), 0);
      assert.ok(engine.retrospectiveResolveTimer);

      context.advanceTo(
        resolveFade.when + resolveFade.duration + 0.01,
      );
      assert.ok(runway.sources.every((source) => source.stoppedAt !== null));
      assert.equal(
        context.decodedSectionIds.has('resolve'),
        false,
        'the graph outro must not be fetched for a continuous runway',
      );
    });
  },
);

test(
  'a finite resolve cue ends naturally without creating an automatic loop',
  { concurrency: false },
  async () => {
    const manifest = makeManifest({
      entrySection: 'resolve',
      sections: [
        section('resolve', 'resolve', false, { bars: 3, role: 'resolve' }),
      ],
      transitions: [],
    });
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('resolve', 1, 0.2));
      const resolve = activeVoice(engine);
      const sources = [...resolve.sources];

      assert.ok(sources.every((source) => source.loop === false));
      context.advanceTo(
        resolve.startedAt + resolve.durationSeconds + BAR_SECONDS,
      );

      assert.ok(sources.every((source) => source.ended));
      assert.equal(context.bufferSources.length, STEM_IDS.length);
      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);
      assert.equal(resolve.decisionTimer, null);
      assert.equal(activeVoice(engine).section.id, 'resolve');
    });
  },
);

test(
  'a replay restart can follow an authored route from resolve back to gameplay',
  { concurrency: false },
  async () => {
    const manifest = makeManifest({
      entrySection: 'resolve',
      sections: [
        section('resolve', 'resolve', false, { bars: 3, role: 'resolve' }),
        section('entry', 'sparse', true),
      ],
      transitions: [transition('resolve', 'entry', 'section-end')],
    });
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('resolve', 1, 0.2));
      const resolve = activeVoice(engine);
      context.advanceTo(resolve.startedAt + resolve.durationSeconds + 0.01);

      engine.resetForDiscontinuity();
      engine.setDirection(direction('sparse'));
      await flushAsync();

      const restart = assertPending(engine, 'entry', false);
      context.advanceTo(restart.when + restart.crossfadeSeconds + 0.01);
      assert.equal(activeVoice(engine).section.id, 'entry');
    });
  },
);

test(
  'event impulses ease responsive stems promptly, stack by strength, and retain their musical release',
  { concurrency: false },
  async () => {
    const manifest = makeManifest({
      entrySection: 'loop',
      sections: [section('loop', 'sparse', true)],
      transitions: [],
    });
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('sparse', 0.25, 0.25));
      const voice = activeVoice(engine);
      const foundation = voice.stemGains.get('foundation').gain;
      const drums = voice.stemGains.get('drums').gain;

      engine.setDirection(direction('sparse', 0.25, 0.25), [
        trigger('contact', 1),
      ]);
      const contactTarget = lastTargetCall(drums);
      const foundationAfterContact = lastTargetCall(foundation);
      assert.equal(contactTarget.timeConstant, 0.05);
      assert.equal(foundationAfterContact.value, 1);
      assert.equal(engine.direction.state, 'sparse');
      assert.equal(engine.pending, null);

      engine.setDirection(direction('sparse', 0.25, 0.25), [
        trigger('shot', 2),
      ]);
      const shotTarget = lastTargetCall(drums);
      const shotTimer = engine.accentTimer;
      assert.ok(shotTarget.value > contactTarget.value);
      assert.equal(shotTarget.timeConstant, 0.045);

      engine.setDirection(direction('sparse', 0.25, 0.25), [
        trigger('damage', 3),
      ]);
      const damageTarget = lastTargetCall(drums);
      const damageTimer = engine.accentTimer;
      assert.notEqual(damageTimer, shotTimer);
      assert.ok(damageTarget.value > shotTarget.value);
      assert.equal(damageTarget.timeConstant, 0.035);
      assert.equal(lastTargetCall(foundation).value, 1);

      // A duplicate delivery is harmless and does not extend the envelope.
      engine.setDirection(direction('sparse', 0.25, 0.25), [
        trigger('damage', 3),
      ]);
      assert.equal(engine.accentTimer, damageTimer);
      assert.equal(engine.receivedTriggerKeys.size, 3);

      context.advanceTo(shotTimer.stopTime);
      assert.equal(engine.accentTimer, damageTimer);
      assert.equal(damageTimer.disconnected, false);
      assert.equal(engine.accentBoost, 0.34);

      context.advanceTo(damageTimer.stopTime);
      const released = lastTargetCall(drums);
      assert.equal(engine.accentBoost, 0);
      assert.equal(released.timeConstant, 1);
      assert.ok(released.value < damageTarget.value);

      // Direction samples arrive throughout playback. They must preserve the
      // event's release instead of replacing it with the ordinary response on
      // the very next frame.
      engine.setDirection(direction('sparse', 0.25, 0.25));
      assert.equal(lastTargetCall(drums).timeConstant, 1);
      assert.ok(engine.accentReleaseUntil > context.currentTime);

      context.advanceTo(engine.accentReleaseUntil + 0.001);
      engine.setDirection(direction('sparse', 0.25, 0.25));
      assert.equal(lastTargetCall(drums).timeConstant, 1.2);
    });
  },
);

test(
  'ordinary vertical mix changes take a beat to rise and multiple beats to settle',
  { concurrency: false },
  async () => {
    const manifest = makeManifest({
      entrySection: 'loop',
      sections: [section('loop', 'sparse', true)],
      transitions: [],
    });
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('sparse', 0.2, 0.2));
      const drums = activeVoice(engine).stemGains.get('drums').gain;

      engine.setDirection(direction('sparse', 0.8, 0.8));
      const rise = lastTargetCall(drums);
      assert.equal(rise.when, context.currentTime);
      assert.equal(rise.timeConstant, 0.5);
      assert.equal(lastHoldCall(drums).when, context.currentTime);
      assert.equal(rise.timeConstant, BAR_SECONDS / 4);

      engine.setDirection(direction('sparse', 0.3, 0.3));
      const settle = lastTargetCall(drums);
      assert.equal(settle.timeConstant, 1.2);
      assert.ok(settle.value < rise.value);

      // The compatibility path still schedules a continuous target on Web
      // Audio implementations that predate cancelAndHoldAtTime.
      drums.cancelAndHoldAtTime = undefined;
      engine.setDirection(direction('sparse', 0.2, 0.2));
      assert.equal(lastCancelCall(drums).when, context.currentTime);
    });
  },
);

test(
  'horizontal changes retain authored bar timing and equal-power headroom',
  { concurrency: false },
  async () => {
    await withEngine(directStatesManifest(), async ({ context, engine }) => {
      await engine.start(direction('sparse'));
      const entry = activeVoice(engine);

      engine.setDirection(direction('combat'));
      advanceHorizontalCommit(engine, context);
      await flushAsync();

      const pending = assertPending(engine, 'combat-loop', false);
      const elapsedBars =
        (pending.when - entry.startedAt) / BAR_SECONDS;
      assert.ok(
        Math.abs(elapsedBars - Math.round(elapsedBars)) < 1e-9,
        'the destination must still start on its authored quantum',
      );
      assert.equal(pending.crossfadeSeconds, BAR_SECONDS * 0.25);

      const fadeOut = lastCurveCall(entry.bus.gain);
      const fadeIn = lastCurveCall(pending.to.bus.gain);
      assert.equal(fadeOut.when, pending.when);
      assert.equal(fadeIn.when, pending.when);
      assert.equal(fadeOut.duration, pending.crossfadeSeconds);
      assert.equal(fadeIn.duration, pending.crossfadeSeconds);
      assert.equal(fadeIn.curve.length, fadeOut.curve.length);
      for (let index = 0; index < fadeIn.curve.length; index += 1) {
        assert.ok(
          Math.abs(
            fadeIn.curve[index] ** 2 + fadeOut.curve[index] ** 2 - 1,
          ) < 1e-6,
        );
      }
    });
  },
);

test(
  'a staged seam retreats energy stems, makes a short linear handoff, and rises without losing intensity automation',
  { concurrency: false },
  async () => {
    await withEngine(stagedRetargetManifest(), async ({ context, engine }) => {
      await engine.start(direction('sparse', 0.7, 0.7));
      const entry = activeVoice(engine);

      engine.setDirection(direction('combat', 0.78, 0.78));
      advanceHorizontalCommit(engine, context);
      await flushAsync();

      const pending = assertPending(engine, 'combat-loop', false);
      assert.ok(pending.stagedSeam);
      assert.equal(pending.crossfadeSeconds, BAR_SECONDS * 0.25);
      assert.ok(
        Math.abs(
          pending.when - pending.stagedSeam.retreatAt - BAR_SECONDS,
        ) < 1e-9,
      );
      assert.ok(
        Math.abs(
          pending.stagedSeam.settledAt - pending.when - BAR_SECONDS,
        ) < 1e-9,
      );
      assert.equal(
        (pending.when - entry.startedAt) % BAR_SECONDS,
        0,
        'the handoff remains on the soundtrack grid',
      );

      const outgoingBus = lastCurveCall(entry.bus.gain);
      const incomingBus = lastCurveCall(pending.to.bus.gain);
      assert.equal(outgoingBus.when, pending.when);
      assert.equal(incomingBus.when, pending.when);
      assert.equal(outgoingBus.duration, BAR_SECONDS * 0.25);
      for (let index = 0; index < incomingBus.curve.length; index += 1) {
        assert.ok(
          Math.abs(
            incomingBus.curve[index] + outgoingBus.curve[index] - 1,
          ) < 1e-6,
          'linear handoff must not create transition gain overshoot',
        );
      }

      const outgoingDrums = entry.seamGains.get('drums').gain;
      const incomingDrums = pending.to.seamGains.get('drums').gain;
      const retreat = lastCurveCall(outgoingDrums);
      const rise = lastCurveCall(incomingDrums);
      assert.equal(retreat.when, pending.stagedSeam.retreatAt);
      assert.equal(retreat.duration, BAR_SECONDS);
      assert.equal(retreat.curve[0], 1);
      assert.equal(retreat.curve.at(-1), 0);
      assert.equal(rise.when, pending.when);
      assert.equal(rise.duration, BAR_SECONDS);
      assert.equal(rise.curve[0], 0);
      assert.equal(rise.curve.at(-1), 1);
      assert.equal(
        entry.seamGains
          .get('foundation')
          .gain.calls.some((call) => call.method === 'setValueCurveAtTime'),
        false,
        'the tonal anchor stays present until the short bus handoff',
      );

      const seamCallCount = incomingDrums.calls.length;
      engine.setDirection(direction('combat', 0.32, 0.32));
      assert.equal(
        incomingDrums.calls.length,
        seamCallCount,
        'ordinary intensity frames must not cancel the seam envelope',
      );
      assert.equal(
        lastTargetCall(pending.to.stemGains.get('drums').gain).when,
        context.currentTime,
      );

      context.advanceTo(pending.when + 0.01);
      assert.equal(activeVoice(engine).section.id, 'combat-loop');
      assert.equal(engine.transitionLockedUntil, pending.stagedSeam.settledAt);

      engine.setDirection(direction('tension', 0.45, 0.45));
      context.advanceTo(pending.stagedSeam.settledAt - 0.01);
      await flushAsync();
      assert.equal(engine.pending, null, 'retarget stays queued while rising');

      context.advanceTo(pending.stagedSeam.settledAt + 0.005);
      await flushAsync();
      assertPending(engine, 'tension-loop', false);
    });
  },
);

test(
  'canceling during a staged retreat restores energy stems smoothly',
  { concurrency: false },
  async () => {
    await withEngine(stagedRetargetManifest(), async ({ context, engine }) => {
      await engine.start(direction('sparse', 0.75, 0.75));
      const entry = activeVoice(engine);
      engine.setDirection(direction('combat', 0.8, 0.8));
      advanceHorizontalCommit(engine, context);
      await flushAsync();

      const pending = assertPending(engine, 'combat-loop', false);
      assert.ok(pending.stagedSeam);
      context.advanceTo(pending.stagedSeam.retreatAt + BAR_SECONDS * 0.25);
      const seamGain = entry.seamGains.get('drums').gain;
      const setValueCallsBefore = seamGain.calls.filter(
        (call) => call.method === 'setValueAtTime',
      ).length;

      engine.resetForDiscontinuity();

      assert.equal(engine.pending, null);
      const restore = lastTargetCall(seamGain);
      assert.equal(restore.value, 1);
      assert.equal(restore.when, context.currentTime);
      assert.equal(restore.timeConstant, BAR_SECONDS / 8);
      assert.equal(lastHoldCall(seamGain).when, context.currentTime);
      assert.equal(
        seamGain.calls.filter((call) => call.method === 'setValueAtTime')
          .length,
        setValueCallsBefore,
        'restoration must not snap the energy layer back to full gain',
      );
      assert.ok(pending.to.sources.every((source) => source.stoppedAt !== null));
    });
  },
);

test(
  'hold-to-resolve uses the retreat available before the prompt boundary',
  { concurrency: false },
  async () => {
    await withEngine(stagedRetargetManifest(), async ({ context, engine }) => {
      await engine.start(direction('sparse', 0.7, 0.2));
      const entry = activeVoice(engine);
      context.advanceTo(entry.startedAt + BAR_SECONDS * 0.75);

      engine.setDirection(direction('resolve', 0.9, 0.2));
      await flushAsync();

      const pending = assertPending(engine, 'resolve', false);
      assert.ok(pending.stagedSeam);
      assert.ok(
        pending.when - context.currentTime < BAR_SECONDS,
        'resolve must not wait another bar to obtain a full retreat',
      );
      assert.ok(
        pending.stagedSeam.retreatAt >= context.currentTime - 1e-9,
      );
      assert.ok(
        pending.when - pending.stagedSeam.retreatAt < BAR_SECONDS,
      );
      assert.equal(pending.crossfadeSeconds, BAR_SECONDS * 0.25);
    });
  },
);

test(
  'adaptive seam metadata leaves zero-overlap joins on the legacy cut path',
  { concurrency: false },
  async () => {
    const manifest = stagedRetargetManifest();
    manifest.transitions[0].crossfadeBars = 0;
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('sparse'));
      engine.setDirection(direction('combat'));
      advanceHorizontalCommit(engine, context);
      await flushAsync();

      const pending = assertPending(engine, 'combat-loop', false);
      assert.equal(pending.stagedSeam, undefined);
      assert.equal(pending.crossfadeSeconds, 0);
      assert.equal(
        activeVoice(engine)
          .seamGains.get('drums')
          .gain.calls.some((call) => call.method === 'setValueCurveAtTime'),
        false,
      );
    });
  },
);

test(
  'a distinctive stinger requires overtime or destruction, obeys cooldown, and resets for a new presentation segment',
  { concurrency: false },
  async () => {
    const manifest = stingerManifest();
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('climax', 0.8, 0.7));

      engine.setDirection(direction('climax', 0.8, 0.7), [trigger('shot', 1)]);
      await flushAsync();
      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);

      engine.setDirection(direction('climax', 0.8, 0.7), [
        trigger('damage', 2),
      ]);
      await flushAsync();
      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);
      assert.equal(engine.stingerArmedUntil, 0);

      engine.setDirection(direction('climax', 0.8, 0.7), [
        trigger('overtime', 3),
      ]);
      await flushAsync();
      const first = assertPending(engine, 'impact-stinger', false);
      assert.equal(
        engine.stingerCooldownUntil.get('impact-stinger'),
        first.when + 32,
      );
      context.advanceTo(first.when + first.crossfadeSeconds + 0.01);
      assert.equal(activeVoice(engine).section.role, 'stinger');

      await advanceFiniteToSuccessor(engine, context);
      assert.equal(activeVoice(engine).section.id, 'climax-b');

      engine.setDirection(direction('climax', 0.8, 0.7), [
        trigger('destruction', 4),
      ]);
      await flushAsync();
      assert.equal(engine.pending, null, 'cooldown blocks an immediate repeat');

      engine.resetForDiscontinuity();
      assert.equal(engine.stingerCooldownUntil.size, 0);
      engine.setDirection(direction('climax', 0.8, 0.7), [
        trigger('destruction', 5),
      ]);
      await flushAsync();
      assertPending(engine, 'impact-stinger', false);
    });
  },
);

test(
  'a destruction during a matching-state crossfade retries its live stinger arm after unlock',
  { concurrency: false },
  async () => {
    const manifest = stingerAfterTransitionManifest();
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('sparse', 0.45, 0.55));
      engine.setDirection(direction('combat', 0.78, 0.72));
      advanceHorizontalCommit(engine, context);
      await flushAsync();
      const combat = assertPending(engine, 'combat-loop', false);

      context.advanceTo(combat.when + 0.1);
      assert.equal(activeVoice(engine).section.id, 'combat-loop');
      assert.ok(context.currentTime < engine.transitionLockedUntil);

      engine.setDirection(direction('combat', 0.85, 0.72), [
        trigger('destruction', 12),
      ]);
      await flushAsync();
      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);
      assert.ok(engine.stingerArmedUntil > context.currentTime);

      context.advanceTo(combat.when + combat.crossfadeSeconds + 0.005);
      await flushAsync();
      assertPending(engine, 'combat-stinger', false);
      assert.equal(engine.stingerArmedUntil, 0);
    });
  },
);

test(
  'a finite mandatory successor retries decode failure on a bounded audio-clock backoff',
  { concurrency: false },
  async () => {
    const manifest = finiteRetryManifest();
    await withEngine(manifest, async ({ context, engine, errors }) => {
      // The finite voice prefetches once; fail that and the first mandatory
      // attempt, then allow the audio-clock retry to recover.
      context.failNextDecodes('exit-loop', STEM_IDS.length * 2);
      await engine.start(direction('tension'));
      await flushAsync();
      assert.equal(context.decodeCount('exit-loop'), STEM_IDS.length);

      const finite = activeVoice(engine);
      context.advanceTo(finite.decisionTimer.stopTime);
      await flushAsync();

      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);
      assert.ok(finite.decisionTimer, 'failed successor must rearm a retry');
      assert.equal(finite.decisionTimer.stopTime, context.currentTime + 0.5);
      assert.equal(context.decodeCount('exit-loop'), STEM_IDS.length * 2);

      await flushAsync();
      assert.equal(
        context.decodeCount('exit-loop'),
        STEM_IDS.length * 2,
        'microtasks alone must not hammer the failed asset',
      );

      context.advanceTo(finite.decisionTimer.stopTime);
      await flushAsync();
      assertPending(engine, 'exit-loop', true);
      assert.equal(context.decodeCount('exit-loop'), STEM_IDS.length * 3);
      assert.equal(finite.successorRetryAttempts, 0);
      assert.deepEqual(errors, []);
    });
  },
);

test(
  'a permanently broken finite successor stops after four retries and surfaces one fatal error',
  { concurrency: false },
  async () => {
    const manifest = finiteRetryManifest();
    await withEngine(manifest, async ({ context, engine, errors }) => {
      context.failNextDecodes('exit-loop', 100);
      await engine.start(direction('tension'));
      await flushAsync();

      const finite = activeVoice(engine);
      context.advanceTo(finite.decisionTimer.stopTime);
      await flushAsync();

      const expectedBackoffs = [0.5, 1, 2, 4];
      for (const delay of expectedBackoffs) {
        assert.ok(finite.decisionTimer);
        assert.equal(
          finite.decisionTimer.stopTime,
          context.currentTime + delay,
        );
        context.advanceTo(finite.decisionTimer.stopTime);
        await flushAsync();
      }

      assert.equal(finite.decisionTimer, null);
      assert.equal(finite.successorRetryAttempts, 4);
      assert.equal(engine.pending, null);
      assert.equal(engine.loading, null);
      assert.equal(errors.length, 1);
      assert.match(errors[0].message, /simulated decode failure/);
      const attemptsAtFailure = context.decodeCount('exit-loop');

      context.advanceTo(context.currentTime + 20);
      await flushAsync();
      assert.equal(context.decodeCount('exit-loop'), attemptsAtFailure);
    });
  },
);

test(
  'horizontal calls wait two audio bars and coalesce to the latest state',
  { concurrency: false },
  async () => {
    const manifest = directStatesManifest();
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('sparse'));

      engine.setDirection(direction('tension'));
      engine.setDirection(direction('combat'));
      engine.setDirection(direction('pursuit'));

      assert.equal(engine.direction.state, 'sparse');
      assert.deepEqual([...context.decodedSectionIds], ['entry']);
      assert.equal(
        engine.horizontalTimer.stopTime,
        engine.horizontalAnchor + BAR_SECONDS * 2,
      );
      advanceHorizontalCommit(engine, context);
      await flushAsync();
      assert.equal(engine.direction.state, 'pursuit');
      assertPending(engine, 'pursuit-loop', false);
      assert.equal(context.decodedSectionIds.has('tension-loop'), false);
      assert.equal(context.decodedSectionIds.has('combat-loop'), false);

      engine.setDirection(direction('combat'));
      engine.setDirection(direction('tension'));
      assert.equal(engine.direction.state, 'pursuit');
      assert.ok(
        Math.abs(
          engine.horizontalTimer.stopTime -
            context.currentTime -
            BAR_SECONDS * 2,
        ) < 1e-9,
      );
      advanceHorizontalCommit(engine, context);
      await flushAsync();

      assert.equal(engine.direction.state, 'tension');
      assertPending(engine, 'tension-loop', false);
      assert.equal(context.decodedSectionIds.has('combat-loop'), false);
    });
  },
);

test(
  'terminal resolve bypasses the phrase latch but still begins on the next authored quantum',
  { concurrency: false },
  async () => {
    const manifest = directStatesManifest(true);
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('sparse'));
      const entry = activeVoice(engine);
      context.advanceTo(entry.startedAt + BAR_SECONDS * 0.5);

      engine.setDirection(direction('tension'));
      assert.ok(engine.horizontalTimer);
      engine.setDirection(direction('resolve', 0.95, 0.2));
      await flushAsync();

      assert.equal(engine.direction.state, 'resolve');
      assert.equal(engine.horizontalTimer, null);
      const pending = assertPending(engine, 'resolve', false);
      assert.ok(
        pending.when - context.currentTime <= BAR_SECONDS,
        'resolve must not wait through an extra horizontal-latch bar',
      );

      context.advanceTo(pending.when + pending.crossfadeSeconds + 0.01);
      assert.equal(activeVoice(engine).section.id, 'resolve');
    });
  },
);

test(
  'adaptive routing chooses the lowest worst-case bar cost rather than the fewest hops',
  { concurrency: false },
  async () => {
    const manifest = weightedRouteManifest();
    await withEngine(manifest, async ({ engine }) => {
      await engine.start(direction('sparse'));
      engine.resetForDiscontinuity();
      engine.setDirection(direction('combat'));
      await flushAsync();

      assertPending(engine, 'fast-one', false);
    });
  },
);

test(
  'the resolve stem envelope spans the full finite cue and approaches targetIntensity on the audio clock',
  { concurrency: false },
  async () => {
    const manifest = makeManifest({
      entrySection: 'resolve',
      sections: [
        section('resolve', 'resolve', false, { bars: 3, role: 'resolve' }),
      ],
      transitions: [],
    });
    await withEngine(manifest, async ({ context, engine }) => {
      await engine.start(direction('resolve', 1, 0.2));
      const voice = activeVoice(engine);
      const drums = voice.stemGains.get('drums').gain;
      const curveCall = drums.calls.find(
        (call) => call.method === 'setValueCurveAtTime',
      );
      assert.ok(curveCall);
      assert.equal(curveCall.when, voice.startedAt);
      assert.equal(curveCall.duration, 3 * BAR_SECONDS);
      assert.equal(curveCall.curve.length, 64);
      assert.ok(curveCall.curve[0] > curveCall.curve.at(-1));
      assert.ok(Math.abs(curveCall.curve[0] - 1) < 1e-6);
      assert.ok(Math.abs(curveCall.curve.at(-1) - response(0.2)) < 1e-6);

      await engine.setPaused(true);
      engine.setDirection(direction('resolve', 0.7, 0.2));
      assert.equal(
        drums.calls.filter((call) => call.method === 'setValueCurveAtTime')
          .length,
        1,
        'ordinary frame samples must not erase the audio-clock resolve curve',
      );
      assert.equal(lastTargetCall(engine.pauseGain.gain).value, 0);
      assert.equal(context.state, 'running');
    });
  },
);

function finiteRoutingManifest() {
  return withAdaptiveSeam(makeManifest({
    entrySection: 'entry',
    sections: [
      section('entry', 'sparse', true),
      section('finite', 'tension', false, { role: 'bridge' }),
      section('tension-loop', 'tension', true),
      section('combat-loop', 'combat', true),
      section('resolve', 'resolve', false, { bars: 3, role: 'resolve' }),
    ],
    transitions: [
      transition('entry', 'finite', 'next-quantum'),
      transition('finite', 'tension-loop', 'section-end'),
      transition('finite', 'combat-loop', 'next-quantum', {
        quantizeBars: 2,
      }),
      transition('finite', 'resolve', 'next-quantum'),
    ],
  }));
}

function rotationManifest() {
  return makeManifest({
    entrySection: 'loop-a',
    sections: [
      section('loop-a', 'sparse', true),
      section('loop-b', 'sparse', true),
      section('loop-bridge', 'sparse', false, { role: 'bridge' }),
      section('tension-loop', 'tension', true),
    ],
    transitions: [
      transition('loop-a', 'loop-b', 'next-quantum'),
      transition('loop-a', 'loop-bridge', 'next-quantum', { weight: 2 }),
      transition('loop-bridge', 'loop-b', 'section-end'),
      transition('loop-a', 'tension-loop', 'next-quantum'),
    ],
  });
}

function stingerManifest() {
  return withAdaptiveSeam(makeManifest({
    entrySection: 'climax-a',
    sections: [
      section('climax-a', 'climax', true),
      section('impact-stinger', 'climax', false, {
        bars: 1,
        role: 'stinger',
        cooldownSeconds: 32,
      }),
      section('climax-b', 'climax', true),
    ],
    transitions: [
      transition('climax-a', 'impact-stinger', 'next-quantum'),
      transition('impact-stinger', 'climax-b', 'section-end'),
      transition('climax-b', 'impact-stinger', 'next-quantum'),
    ],
  }));
}

function stingerAfterTransitionManifest() {
  return makeManifest({
    entrySection: 'entry',
    sections: [
      section('entry', 'sparse', true),
      section('combat-loop', 'combat', true),
      section('combat-stinger', 'combat', false, {
        bars: 1,
        role: 'stinger',
        cooldownSeconds: 32,
      }),
    ],
    transitions: [
      transition('entry', 'combat-loop', 'next-quantum'),
      transition('combat-loop', 'combat-stinger', 'next-quantum'),
    ],
  });
}

function finiteRetryManifest() {
  return makeManifest({
    entrySection: 'finite',
    sections: [
      section('finite', 'tension', false, { role: 'bridge' }),
      section('exit-loop', 'tension', true),
    ],
    transitions: [transition('finite', 'exit-loop', 'section-end')],
  });
}

function directStatesManifest(includeResolve = false) {
  const sections = [
    section('entry', 'sparse', true),
    section('tension-loop', 'tension', true),
    section('pursuit-loop', 'pursuit', true),
    section('combat-loop', 'combat', true),
  ];
  const transitions = [
    transition('entry', 'tension-loop', 'next-quantum'),
    transition('entry', 'pursuit-loop', 'next-quantum'),
    transition('entry', 'combat-loop', 'next-quantum'),
  ];
  if (includeResolve) {
    sections.push(
      section('resolve', 'resolve', false, { bars: 3, role: 'resolve' }),
    );
    transitions.push(transition('entry', 'resolve', 'next-quantum'));
  }
  return makeManifest({ entrySection: 'entry', sections, transitions });
}

function stagedRetargetManifest() {
  return withAdaptiveSeam(makeManifest({
    entrySection: 'entry',
    sections: [
      section('entry', 'sparse', true),
      section('combat-loop', 'combat', true),
      section('tension-loop', 'tension', true),
      section('resolve', 'resolve', false, { bars: 3, role: 'resolve' }),
    ],
    transitions: [
      transition('entry', 'combat-loop', 'next-quantum', {
        crossfadeBars: 1,
      }),
      transition('entry', 'resolve', 'next-quantum', {
        crossfadeBars: 1,
      }),
      transition('combat-loop', 'tension-loop', 'next-quantum', {
        crossfadeBars: 1,
      }),
    ],
  }));
}

function withAdaptiveSeam(manifest) {
  return {
    ...manifest,
    adaptiveSeam: {
      strategy: 'staged',
      retreatBars: 1,
      overlapBars: 0.25,
      riseBars: 1,
      curve: 'linear',
    },
  };
}

function retrospectiveManifest() {
  return {
    ...directStatesManifest(true),
    retrospectiveCue: {
      id: 'final-runway',
      startBar: 0,
      barCount: 24,
      anchorBar: 16,
      durationSeconds: 24 * BAR_SECONDS,
      files: Object.fromEntries(
        STEM_IDS.map((stemId) => [
          stemId,
          `retrospective-cues/final-runway/${stemId}.m4a`,
        ]),
      ),
    },
  };
}

function straightThroughManifest() {
  return {
    ...directStatesManifest(true),
    straightThroughCue: {
      id: 'original-mix',
      startBar: 0,
      barCount: 24,
      durationSeconds: 24 * BAR_SECONDS,
      file: 'straight-through/original-mix.m4a',
    },
  };
}

function weightedRouteManifest() {
  return makeManifest({
    entrySection: 'entry',
    sections: [
      section('entry', 'sparse', true),
      section('slow-bridge', 'tension', false, {
        bars: 8,
        role: 'bridge',
      }),
      section('fast-one', 'tension', false, { bars: 1, role: 'bridge' }),
      section('fast-two', 'pursuit', false, { bars: 1, role: 'bridge' }),
      section('combat-loop', 'combat', true),
    ],
    transitions: [
      transition('entry', 'slow-bridge', 'next-quantum'),
      transition('slow-bridge', 'combat-loop', 'section-end'),
      transition('entry', 'fast-one', 'next-quantum'),
      transition('fast-one', 'fast-two', 'section-end'),
      transition('fast-two', 'combat-loop', 'section-end'),
    ],
  });
}

function makeManifest({ entrySection, sections, transitions }) {
  return {
    schemaVersion: 1,
    id: 'engine-test',
    title: 'Engine Test',
    provenance: {
      sourceTool: 'test',
      rightsStatus: 'rights-cleared',
      shipApproval: 'approved',
    },
    bpm: 120,
    beatsPerBar: 4,
    sampleRate: SAMPLE_RATE,
    gridOriginFrame: 0,
    barFrames: BAR_FRAMES,
    sourceEndFrame: SECTION_BARS * BAR_FRAMES,
    segmentBars: SECTION_BARS,
    durationSeconds: SECTION_SECONDS,
    masterGainDb: -3,
    adaptiveLatencyBudgetBars: { gameplay: 100, resolve: 100 },
    entrySection,
    stems: [
      {
        id: 'foundation',
        label: 'Foundation',
        role: 'foundation',
        gainDb: 0,
        response: { minimum: 0, full: 0 },
      },
      {
        id: 'drums',
        label: 'Drums',
        role: 'rhythm',
        gainDb: 0,
        response: { minimum: 0, full: 1 },
      },
      {
        id: 'guitar',
        label: 'Guitar',
        role: 'drive',
        gainDb: 0,
        response: { minimum: 0.55, full: 1 },
      },
    ],
    sections,
    transitions,
    assets: {},
  };
}

function section(
  id,
  classification,
  loopable,
  {
    bars = SECTION_BARS,
    role = loopable
      ? 'hold'
      : classification === 'resolve'
        ? 'resolve'
        : 'bridge',
    cooldownSeconds,
  } = {},
) {
  return {
    id,
    label: id,
    classification,
    role,
    startBar: 0,
    barCount: bars,
    durationSeconds: bars * BAR_SECONDS,
    energy: 0.5,
    loopable,
    ...(loopable ? { repeat: { minimumBars: bars * 2 } } : {}),
    ...(cooldownSeconds === undefined ? {} : { cooldownSeconds }),
    files: Object.fromEntries(
      STEM_IDS.map((stemId) => [stemId, `assets/${id}/${stemId}.m4a`]),
    ),
  };
}

function transition(
  from,
  to,
  timing,
  { quantizeBars = 1, crossfadeBars = 0.25, weight = 1 } = {},
) {
  return {
    from,
    to,
    timing,
    quantizeBars,
    crossfadeBars,
    weight,
  };
}

function direction(state, intensity = 0.5, targetIntensity = intensity) {
  return { state, intensity, targetIntensity, momentum: 0 };
}

function trigger(type, sourceTick) {
  return { type, sourceTick };
}

async function withEngine(manifest, run) {
  const originalFetch = globalThis.fetch;
  const durations = new Map(
    manifest.sections.map((candidate) => [
      candidate.id,
      candidate.durationSeconds,
    ]),
  );
  if (manifest.retrospectiveCue) {
    durations.set(
      manifest.retrospectiveCue.id,
      manifest.retrospectiveCue.durationSeconds,
    );
  }
  if (manifest.straightThroughCue) {
    durations.set(
      manifest.straightThroughCue.id,
      manifest.straightThroughCue.durationSeconds,
    );
  }
  const context = new FakeAudioContext(durations);
  globalThis.fetch = async (input) => {
    const url = new URL(String(input));
    const parts = url.pathname.split('/');
    const sectionId =
      manifest.straightThroughCue &&
      url.pathname.endsWith(`/${manifest.straightThroughCue.file}`)
        ? manifest.straightThroughCue.id
        : decodeURIComponent(parts.at(-2));
    const bytes = new TextEncoder().encode(sectionId);
    return {
      ok: true,
      arrayBuffer: async () =>
        bytes.buffer.slice(
          bytes.byteOffset,
          bytes.byteOffset + bytes.byteLength,
        ),
    };
  };
  const loaded = {
    catalog: {
      schemaVersion: 1,
      defaultId: manifest.id,
      tracks: [
        {
          id: manifest.id,
          title: manifest.title,
          manifest: 'manifest.json',
        },
      ],
    },
    entry: {
      id: manifest.id,
      title: manifest.title,
      manifest: 'manifest.json',
    },
    manifest,
    manifestUrl: new URL('https://soundtrack.test/manifest.json'),
  };
  const errors = [];
  const engine = new SoundtrackEngine(loaded, context, (error) => {
    errors.push(error);
  });
  try {
    await run({ context, engine, errors });
  } finally {
    try {
      await engine.dispose();
    } finally {
      globalThis.fetch = originalFetch;
    }
  }
}

async function enterFiniteCue(engine, context) {
  await engine.start(direction('sparse'));
  engine.setDirection(direction('tension'));
  advanceHorizontalCommit(engine, context);
  await flushAsync();
  const transitionToFinite = assertPending(engine, 'finite', false);
  context.advanceTo(
    transitionToFinite.when + transitionToFinite.crossfadeSeconds + 0.01,
  );
  assert.equal(activeVoice(engine).section.id, 'finite');
}

async function advanceFiniteToSuccessor(engine, context) {
  const finite = activeVoice(engine);
  assert.ok(finite.decisionTimer);
  context.advanceTo(finite.decisionTimer.stopTime);
  await flushAsync();
  const successor = engine.pending;
  assert.ok(successor);
  context.advanceTo(successor.when + successor.crossfadeSeconds + 0.01);
  await flushAsync();
}

function advanceToDecision(engine, context) {
  const voice = activeVoice(engine);
  assert.ok(voice.decisionTimer);
  context.advanceTo(voice.decisionTimer.stopTime);
}

function advanceHorizontalCommit(engine, context) {
  const timer = engine.horizontalTimer;
  assert.ok(timer, 'expected a queued horizontal commit');
  context.advanceTo(timer.stopTime);
}

function activeVoice(engine) {
  assert.ok(engine.active, 'expected an active soundtrack voice');
  return engine.active;
}

function assertPending(engine, destination, mandatory) {
  assert.ok(engine.pending, `expected a pending transition to ${destination}`);
  assert.equal(engine.pending.to.section.id, destination);
  assert.equal(engine.pending.mandatory, mandatory);
  return engine.pending;
}

function lastTargetCall(parameter) {
  const calls = parameter.calls.filter(
    (candidate) => candidate.method === 'setTargetAtTime',
  );
  assert.ok(calls.length > 0, 'expected setTargetAtTime automation');
  return calls.at(-1);
}

function lastCurveCall(parameter) {
  const calls = parameter.calls.filter(
    (candidate) => candidate.method === 'setValueCurveAtTime',
  );
  assert.ok(calls.length > 0, 'expected setValueCurveAtTime automation');
  return calls.at(-1);
}

function lastHoldCall(parameter) {
  const calls = parameter.calls.filter(
    (candidate) => candidate.method === 'cancelAndHoldAtTime',
  );
  assert.ok(calls.length > 0, 'expected cancelAndHoldAtTime automation');
  return calls.at(-1);
}

function lastCancelCall(parameter) {
  const calls = parameter.calls.filter(
    (candidate) => candidate.method === 'cancelScheduledValues',
  );
  assert.ok(calls.length > 0, 'expected cancelScheduledValues automation');
  return calls.at(-1);
}

function response(intensity) {
  return intensity * intensity * (3 - 2 * intensity);
}

async function flushAsync() {
  for (let turn = 0; turn < 12; turn += 1) {
    await Promise.resolve();
  }
}

class FakeAudioContext {
  constructor(durations) {
    this.currentTime = 0;
    this.state = 'running';
    this.destination = {};
    this.bufferSources = [];
    this.gainNodes = [];
    this.events = [];
    this.nextEventId = 0;
    this.decodeGates = new Map();
    this.decodeFailures = new Map();
    this.decodeFrameDeltas = new Map();
    this.decodeCalls = [];
    this.decodedSectionIds = new Set();
    this.durations = durations;
    this.suspendGate = null;
    this.resumeGate = null;
    this.suspendCalls = 0;
    this.resumeCalls = 0;
    this.closeCalls = 0;
  }

  createDynamicsCompressor() {
    return connectable({
      threshold: audioParam(),
      knee: audioParam(),
      ratio: audioParam(),
      attack: audioParam(),
      release: audioParam(),
    });
  }

  createGain() {
    const gain = connectable({ gain: audioParam() });
    this.gainNodes.push(gain);
    return gain;
  }

  createBufferSource() {
    const source = connectable({
      buffer: null,
      loop: false,
      loopEnd: 0,
      startedAt: null,
      startedOffset: 0,
      startedDuration: null,
      stoppedAt: null,
      ended: false,
      start: (when, offset = 0, duration = null) => {
        source.startedAt = when;
        source.startedOffset = offset;
        source.startedDuration = duration;
      },
      stop: (when) => {
        source.stoppedAt = when;
      },
    });
    this.bufferSources.push(source);
    return source;
  }

  createConstantSource() {
    let stopRevision = 0;
    const timer = connectable({
      offset: audioParam(),
      onended: null,
      stopTime: null,
      ended: false,
      start: () => {},
      stop: (when) => {
        const revision = ++stopRevision;
        timer.stopTime = when;
        this.schedule(when, () => {
          if (revision !== stopRevision) return;
          timer.ended = true;
          timer.onended?.();
        });
      },
    });
    return timer;
  }

  async decodeAudioData(bytes) {
    const sectionId = new TextDecoder().decode(bytes);
    this.decodedSectionIds.add(sectionId);
    this.decodeCalls.push(sectionId);
    const failures = this.decodeFailures.get(sectionId) ?? 0;
    if (failures > 0) {
      this.decodeFailures.set(sectionId, failures - 1);
      throw new Error(`simulated decode failure for ${sectionId}`);
    }
    const gate = this.decodeGates.get(sectionId);
    if (gate) {
      await gate.promise;
      if (this.decodeGates.get(sectionId) === gate) {
        this.decodeGates.delete(sectionId);
      }
    }
    const duration = this.durations.get(sectionId);
    assert.ok(duration, `missing fake duration for ${sectionId}`);
    const expectedFrames = Math.round(duration * SAMPLE_RATE);
    const length =
      expectedFrames + (this.decodeFrameDeltas.get(sectionId) ?? 0);
    return {
      length,
      sampleRate: SAMPLE_RATE,
      duration: length / SAMPLE_RATE,
    };
  }

  setDecodedFrameDelta(sectionId, frames) {
    this.decodeFrameDeltas.set(sectionId, frames);
  }

  failNextDecodes(sectionId, count) {
    this.decodeFailures.set(sectionId, count);
  }

  decodeCount(sectionId) {
    return this.decodeCalls.filter((candidate) => candidate === sectionId)
      .length;
  }

  holdDecode(sectionId) {
    assert.equal(this.decodeGates.has(sectionId), false);
    this.decodeGates.set(sectionId, deferred());
  }

  releaseDecode(sectionId) {
    const gate = this.decodeGates.get(sectionId);
    assert.ok(gate, `no held decode for ${sectionId}`);
    gate.resolve();
  }

  holdNextSuspend() {
    assert.equal(this.suspendGate, null);
    this.suspendGate = deferred();
  }

  releaseSuspend() {
    assert.ok(this.suspendGate);
    this.suspendGate.resolve();
  }

  holdNextResume() {
    assert.equal(this.resumeGate, null);
    this.resumeGate = deferred();
  }

  releaseResume() {
    assert.ok(this.resumeGate);
    this.resumeGate.resolve();
  }

  async suspend() {
    this.suspendCalls += 1;
    const gate = this.suspendGate;
    if (gate) {
      await gate.promise;
      if (this.suspendGate === gate) this.suspendGate = null;
    }
    if (this.state !== 'closed') this.state = 'suspended';
  }

  async resume() {
    this.resumeCalls += 1;
    const gate = this.resumeGate;
    if (gate) {
      await gate.promise;
      if (this.resumeGate === gate) this.resumeGate = null;
    }
    if (this.state !== 'closed') this.state = 'running';
  }

  async close() {
    this.closeCalls += 1;
    this.state = 'closed';
  }

  schedule(when, callback) {
    this.events.push({
      at: Math.max(this.currentTime, when),
      callback,
      id: this.nextEventId,
    });
    this.nextEventId += 1;
  }

  advanceTo(target) {
    assert.equal(this.state, 'running');
    assert.ok(target >= this.currentTime);
    while (true) {
      this.events.sort(
        (left, right) => left.at - right.at || left.id - right.id,
      );
      const event = this.events[0];
      if (!event || event.at > target) break;
      this.events.shift();
      this.currentTime = event.at;
      event.callback();
    }
    this.currentTime = target;
    for (const source of this.bufferSources) {
      if (source.startedAt === null || source.ended) continue;
      const naturalEnd =
        source.buffer === null
          ? Number.POSITIVE_INFINITY
          : source.startedAt +
            Math.max(
              0,
              source.startedDuration ??
                source.buffer.duration - source.startedOffset,
            );
      if (
        (source.stoppedAt !== null && source.stoppedAt <= target) ||
        (!source.loop && naturalEnd <= target)
      ) {
        source.ended = true;
      }
    }
  }
}

function audioParam() {
  return {
    value: 1,
    calls: [],
    cancelAndHoldAtTime(when) {
      this.calls.push({ method: 'cancelAndHoldAtTime', when });
    },
    cancelScheduledValues(when) {
      this.calls.push({ method: 'cancelScheduledValues', when });
    },
    setTargetAtTime(value, when, timeConstant) {
      this.value = value;
      this.calls.push({
        method: 'setTargetAtTime',
        value,
        when,
        timeConstant,
      });
    },
    setValueAtTime(value, when) {
      this.value = value;
      this.calls.push({ method: 'setValueAtTime', value, when });
    },
    setValueCurveAtTime(curve, when, duration) {
      const stored = Float32Array.from(curve);
      this.value = stored.at(-1);
      this.calls.push({
        method: 'setValueCurveAtTime',
        curve: stored,
        when,
        duration,
      });
    },
  };
}

function connectable(properties) {
  return {
    connected: false,
    disconnected: false,
    connections: [],
    connect(target) {
      this.connected = true;
      this.connections.push(target);
      return target;
    },
    disconnect() {
      this.disconnected = true;
    },
    ...properties,
  };
}

function deferred() {
  let resolve;
  const promise = new Promise((complete) => {
    resolve = complete;
  });
  return { promise, resolve };
}
