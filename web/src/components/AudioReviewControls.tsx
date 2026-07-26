import { useState } from 'react';
import clsx from 'clsx';
import { audioCandidates } from '../audio/audioCandidates';
import type { ReplayAudioController } from '../audio/useReplayAudio';

export default function AudioReviewControls({
  audio,
  onRestart,
}: {
  audio: ReplayAudioController;
  onRestart?: () => void;
}) {
  const [arming, setArming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const arm = async () => {
    setArming(true);
    setError(null);
    try {
      await audio.enable();
      onRestart?.();
    } catch {
      setError('Audio could not start in this browser.');
    } finally {
      setArming(false);
    }
  };

  return (
    <section
      className="rounded-lg border border-arena-edge bg-arena-panel p-3"
      aria-label="Audio candidate review"
    >
      <div className="flex flex-wrap items-center gap-2">
        <div className="mr-1">
          <p className="font-mono text-[9px] tracking-[0.18em] text-arena-accent">
            AUDIO REVIEW
          </p>
          <p className="text-xs text-arena-dim">
            Same replay · four sound directions
          </p>
        </div>

        {audioCandidates.map((candidate) => (
          <button
            key={candidate.id}
            type="button"
            onClick={() => {
              audio.setCandidate(candidate.id);
              if (audio.enabled) onRestart?.();
            }}
            className={clsx(
              'min-w-24 rounded border px-3 py-2 text-left transition-colors',
              audio.candidateId === candidate.id
                ? 'border-arena-accent bg-arena-accent/10 text-arena-text'
                : 'border-arena-edge text-arena-dim hover:border-arena-dim hover:text-arena-text',
            )}
            aria-pressed={audio.candidateId === candidate.id}
          >
            <span className="block font-mono text-[9px] text-arena-accent">
              {candidate.number}
            </span>
            <strong className="block text-xs">{candidate.label}</strong>
          </button>
        ))}

        <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
          {!audio.enabled ? (
            <button
              type="button"
              onClick={() => void arm()}
              disabled={arming}
              className="rounded border border-arena-accent bg-arena-accent/15 px-3 py-2 font-mono text-xs text-arena-accent transition-colors hover:bg-arena-accent/25 disabled:opacity-50"
            >
              {arming ? 'LOADING…' : '▶ ENABLE AUDIO'}
            </button>
          ) : (
            <>
              <button
                type="button"
                onClick={() => audio.setMuted(!audio.muted)}
                className="rounded border border-arena-edge px-3 py-2 font-mono text-xs text-arena-text hover:bg-arena-edge"
              >
                {audio.muted ? 'UNMUTE' : 'MUTE'}
              </button>
              <label className="flex items-center gap-2 font-mono text-[9px] text-arena-dim">
                VOL
                <input
                  type="range"
                  min={0}
                  max={1}
                  step={0.01}
                  value={audio.volume}
                  onChange={(event) =>
                    audio.setVolume(Number(event.currentTarget.value))
                  }
                  className="w-20 accent-arena-accent"
                  aria-label="Audio volume"
                />
                {Math.round(audio.volume * 100)}%
              </label>
              <button
                type="button"
                onClick={() => void audio.previewUnlock()}
                className="rounded border border-arena-edge px-3 py-2 font-mono text-[10px] text-arena-dim hover:border-arena-accent hover:text-arena-accent"
              >
                TEST UNLOCK
              </button>
            </>
          )}
        </div>
      </div>

      <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 border-t border-arena-edge pt-2 font-mono text-[9px] tracking-wide text-arena-dim">
        <span>SHOT → PROJECTILE</span>
        <span>DAMAGE → IMPACT</span>
        <span>DESTROYED → DESTRUCTION</span>
        <span>MATCH END → UNLOCK · REVIEW ONLY</span>
        {audio.enabled && (
          <span className="ml-auto text-arena-accent">
            {audio.suspendedForSpeed
              ? 'AUDIO PAUSED ABOVE 2×'
              : 'ACTIVE · CHANGING PACK RESTARTS THE REPLAY'}
          </span>
        )}
        {error && <span className="ml-auto text-red-400">{error}</span>}
      </div>
    </section>
  );
}
