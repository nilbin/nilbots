# Bastion Mobilize policy pass

Budget: one balance pass, with no opponent-result feedback before the source
was frozen.

The pass adds only contract-gated remobilization behavior:

1. Record the active Frontline position when this life starts Anchor.
2. If the same life is now a turret and the authoritative active position has
   changed, resolve a `mobilize` route from the contract and submit its current
   legality-catalog action ID/code.
3. After completion, continue the existing mobile objective doctrine.

The trigger runs before turret fire so continuous enemy visibility cannot keep
an obsolete post permanently rooted. If the action or route is absent, the
baseline turret policy is unchanged.

The first WASM smoke (`bastion` team 0 versus `adapter`, seed `104729`) verified
three Anchor completions and three Mobilize completions. Each completion kept
the exact actor ID. Health changed `4 -> 3`, `5 -> 3`, and `5 -> 3` according
to the declared cap, and `transform` was unavailable to each mobilized life,
proving the one-way anti-cycle rule.
