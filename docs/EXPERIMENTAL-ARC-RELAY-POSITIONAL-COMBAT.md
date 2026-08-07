# The Positional Combat Remake — Goal (owner-set 2026-08-11)

**Every fight ends, and every death explains itself.**

Combat outcomes are a pure, readable function of the board — facing, numbers,
interposition, level, terrain — never of execution micro. A spectator pausing
any death can name its causes from the frame ("rear-arc, 2v1, cornered"); a
player losing any fight can name the authored judgment that lost it ("I
engaged without isolation"). The dodge duel, and with it every shuffle, dance,
and standoff, becomes unrepresentable rather than discouraged.

## The layering it enforces

- **Sheets own all judgment** — when to engage, when to break off, where to
  fight, who escorts whom. Play style is entry and exit conditions; courage is
  a number the player wrote.
- **The mind owns few, convergent verbs** — move, patrol, attack (declare →
  windup cone → first-body-in-the-way resolve), withdraw. Every verb reaches a
  fixed point; hesitation is not a state the executor can express.
- **The rules own resolution** — each combat beat, someone loses health or
  ground. Speed, reach, and cooldown define classes; facing and interposition
  define drama.

## Acceptance — measured, not felt

1. A 24-seed battery with **zero felt-degeneracy bar trips** on either sheet
   (statues, dances, parked, at current calibrations), yielding the first
   gallery that needs no diagnostic label.
2. **No engagement exceeds its authored duration**: every commit ends in a
   kill, a withdrawal, or a timeout exit — verified by the doctrine report,
   with no unresolved-contact streaks.
3. **Sheets carry over untouched.** hunter-v1 and wellwright-v1 run unmodified
   on the new ruleset; only executor combat machinery is replaced — and net
   code shrinks.
4. Determinism, replay hashes, and the mint discipline hold:
   `arc-relay-ambush-11` behind `GameRules` flags, frozen-beside, judged by
   battery + owner replay review as the final gate.

## Not goals

Balance between the two sheets (tuning comes after the model proves out);
ranged area-denial (v2, only if the melee-beat core feels right); any change
to the strategy layer — the pulse race, doctrines, veterancy, and vision rules
are the parts already working, and they are load-bearing.

## Mechanic sketch (v1, subject to the mint's tests)

Attack beat: **declare → windup → resolve.**

1. **Declare.** A committed attacker locks its heading AND names the body it
   is shooting at; the threatened cone becomes public state (the viewer
   draws it; the victim's mind sees it). The cone is the REAL filled wedge
   (DECISIONS #213): every wall-reachable tile within ±45° of the heading
   out to the gun's Chebyshev reach. A lit tile is exactly a hittable tile.
   The strike LOCKS the named body when that body stands inside the wedge at
   declare — the lock is the mind's target and nothing else (DECISIONS #222
   and its owner correction): not the nearest body, not the first enemy on
   the ray, never a friendly, never a substitute. Naming nobody, or naming a
   body outside the wedge, locks nothing.
2. **Windup** (1 tick — owner tuning; movement precedes combat on the
   resolve tick, so this is exactly one honest move). Counterplay is
   positional only: leave the wedge, break the shooter's sight, or interpose
   a body. The lock follows its target anywhere INSIDE the frozen wedge; it
   cancels when that body dies, crosses the wedge boundary, or leaves the
   shooter's line of sight. The declarer is rooted: commanding a move
   abandons its own declare (DECISIONS #221).
3. **Resolve.** Single-target: the bolt is delivered along the canonical
   strike line from the frozen origin to the locked body, and **the first
   body on that line eats the hit** (bodyguarding, screening, and
   wrong-victim scrums all emerge from this clause — interposition is a
   delivery rule, never a lock rule). An unlocked strike fires the
   theatrical whiff down the centre. Damage = class base × facing multiplier
   (front ×1, rear ×2 — existing rule) × level track. Whiff still pays the
   cooldown.

Class identity comes from the windup/reach/cooldown triangle. Single-target
only in v1; any area sweep is a future deliberate class trait, hitting
friend and foe alike.
