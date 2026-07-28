# Frontline Labs competence v1

This retained four-bot cohort separates basic tactical competence from each
bot's existing doctrine. Adapter, Bastion, Fabricator, and Pressure keep their
baseline-v2 policy and receive the same contract-generic immediate priorities:

1. submit an available Fabricate action for the first legal Ready slot;
2. leave a hostile projectile path that reaches the body on its next advance,
   while preserving active-objective proximity;
3. take a visible, range-valid, wall-clear direct shot.

The pass deliberately adds no focus fire, shared body roles, curved-shot
traps, forced-movement strategy, transform policy, or opponent adaptation.
Single-projectile evasion is mechanical hygiene rather than strategic skill;
this is a mechanically credible floor cohort, not proof of capable or expert
play.

Every entrant retains its source, local project, controlled SDK 0.10.3 WASM,
artifact hash, and original doctrine/DX notes. `FrontlineCompetence.cs` is
copied byte-identically into each project because the controlled builder only
admits source files inside the submitted project root; the root copy is the
canonical review copy.

## Screen

Seed `104729` supplies one mirrored round robin: six unordered pairs in both
participant assignments, 12 verified WASM matches per arm. This intentionally
uses fewer tournament iterations and retains every candidate instead of
crowning or deleting down to one champion.

| Arm | Breach | MaxTicks | Attacks/100t | Damage/100t |
| --- | ---: | ---: | ---: | ---: |
| hosted | 2 | 10 | 47.7 | 16.8 |
| remote explicit fabrication | 3 | 9 | 49.5 | 18.0 |
| late gain `300:2` | 2 | 10 | 47.2 | 16.9 |
| net objective control | 2 | 10 | 47.8 | 16.7 |

Remote fabrication reduces aggregate Ready share from 10.3% to 3.6% and
raises average post-first-unlock bodies from 1.71 to 1.86. It passes the causal
population screen but does not fix match completion. Net control increases
pushes from 63 to 74 without increasing breaches. Full interpretation lives in
`docs/FRONTLINE-LABS-MEASUREMENT.md` and the V9 section of
`../BALANCE-ITERATION-2026-07-28.md`.

The complete local replay trees are intentionally ignored by Git. Compact
identity and result facts are preserved in `screen.json`; regenerate full
evidence from the frozen artifacts and seed when needed.
