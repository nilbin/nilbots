import Markdown from '../components/Markdown';
// The one source of rules prose. Imported raw at build time, so the site, the text
// mirror at /llms-full.txt and the README written by `nilbots new` are the same words
// by construction rather than by discipline.
import playerGuide from '../../../../docs/PLAYER-GUIDE.md?raw';

/// The version stamp lives here (and is pinned by DocDriftTests) rather than in the
/// guide's filename — a versioned filename meant renaming the file and chasing nine
/// references on every ruleset bump.
const RULES_VERSION = '0.5';

/// On-site documentation (plan §29.6): enough for a player to go from zero to a
/// submitted bot without reading the repository.
export default function DocsPage() {
  return (
    <div className="prose-invert mx-auto flex max-w-3xl flex-col gap-8 text-sm leading-relaxed">
      <section>
        <h1 className="mb-2 text-2xl font-black tracking-wide">How to play</h1>
        <p className="text-arena-dim">
          Write a C# bot, submit it, and watch it fight. Matches are deterministic:
          the same bots, map and seed always produce the same battle — so when your
          bot loses, you can replay the exact match and see why.
        </p>
      </section>

      <Doc title="Quick start (browser only)">
        <ol className="list-decimal space-y-1 pl-5">
          <li>Create an account and open <b>My garage</b>.</li>
          <li>Create a bot, then paste your C# into <b>Submit new version</b> — the
            server compiles it to WebAssembly and validates it.</li>
          <li>Open any bot's page and hit <b>FIGHT</b> — or <b>FIGHT FOR RATING</b>
            for a ranked 6-game set that moves elo.</li>
          <li>Watch the broadcast live; click your bot in the viewer to see exactly
            what it saw and why it acted.</li>
        </ol>
      </Doc>

      <Doc title="The bot API">
        <pre className="overflow-x-auto rounded bg-arena-bg p-3 font-mono text-xs">{`using BotArena.Sdk;

public sealed class MyBot : IBot
{
    public BotAction Tick(BotContext context)
    {
        // One action per tick: Actions.Wait/MoveForward/TurnLeft/TurnRight/Shoot
        return Actions.MoveForward();
    }
}`}</pre>
        <p className="mt-2">
          One instance lives for the whole match — fields persist between ticks.
          <code className="font-mono"> BotContext</code> gives you:
        </p>
        <ul className="mt-1 list-disc space-y-1 pl-5">
          <li><code className="font-mono">Position, Facing, Health, Cooldown, Tick</code> — your own state.</li>
          <li><code className="font-mono">VisibleTiles / VisibleEnemies / VisibleEvents</code> — what you can
            see in your facing cone (range 6, walls block sight). You never get the full map.</li>
          <li><code className="font-mono">VisibleProjectiles / HeardSounds</code> — exact currently visible
            projectile danger and coarse cues for loud events beyond sight.</li>
          <li><code className="font-mono">ZoneTiles / ControlPressure / ControlPressureLimit</code> — public
            territorial geometry and the shared signed objective meter.</li>
          <li><code className="font-mono">ShotPrograms</code> — the legal envelope for private immutable
            curved shots; validate and preview a program before firing.</li>
          <li><code className="font-mono">PreviousActionResult</code> — Success, Blocked, OnCooldown…</li>
          <li><code className="font-mono">Random</code> — the <i>only</i> allowed randomness. System clocks and
            <code className="font-mono"> System.Random</code> are neutralized in the sandbox.</li>
          <li><code className="font-mono">Debug.Write(...)</code> — notes shown in the replay viewer. They are
            part of the replay, which is public once the broadcast reveals it —
            great for debugging, bad for secrets.</li>
          <li>Helpers: <code className="font-mono">IsWallAhead(), CanSee(p), CanShoot</code>.</li>
        </ul>
      </Doc>

      {/* The rules are NOT restated here. This renders the canonical player guide
          (docs/PLAYER-GUIDE.md) that also backs /llms-full.txt and every scaffolded
          README, so the site cannot drift from the game — which it did once, teaching
          a retired zone-tick win condition for two days. */}
      <Doc title={`Rules of the arena (v${RULES_VERSION})`}>
        <Markdown source={playerGuide} />
      </Doc>

      <Doc title="Determinism (why replays are trustworthy)">
        <p>
          A match is a pure function of the two artifacts, the map, the rules
          version and the seed — replaying it always produces the identical battle,
          byte for byte (that's the replay hash). <code className="font-mono">context.Random</code> is
          derived from the match seed and your slot, so even your "random" choices
          replay exactly. Corollary: if neither bot consults
          <code className="font-mono"> Random</code>, different seeds produce the <i>same</i> game — vary
          maps and starting positions (<code className="font-mono">--swap</code>), not just seeds, when
          testing. Replay hashes are comparable only within one runtime kind —
          in-process and WASM runs of the same match match in behavior but hash
          differently, because the runtime is part of a replay's identity; verify
          cross-runtime by comparing results, not hashes. The replay JSON schema and
          its reading conventions are documented
          in <code className="font-mono">docs/REPLAY-FORMAT.md</code> in the repo, and
          <code className="font-mono"> nilbots replay &lt;file&gt; --summary</code> prints a
          compact digest (<code className="font-mono">--full</code> for every tick).
        </p>
      </Doc>

      <Doc title="Scripting the API (bots, CI, headless)">
        <p className="mb-2">
          Everything the site does goes through the JSON API, and cookie auth works
          headless — no browser needed:
        </p>
        <pre className="overflow-x-auto rounded bg-arena-bg p-3 font-mono text-xs">{`curl -c jar -H 'Content-Type: application/json' \\
  -d '{"displayName":"Me","email":"me@x","password":"..."}' <server>/api/accounts/register
curl -b jar -H 'Content-Type: application/json' \\
  -d '{"name":"MyBot","accent":"#22d3ee","lookId":"vanguard"}' <server>/api/bots
curl -b jar -H 'Content-Type: application/json' \\
  -d '{"entryType":"MyBot","files":[{"name":"MyBot.cs","content":"..."}]}' \\
  <server>/api/bots/<id>/versions        # then poll /api/bots/<id>/build-status
curl -b jar -H 'Content-Type: application/json' \\
  -d '{"botId":"...","rules":"0.5"}' \\
  <server>/api/matches/ranked          # opponent is matchmade by rating; rules optional
                                       # (shipped versions only — each has its own ladder)
curl -b jar -H 'Content-Type: application/json' \\
  -d '{"botId":"...","opponentBotId":"...","mapId":"arena-01"}' \\
  <server>/api/matches/challenge       # unranked: you pick, nothing touches the ladder
curl <server>/api/leaderboard?rules=0.5      # pick a ladder; default = current rules
curl <server>/api/matches/<matchId>/replay   # public once the broadcast reveals it`}</pre>
        <p className="mt-2 text-arena-dim">
          <code className="font-mono">/build-status</code> is the slim polling view — it returns an
          <b> array</b> of versions, newest first, so poll <code className="font-mono">[0].status</code>,
          not <code className="font-mono">.status</code>. The CLI's
          <code className="font-mono"> nilbots submit</code> wraps this flow plus artifact-parity checking.
        </p>
      </Doc>

      <Doc title="Local development (CLI)">
        <pre className="overflow-x-auto rounded bg-arena-bg p-3 font-mono text-xs">{`dotnet tool install --global Nilbots
nilbots register                                   # create account + OAuth/PKCE sign-in
nilbots new MyBot && cd MyBot
nilbots play --runtime in-process --bot . --opponent hunter   # fastest — iterate here
nilbots play --bot . --opponent hunter --seed 42   # official WASM sandbox (exact)
nilbots watch . --opponent hunter --runtime in-process        # replay on every save
nilbots submit .                                   # creates bot + official build + parity`}</pre>
        <p className="mt-2 text-arena-dim">
          Iterate in-process (plain .NET build, same engine and deterministic
          randomness, seconds per run), then verify in the default WASM mode — the
          exact sandbox the server uses, with fuel/memory limits enforced —
          before submitting. <code className="font-mono"> submit</code> reports whether your local artifact is
          bit-identical to the server's build.
        </p>
      </Doc>
    </div>
  );
}

function Doc({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-xl border border-arena-edge bg-arena-panel/60 p-5">
      <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim uppercase">{title}</h2>
      {children}
    </section>
  );
}
