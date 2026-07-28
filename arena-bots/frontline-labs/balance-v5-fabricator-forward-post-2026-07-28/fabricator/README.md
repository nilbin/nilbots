# Fabricator

Fabricator treats a Ready child slot as a strategic investment rather than an
automatic button press. The Prime returns to its contract-declared protected
home pad only when the remaining match time can pay for the trip, fabrication
windup, the new body's trip back to the active objective, and a useful capture
window. It finishes an uncontested capture already in progress before leaving
when no ally can hold it.

Once active, bodies remain mobile. They deterministically assign the closest
mobile body to objective control and spread the others around traversable
perimeter tiles. Shared allied observations let those firing posts divide
visible targets, align straight shots, and cover the Prime while it returns
home for another child. If a future contract has no fabrication path, legal
replication is a conservative fallback; Anchor is intentionally declined
because this doctrine values mobile crossfire.

After an uncontested claim reaches half of its contract-provided capture
threshold and one mobile body remains on point, exactly one surplus body takes
the legal perimeter post with the shortest map path to the next objective.
This preserves the crossfire formation while giving a completed push a
forward staging body.

All counts, forms, transition timings, attack ranges, map geometry, home pads,
objective regions, and match limits come from `StartLife.Contract`. Every
decision resolves its current numeric action code and legal typed argument
from the per-tick action catalog. Missing optional capabilities fall back to a
legal wait or another bounded catalog action.

Build the bot with:

```bash
nilbots build .
```
