# Bastion

Balance v8 gives Bastion one candidate-aware policy pass for the declared
Mobilize action.

Bastion uses the first compatible fabricated child as its dedicated Anchor.
That child travels to a legal tile immediately beside the active objective,
preferring the forward edge toward the next objective, and becomes a turret
there. It fires down visible lanes to make the capture area costly to enter.
When the active objective advances away from the position that this life
fortified, the turret uses the contract-declared `mobilize` route and returns
to mobile objective play. The same life cannot Anchor again, so Mobilize
cannot be used as a healing cycle.

The Prime remains mobile. It contests objectives, returns to its contract-bound
home region whenever a compatible child slot is Ready, fabricates, and then
resumes the advance. Any other fabricated bodies also stay mobile, so the
doctrine keeps capture weight in play instead of mistaking static firepower for
territorial progress.

The implementation reads objective regions, advance direction, home-region
bindings, lifecycle slots, transition placement tags, and action constraints
from the match contract. It resolves every numeric action code from the
current legality catalog and simply continues mobile objective play when
Fabricate, Transform, Mobilize, or turret fire is absent.

Build the submission-equivalent artifact with:

```bash
nilbots build .
```
