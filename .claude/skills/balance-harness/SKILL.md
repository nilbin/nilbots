---
name: balance-harness
description: A/B-test a game-rules candidate against the current rules using the frozen champion population and fixed seeds, then make a data-driven ship/no-ship call. Use when evaluating any gameplay change (new mechanic, tuning numbers, new maps) before pinning a rules version.
---

# Balance harness: rules changes prove themselves

Methodology lives in docs/GAME-DESIGN.md; the instrument is
`scripts/balance-eval.py` + the CLI's `--rules` flag. History: rules 0.2
(seed-spawn variation), 0.3 (range cap + lane-safe spawns), and 0.4 (zone
control — shipped on draws 37%→12% despite median length doubling, the
recorded trade) all shipped on this data; the energy candidate did not
(DECISIONS #47). When a candidate changes what "a good game" looks like
(hill's Domination endings replaced Eliminations), report against the
standard criteria AND say which criterion the candidate redefines — don't
quietly bend the gate.

## 1. Implement the candidate behind GameRules flags

Gameplay values live ONLY in `GameRules` (CLAUDE.md invariant). Add the
candidate as flags/values defaulting to off, wire an experiment entry into
`CliSupport.ResolveRules` with a visibly non-official version string
(`0.X-exp-<name>` — it flows into replays and seed derivation). Rules 0.1/0.2
behavior must stay bit-identical: run the full test suite.

## 2. Run the comparison

```bash
python3 scripts/balance-eval.py                       # champions, 0.1 vs 0.2 vs energy
python3 scripts/balance-eval.py --rulesets 0.2,<exp> \
  --bots extra=var/artifacts/<hash>.wasm ...          # add current-gen bots
```

Fixed seeds (default 101,202,303) make arms comparable. Champions are frozen
and rules-unaware — good for mechanical effects, blind to strategy adaptation.
For adaptation effects (e.g. resource management), run an agent-arena
tournament under `--rules <exp>` so the bots are written FOR the candidate.
During source iteration, pass project directories plus `--runtime in-process`
to avoid a NativeAOT compile for every edit; the CLI still runs prebuilt
champion `.wasm` files in the sandbox in this mixed mode. Re-run at least one
representative set with the default WASM runtime before accepting results.

## 3. Ship/no-ship

Ship only if, versus current rules: **draw rate down, median end tick down,
elimination share up, and no diversity collapse** (distinct archetypes still
finish in different orders / with different action mixes). One arm failing =
no ship; keep the candidate behind its `--rules` flag and record why.

## 4. Pinning a version

Winner: add `GameRules.V0_X`, point `GameRules.Current` + 
`BotArenaVersions.GameRulesVersion` at it, update the site rules card +
template README, add the DECISIONS entry with the numbers table, and paste the
table into GAME-DESIGN's data section. Loser: DECISIONS entry with the numbers
and what population would be needed to re-test.
