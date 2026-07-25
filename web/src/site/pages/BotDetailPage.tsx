import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import AppearanceEditor from '../components/AppearanceEditor';
import { api, type BotDetail, type BotSummary, type Meta } from '../api';

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

export default function BotDetailPage() {
  // Slug or id — the API resolves either, so old GUID links keep working.
  const { botKey } = useParams<{ botKey: string }>();
  const [bot, setBot] = useState<BotDetail | null>(null);
  const [missing, setMissing] = useState(false);
  const [expanded, setExpanded] = useState<string | null>(null);

  const load = useCallback(async () => {
    const data = await api.get<BotDetail>(`/api/bots/${botKey}`);
    setBot(data);
    return data;
  }, [botKey]);

  useEffect(() => {
    let timer: number | undefined;
    const poll = async () => {
      // A rejected fetch used to leave the page on "Loading…" forever, so a mistyped
      // or deleted bot looked like a hung site (UI audit).
      try {
        const data = await load();
        if (data.versions.some((v) => v.status === 'Pending' || v.status === 'Building'))
          timer = window.setTimeout(poll, 2500);
      } catch {
        setMissing(true);
      }
    };
    void poll();
    return () => window.clearTimeout(timer);
  }, [load]);

  if (missing)
    return (
      <div className="rounded-xl border border-arena-edge bg-arena-panel p-6">
        <p className="font-semibold">No bot called “{botKey}”.</p>
        <p className="mt-1 text-sm text-arena-dim">
          It may have been renamed or never existed.{' '}
          <Link to="/bots" className="text-arena-accent hover:underline">
            Browse every bot
          </Link>
          .
        </p>
      </div>
    );
  if (!bot) return <p className="text-sm text-arena-dim">Loading…</p>;
  const look = botLook(bot.lookId);
  const projectile = projectileLook(bot.projectileLookId);

  return (
    <div className="flex flex-col gap-8">
      <header className="flex flex-wrap items-center gap-3">
        <span
          className="flex size-12 items-center justify-center rounded-lg border border-arena-edge bg-arena-panel"
          style={{ boxShadow: `inset 0 -2px 0 ${bot.accent}55` }}
        >
          <img src={look.imageUrl} alt="" className="size-11 object-contain" />
        </span>
        <h1 className="text-2xl font-black tracking-wide">{bot.name}</h1>
        <span className="text-sm text-arena-dim">
          {look.label} · {projectile.label} · by {bot.owner}
        </span>
        <ProjectilePreview
          look={projectile}
          accent={bot.accent}
          className="h-7 w-14"
        />
      </header>

      {bot.isOwner && (
        <AppearanceEditor
          bot={bot}
          onSaved={load}
          entitlementRevision={
            bot.versions.filter((version) => version.status === 'Built').length
          }
        />
      )}

      <ChallengePanel bot={bot} />

      <section>
        <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">VERSIONS</h2>
        {bot.versions.length === 0 && (
          <p className="text-sm text-arena-dim">No versions submitted yet.</p>
        )}
        <ul className="flex flex-col gap-2">
          {bot.versions.map((version) => (
            <li key={version.id} className="rounded-lg border border-arena-edge bg-arena-panel/60 p-4">
              <div className="flex flex-wrap items-center gap-3">
                <span className="font-semibold">v{version.versionNumber}</span>
                <StatusBadge status={version.status} />
                {version.isActive && (
                  <span className="rounded bg-arena-accent/15 px-2 py-0.5 font-mono text-[11px] text-arena-accent">
                    ACTIVE
                  </span>
                )}
                {version.artifactHash && (
                  <span className="font-mono text-[11px] text-arena-dim">
                    {version.artifactHash.slice(0, 14)}…
                  </span>
                )}
                <span className="ml-auto font-mono text-[11px] text-arena-dim">
                  {new Date(version.createdAt).toLocaleString()}
                </span>
                {bot.isOwner && (version.buildLog || version.sources) && (
                  <button
                    onClick={() => setExpanded(expanded === version.id ? null : version.id)}
                    className="rounded border border-arena-edge px-2 py-0.5 text-xs text-arena-dim hover:text-arena-text"
                  >
                    {expanded === version.id ? 'Hide details' : 'Details'}
                  </button>
                )}
              </div>
              {expanded === version.id && (
                <div className="mt-3 flex flex-col gap-3">
                  {version.sources?.map((source) => (
                    <div key={source.relativePath}>
                      <p className="mb-1 font-mono text-[11px] text-arena-dim">{source.relativePath}</p>
                      <pre className="max-h-64 overflow-auto rounded bg-arena-bg p-3 font-mono text-xs whitespace-pre-wrap">
                        {source.content}
                      </pre>
                    </div>
                  ))}
                  {version.buildLog && (
                    <div>
                      <p className="mb-1 font-mono text-[11px] text-arena-dim">build log</p>
                      <pre className="max-h-48 overflow-auto rounded bg-arena-bg p-3 font-mono text-[11px] whitespace-pre-wrap text-arena-dim">
                        {version.buildLog}
                      </pre>
                    </div>
                  )}
                </div>
              )}
            </li>
          ))}
        </ul>
      </section>

      <MatchHistory botId={bot.id} botSlug={bot.slug} />

      {bot.isOwner && <SubmitPanel bot={bot} onSubmitted={load} />}
    </div>
  );
}

interface BotMatchRow {
  id: string;
  mapId: string;
  status: string;
  broadcasting: boolean;
  setGame: number | null;
  createdAt: string;
  outcome: string | null;
  opponent: { botId: string; nameSnapshot: string; accentSnapshot: string } | null;
}

function MatchHistory({ botId, botSlug }: { botId: string; botSlug: string }) {
  const [data, setData] = useState<{
    wins: number;
    losses: number;
    draws: number;
    matches: BotMatchRow[];
  } | null>(null);

  useEffect(() => {
    void api.get<NonNullable<typeof data>>(`/api/bots/${botId}/matches`).then(setData);
  }, [botId]);

  if (!data || data.matches.length === 0) return null;

  return (
    <section>
      <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">
        MATCH HISTORY ·{' '}
        <span className="text-emerald-400">{data.wins}W</span>{' '}
        <span className="text-red-400">{data.losses}L</span>{' '}
        <span className="text-arena-text">{data.draws}D</span>
        <Link to={`/?bot=${botSlug}`} className="ml-2 font-normal text-arena-accent hover:underline">
          every match →
        </Link>
      </h2>
      <ul className="flex flex-col gap-1.5">
        {data.matches.map((match) => (
          <li key={match.id}>
            <Link
              to={`/matches/${match.id}`}
              className="flex items-center gap-3 rounded-lg border border-arena-edge bg-arena-panel/60 px-4 py-2 text-sm transition-colors hover:border-arena-dim"
            >
              <span
                className={
                  'w-10 font-mono text-[11px] ' +
                  (match.outcome === 'Win'
                    ? 'text-emerald-400'
                    : match.outcome === 'Loss'
                      ? 'text-red-400'
                      : 'text-arena-dim')
                }
              >
                {match.broadcasting ? 'LIVE' : (match.outcome ?? match.status.toLowerCase())}
              </span>
              <span className="flex items-center gap-2">
                vs
                <span
                  className="inline-block size-2.5 rounded-full"
                  style={{ background: match.opponent?.accentSnapshot }}
                />
                {match.opponent?.nameSnapshot ?? '?'}
              </span>
              <span className="ml-auto font-mono text-[11px] text-arena-dim">
                {match.setGame ? `ranked g${match.setGame} · ` : ''}
                {match.mapId} · {new Date(match.createdAt).toLocaleString()}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

function StatusBadge({ status }: { status: string }) {
  const color =
    status === 'Built'
      ? 'text-emerald-400'
      : status === 'Failed'
        ? 'text-red-400'
        : 'text-amber-300';
  return <span className={`font-mono text-[11px] ${color}`}>{status.toUpperCase()}</span>;
}

function SubmitPanel({ bot, onSubmitted }: { bot: BotDetail; onSubmitted: () => Promise<unknown> }) {
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

function ChallengePanel({ bot }: { bot: BotDetail }) {
  const [allBots, setAllBots] = useState<BotSummary[]>([]);
  const [myBots, setMyBots] = useState<BotSummary[]>([]);
  const [meta, setMeta] = useState<Meta | null>(null);
  const [opponentId, setOpponentId] = useState('');
  const [challengerId, setChallengerId] = useState('');
  const [mapId, setMapId] = useState('arena-01');
  const [ranked, setRanked] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    void api.get<BotSummary[]>('/api/bots').then((bots) => {
      setAllBots(bots.filter((b) => b.activeVersion));
    });
    void api
      .get<{ id: string; name: string }[]>('/api/bots/mine')
      .then(async (mine) => {
        const all = await api.get<BotSummary[]>('/api/bots');
        setMyBots(all.filter((b) => mine.some((m) => m.id === b.id) && b.activeVersion));
      })
      .catch(() => setMyBots([]));
    void api.get<Meta>('/api/meta').then(setMeta);
  }, []);

  const isMine = bot.isOwner && bot.versions.some((v) => v.status === 'Built');
  // On my own bot page: challenge others with this bot. On another bot's page:
  // challenge it with one of my bots.
  const challenger = isMine ? bot.id : challengerId;
  const opponent = isMine ? opponentId : bot.id;
  const selectable = isMine
    ? allBots.filter((b) => b.id !== bot.id)
    : myBots.filter((b) => b.id !== bot.id);

  if (selectable.length === 0 || (!isMine && myBots.length === 0)) return null;

  const fight = async () => {
    setError(null);
    try {
      if (ranked) {
        // No opponent: the server matchmakes by rating (DECISIONS #95).
        const set = await api.post<{ id: string }>('/api/matches/ranked', {
          botId: challenger,
        });
        navigate(`/sets/${set.id}`);
      } else {
        const match = await api.post<{ id: string }>('/api/matches/challenge', {
          botId: challenger,
          opponentBotId: opponent,
          mapId,
        });
        navigate(`/matches/${match.id}`);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Challenge failed.');
    }
  };

  return (
    <section className="flex flex-wrap items-end gap-3 rounded-xl border border-arena-edge bg-arena-panel p-5">
      <div className="flex flex-col gap-1 text-xs text-arena-dim">
        {isMine ? (ranked ? 'Opponent (matchmade)' : 'Opponent') : 'Challenge with'}
        <select
          value={isMine ? opponentId : challengerId}
          disabled={isMine && ranked}
          onChange={(e) => (isMine ? setOpponentId(e.target.value) : setChallengerId(e.target.value))}
          className="rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm text-arena-text disabled:opacity-40"
        >
          <option value="">Choose a bot…</option>
          {selectable.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name} ({b.owner})
            </option>
          ))}
        </select>
      </div>
      <div className="flex flex-col gap-1 text-xs text-arena-dim">
        Map
        <select
          value={mapId}
          onChange={(e) => setMapId(e.target.value)}
          className="rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm text-arena-text"
        >
          {(meta?.maps ?? []).map((m) => (
            <option key={m.id} value={m.id}>
              {m.id} ({m.width}×{m.height})
            </option>
          ))}
        </select>
      </div>
      {isMine && (
        <label className="flex cursor-pointer items-center gap-2 pb-2 text-xs text-arena-dim select-none">
          <input
            type="checkbox"
            checked={ranked}
            onChange={(e) => setRanked(e.target.checked)}
            className="accent-(--color-arena-accent)"
          />
          Ranked set (6 games, mirrored starts, moves elo — opponent matchmade)
        </label>
      )}
      <button
        onClick={() => void fight()}
        disabled={!challenger || (!ranked && !opponent)}
        className="rounded-md bg-arena-accent px-5 py-2 text-sm font-bold text-slate-950 disabled:opacity-40"
      >
        {ranked ? 'FIGHT FOR RATING' : 'FIGHT'}
      </button>
      {error && <p className="w-full text-sm text-red-400">{error}</p>}
    </section>
  );
}
