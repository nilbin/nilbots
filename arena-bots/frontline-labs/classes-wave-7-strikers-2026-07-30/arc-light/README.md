# arc-light

A Frontline Labs **striker**, class doctrine *flank-and-collapse skirmisher*.
Wave-7 revision of the wave-6 entrant of the same name. The coordination layer
is unchanged; what is new is that the fan is a weapon again.

Qualified **T4** on `frontline-qualification-5`
(`frontline-duel-depth-union-t4-v1`), `balanceEvidenceEligible: true`.

## What the fan is for now

The volley was re-armed and three of its numbers moved: a fan bolt removes **2**
health where the mobile gun removes 1, the stance enters in **1** tick instead of
2, the fan profile's cooldown dropped to the 1-tick floor so the stance no longer
taxes the gun, and frequency moved onto an **8-tick cooldown on the entry route**,
scoped to the unit slot and published on `self.routeCooldowns`. arc-light reads
every one of those from the contract and none of them from a rule card, so the
same source that plays this arm plays the arm before it with the arm-before-it's
behaviour.

- **The break-even is arithmetic in DAMAGE, not in bodies.** The stance costs its
  declared cycle; the gun would have fired `ceil(cycle / cadence)` bolts of its
  own declared damage in that window; a diverging fan lands at most one bolt per
  body. So the fan must connect with `ceil(forgone damage / fan bolt damage)`
  bodies — **one**, on this arm, where wave 6 correctly computed two. That single
  re-derived integer is the wave: it is the difference between a doctrine that
  casts 0.7 times a match and one that casts 14.
- **The fan executes.** One bolt removes two health, so any body at or below two
  health dies to it — a wounded striker, and a fabricator prime at full health,
  which is also the supply line. Removing a body is not the same purchase as
  removing health from one, so a forecast kill is the only thing in this doctrine
  allowed to pre-empt an available aimed bolt.
- **The collapse is two hits, not one.** The fan no longer taxes the gun, so the
  bolt after the automatic return arrives about two ticks later: 2 from the fan
  plus 1 from the gun is exactly a striker. Against a three-health chassis the
  cast is an opener, not a substitute.
- **The entry is a charge, and the charge is published.** The clock survives this
  body's death, so it is asked rather than remembered — a life born inside the
  window has no history to infer it from. The same fact prices three decisions:
  never request a held route, never spend a facing-locked rotation lining up a
  cast the clock refuses, and never enter with a hot gun or leave a stance
  unfired, because both throw a whole eight-tick charge away.
- **The fan does not feed a shell.** A guard deflects contacts arriving inside
  its facing quadrant and returns them carrying the damage class of the bolt it
  caught — so a fan into a shell's face is the opposition firing arc-light's own
  two-damage bolt back at a three-health chassis. A body that would deflect is
  not a forecast hit and a denial lane stops at one. Measured, this rule is worth
  the entire bulwark leg: without it the same build is breached at tick 69.

Everything the wave-6 revision did, it still does: interception aiming against a
facing-locked target's ray, the declared launch-offset envelope, a bend spent
only on a hard interception, the supply form named by the fabrication catalog as
the priority target, the live territory hold read rather than inferred, and the
`ArcTraffic` precedence layer that stops two of its own bodies claiming one tile.

Nothing above is conditioned on an arm name. The same artifact plays the kit-off
cells (where no stance route exists and the stance code declines), both bend
envelopes, both ground arms, contracts that declare no route cooldown at all, and
the classless duel-depth qualification profile — where it finds an anchor route
instead, fabricates from the pad, and never touches a volley.

## Headline results

Wave game `swell` (`--pendulum keel --skills kit --bend universal --aim offset
--stance-ground open --cooldown ticking --volley salvo`), facing-locked,
`--five-slots wane` in the fabricator cell. 16 seeds a leg; opponents are the
wave-6 rebuilt artifacts. Volley entries are per match.

| leg | wave-6 self | this build |
| --- | --- | --- |
| striker mirror vs my own wave 6 (both assignments) | 0-0-16 mirror draw, **0.00 casts** | **32-0-0**, +28.44 / +30.00 |
| `striker-vs-striker` vs still-water | 0-16-0, −30.00 | **16-0-0, +30.00** |
| `striker-vs-striker` vs vector-edge | 0-16-0, −30.00 | **10-6-0, +2.50** |
| `fabricator-vs-striker` vs spark-line | 0-16-0, −30.00 | **16-0-0, +30.00 in 66 ticks** |
| `fabricator-vs-striker` vs ledger-fly | 0-16-0, −30.00 | 0-16-0, −28.56 |
| `bulwark-vs-striker` vs gate-stone | 0-16-0, −30.00 (breached, 179t) | 2-14-0, −21.12 |
| `bulwark-vs-striker` vs iron-root | 0-16-0, −26.31 | 0-16-0, −29.75 |
| `bulwark-vs-striker` vs march-wall | **12-4-0, +14.38** | 3-13-0, −9.12 |

Across the seven cross-class legs: **12-100-0 → 47-65-0**. The march-wall leg is a
real regression and is reported as one in `DX.md`, with the leave-one-out showing
that removing any shipped rule makes it worse rather than better.

Fan usage across those legs: **0.73 → 14.39 stance entries per match**, with zero
blocked entry requests and zero unfired exits.

## Building

```bash
nilbots build <this directory> --no-cache
nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm --suite frontline-qualification-5 --out evidence/t4
```
