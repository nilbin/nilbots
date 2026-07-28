# Adapter

Adapter does not commit to a scripted opening. Every active body independently
reassesses the current territorial score, objective claim and progress, allied
mobile coverage, visible enemies, remaining time, and its live action catalog.

Its default job is to put mobile weight on the active objective. When an ally
already has secure control, another mobile body screens from a nearby legal
support tile instead of stacking for no extra capture speed. Visible enemies
interrupt navigation only for a legal clear shot, while a one-hit body may
sidestep a visible lethal projectile unless abandoning the objective would
hand over an active enemy claim.

Adapter also changes shape when the contract and situation justify it:

- a Ready allied slot may be fabricated from a legal target;
- Split is reserved for a visible force deficit, a comeback, an objective
  emergency, or a late push;
- a mobile child may become a zero-weight ranged support form only when
  another allied mobile is already covering the objective;
- absolute-heading and programmed mobile shots are used only when their
  current typed constraints admit the selected heading or aim offset.

All form weights, attack ranges, objective regions, scores, topology, action
codes, directions, form targets, and unit targets come from `StartLife.Contract`
or the current legality mask. Optional Labs capabilities fall away cleanly when
absent. Decisions are deterministic and use no clock, file, network, or shared
cross-life state.
