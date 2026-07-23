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
            see (range 6, walls block sight). You never get the full map.</li>
          <li><code className="font-mono">PreviousActionResult</code> — Success, Blocked, OnCooldown…</li>
          <li><code className="font-mono">Random</code> — the <i>only</i> allowed randomness. System clocks and
            <code className="font-mono"> System.Random</code> are neutralized in the sandbox.</li>
          <li><code className="font-mono">Debug.Write(...)</code> — notes shown in the replay viewer. They are
            part of the replay, which is public once the broadcast reveals it —
            great for debugging, bad for secrets.</li>
          <li>Helpers: <code className="font-mono">IsWallAhead(), CanSee(p), CanShoot</code>.</li>
        </ul>
      </Doc>

      <Doc title="Rules of the arena (v0.4)">
        <ul className="list-disc space-y-1 pl-5">
          <li>Tile grid, four facings, both bots decide simultaneously from the
            pre-tick state. 3 HP each, max 500 ticks.</li>
          <li><b>The zone (v0.4).</b> Every map declares zone tiles —
            <code className="font-mono"> context.ZoneTiles</code>, the full list from tick 0, not gated by
            vision; some maps split the zone into disconnected pads. At the end of
            every tick you are alive and the <b>sole</b> bot standing on zone tiles
            you gain 1 zone-tick; a <b>contested</b> zone (both bots on it) pays
            nobody — evict, don't share. Reaching <b>150 zone-ticks wins
            immediately</b> (Domination). Scores are public:
            <code className="font-mono"> MyZoneTicks</code> / <code className="font-mono">EnemyZoneTicks</code> — a
            frozen counter while you stand on the zone proves the enemy is on it
            too, even unseen.</li>
          <li>Spawn positions and facings vary by match seed (deterministically —
            replays are still exact), <b>never share a clear firing lane</b>, and
            are <b>zone-distance-fair</b> (within 2 walking steps of each other to
            the nearest zone tile, v0.4) — the opening race is winnable from either
            side. Don't hardcode an opening; read your surroundings. Ranked sets
            mirror both spawns, so asymmetric starts stay fair across a set.</li>
          <li>Shooting is an instant ray in your facing direction with a
            <b> range of 8 tiles</b> (v0.3 — was unlimited): the first wall or bot
            within range stops it; 1 damage; 2-tick cooldown (a shot every 3rd
            tick). Cross-map lane camping no longer works — control is local.</li>
          <li>Vision is <b>omnidirectional</b> (facing only affects moving and
            shooting), measured as Chebyshev distance ≤ 6, and <b>corner-strict</b>:
            if the straight line to a tile touches any wall — corners included —
            the tile is hidden. That can hide even a <i>diagonally adjacent</i> wall,
            and <code className="font-mono">IsWall()</code> returns false for unseen tiles: remember the
            map yourself. Shots still outrange sight (8 &gt; 6) — a bot you can't
            see can hit you down a clear straight line, and vice versa.</li>
          <li>Two duel-deciding corollaries of the resolution order: a shooter's ray
            fires from its <b>pre-move</b> position and facing (you cannot move and
            shoot the same tick), and moves resolve <b>before</b> shots — so a
            perpendicular sidestep dodges a ray fired at where you stood.</li>
          <li>Movement: two bots can't share a tile; moving into the same tile or
            swapping fails for both; blocked moves become Wait.</li>
          <li>Resolution order each tick: turn → move → shoot → damage
            (simultaneous). Shots resolve against the <b>post-move</b> board — the
            shooter itself hasn't moved (shooting was its action for the tick), but
            its target may have. Both bots destroyed on the same
            tick is a <b>draw</b> — crossing shots are real, watch your approach.</li>
          <li>Win by <b>Domination</b> (150 zone-ticks), by destroying the opponent,
            or at <b>tick 500</b> by more <b>zone-ticks</b>, then more health, then
            more damage dealt; all equal is a draw. A health lead <i>without</i> the
            zone loses the tiebreak — the zone is the objective; shooting is how you
            take and hold it. A bot that crashes 3 times is disqualified —
            exceptions, infinite loops and out-of-memory all count.</li>
          <li>Ranked sets are 6 games across 3 map/seed pairs (pool: basic-01,
            arena-01 and crossfire-01), each played from both starting positions; elo
            moves once per set. Rehearse the exact format locally: <code className="font-mono">botarena set --bot . --opponent hunter</code>.</li>
          <li><code className="font-mono">VisibleEvents</code> describe <b>last</b> tick, delivered when part of
            the event is on a tile you can see now — a shot fired from beyond your
            vision is still delivered if the ray enters it. An event's
            <code className="font-mono"> Slot</code> is the <i>acting</i> bot (for Damage: the dealer, not the
            victim); compare with <code className="font-mono">context.Slot</code> to attribute your own.</li>
        </ul>
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
          <code className="font-mono"> botarena replay &lt;file&gt; --summary</code> prints a
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
curl -b jar -d '{"name":"MyBot","accent":"#22d3ee"}' <server>/api/bots
curl -b jar -d '{"entryType":"MyBot","files":[{"name":"MyBot.cs","content":"..."}]}' \\
  <server>/api/bots/<id>/versions        # then poll /api/bots/<id>/build-status
curl -b jar -d '{"botId":"...","opponentBotId":"..."}' <server>/api/matches/ranked
curl <server>/api/matches/<matchId>/replay   # public once the broadcast reveals it`}</pre>
        <p className="mt-2 text-arena-dim">
          <code className="font-mono">/build-status</code> is the slim polling view — it returns an
          <b> array</b> of versions, newest first, so poll <code className="font-mono">[0].status</code>,
          not <code className="font-mono">.status</code>. The CLI's
          <code className="font-mono"> botarena submit</code> wraps this flow plus artifact-parity checking.
        </p>
      </Doc>

      <Doc title="Local development (CLI)">
        <pre className="overflow-x-auto rounded bg-arena-bg p-3 font-mono text-xs">{`botarena new MyBot && cd MyBot
botarena play --runtime in-process --bot . --opponent hunter   # fastest — iterate here
botarena play --bot . --opponent hunter --seed 42   # official WASM sandbox (exact)
botarena watch . --opponent hunter --runtime in-process        # replay on every save
botarena login                                      # browser sign-in (OAuth + PKCE)
botarena submit .                                   # official server build + parity check`}</pre>
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
