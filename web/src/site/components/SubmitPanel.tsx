import { useState } from 'react';
import { type BotDetail } from '../api';
import { useSubmitVersion } from '../queries';
import { errorMessage } from '../errorMessage';

const STARTER_SOURCE = `using BotArena.Sdk;

public sealed class MyBot : IBot
{
    public BotAction Tick(BotContext context)
    {
        var enemy = context.VisibleEnemies.Count > 0 ? context.VisibleEnemies[0] : null;
        if (enemy is not null)
        {
            bool aligned = context.Position.X == enemy.Position.X
                        || context.Position.Y == enemy.Position.Y;
            if (aligned && context.CanShoot)
            {
                context.Debug.Write("Firing at {0}", enemy.Position);
                return Actions.Shoot();
            }
        }
        if (context.PreviousActionResult == ActionResult.Blocked || context.IsWallAhead())
            return context.Random.NextBool() ? Actions.TurnLeft() : Actions.TurnRight();
        return Actions.MoveForward();
    }
}
`;

// No `onSubmitted` callback: the mutation invalidates the bot query itself, which both
// refreshes this page and starts `useBot`'s build polling. Threading a refetch down from
// the page was the caller having to remember what the write invalidated.
export default function SubmitPanel({ bot, botKey }: { bot: BotDetail; botKey: string }) {
  const latest = bot.versions[0];
  const [entryType, setEntryType] = useState(latest?.entryType ?? 'MyBot');
  const [source, setSource] = useState(latest?.sources?.[0]?.content ?? STARTER_SOURCE);
  const building = bot.versions.some((v) => v.status === 'Pending' || v.status === 'Building');
  const submission = useSubmitVersion(botKey, bot.id);

  const submit = () =>
    submission.mutate({ entryType, files: [{ name: 'Bot.cs', content: source }] });

  return (
    <section id="submit" className="panel pad scroll-mt-4">
      <h2 className="lab mb-2">Submit new version</h2>
      <p className="t-meta mb-3">
        Paste your bot's C# source. The server compiles it to WebAssembly with the
        official toolchain and runs a validation match; the newest successful build
        becomes your active version. Use <code className="val">context.Random</code>{' '}
        for randomness — system clocks and <code className="val">System.Random</code>{' '}
        are neutralized in the sandbox.
      </p>
      <label className="t-meta mb-3 flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-2">
        Entry class
        <input
          value={entryType}
          onChange={(e) => setEntryType(e.target.value)}
          className="field min-w-0 font-mono sm:w-48"
        />
      </label>
      <label className="flex flex-col gap-1.5">
        <span className="lab">Source</span>
        <textarea
          value={source}
          onChange={(e) => setSource(e.target.value)}
          spellCheck={false}
          rows={18}
          className="field term w-full resize-y text-arena-text"
        />
      </label>
      {submission.isError && (
        <p className="t-body mt-2 text-arena-hot">
          {errorMessage(submission.error, 'Submission failed.')}
        </p>
      )}
      <button
        type="button"
        onClick={submit}
        disabled={submission.isPending || building}
        className="btn btn-strong mt-3 min-h-11 disabled:opacity-50"
      >
        {building
          ? 'Build in progress…'
          : submission.isPending
            ? 'Submitting…'
            : 'Submit & build'}
      </button>
    </section>
  );
}
