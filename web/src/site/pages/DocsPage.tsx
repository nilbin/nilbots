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

      <Doc title="Rules of the arena (v0.5)">
        <ul className="list-disc space-y-1 pl-5">
          <li>Tile grid, four facings, both bots decide simultaneously from the
            pre-tick state. 3 HP each, max 500 ticks.</li>
          <li><b>The zone is territory.</b> Every map exposes
            <code className="font-mono"> context.ZoneTiles</code> from tick 0.
            After movement, projectiles and damage, the sole active zone occupant
            gains 1 signed <code className="font-mono">ControlPressure</code> with
            <b> any action</b>. If both bots occupy the zone—or neither does—existing
            pressure decays 1 toward zero every 2 ticks. Positive favors slot 0;
            negative favors slot 1. Reaching <b>±100 wins immediately</b> by
            Domination. From tick 200 the limit becomes ±10 and sole gain becomes 2,
            while decay remains. Evict, don&apos;t merely arrive.</li>
          <li>Spawn positions and facings vary by match seed (deterministically —
            replays are still exact), <b>never share a clear firing lane</b>, and
            are <b>zone-distance-fair</b> (within 2 walking steps of each other to
            the nearest zone tile) — the opening race is winnable from either
            side. Don't hardcode an opening; read your surroundings. Ranked sets
            mirror both spawns, so asymmetric starts stay fair across a set.</li>
          <li>Shoot launches a range-8 projectile onto the first adjacent path tile
            that tick. On every later tick it crosses <b>two ordered tiles</b> after
            bot movement. Every intermediate tile checks walls, strict diagonal
            corners, final range, and the first bot encountered—speed two never
            tunnels. A projectile tile is lethal to non-owners; a hit deals 1 damage;
            cooldown is 2 ticks.</li>
          <li><code className="font-mono">VisibleProjectiles</code> exposes current
            position and eight-way <code className="font-mono">Heading</code>;
            <code className="font-mono"> TicksUntilAdvance == 1</code> means it moves
            this tick after your movement, and
            <code className="font-mono"> TilesPerAdvance / RemainingTiles</code>
            make the threat computable.</li>
          <li><b>Programmed skill shots.</b>
            <code className="font-mono"> context.ShotPrograms</code> is the exact
            legal envelope: initial aim −1/0/+1 45° octants, first bend after 1–4
            tiles, later bends every 1–3 tiles, and 1–3 bends in one direction.
            Validate with <code className="font-mono">limits.IsValid</code>, preview
            with <code className="font-mono">ShotPaths.Preview</code>, and return
            <code className="font-mono"> Actions.Shoot(program)</code>. The future
            path is private and immutable; opponents see only each manifested
            heading. Plain <code className="font-mono">Actions.Shoot()</code> stays
            straight.</li>
          <li>Vision is a <b>90° cone toward your facing</b>, plus the eight
            adjacent tiles, Chebyshev range 6, and corner-strict. Turning is
            scanning, aiming, and changing movement direction together; your back
            is genuinely blind. Even a diagonally adjacent wall can be hidden by a
            clipped corner, so remember observed terrain.</li>
          <li>Loud unseen Shot/Damage/Destroyed events within range 8 arrive one
            tick later in <code className="font-mono">HeardSounds</code> as event
            kind + eight-way bearing + near/medium/far—never exact coordinates or
            slots. An event appears in full-detail
            <code className="font-mono"> VisibleEvents</code> or as sound, never both.</li>
          <li>Movement: two bots can't share a tile; moving into the same tile or
            swapping fails for both; blocked moves become Wait.</li>
          <li>Resolution order: observe → turn → bot movement → existing projectile
            substeps/collisions → new-shot launch → simultaneous damage →
            territorial pressure → victory. Both bots destroyed on the same tick
            is a draw.</li>
          <li>Win by Domination, elimination, or at tick 500 by pressure sign, then
            health, then damage dealt; all equal is a draw. A lead decays while the
            zone remains empty or contested. A bot that crashes 3 times is
            disqualified—exceptions, infinite loops and out-of-memory all count.</li>
          <li>Ranked sets are 6 games across 3 map/seed pairs — 3 maps sampled per
            set from the pool (basic-01, arena-01, crossfire-01, bastion-01,
            gallery-01; see <code className="font-mono">nilbots maps</code>) — each
            played from both starting positions; elo
            moves once per set, on the <b>ladder of the rules the set was played
            under</b> — every rules version has its own ladder, and a challenge may
            pin an older ruleset. Rehearse the exact format
            locally: <code className="font-mono">nilbots set --bot . --opponent hunter</code>.</li>
          <li><code className="font-mono">VisibleEvents</code> describe last tick.
            Under cone vision, full detail requires the event&apos;s primary tile to
            be visible: the shooter/mover, or the damaged/destroyed bot. An unseen
            loud event arrives only as a redacted sound. An event&apos;s
            <code className="font-mono"> Slot</code> is the acting bot (for Damage:
            the dealer, not the victim); compare with
            <code className="font-mono"> context.Slot</code>.</li>
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
  -d '{"name":"MyBot","accent":"#22d3ee"}' <server>/api/bots
curl -b jar -H 'Content-Type: application/json' \\
  -d '{"entryType":"MyBot","files":[{"name":"MyBot.cs","content":"..."}]}' \\
  <server>/api/bots/<id>/versions        # then poll /api/bots/<id>/build-status
curl -b jar -H 'Content-Type: application/json' \\
  -d '{"botId":"...","opponentBotId":"...","rules":"0.5"}' \\
  <server>/api/matches/ranked          # rules optional; every ruleset has its own ladder
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
