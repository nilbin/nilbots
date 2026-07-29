# Match-dynamics forensics — the pendulum (2026-07-29)

Agent-produced forensics over all 810 verified replays of
`frontline-classes-wave-2-movement-factorial-v1(-holdout)`, commissioned
against the owner verdict "combats tend to be a bit repetitive / games
feel a bit dull" (quantified by the blind viewing pass at mean fun 2.75,
no 5s). Viewer time base: 5 ticks/s at 1×, so a 500-tick match is 100
seconds.

**Headline: the game is a mean-reverting pendulum.**
`P(the leading side pushes further) = 0.350` (n = 4,282 frontline
transitions), decaying with depth (0.435 at ±1, 0.255/0.164 at ±2). A
driftless random walk would have breached almost every match; instead
68% reach the cap and a capped match makes ~9.6 advances ending 0.8
positions from centre. The final 45% of a capped match is statistically
indistinguishable from any other slice.

## The two structural causes

1. **The objective walks toward the loser's spawn.** Reinforcement
   transit (spawn → standing on the active objective) is 4 ticks when
   trailing by 2 positions and 20 ticks when leading by 2 — a 5×
   penalty for winning.
2. **Death carries no persistent cost.** Free, automatic, full-health
   return in 18 (prime) / 30 (child) ticks. Median death → back-on-
   objective downtime is 35 ticks; the whole match runs on a ~33-tick
   metronome (respawn 18 + transit 12 ≈ capture 15 + pause 5 ≈ measured
   advance-reversal latency 33).

No counterweight exists: capture progress is fully reversible (34.7% of
all progress earned is destroyed by decay; 48% of sole-presence ticks
are wasted), contest nulls for free (83.2% of contested ticks are 1v1 —
one 1-HP body suffices; 40.6% of sole ticks have an enemy in the
doorway declining to step in), kills don't convert (52% of kills leave
an enemy near the objective and are worth +2pp of advance probability;
a clearing kill buys exactly one capture before the respawn reverses
it), and the Bulwark's identity verb has objective weight 0 (turret
bodies contributed 201 objective body-ticks out of 4.1 million;
transform+mobilize are 0.13% of all decisions).

## What repeats, quantified

- Combat itself is fine: median episode 9 ticks (1.8 s), one every
  ~7 s, 83% end in a kill, shot hit rate 45.6%.
- The match uses ~15% of the map (median 35 effective tiles); the
  top-10 damage tiles carry 48–59% of all damage; 39.8% of body-ticks
  stand on the active objective.
- **The staring contest: 22.1% of all viewing time is bodies within 3
  tiles of each other doing no damage** (82.6% of close-contact ticks
  are damage-free).
- Median sole-presence run is 4 ticks vs the 15 needed (8.0% of runs
  reach 15); 73.9% of incomplete runs die to contest.
- 38.5% of capped matches contain a ≥50-tick frozen scoreboard (p90
  163 ticks); 22.7% contain an exact whole-state limit cycle ≥50 ticks
  (worst observed: a 6-tick cycle sustained for 375 ticks).
- Per-body traces: dwell fraction 0.637, position autocorrelation 0.53
  with argmax lag 2 in 87.5% of traces — the two-beat dance.
- Openings are deterministic in practice: same-cell matches first
  diverge at median tick 20.

## Movement arms: relabel, not remove

facing-locked abolishes strafe-oscillation entirely (16.3% → 0.0% of
ticks) but replaces it with stand-and-spin (dwell 0.58→0.68, rotations
×4, idle 23.5%, lag-2 autocorr *rises*) and has the worst cap share
(0.804) while producing the most deaths. move-sets-facing is worst on
nearly every dullness metric. preserve-facing is least stale only
because it is most decisive. Episode length, gaps, and the score
plateau are identical across arms. **Movement coupling is a texture
knob, not a pacing knob** — consistent with the blind pass's flat fun
scores.

## The numbers-only disproof already in the corpus

`thin-fronts` (3-tile objectives) is the "make captures easier" arm in
geometric form: contest halved (39.5%→17.1%), captures +40%, frozen
scoreboards −73% — and the **worst cap share of the three maps (0.744)**
with the most concentrated damage. Cheaper captures on a mean-reverting
frontline raise the pendulum's frequency, not its amplitude.
Counterfactual replays agree: threshold 15→10 gives ×1.76 captures;
removing the 5-tick redeploy pause is ×1.05 (do not spend a change on
it); decay-only-under-enemy-sole is the best cheap partial ratchet
(×1.29 and halves wasted sole ticks).

## Ranked interventions (pass/fail pre-registered metrics included)

Structural — where the dullness lives:
- **S1 Territory ratchet** (captured positions sticky / banked score):
  pass if P(leader extends) 0.350→>0.50, |net displacement|/advance
  0.125→>0.40, cap share 0.68→<0.35, draws 9.3%→~0.
- **S2 Forward spawn/rally** (reinforcement distance independent of
  lead): mandatory companion to S1; pass if the 4→20 transit gradient
  flattens to ±3.
- **S3 Contest costs something** (majority to null, or majority keeps
  reduced gain): pass if contested share 32%→<20%, wasted sole ticks
  48%→<25%, frozen scoreboards 38.5%→<15%.
- **S4 Overtime escalation** instead of the flat cap: pass if cap share
  <0.15 and score-sign predictiveness at t=300 rises 0.659→>0.80.
- **S5 Map geometry** (objectives off the two corridors): treats visual
  sameness; pass if effective tiles 35→>60.

Numbers-only — texture and tempo underneath a ratchet, not a substitute:
- N1 threshold 15→8–10 (×1.76–2.10 captures; falsification test: if
  reversal rate stays ≈0.65, numbers-only is confirmed insufficient);
- N2 decay only under enemy sole presence (cheapest partial ratchet);
- N3 respawn 18→8–10 **only paired with S2** (alone it strengthens the
  rubber band); N4 TTK reduction (fight texture only); N5 redeploy
  pause — measured no-op.

Caveats: 37.9% wait share, the limit cycles, and doorway-declining
would all shrink with better bots; but P(leader extends)=0.350 and the
transit gradient are rules-and-geometry facts a stronger bot cannot
escape. Counterfactuals hold observed presence fixed (directional, not
predictive).

Full analysis pipeline in the session scratchpad (`agent-dynamics/`);
reproducible from the two registered factorial specs.
