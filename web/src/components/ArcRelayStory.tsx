import { useMemo } from 'react';
import clsx from 'clsx';
import type { ReplayModel } from '../replayModel';
import { createPresenter } from '../replayPresentation';
import { playerAccent } from '../presentation/playerAccent';
import { styleVariables } from '../presentation/styleVariables';

/** Arc Relay's broadcast layer: the current objective sentence and its big beats. */
export default function ArcRelayStory({
  replay,
  tick,
}: {
  replay: ReplayModel;
  tick: number;
}) {
  const presenter = useMemo(() => createPresenter(replay), [replay]);
  const story = presenter.at(tick).arcRelay;
  if (!story) return null;

  const cueAccent = story.cue.accent
    ? playerAccent(story.cue.accent, 'panel')
    : null;
  const beatAccent = story.beat?.accent
    ? playerAccent(story.beat.accent, 'panel')
    : null;

  return (
    <div className="pointer-events-none absolute inset-x-3 top-3 z-10 flex flex-col items-center gap-2">
      <div
        className={clsx(
          'max-w-[min(88%,620px)] rounded-[3px] border bg-arena-bg/88 px-3 py-2 text-center backdrop-blur-[4px]',
          cueAccent ? 'player-accent-border' : 'border-arena-edge',
        )}
        style={
          cueAccent
            ? styleVariables({ '--player-accent': cueAccent })
            : undefined
        }
      >
        <p className="lab">What matters now</p>
        <p
          className={clsx(
            'type-display mt-0.5 text-[14px] leading-tight tracking-[0.08em]',
            cueAccent ? 'player-accent-text' : 'text-arena-text',
          )}
        >
          {story.cue.headline}
        </p>
        <p className="t-micro mt-0.5 text-arena-dim">{story.cue.detail}</p>
      </div>

      {story.beat && (
        <div
          role="status"
          aria-live="polite"
          className={clsx(
            'rounded-[3px] border-2 bg-arena-bg/94 px-5 py-2.5 text-center backdrop-blur-[5px]',
            beatAccent ? 'player-accent-border' : 'border-arena-text',
          )}
          style={
            beatAccent
              ? {
                  ...styleVariables({ '--player-accent': beatAccent }),
                  opacity: Math.max(0.35, story.beat.strength),
                }
              : { opacity: Math.max(0.35, story.beat.strength) }
          }
        >
          <p
            className={clsx(
              'type-display text-[20px] tracking-[0.14em]',
              beatAccent ? 'player-accent-text' : 'text-arena-text',
            )}
          >
            {story.beat.headline}
          </p>
          <p className="t-body mt-0.5 text-arena-text">
            {story.beat.detail}
          </p>
        </div>
      )}
    </div>
  );
}
