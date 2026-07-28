import clsx from 'clsx';

/**
 * The garage with nothing in it — a signed-in account that has never shipped a bot.
 *
 * This is the one hand-off from the website to the CLI, so it is a sequence rather than a
 * page of prose, and it is numbered because the order is load-bearing: none of it works
 * before the tool is installed. (Numbering a set of options that can be done in any order
 * is how a menu ends up reading like instructions — so nothing else on the site is
 * numbered.)
 *
 * **Every command here is one the CLI actually answers to**, which is the whole value of
 * the screen. Two lines in the design mock do not: `brew install nilbots` — there is no
 * formula, the CLI ships as the `Nilbots` .NET global tool — and `nilbots new my-bot`,
 * which `NewCommand` rejects, because the name becomes a C# type and must be an
 * identifier. A hyphen fails before a single file is written. The commands below are the
 * ones in `Program.cs`'s dispatch and help text.
 */

type Step = {
  title: string;
  /** Pasteable, in order. More than one line only where the CLI genuinely needs two. */
  commands: readonly string[];
  note: string;
  /** The last step is the one thing on the page that is not done on the page. */
  waiting?: boolean;
};

const STEPS: readonly Step[] = [
  {
    title: 'Install the CLI',
    commands: ['dotnet tool install --global Nilbots'],
    // The .NET SDK is a real prerequisite, and the first command failing on a missing
    // one is exactly the sort of stop this screen exists to prevent.
    note: 'Needs the .NET 10 SDK. The tool is the build: it compiles your C# through the same pipeline the server uses.',
  },
  {
    title: 'Start from a template',
    commands: ['nilbots new MyBot && cd MyBot'],
    note: 'A working bot that moves, remembers and shoots. The name becomes a C# type, so letters and digits only.',
  },
  {
    title: 'Watch it fight, locally',
    commands: ['nilbots play --bot . --opponent hunter --seed 42'],
    note: 'Writes a replay and a viewer.html you can open and scrub. No account, nothing uploaded.',
  },
  {
    title: 'Put it on the ladder',
    // `submit` refuses before it compiles when nobody is signed in, and the account you
    // are reading this page with is not the CLI's — the browser grant is a separate step.
    commands: ['nilbots login', 'nilbots submit'],
    note: 'The server rebuilds your source and tells you whether its artifact is byte-for-byte the one you played.',
    waiting: true,
  },
];

export default function FirstRun() {
  return (
    <section className="rounded-lg border border-arena-edge bg-arena-panel px-3.5 py-3">
      {/* One column until the code has somewhere to go: the peek is a 400px rail, and
          below that width it stacks under the steps rather than shrinking to a gutter. */}
      <div className="grid grid-cols-[minmax(0,1fr)] gap-[26px] min-[940px]:grid-cols-[minmax(0,1fr)_400px]">
        <div className="min-w-0">
          <p className="type-label mb-2 text-[10.5px] text-arena-dim">No bots yet</p>
          <h1 className="type-display mb-5 max-w-[34ch] text-[21px] text-arena-text">
            Write a bot in C#. It compiles to WebAssembly and fights on a
            deterministic grid.
          </h1>
          <ol className="grid max-w-[640px] gap-4">
            {STEPS.map((step, index) => (
              <li
                key={step.title}
                className="grid grid-cols-[26px_minmax(0,1fr)] items-start gap-3.5"
              >
                <span
                  className={clsx(
                    'tabular grid h-6 w-6 place-items-center rounded-full border font-mono text-[11px]',
                    // The waiting step is brighter because it is the live one, not
                    // because it is important — the others are done or ahead of you.
                    step.waiting
                      ? 'border-arena-text text-arena-text'
                      : 'border-arena-edge text-arena-dim',
                  )}
                >
                  {/* The number is read aloud rather than hidden: a grid `li` loses its
                      list-item role in the accessibility tree, so the marker the browser
                      would have spoken is not there to fall back on. */}
                  {index + 1}
                </span>
                <div className="min-w-0">
                  <h2 className="text-[14px] font-semibold text-arena-text">
                    {step.title}
                  </h2>
                  <pre className="mt-[7px] overflow-x-auto rounded border border-arena-edge bg-arena-bg px-[13px] py-[11px] font-mono text-[11.5px] leading-[1.75] text-arena-text">
                    {step.commands.map((command) => (
                      <span key={command} className="block">
                        <span className="text-arena-dim">$</span> {command}
                      </span>
                    ))}
                  </pre>
                  <p className="mt-[7px] text-[12.5px] text-arena-dim">
                    {step.note}
                  </p>
                  {step.waiting && <WaitingLine />}
                </div>
              </li>
            ))}
          </ol>
        </div>
        <StarterPeek />
      </div>
    </section>
  );
}

/**
 * The promise that the page turns itself over.
 *
 * It is kept by `useMyBots`, which polls while the account has no bots and stops the
 * moment it has one — so a submission made in a terminal in another window replaces this
 * screen with the garage, without a reload. Saying it in the copy and then requiring F5
 * would be worse than not saying it.
 */
function WaitingLine() {
  return (
    <p
      role="status"
      className="mt-[7px] flex items-center gap-[7px] text-[12.5px] text-arena-dim"
    >
      <span
        aria-hidden
        className="h-1.5 w-1.5 flex-none animate-pulse rounded-full bg-arena-ok motion-reduce:animate-none"
      />
      Waiting for your first submission — this page turns over on its own.
    </p>
  );
}

/**
 * The starter template, trimmed to what fits beside the steps.
 *
 * Every symbol in it is real: this is `templates/botarena-bot/BOTNAME.cs` with the
 * remembering removed, which is exactly what the note under it says was removed. Showing
 * the code is the argument for the game; a paragraph about C# is not.
 */
const STARTER = `public sealed class MyBot : IBot
{
  public BotAction Tick(BotContext context)
  {
    var enemy = context.VisibleEnemies.Count > 0 ? context.VisibleEnemies[0] : null;
    bool aligned = enemy is not null && (context.Position.X == enemy.Position.X
      || context.Position.Y == enemy.Position.Y);
    if (aligned && context.CanShoot)
      return Actions.Shoot();

    if (context.IsWallAhead())
      return context.Random.NextBool() ? Actions.TurnLeft() : Actions.TurnRight();
    return Actions.MoveForward();
  }
}`;

const CSHARP_KEYWORDS =
  /\b(public|sealed|private|class|var|bool|int|if|return|is|not|null|new|true|false)\b/g;

/**
 * Keywords, separated by a dimmer ink and nothing else.
 *
 * Deliberately not a syntax highlighter. In this palette a saturated colour means a
 * player picked it, so spending four hues on `public`, `var` and `return` would leave a
 * bot's accent — the only colour on the site that carries meaning — reading as
 * decoration. Weight and value do the same job for free.
 */
function dimKeywords(source: string) {
  const parts: React.ReactNode[] = [];
  let cut = 0;
  for (const match of source.matchAll(CSHARP_KEYWORDS)) {
    const at = match.index ?? 0;
    if (at > cut) parts.push(source.slice(cut, at));
    parts.push(
      <span key={at} className="text-arena-dim">
        {match[0]}
      </span>,
    );
    cut = at + match[0].length;
  }
  parts.push(source.slice(cut));
  return parts;
}

function StarterPeek() {
  return (
    <div className="min-w-0 min-[940px]:border-l min-[940px]:border-arena-edge min-[940px]:pl-[22px]">
      <p className="type-label mb-2 text-[10.5px] text-arena-dim">
        What you actually write
      </p>
      <pre className="overflow-x-auto rounded border border-arena-edge bg-arena-bg px-[13px] py-[11px] font-mono text-[11.5px] leading-[1.75] text-arena-text">
        {dimKeywords(STARTER)}
      </pre>
      <p className="mt-[11px] text-[12.5px] text-arena-dim">
        The real template is a little longer — it keeps a field between ticks,
        because one instance plays the whole match and remembers what it has
        seen.
      </p>
      <p className="mt-[11px] text-[12.5px] text-arena-dim">
        Syntax colour is deliberately absent: colour here means a player chose
        it, so code separates by weight instead.
      </p>
    </div>
  );
}
