# Cosmetic catalog and entitlements

Status: product/architecture design. Bot and projectile selection exist; every
shipped option is currently starter-accessible. Achievement, challenge, and
payment grant systems are not implemented yet.

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

Before the first gated item ships, add one server-readable, version-controlled
catalog as the authority for stable keys:

- `bot-look:<id>`
- `projectile-look:<id>`
- Future presentation-only kinds such as `profile-title:<id>` or
  `entrance-effect:<id>`

Each catalog item records kind, stable ID, player-facing label, availability
(`starter` or `entitlement`), preview metadata, and an optional unlock hint.
Rendering details remain in their visual manifests. IDs are immutable and
never reused for different art; replacements receive a new ID.

Do not put prices, payment-provider product IDs, achievement evaluation logic,
or gameplay modifiers in visual manifests.

## Grant ledger

The future database model is an append-oriented `EntitlementGrant`:

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
- A later commerce module records orders and signed webhook events, then emits
  or revokes grants. Payment integration is explicitly deferred; no provider,
  checkout, price, or payment schema is part of the current projectile work.

Do not build a generic rules engine for achievements initially. Award from
explicit durable product events through one entitlement service.

## Equip and replay rules

`PUT /api/bots/{botId}/appearance` is the authorization boundary. It verifies
bot ownership, catalog validity, and—once gating ships—account entitlement in
one transaction. Code submission may synchronize local manifest appearance
through the same service, but compiling a bot is not required to change its
appearance.

The UI should list starter and owned items normally. Locked items may remain
visible with an unlock hint, but cannot be submitted as equipped. The server,
not the browser or CLI, is authoritative. Offline local play may preview every
bundled cosmetic; official server use requires entitlement.

Matches snapshot accent, bot-look ID, and projectile-look ID. Viewing a replay
never rechecks entitlement. Revocation affects future equips and matches; it
does not rewrite historical replays. If the last grant for an equipped item is
revoked, return affected bots to a starter item transactionally or require an
appearance change before their next official match—choose that policy when the
first revocable source ships.

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

## Delivery order

1. Ship starter projectile looks and bot appearance editing.
2. Add the canonical catalog, entitlement service, grant ledger, and locked UI
   state.
3. Prove the system with one achievement unlock and one challenge unlock.
4. Expand achievements/challenges based on product use.
5. Consider payment-provider integration later as a separate commerce project.
