# Selling entitlements

Research, not a decision. Written to be argued with; nothing here is implemented.

Not tax advice either. The thresholds below come from public guidance and set the *shape*
of the problem; Skatteverket decides the specifics. That said, the shape is smaller than it
looks — an earlier draft of this file implied a company was needed, which is wrong, and the
correction is in "You do not need to register anything to start".

## The constraint that decides everything is Apple, not the payment provider

Cosmetics are digital content unlocked inside the app, which is the exact case
[App Review Guideline 3.1.1](https://developer.apple.com/app-store/review/guidelines/)
names:

> If you want to unlock features or functionality within your app, (by way of example:
> subscriptions, in-game currencies, game levels, access to premium content, or unlocking a
> full version), you must use in-app purchase. Apps may not use their own mechanisms to
> unlock content or functionality, such as license keys […]

And outside the US storefront you may not even *point* at an alternative:

> In all other storefronts, except for the United States storefront […] apps and their
> metadata may not include buttons, external links, or other calls to action that direct
> customers to purchasing mechanisms other than in-app purchase.

The EU has an entitlement that permits link-outs, but taking it means Apple's alternative
business terms: roughly **2% initial acquisition + 5–13% store services + 5% Core Technology
Commission**. The per-install Core Technology Fee was supposed to become that revenue-based
commission in January 2026 and
[the transition has not fully landed](https://www.revenuecat.com/blog/growth/apple-eu-dma-update-june-2025/),
so the terms are still moving.

**This is the whole reason to start on the web.** The rule binds apps, not websites. Selling
on nilbots.com costs nothing in App Store terms today, and the app simply shows what you own.

## Which means the first decision is: do not sell in the app yet

The app is not on the App Store. Until it is, there is no Apple cut, no entitlement to apply
for, and no terms to sign. Selling on the site now:

- keeps 100% of the margin minus payment fees;
- takes the App Store review risk off the critical path entirely;
- costs nothing later, because the entitlement grant is already event-sourced.

That last point is the useful one. `CosmeticEntitlementService.GrantForEventAsync(userId,
sourceKind, sourceId)` already grants from `achievement` and `challenge` sources, and the
unlock toast, the catalog and the garage all read the result rather than the cause. **A
purchase is a new source kind.** If iOS in-app purchase is ever added it becomes a second
source, exactly as `IPushTransport` made Expo a swappable transport rather than a rewrite.

## Merchant of record, not a payment processor

The second constraint is VAT on digital services, and it is why the choice is not "Stripe,
obviously".

Selling digital goods to EU consumers means charging the *customer's* country VAT once
cross-border B2C sales pass **€10,000/year**, via One Stop Shop. Swedish domestic
registration is required at **SEK 120,000** turnover. Below those you charge Swedish VAT.
Above them, someone has to register, calculate per-country rates, file, and remit.

Two ways to answer that:

| | Who is the legal seller | Who owes VAT compliance | Cost |
|---|---|---|---|
| **Processor** (Stripe) | you | you | ~2.9% + fixed, plus Stripe Tax, plus your time |
| **Merchant of record** (Paddle, Lemon Squeezy, Polar, FastSpring) | them | them | ~5% + fixed, inclusive |

A merchant of record buys from you and sells to the customer, so the VAT registration,
filing and liability in every jurisdiction is theirs. The headline rate is higher and it is
[routinely the cheaper option](https://fungies.io/merchant-of-record-guide/) for a solo
seller once registrations and filings are priced in.

For a one-person project with no finance function, the MoR case is not close. The 2 points of
margin buy the entire compliance surface.

## You do not need to register anything to start

Neither side requires a company, and the Swedish side requires less than it first appears.

**The provider.**
[Paddle does not require a legal entity](https://help.boathouse.co/guides/beginners-guide-to-paddle/faq-can-i-sell-via-paddle-as-an-individual)
— individuals and sole traders are accepted, and business verification is skipped for them.
Lemon Squeezy runs KYC/KYB per store and supports individuals in supported countries.

**Sweden.** An *enskild firma* is not a company: it is you, trading under your personnummer,
and registering the name with Bolagsverket is voluntary. There is no entity to form.

- **Hobbyverksamhet** covers activity without a profit motive or of small scale, and
  [Skatteverket names internet income — e-sport, blogging and similar — as potentially
  falling in it](https://www.skatteverket.se/privat/skatter/arbeteochinkomst/inkomster/hobby.4.58d555751259e4d661680003940.html).
  A surplus is declared on form **T2** with the ordinary return. Keep the records seven
  years.
- **Näringsverksamhet** is the other side of that line — self-employed, regular, and run for
  profit — and is where F-skatt belongs. A cosmetics shop with steady sales drifts this way
  over time; the line is a judgement about the activity, not a revenue number.
- **VAT is exempt below SEK 120,000** turnover (raised from 80,000 on 1 January 2025), and
  [applies automatically without applying](https://www.bokio.se/blogg/hojd-omsattningsgrans-for-moms/)
  if you are not already registered. It ends by itself on the transaction that crosses the
  threshold.

**And a merchant of record removes most of the rest.** You are not selling to consumers at
all — you sell to Paddle, Paddle sells to the player. The €10,000 OSS threshold and 27
national VAT rates are theirs by construction, not something you are exempt from but
something you are not party to.

The realistic first step is therefore: sell, declare the income, register nothing, and
revisit F-skatt and VAT when the numbers say to.

## Provider: Paddle

Chosen, and the deciding fact is narrower than the general MoR case:
[Paddle's acceptable use policy explicitly permits in-game items such as skins](https://www.paddle.com/help/start/intro-to-paddle/what-am-i-not-allowed-to-sell-on-paddle),
provided you own and operate the game — which is exactly this. Most MoRs are written for
SaaS, and "is a game cosmetic even allowed" is a question worth answering before building
against one.

Alongside that: no legal entity required, 100+ tax jurisdictions covered, and an
established track record with software and games rather than a young platform.

Two things to expect. **Domain review** — they verify you own and operate nilbots.com, so
the site needs to be live and describe the product before applying. And their prohibition on
*exchanges and trading platforms* for virtual currency is worth reading if a marketplace or
bot trading is ever considered; selling cosmetics direct is fine, letting players trade them
is a different policy question.

Lemon Squeezy was the obvious alternative and is now owned by Stripe, which makes its
independent future the thing you would be betting on. Polar is cheaper and newer; worth a
look if Paddle's review goes badly.

## What I would do

1. **Web-only sales through Paddle**, with the provider behind `IStorePaymentProvider` so
   the App Store's own flow can be a second implementation rather than a rewrite.
2. **Grant on webhook**, into the entitlement system that already exists — a `purchase`
   source kind, the payment reference as the source id, and the existing dedupe making a
   replayed webhook silent the same way a retried job already is.
3. **Declare the income; register nothing yet.** Under SEK 120,000 there is no VAT to
   register for, and a merchant of record is the party selling to consumers anyway. F-skatt
   and an enskild firma are what to revisit when this stops looking like a hobby — which is
   a judgement about regularity and profit motive, not a threshold that trips.
4. **Leave iOS purchases alone** until the app is actually shipping and there is revenue to
   reason about. Then it is IAP, as a second grant source.

## The launch inventory already exists, and costs nothing to sell

`cosmetics/catalog.json` has 11 starter items and 12 locked ones. Six of the locked ones are
earnable today — Mantis at 1300 rating, Lancer, Aureate Warden, Talon, Arc Spark and Regent
Lance, from first build / first unranked match / 100 ranked matches / 1300 rating. **Those
should stay earned.** Selling something a player is currently grinding toward devalues the
grind and the toast that celebrates it.

The other six are reserved against sources nothing grants:

| pair | bot look | projectile | reserved source kind |
|---|---|---|---|
| 1 | `helio-kite` | `helix-dart` | `achievement` |
| 2 | `scrap-jackal` | `gravity-knot` | `challenge` |
| 3 | `glass-manta` | `prism-fan` | `competition` |

They are unreachable — no code path grants a `reserved-*` source — so making them
purchasable takes nothing away from anyone. And they already fall into three (look,
projectile) pairs by source kind, which is the packaging without having to invent it.

**A package needs one small change to be real.** `BotLook.defaultProjectileLookId` exists,
and `GaragePage.selectLook` already auto-selects the matching projectile when a look is
chosen — but **no look manifest declares one**, so that path has never executed. Setting it
on these three looks makes "buy the pair, get the pair" the behaviour the garage already
implements, rather than a new concept in the checkout.

Whether the entitlement is one purchase granting two items, or one item that implies the
other, is a catalog question: `GrantForEventAsync` already grants every item matching a
source, so a single `purchase/pack-helio-kite` source granting both is the smaller change.

## Selling outside the EU

Twenty-seven countries is the part that has a *name*. Globally it is worse, and the
difference decides the provider rather than complicating it.

- **The US has no national sales tax.** Around 45 states levy one, most with economic nexus
  at [$100,000 or 200 transactions](https://www.avalara.com/us/en/learn/guides/state-by-state-guide-economic-nexus-laws.html)
  ($500,000 in California and Texas), each with its own registration deadline, filing
  cadence and — the awkward part — its own definition of whether a digital good is taxable
  at all. Sellers routinely find out they crossed a threshold when a state writes to them,
  by which point back taxes and penalties have accrued.
- **Elsewhere**, a long list of digital-services regimes with low or zero registration
  thresholds: Norway, Switzerland, the UK, Canada, Australia, New Zealand, Japan, South
  Korea, Singapore, India.

Doing that yourself is not a bigger version of the EU problem, it is a different job.

**A merchant of record makes global the default rather than a project**, because being
registered everywhere *is* the product — Paddle and FastSpring cover 100+ jurisdictions.
Going worldwide changes nothing about the integration: the same webhook grants the same
entitlement, and the tax difference between a buyer in Malmö and one in Ohio is entirely
theirs.

So the global ambition strengthens the recommendation rather than complicating it. The one
thing it does change is that **Stripe stops being a serious alternative** — at EU-only scale
"you handle VAT yourself" is a bad trade; at worldwide scale it is not a trade anyone should
take without a finance function.

## What is deliberately not answered here

- Prices, currencies, and whether the three packs are priced alike.
- Refunds, and what a refund does to an entitlement already equipped on a bot that has since
  fought ranked matches. The equipped appearance is snapshotted onto past matches, so
  revoking cannot rewrite history — and probably should not try.
- Whether paid cosmetics should be visually distinguishable from earned ones. A ladder where
  the rare-looking chassis was bought rather than won is a different game from one where it
  was not, and that is a design decision rather than a commerce one.
