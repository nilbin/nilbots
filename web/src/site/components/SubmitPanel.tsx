import { useState } from 'react';
import { api, type BotDetail } from '../api';

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

export default function SubmitPanel({ bot, onSubmitted }: { bot: BotDetail; onSubmitted: () => Promise<unknown> }) {
  const latest = bot.versions[0];
  const [entryType, setEntryType] = useState(latest?.entryType ?? 'MyBot');
  const [source, setSource] = useState(latest?.sources?.[0]?.content ?? STARTER_SOURCE);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const building = bot.versions.some((v) => v.status === 'Pending' || v.status === 'Building');

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      await api.post(`/api/bots/${bot.id}/versions`, {
        entryType,
        files: [{ name: 'Bot.cs', content: source }],
      });
      await onSubmitted();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Submission failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="rounded-xl border border-arena-edge bg-arena-panel p-5">
      <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">
        SUBMIT NEW VERSION
      </h2>
      <p className="mb-3 text-xs text-arena-dim">
        Paste your bot's C# source. The server compiles it to WebAssembly with the
        official toolchain and runs a validation match; the newest successful build
        becomes your active version. Use <code className="font-mono">context.Random</code>{' '}
        for randomness — system clocks and <code className="font-mono">System.Random</code>{' '}
        are neutralized in the sandbox.
      </p>
      <label className="mb-3 flex items-center gap-2 text-xs text-arena-dim">
        Entry class
        <input
          value={entryType}
          onChange={(e) => setEntryType(e.target.value)}
          className="rounded-md border border-arena-edge bg-arena-bg px-2 py-1 font-mono text-sm text-arena-text outline-none focus:border-arena-accent"
        />
      </label>
      <textarea
        value={source}
        onChange={(e) => setSource(e.target.value)}
        spellCheck={false}
        rows={18}
        className="w-full rounded-md border border-arena-edge bg-arena-bg p-3 font-mono text-xs text-arena-text outline-none focus:border-arena-accent"
      />
      {error && <p className="mt-2 text-sm text-red-400">{error}</p>}
      <button
        onClick={() => void submit()}
        disabled={busy || building}
        className="mt-3 rounded-md bg-arena-accent px-4 py-2 text-sm font-semibold text-slate-950 disabled:opacity-50"
      >
        {building ? 'Build in progress…' : busy ? 'Submitting…' : 'Submit & build'}
      </button>
    </section>
  );
}
