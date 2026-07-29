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
2. ~~**Deployable barricade**~~ — **killed on owner review, and the
   kill generalizes.** Under honest slot economics (a wall must consume
   a child slot: a dedicated wall slot varies topology by class against
   #153, and a free wall repeats the striker free-option mistake) the
   barricade is **strictly dominated by fabricating a child**: same
   slot, same action, same 3 HP, identical bolt-blocking (enemy bolts
   stop on any body) — while the child also shoots, moves, and carries
   objective weight 1. Its correct usage rate is zero; the adoption
   gate would kill it in the pilot. The owner's framing is the durable
   lesson: *a skill must not be a degenerate form of an existing skill*
   ("a turret with less HP and no attack"). Player-authored static
   cover only becomes viable if walls are ever additional matter
   outside the body economy — which is the rejected
   new-entity-kind/new-topology cost. Bodies are the game's cover.
   **SURGE replaces it as the fabricator's one new thing** (see the
   kit); the degeneracy objection that benched surge evaporates with
   the barricade gone.
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

## Current kit (owner-iterated, pending the DECISIONS entry)

Owner-directed rebuild from the three blind-validated fun primitives —
turret-feel, curve-feel, numbers-feel — with the counter-cycle traced
per edge and capped ("the factor can't be TOO weighing"). Bends become
shared grammar on mobile facing-aimed guns (never on specials).

- **Striker VOLLEY** (renamed from lance on owner review — the
  telegraphed 1-wide piercing line is dodged for free; width is what
  beats the covering number). Public windup, then simultaneous
  damage-1 bolts down three adjacent lanes. Tuned as zoning, not a
  wipe: it must *soften* striker-vs-fabricator toward the band, not
  deepen the game's most lopsided edge. Open question: striker also
  keeps the deepest curve envelope as heritage.
  **Volley is a CAST, not a stance (owner ruling).** A parked striker
  firing repeated fans is artillery — the bulwark's fantasy leaking
  into the striker's kit. The prototype implements it on stance
  machinery (the engine's only public-windup primitive), so gallery
  drivers must play it enter→fire-once→exit, never squatting; if the
  skill survives the screen, the adoption-grade mechanism is
  **auto-exit (owner-chosen)**: firing the fan automatically triggers
  the return transition (a new causal fact with its own reason code —
  the validator-teaching pattern #156 and automatic-activation already
  established), which is both smaller than a windup-bearing attack
  primitive and stronger than it — artillery squatting becomes
  impossible by rule, not by driver etiquette. Authors submit enter
  (aiming by facing) then shoot; no exit bookkeeping. Renderer note:
  the wide-arrow glyph is the skill's identity; the stance silhouette
  is transitional plumbing.
- **Bulwark AEGIS SHELL** (barrage benched on owner review, and the
  RPS trace agrees: a radial proximity burst punishes whoever stands
  CLOSE — fabricator bodies, the class bulwark must lose to — while
  the range-8 striker it exists to blunt never enters the radius).
  Reversible windup stance absorbing projectiles in its facing arc;
  slow or immobile; bounded tenure. Blanks poke, loses to flanks and
  multi-angle bodies. The staring-contest objection that benched
  shell was priced before the pendulum fix; with ratchet +
  contest-majority attacking passivity structurally, skills no longer
  carry the pacing burden.
  **Shield break (owner ruling — indefinite absorption is an
  off-switch for ranged combat; the probe's 121-absorb zero-damage
  match is the proof).** The shell absorbs a BUDGET of bolts (~3) and
  shatters on the last: forced auto-exit, per-absorb crack visuals,
  exit+reentry windups as the punish gap. **Facing locks on entry** —
  the protected quadrant is chosen when the shield rises and never
  tracks, so flanking stays real even 1v1 (weight 1 + tracking arc
  would be an invincible capturer). Counter-play becomes two-axis: go
  around it, or feed it three bolts to buy the window. Adoption-grade
  mechanism is SHARED with volley: one threshold-triggered automatic
  return transition (volley fires-count 1, shell absorbs-count N) —
  one primitive, one new causal fact. Gallery drivers play the budget
  by etiquette until then.
  **Deflection, not absorption (owner ruling).** The shield DEFLECTS:
  the incoming bolt dies on the arc and a new bolt launches from the
  shell along the reversed heading, owned by the bulwark's team (the
  ownership flip is what makes it lethal to the shooter and renders
  it in the deflector's colors). Fully public and dodgeable — a tempo
  tax on frontal poke, not auto-damage. Feeding the break now costs
  footwork: each of the ~3 shatter-buying bolts has a return to
  sidestep. Converts the stance from a null into a threat and gives
  the viewer the ricochet beat; still bounded by shield-break and the
  locked arc.
- **Fabricator FIVE SLOTS** (surge died with the treadmill critique:
  faster replacement of free bodies accelerates the exact loop the
  dynamics report convicted). The fabricator simply has more unit
  slots than any other class — the numbers class literally has
  numbers. Slot counts are contract data, not array shapes; this is a
  new topology profile for fabricator arms, deliberately amending
  #153's same-topology reading as the good asymmetry (the barricade
  was the bad one). Out-bodies the fortress arithmetically; each body
  stays the weakest in the game, so volleys and cadence still farm
  them.

**Cycle-magnitude band (pre-registration guard):** every cross-class
edge on the presumptive arm must land at |payoff| ∈ [0.15, 0.40] —
below-band is no cycle, above-band means the matchup screen decides
the game (today's +0.67 striker-vs-fabricator fails high). Public
windups on every skill are the deeper guard: a punishable commitment
keeps doctrine stronger than matchup.

Benched, pre-registered as fallbacks: SUPPRESSION BARRAGE, CHARGE,
DASH. Dead: barricade (dominated), surge (treadmill accelerant),
lance-as-line (free dodge), energy (#47/#48). Split stays parked for
a future swarm class.

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
