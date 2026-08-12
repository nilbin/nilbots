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
          <li>Open the <Link to="/entrants/sheets/new" className="text-link">sheet workshop</Link> and save an eight-slot sheet.</li>
          <li>Pick a crest and field up to three sheets in Season 1.</li>
          <li>Ranked play is automatic. The Season finds similarly rated sheets from other accounts and schedules up to six games per sheet in a rolling 24 hours.</li>
          <li>For an immediate unranked game, use <b>Practice &amp; watch</b> in the sheet workshop. Choose a stock opponent and seed, then return to the same draft.</li>
          <li>Watch the causal broadcast. The score strip shows only integrity, charge and time already reached by the playhead.</li>
        </ol>
      </Doc>

      <Doc title="Tactical sheets">
        <p>
          A sheet is an Orders document plus a Tapline map plan. Exact class bodies live
          inside roles under the two-copy cap. Each role reads one nested priority list
          made from five physical actions: move, fight, use a signature skill, handoff a
          Core, or drop it. Named conditions control entry, exit, timeout and cooldown;
          one capacity rule limits bodies claiming the same resolved target. Routes,
          zones and anchors are plotted directly on the map. Saving compiles through the
          same shared compiler as the CLI and fields the sheet in the current Season by
          default. A revision preserves entrant identity and rating; <b>save as copy</b>
          starts a new entrant and rating.
        </p>
        <p className="mt-2 text-arena-dim">
          Core pickup and reactor banking happen when a body physically reaches the tile.
          A voluntary handoff or drop prevents that same body from immediately reclaiming
          that same Core. Path traversal, legal movement, aiming and collision avoidance
          remain deterministic executor mechanics rather than repeated micro-orders.
        </p>
        <p className="mt-2 text-arena-dim">
          Hosted playlist v19 and the local <code>nilbots arc-relay</code> default
          both run tapline-1 on Tapline. Sheets authored for earlier maps
          remain readable history but must be replotted natively before admission to the
          current Season; coordinates are never projected between maps. Season 1 is the
          first public eight-week competition on v19: every entrant starts at 1200 with a
          zero-match record. Earlier development ladders are not presented as Season history.
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
        </p>
        <p className="mt-2 text-arena-dim">
          Your mind should read the action legality mask every tick. If a visible target
          is outside the published three-heading cone, rotate or reposition before
          firing instead of submitting a forbidden heading.
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
