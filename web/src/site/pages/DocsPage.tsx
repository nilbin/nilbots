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
          Your competitive identity is an entrant: either a commander sheet executed by
          the frozen stock mind, or your own submitted mind. Both get the same crest,
          eight-body composition, match history and persistent rating.
        </p>
      </section>

      <Doc title="Quick start">
        <ol className="list-decimal space-y-1 pl-5">
          <li><Link to="/login?mode=register" className="text-link">Create an account</Link>.</li>
          <li>Open <Link to="/relay" className="text-link">Relay</Link> and save an eight-slot sheet, or submit a C# mind with its declared composition.</li>
          <li>Pick a crest. A custom mind must finish its controlled build and complete one hosted preflight before entering ranked play.</li>
          <li>Opt in up to three entrants. Passive pairing finds similarly rated entrants from other accounts; your own scrimmages are always unrated.</li>
          <li>Watch the causal broadcast. The score strip shows only integrity, charge and time already reached by the playhead.</li>
        </ol>
      </Doc>

      <Doc title="Sheets and minds">
        <p>
          A sheet is saved deterministic data: eight unlocked classes under the two-copy
          cap, assignments, routes and ordered gambits. Saving a revision preserves the
          entrant identity and rating; <b>save as copy</b> starts a new entrant and rating.
        </p>
        <p className="mt-2">
          A custom mind submits source files through the controlled toolchain and runs only
          as sandboxed WASM with fuel and memory limits. Every revision declares and
          snapshots its eight classes. A runtime fault or a post-match felt-degeneracy bar
          suspends pairing until a corrected revision passes preflight.
        </p>
      </Doc>

      <Doc title="Custom mind shape">
        <pre className="term">{`using BotArena.Sdk;

public sealed class MyRelayMind : IGenericMindBot
{
    public void StartMatch(MindStart start) { }

    public void Think(MindContext mind)
    {
        foreach (MindBody body in mind.Bodies)
            body.Hold("choose an action from the visible Arc Relay state");
    }

    public void EndMatch(MindEnd end) { }
}`}</pre>
        <p className="mt-2 text-arena-dim">
          The browser submission form is the supported hosted path for this launch slice.
          Composition and entitlements are validated again on the server; client controls
          are guidance, never authority.
        </p>
      </Doc>

      <Doc title="Ratings, secrecy and determinism">
        <p>
          Ranked Arc Relay uses exact-compatible Elo attached to entrant identity. Each
          match records the precise sheet revision or mind artifact and composition hashes,
          while revisions keep the rating. Pairing avoids recent opponents, bounds daily
          volume and never pairs two entrants owned by the same account.
        </p>
        <p className="mt-2">
          Match and ladder surfaces follow the broadcast clock: results and rating changes
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
