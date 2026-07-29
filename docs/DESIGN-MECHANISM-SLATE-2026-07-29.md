# Mechanism slate — widening the search space (2026-07-29)

Agent-produced design exploration commissioned against the owner
direction "games feel a bit dull… do we need to add more mechanisms to
the game/classes? To make the diversity and search space larger", plus
owner rulings received on review: **energy is not a candidate**
(tried and closed — DECISIONS #47/#48: "taxes aggression as much as
camping", stalemate fortresses), **curved shots deliver watchable
value** (blind-validated: both fun-4 games in the viewing pass featured
the high-bend striker), **bend exclusivity is in question**, and —
ruling received after the slate landed — **"I like turrets because
they add something visually, it's essentially a cooldown which adds
depth and it's overall just good fun. I think we need more such
skills. Not necessarily static, just skills."**

The slate's structural diagnosis (three contract facts): the scoring
channel has a free counter (contest nulls sole presence at the cost of
standing nearby); kills do not convert (18-tick full-health respawn vs
15-tick capture); nothing in a match ratchets. Striker dominance is
structural: its exclusive verb is a free option on an action it takes
anyway, while bulwark and fabricator pay for their verbs in scoring
currency (turret = objective weight 0; fabricate = a combat action).

## Ranked slate (as amended by owner rulings)

1. ~~Energy budget~~ — **struck by owner ruling** (#47/#48 precedent:
   punishes pushing, rewards camping; dormant fields stay dormant).
2. **Deployable barricade** — a fabricatable 3-HP wall body on the
   existing bounded-fabrication machinery (fourth slot, wait-only form,
   objective weight 0, facing-relative placement; objective tiles must
   be transition/placement-forbidden). Creates eviction pressure, is
   the best spectator mechanic on the slate, and manufactures organic
   demand for bends (cover deletes the straight lane). Zero
   observation-schema change. Cost S/M. Risks: turtling, objective
   denial — both have pre-registered detector metrics.
3. **Public stances / class skills** — generalize Anchor's machinery
   (reversible windup-gated same-life transitions; `ObservedFormTransition`
   is already public to enemies with start/completion ticks) into a
   per-class skill kit: e.g. striker overwatch (windup 2 → range 10,
   damage 2, immobile, reversible), fabricator field-works (windup 1 →
   faster/wider fabrication, slower gun). **This is the direct
   generalization of what the owner likes about turrets** — a visible
   shape change, a countdown an opponent can read and punish, a
   cooldown-like cycle — and it is the *public* commitment mechanic the
   private bend structurally cannot be (skill-shot forensics: the
   bend's mixup value is capped at 1/3 by a covering number invariant
   to envelope size, and 80% of bends render too short to read).
   Data-only on existing types. Design guard: every stance must forfeit
   objective weight or mobility while active (the turret bargain), stay
   reversible, and keep bounded tenure (liveness).
4. **Directional armor** (front 1 / flank 1 / rear 2) — makes facing a
   defended asset, prices strafing economically, rewards flanks (the
   first mechanically-paid team play), and gives facing-locked's
   rotation share meaning. Zero schema change (Facing and Heading are
   already public). Turrets exempt. Cost M.
5. **Dual-live frontline** (two active positions) — the first real
   allocation problem; the only slate item that changes the observation
   tensor shape. Gate behind static/exact analysis of two-front
   geometry. Cost M/L.
6. **Projectile interception** — goal-3 trap (useless at T2–T4,
   possibly dominant at T7–T8); collision-profile change; measure with
   a purpose-built sentinel before believing anything.
7. **Sensor doctrine** — cheap audible-kind append is worth taking
   (fabrication and form transitions audible at radius 8, which makes
   skills *audible* commitments too); per-class perception is deferred —
   adding hidden information while the existing hidden information
   (the bend) is measured inert inverts the diagnosis.
8. **Skirmisher (Air layer)** — the enum exists but it is a genuine new
   engine capability with six semantics to author, and the classic
   dominates-low-play trap. Later.

Explicitly rejected: attach/symbiote (occupancy invariant, illegible),
stealth as a headline (wrong order of operations), salvage pickups (a
fourth entity kind to deliver what two respawn numbers can), destructible
static walls (map fingerprint/time-varying observation cascade;
barricades deliver ~80% as actors), and **more shot-program
parameters** (proven worthless: 9, 17, and 217 programs solve to the
same value).

## Design guards (owner rulings)

- **No skill may reintroduce facing-decoupled movement.** Strafe is dead
  (DECISIONS #159) and facing-locked's balance win *is* the dodge tax —
  sidestepping costs a rotation. Any skill granting lateral or
  facing-free movement (dash-sideways, blink, dodge-step) silently
  refunds that tax and unravels the coupling. Mobility skills must
  commit along facing (charge is the template: less freedom than
  normal movement, not more).
- Skills are priced in public windups and tenure, never energy
  (#47/#48) and never hidden state.
- Every stance pays in the scoring currency deliberately or keeps
  objective weight 1 deliberately — the turret's accidental 0.13%
  usage is the cautionary precedent either way.

## Process requirements

Every piloted mechanism enters `balance/frontline-ablation-debt-v1.json`
at authoring time with its factors registered separately (the barricade
bundles slot-count + form + route and will be unattributable
otherwise). The dynamics report's pass/fail metrics are the
pre-registration targets. The numbers-only lethality/respawn arm runs
as the control factor in the same factorial (steelman: the largest
balance effect ever measured here came from a non-mechanism change).
