import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { botLook, botLookOptions } from '../../render/arenaThemes';
import { api, type MyBot } from '../api';
import { useAuth } from '../auth';

/// The player dashboard: my bots + create a new one.
function CliAccess() {
  return (
    <section className="max-w-xl rounded-xl border border-arena-edge bg-arena-panel p-5">
      <h2 className="mb-2 font-mono text-xs tracking-widest text-arena-dim">CLI ACCESS</h2>
      <p className="text-xs text-arena-dim">
        Develop locally and submit from your terminal:{' '}
        <code className="font-mono">nilbots register</code> opens this site in your browser to
        create an account and sign you in securely (OAuth + PKCE), then{' '}
        <code className="font-mono">nilbots submit</code> creates your bot and uploads it for the official
        server build and reports whether your local artifact matches it bit-for-bit.
      </p>
    </section>
  );
}

const looks = botLookOptions();

export default function GaragePage() {
  const { user, loading } = useAuth();
  const [bots, setBots] = useState<MyBot[] | null>(null);
  const [name, setName] = useState('');
  const [accent, setAccent] = useState('#22d3ee');
  const [lookId, setLookId] = useState('vanguard');
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    if (user) void api.get<MyBot[]>('/api/bots/mine').then(setBots);
  }, [user]);

  if (loading) return <p className="text-sm text-arena-dim">Loading…</p>;
  if (!user) {
    navigate('/login');
    return null;
  }

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);
    try {
      const bot = await api.post<{ id: string }>('/api/bots', {
        name,
        accent,
        lookId,
      });
      navigate(`/bots/${bot.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to create bot.');
    }
  };

  return (
    <div className="flex flex-col gap-8">
      <section>
        <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">MY BOTS</h2>
        {bots === null ? (
          <p className="text-sm text-arena-dim">Loading…</p>
        ) : bots.length === 0 ? (
          <p className="text-sm text-arena-dim">
            No bots yet — build your first one below.
          </p>
        ) : (
          <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            {bots.map((bot) => {
              const look = botLook(bot.lookId);
              return (
                <li key={bot.id}>
                  <Link
                    to={`/bots/${bot.slug}`}
                    className="flex items-center gap-3 rounded-lg border border-arena-edge bg-arena-panel/60 p-4 transition-colors hover:border-arena-dim"
                  >
                    <img src={look.imageUrl} alt="" className="size-9 object-contain" />
                    <span>
                      <span className="block font-semibold">{bot.name}</span>
                      <span className="block font-mono text-[10px] text-arena-dim">
                        {look.label}
                      </span>
                    </span>
                    <span className="ml-auto font-mono text-[11px] text-arena-dim">
                      {bot.latestVersion
                        ? `v${bot.latestVersion.versionNumber} ${bot.latestVersion.status.toLowerCase()}`
                        : 'no versions'}
                    </span>
                  </Link>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      <CliAccess />

      <section className="max-w-md rounded-xl border border-arena-edge bg-arena-panel p-5">
        <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">NEW BOT</h2>
        <form onSubmit={create} className="flex flex-col gap-3">
          <label className="flex flex-col gap-1 text-xs text-arena-dim">
            Name
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              minLength={2}
              maxLength={40}
              placeholder="Murder Roomba"
              className="rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm text-arena-text outline-none focus:border-arena-accent"
            />
          </label>
          <label className="flex items-center gap-3 text-xs text-arena-dim">
            Accent color
            <input
              type="color"
              value={accent}
              onChange={(e) => setAccent(e.target.value)}
              className="h-8 w-14 cursor-pointer rounded border border-arena-edge bg-arena-bg"
            />
            <span className="font-mono">{accent}</span>
          </label>
          <label className="flex flex-col gap-1 text-xs text-arena-dim">
            Chassis
            <select
              value={lookId}
              onChange={(event) => setLookId(event.target.value)}
              className="rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm text-arena-text outline-none focus:border-arena-accent"
            >
              {looks.map((look) => (
                <option key={look.id} value={look.id}>
                  {look.label}
                </option>
              ))}
            </select>
          </label>
          {error && <p className="text-sm text-red-400">{error}</p>}
          <button
            type="submit"
            className="mt-1 self-start rounded-md bg-arena-accent px-4 py-2 text-sm font-semibold text-slate-950"
          >
            Create bot
          </button>
        </form>
      </section>
    </div>
  );
}
