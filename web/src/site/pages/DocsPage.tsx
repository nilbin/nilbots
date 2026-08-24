import { Link } from 'react-router-dom';
import Markdown from '../components/Markdown';
import playerGuide from '../../../../docs/PLAYER-GUIDE.md?raw';

// Retained for the read-only Duel archive and pinned by DocDriftTests.
const RULES_VERSION = '0.5';

export default function DocsPage() {
  return (
    <div className="t-body mx-auto flex max-w-3xl flex-col gap-3.5 leading-relaxed">
      <section>
        <h1 className="mb-2 type-display text-[26px]">Arc Relay field guide</h1>
        <p className="text-arena-dim">
          Your competitive identity is a tactical sheet: eight bodies, one authored plan,
          a crest, match history and persistent Season rating.
        </p>
      </section>

      <Doc title="Quick start">
        <ol className="list-decimal space-y-1 pl-5">
          <li><Link to="/login?mode=register" className="text-link">Create an account</Link>.</li>
          <li>Open the <Link to="/entrants/sheets/new" className="text-link">Tactics editor</Link>, pick your eight bots and save a sheet.</li>
          <li>Pick a crest and field up to three sheets in Season 1.</li>
          <li>Ranked play is automatic. The Season finds similarly rated sheets from other accounts and schedules up to six games per sheet in a rolling 24 hours.</li>
          <li>For an immediate unranked game, use <b>Practice</b> in the Tactics editor, then return to the same draft.</li>
          <li>Watch the causal broadcast. The score strip shows only integrity, charge and time already reached by the playhead.</li>
        </ol>
      </Doc>

      <Doc title="Tactical sheets">
        <p>
          A sheet is a squad of eight bots plus an ordered list of whole-team
          <b> Tactics</b>. Each Tactic is one committed operation in ordered, named
          phases: every phase says what each of its one to eight temporary Groups does
          on the map — exact posts, an area, a picket line, a quiet approach, a
          convergence, a push, a patrol, the run home — and carries a latched
          “Continue when…” that moves the whole squad forward together. Transitions
          only ever go forward; once a Tactic starts it keeps control until it
          finishes, a Tactic-wide abort fires, or its time limit expires, and outcomes
          count only after release into the phase that watches for them. Respawned
          bots join the current phase. Tactics are read top to bottom; when one
          finishes or aborts, the list is reevaluated. On save the server
          deterministically materializes your Tactics into the executable Orders
          document and compiles it through the same shared compiler as the CLI,
          fielding the sheet in the current Season by default. A revision preserves
          entrant identity and rating.
        </p>
        <p className="mt-2 text-arena-dim">
          Core pickup and reactor banking happen when a body physically reaches the tile.
          A voluntary handoff or drop prevents that same body from immediately reclaiming
          that same Core. Path traversal, legal movement, aiming and collision avoidance
          remain deterministic executor mechanics rather than repeated micro-orders.
        </p>
        <p className="mt-2 text-arena-dim">
          Hosted playlist v37 and the local <code>nilbots arc-relay</code>
          default both run broadloom-7 on the 37x27 Broadloom map — three Wells in
          distinct roomed theatres, two neutral Workshops, signature grammar 7 and
          atomic convoy movement: bodies may enter a tile a teammate leaves in the
          same tick, but swaps, closed cycles and contested destinations still
          block. Sheets authored for earlier maps
          remain match history but are not accepted by the current editor;
          coordinates are never projected between maps. Broadloom values a Core at 1, 2,
          or 4 charge according to its Well&apos;s distance from your reactor, promotes
          veterans through combat, returns destroyed bodies in public 30-tick waves,
          requires at least seven distinct classes, and makes deployments durable targets.
          Its centre Well opens on the same second global beat as the outer Wells, leaving
          the first beat for scouting and counter-positioning before the two-charge Core.
          Facing-quadrant vision reaches 5 tiles, while short, medium and long basic
          guns reach 3, 4 and 6. Immediate team-shared sight lets Longshot use that
          sixth tile when an ally supplies contact. Swift, standard and deliberate
          chassis move on public deterministic 1/1, 4/5 and 2/3 opportunity cycles
          under one Combat Strafe facing model. Palisade&apos;s mobile Prism Screen
          follows its facing for six ticks, absorbs 30 hull of hostile fire, passes
          allied shots, and never blocks bodies. Living teammates harmlessly stop
          allied fire, while friendly sentries, mines and walls remain transparent.
          On death, a bot keeps half its accumulated veteran XP, rounded down, on
          its team-local roster slot. Grammar 7 makes Kestrel&apos;s Dash and
          Switchback&apos;s carrier Exchange Core-safe; Towline&apos;s Hook deals 10,
          pulls a survivor to its front, and may cross one player-drawn wall or
          hostile Hardlight tile; Repulsor damages and pushes every adjacent
          hostile; Lantern Flare grants radius-4 team sight; and Sunder benefits
          from its own Paint. It also promotes blind plotted Falling Star, two
          hidden radius-one mines, full seven-tick Hush suppression, and public
          one-tick first-contact Hook/Sentry rays while Rail stays piercing.
          Playlist v37 opens a fresh rating book at 1200; playlists v23–v36
          remain readable history and their ratings are not carried into v37.
        </p>
        <p className="mt-2 text-arena-dim">
          Ranked, practice, the Tactics editor and the local CLI all run that same
          source-local supply. Each Well keeps its existing public opening, then owns one
          active Core until it is banked into either reactor. That bank starts an exact
          public 46-tick recharge. Carrying, tossing, dropping and stealing do not start
          the clock, and an active Core has no birth countdown.
        </p>
        <p className="mt-2 text-arena-dim">
          Both Workshops are neutral objectives with fully public state. They
          unlock at tick 25. One body trains by holding the training tile for 15
          consecutive qualifying ticks: it must stay alive, stay put, carry no
          Core, take no damage, attack nothing, cast nothing, and no living enemy
          may stand anywhere in the Workshop&apos;s contest room. Any violation
          resets progress to zero, and dying or swapping bodies never inherits a
          channel. Completion grants exactly one veterancy XP through the normal
          capped ladder, and another 15-tick channel can begin on the following
          tick with no Workshop cooldown.
          Guards keep the room empty but never speed the channel up — a visible
          Training signal is an open invitation to contest it or to raid
          somewhere else while you answer.
        </p>
      </Doc>

      <Doc title="Facing and basic fire">
        <p>
          A mobile body fires only straight ahead or along either adjacent diagonal.
          Swift bodies normally turn with travel but may deliberately strafe to keep
          their current aim; Standard bodies preserve aim while moving forward or
          sideways but turn around on a rear move; Deliberate bodies move only forward
          until they spend a turn action. Signature targeting keeps each class&apos;s own
          authored rules, and deployed Sentinels remain omnidirectional.
          Basic short, medium and long guns reach 3, 4 and 6 tiles respectively;
          every body&apos;s facing-quadrant vision reaches 5 tiles.
        </p>
        <p className="mt-2 text-arena-dim">
          Your mind should read the action legality mask every tick. If a visible target
          is outside the published three-heading cone, rotate or reposition before
          firing instead of submitting a forbidden heading. A sheet&apos;s optional
          destination-facing instruction affects only an arrived body; it does not steer
          the route, promise line of sight, or override a higher-priority fight.
        </p>
      </Doc>

      <Doc title="Ratings, secrecy and determinism">
        <p>
          Ranked Arc Relay uses exact-compatible Elo attached to entrant identity. Each
          match records the precise sheet revision and composition hashes while revisions
          keep the rating. Pairing avoids recent opponents, bounds daily volume and never
          pairs two sheets owned by the same account.
        </p>
        <p className="mt-2">
          Match and Season surfaces follow the broadcast clock: results and rating changes
          do not leak ahead of the watchable causal prefix. Gameplay remains a deterministic
          function of pinned artifacts/data, map, rules version and seed, so canonical
          verification remains byte-exact.
        </p>
      </Doc>

      <Doc title="Legacy Duel archive">
        <p>
          Legacy Duel creation, submission and admission are retired from the product surface.
          Historical pages and replays remain read-only in the{' '}
          <Link to="/archive/bots" className="text-link">legacy archive</Link>; the nilbots
          brand, CLI, frozen contracts and verification paths are unchanged.
        </p>
        <details className="mt-3">
          <summary className="lab cursor-pointer">Archived Duel ruleset v{RULES_VERSION}</summary>
          <div className="mt-3"><Markdown source={playerGuide} headingOffset={2} /></div>
        </details>
      </Doc>
    </div>
  );
}

function Doc({ title, children }: { title: string; children: React.ReactNode }) {
  return <section className="panel pad"><h2 className="lab mb-3">{title}</h2>{children}</section>;
}
