# Balance Lab specifications

This directory contains pre-registered, immutable Nilbots Balance Lab inputs.
Generated bots, replays, and reports do not belong here; a spec names their
source identities and the runner freezes them into its explicit output
directory.

The schema and architecture are documented in
[`docs/NILBOTS-BALANCE-LAB.md`](../docs/NILBOTS-BALANCE-LAB.md).

Run a spec with:

```bash
python3 scripts/balance-lab-drive.py \
  --spec balance/<experiment>.json \
  --output /tmp/nilbots-balance/<experiment>
```

Use `--dry-run` to validate and freeze a plan without matches, and `--resume`
to revalidate an existing immutable output before completing or regenerating
its report. The checked-in Frontline spec is deliberately an unqualified
infrastructure smoke and has no holdout.

Candidate contract blocks are generated from the engine rather than copied by
hand:

```bash
python3 scripts/frontline-balance-candidates.py \
  --spec balance/frontline-duel-progression-v1.json \
  --cli src/BotArena.Cli/bin/Release/net10.0/botarena
```

Add `--write` to replace drifted blocks. Review and commit that diff like any
other gameplay-contract change.

A real voting study creates a private reveal plus public commitment with
`scripts/balance-holdout.py`. Never check holdout seeds into its pre-run spec.
Open isolation promises live in `frontline-ablation-debt-v1.json`.
