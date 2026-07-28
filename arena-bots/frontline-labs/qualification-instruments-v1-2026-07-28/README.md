# Frontline qualification instruments v1

This cohort begins the current source-retained calibration and potential
system-opponent population. It is not a tournament and has no champion.

`house-apprentice` is the generated generic-actor starter after one mechanical
competency repair: after a projectile dodge, a body remembers its vacated tile
for one further tick so objective pathing cannot immediately walk it back into
the same shot. It passes the complete cumulative T2 profile. Its T3 boundary
has not been measured because the immutable T3 profile does not exist yet.

Every body life receives a fresh bot instance, so the retained memory is local
to one life. The policy remains intentionally narrow: no curved-shot planning,
transformations, body roles, team tactics, opponent model, or long-horizon
commitment.

The full 84 MB qualification replay tree is retained locally at
`evidence/house-apprentice-t2-v1/` and intentionally ignored by Git. The
tracked `evidence-manifest.json` preserves every relative path and byte hash.
Before this cohort is used as durable release evidence, that replay tree must
be copied to the project artifact store under the same content-addressed
manifest.

