# Cosmetic catalog and entitlements

Status: implemented for starter, achievement, and challenge grants. The
canonical catalog, append-oriented grant ledger, equip enforcement, and locked
UI ship together. Payment grants remain deliberately unimplemented.

## Ownership model

Keep four different facts separate:

1. A version-controlled catalog defines a cosmetic item.
2. A user account may be entitled to equip that item.
3. A bot chooses an equipped appearance from its owner's entitlements.
4. A match snapshots the equipped IDs into its participants and replay.

The resulting flow is:

`catalog item → account entitlement → bot appearance → match snapshot → replay`

Entitlements are account-owned. Accent, chassis, and projectile choices are
bot-owned. Replays are immutable historical records and never consult current
account ownership.

## Catalog

`cosmetics/catalog.json` is the server-readable, version-controlled authority
for stable keys:

- `bot-look:<id>`
- `projectile-look:<id>`
- Future presentation-only kinds such as `profile-title:<id>` or
  `entrance-effect:<id>`

Each catalog item records kind, stable ID, player-facing label, availability
(`starter` or `entitlement`), and an optional durable unlock source and hint.
Rendering details remain in their visual manifests. Automated tests require
every catalog ID and label to match exactly one runtime manifest. IDs are
immutable and never reused for different art; replacements receive a new ID.

The first gated items prove both non-payment paths:

- Lancer: achievement `first-successful-build`.
- Arc Spark: challenge `first-unranked-match`.
- Aureate Warden + Regent Lance: achievement `ranked-matches-100`.

All other current bot and projectile looks are starter-accessible. Local play
may preview the whole bundled catalog; the official server controls equipping.

Do not put prices, payment-provider product IDs, achievement evaluation logic,
or gameplay modifiers in visual manifests.

## Grant ledger

The database model is an append-oriented `EntitlementGrant`:

- `Id`
- `UserId`
- `EntitlementKey`
- `SourceKind`
- `SourceId`
- `GrantedAt`
- optional `RevokedAt`
- optional provenance metadata

Uniqueness on `(UserId, EntitlementKey, SourceKind, SourceId)` makes award and
webhook processing idempotent. Access exists while either the catalog marks an
item as starter-accessible or at least one active grant exists. Multiple grants
may independently authorize the same item.

Source domains own their own truth:

- Achievement completion records progress and emits an idempotent grant.
- Time-limited or authored challenges record their result and emit a grant.
- Promotions/admin tools emit auditable grants.
- A later commerce module may record orders and signed webhook events, then emit
  or revoke grants. Payment integration is explicitly deferred; no provider,
  checkout, price, or payment schema is part of the current projectile work.

Do not build a generic rules engine for achievements initially. Award from
explicit durable product events through one entitlement service.

## Equip and replay rules

Bot creation, `PUT /api/bots/{botId}/appearance`, and appearance fields on
version submission all verify catalog validity and active account entitlement.
`PUT` remains the normal authorization boundary for independent appearance
changes; compiling a bot is not required.

The garage lists locked items with their unlock hint and progress state.
Locked select options stay visible but disabled. The server, not the browser
or CLI, remains authoritative.

The 100-ranked-matches milestone counts completed ranked sets across all bots
and rulesets owned by an account. One user-facing ranked match is the complete
six-game mirrored set. A set between two bots owned by the same account counts
once. The award evaluates after set finalization, grants both companion
cosmetics on match 100, and backfills accounts that crossed the threshold
before the catalog items shipped.

Bot-look manifests may recommend an owned `defaultProjectile`. Selecting the
look applies that companion in the garage/editor, after which projectile choice
remains independent.

Matches snapshot accent, bot-look ID, and projectile-look ID. Viewing a replay
never rechecks entitlement. Revocation affects future equips and matches; it
does not rewrite historical replays. If the last grant for an equipped item is
revoked, the stored choice is not silently reset: bot submission and future
official match admission fail with the stable locked-appearance error until
the owner equips an owned item. This preserves the owner's explicit selection
and the audit trail while preventing newly admitted snapshots from using an
unowned item.

Cosmetics never alter collision, observations, actions, projectile paths,
timing, damage, matchmaking, ratings, or any other gameplay value.

## Payments and downloadable assets

Payments are a later milestone. When adopted, use hosted checkout and signed,
idempotent provider webhooks; nilbots must not handle card details. Refunds or
chargebacks revoke only the purchase grant, so another active achievement or
promotion grant may still authorize the item.

Client-visible cosmetic files cannot be secret or DRM-protected. Payment buys
the right to equip an item on the official service, not exclusive access to its
bytes.

The current self-contained viewer bundles the complete visual catalog. That is
acceptable for the starter set, but not for a large unlock/store catalog.
Before the catalog grows materially, online playback should use immutable
content-addressed assets and exported standalone viewers should embed only the
theme and participant cosmetics referenced by that replay.

## Delivery status

1. Starter projectile looks and bot appearance editing — done.
2. Canonical catalog, entitlement service, grant ledger, and locked UI — done.
3. Build, challenge, and 100-ranked-match achievement unlocks — done.
4. Expand achievements/challenges based on observed product use — later.
5. Payment-provider integration — later, as a separate commerce project.
