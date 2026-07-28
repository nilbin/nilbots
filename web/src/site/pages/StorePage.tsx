import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import { useQueryClient } from '@tanstack/react-query';
import IdentityChip from '../../components/IdentityChip';
import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import type {
  CosmeticCatalog,
  CosmeticCatalogItem,
  MyBot,
  StorePack,
} from '../api';
import BotIdentity from '../components/BotIdentity';
import { LookLibrary, LookMark, remainingPhrase } from '../components/CosmeticUnlocks';
import { ErrorState, LoadingState } from '../components/StateView';
import { useAuth } from '../auth';
import { BOT_LOOK_KIND, PROJECTILE_LOOK_KIND } from '../cosmetics';
import { errorMessage } from '../errorMessage';
import {
  useCosmetics,
  useLeaderboard,
  useMyBots,
  useNotifications,
  useStore,
  useUpdateAppearance,
} from '../queries';

/**
 * Looks: what your bot could look like, and what stands between you and it.
 *
 * Of the 23 cosmetics in the catalog 11 are starters, 6 are earned by playing and 6 are
 * sold, so calling this page a store makes two thirds of it a lie. It is the wardrobe, and
 * the shop is one shelf inside it.
 *
 * The decisive constraint is the colour policy. A look has no colour of its own and
 * `bot.accent` is the player's, so the page cannot show goods "in their colours" — it has
 * to borrow a wearer. That makes the wearer panel the *mechanism* by which anything here
 * can be shown truthfully rather than decoration at the top, which in turn makes this page
 * the appearance editor with the catalogue attached. Two `<select>`s that concatenate an
 * unlock hint into the option text and can show no art at all are the other half of this
 * same screen; building it twice is how the two drift.
 *
 * What the API cannot supply is named rather than faked (`IMPLEMENTATION.md` #4 — a
 * placeholder number is worse than a missing column, because it survives review):
 *
 * - **price** — `StorePackResponse` has no amount, currency or formatted string; prices
 *   live with the merchant of record behind `IStorePaymentProvider`. See `packPrice`.
 * - **checkout** — `CreateCheckoutAsync` exists on that interface, no endpoint maps it,
 *   and `ClosedStore.IsConfigured` is false everywhere. See `checkoutFor`.
 * - **"new since you last looked"** — nothing records that an account has *seen* a grant.
 *   That wants a `SeenAt` on `EntitlementGrant`, not a client-side high-water mark, so
 *   there are no NEW marks.
 * - **when you unlocked it** — `EntitlementGrant` has the timestamp, `CosmeticCatalogEntry`
 *   does not project it. So no "unlocked 3 days ago".
 * - **rarity** — nothing aggregates grants per item. So no "held by 4% of players".
 * - **motion** — a projectile is a static PNG masked in the accent and there is no flight
 *   animation outside a replay. The preview is static; nothing promises otherwise.
 * - **seasons** — the same blocker the Season screen documents. No "this season's look".
 */
export default function StorePage() {
  const { user } = useAuth();
  const client = useQueryClient();
  const revision = useUnlockRevision(Boolean(user));
  const cosmetics = useCosmetics(revision);
  const store = useStore();
  const { data: bots = [] } = useMyBots(Boolean(user));

  // Which bot is wearing the selection. Never "your bot": an account has none, one or
  // thirty, and the strip below is built for all three.
  const [wearerId, setWearerId] = useState<string | null>(null);
  const [draft, setDraft] = useState<Draft | null>(null);

  const wearer = bots.find((bot) => bot.id === wearerId) ?? bots[0] ?? null;
  const appearance = useUpdateAppearance(wearer?.slug ?? '', wearer?.id ?? '');

  // The draft is keyed by the bot it belongs to rather than reset by an effect: switching
  // wearers has to show what *that* bot wears, and an effect racing a render is how a form
  // ends up describing the bot you just moved away from.
  const worn = wornAppearance(wearer);
  const current = draft !== null && draft.forBot === worn.forBot ? draft : worn;

  const owns = (kind: string, id: string) =>
    cosmetics.data?.items.some(
      (item) => item.kind === kind && item.id === id && item.owned,
    ) === true;

  const select = (item: CosmeticCatalogItem) => {
    if (item.kind === PROJECTILE_LOOK_KIND) {
      setDraft({ ...current, projectileLookId: item.id });
      return;
    }
    if (item.kind !== BOT_LOOK_KIND) return;
    const look = botLook(item.id);
    const paired = look.defaultProjectileLookId;
    setDraft({
      ...current,
      lookId: item.id,
      // A chassis that names a matching shot brings it along when you own it — the
      // behaviour the two pickers already had, and the reason the pair reads as a pair.
      projectileLookId:
        paired && owns(PROJECTILE_LOOK_KIND, paired)
          ? paired
          : current.projectileLookId,
      // With no wearer there is no accent of yours, so the chassis's own suggestion stands
      // in — and the panel says so out loud rather than passing it off as the reader's.
      accent: wearer ? current.accent : look.suggestedAccent,
    });
  };

  // Two queries with two blast radii, deliberately. The catalog is the page; the shop is a
  // section, and the library never waits on it.
  if (cosmetics.isPending) return <LoadingState label="Loading looks…" />;
  if (cosmetics.isError)
    return (
      <ErrorState error={cosmetics.error} onRetry={() => void cosmetics.refetch()} />
    );

  // An unknown `kind` is skipped rather than rendered as something it is not.
  const chassis = cosmetics.data.items.filter((item) => item.kind === BOT_LOOK_KIND);
  const shots = cosmetics.data.items.filter(
    (item) => item.kind === PROJECTILE_LOOK_KIND,
  );

  // Appearance only. The two `capacity` packs are a different shelf and the server already
  // says so — `CosmeticCatalog.CapacityCategory`, labelled "Your account". Identity and
  // allowance are not comparable purchases and should not sit in one list.
  const packs =
    store.data?.categories
      .filter((category) => category.id === APPEARANCE_CATEGORY)
      .flatMap((category) => category.packs) ?? [];
  const packByItemKey = new Map<string, string>();
  for (const pack of packs)
    for (const item of pack.items) packByItemKey.set(item.key, pack.label);

  const wornBy = (item: CosmeticCatalogItem) =>
    bots
      .filter(
        (bot) =>
          (item.kind === BOT_LOOK_KIND ? bot.lookId : bot.projectileLookId) ===
          item.id,
      )
      .map((bot) => bot.name);

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-5">
      {/* No hero panel and no border. The design's screens open with a panel carrying
          content, never with a decorated title band. */}
      <header>
        <h1 className="type-display text-[26px]">Looks</h1>
        <p className="t-meta mt-1 max-w-[62ch]">
          A chassis and the shot that comes with it. Nothing here changes how a bot
          fights.
        </p>
      </header>

      <Wearer
        bots={bots}
        wearer={wearer}
        onWearer={(id) => {
          setWearerId(id);
          // The confirmation belongs to the bot it was applied to. Without this it
          // survives the switch and congratulates you about a different bot.
          appearance.reset();
        }}
        current={current}
        onAccent={(accent) => setDraft({ ...current, accent })}
        signedIn={Boolean(user)}
        ownedSelection={
          owns(BOT_LOOK_KIND, current.lookId) &&
          owns(PROJECTILE_LOOK_KIND, current.projectileLookId)
        }
        saving={appearance.isPending}
        error={
          appearance.isError
            ? errorMessage(appearance.error, 'Could not apply that look.')
            : null
        }
        saved={appearance.isSuccess}
        onApply={() =>
          appearance.mutate(
            {
              accent: current.accent,
              lookId: current.lookId,
              projectileLookId: current.projectileLookId,
            },
            {
              // `useUpdateAppearance` stales only `['bot', botKey]`, so without this the
              // wearer strip keeps drawing the old chassis straight after a successful
              // apply. The real fix is one more invalidation inside that hook; this is the
              // same statement, made from the caller that noticed.
              onSuccess: () =>
                void client.invalidateQueries({ queryKey: ['my-bots'] }),
            },
          )
        }
      />

      <ForSale
        packs={packs}
        pending={store.isPending}
        error={store.isError ? store.error : null}
        onRetry={() => void store.refetch()}
        open={store.data?.open ?? false}
        signedIn={Boolean(user)}
        accent={current.accent}
      />

      <LibraryNote
        signedIn={Boolean(user)}
        catalog={cosmetics.data}
        chassis={chassis}
        shots={shots}
      />

      {/* Two sections, not one merged list: they are two independent picks, and a mixed
          list makes "which of these can I combine" unanswerable. */}
      <LookLibrary
        title="Chassis"
        items={chassis}
        accent={current.accent}
        wornBy={wornBy}
        packLabel={(item) => packByItemKey.get(item.key) ?? null}
        packHref="#for-sale"
        selectedId={current.lookId}
        onSelect={select}
      />
      <LookLibrary
        title="Projectiles"
        items={shots}
        accent={current.accent}
        wornBy={wornBy}
        packLabel={(item) => packByItemKey.get(item.key) ?? null}
        packHref="#for-sale"
        selectedId={current.projectileLookId}
        onSelect={select}
      />
    </div>
  );
}

/** `CosmeticCatalog.AppearanceCategory` on the server. The capacity shelf is not ours. */
const APPEARANCE_CATEGORY = 'appearance';

interface Draft {
  /** The bot this selection belongs to; null while nobody is signed in or owns one. */
  forBot: string | null;
  lookId: string;
  projectileLookId: string;
  accent: string;
}

function wornAppearance(wearer: MyBot | null): Draft {
  if (wearer)
    return {
      forBot: wearer.id,
      lookId: wearer.lookId,
      projectileLookId: wearer.projectileLookId,
      accent: wearer.accent,
    };
  // No hardcoded ids: the look registry's own defaults, and the chassis's suggested accent
  // rather than a colour presented as the reader's.
  const fallback = botLook();
  return {
    forBot: null,
    lookId: fallback.id,
    projectileLookId: fallback.defaultProjectileLookId ?? projectileLook().id,
    accent: fallback.suggestedAccent,
  };
}

/**
 * A look that unlocks while this page is open should turn over in place.
 *
 * `useCosmetics(revision)` treats the revision as part of the query's identity, so the
 * catalog after a grant lands is a genuinely different answer rather than a stale one. The
 * signal is the unread feed `NotificationCenter` already polls — same query key, so this
 * costs no extra request. It follows the *poll* rather than the hub: SignalR delivery
 * lands in that component's local state and is not published anywhere this can read, so a
 * live unlock turns over on the next poll. The row moving above the band is the
 * celebration; there is no confetti.
 */
function useUnlockRevision(signedIn: boolean): number {
  const { data: unread } = useNotifications(signedIn);
  const seen = useRef(new Set<string>());
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    let fresh = 0;
    for (const notification of unread ?? []) {
      // Narrowed on the payload's own discriminator, not the outer `kind`: they carry the
      // same string, but TypeScript cannot use one property to narrow a sibling.
      if (notification.payload?.kind !== 'entitlement-earned') continue;
      if (seen.current.has(notification.id)) continue;
      seen.current.add(notification.id);
      fresh += 1;
    }
    // Monotonic on purpose: a count of unread unlocks falls again as they are
    // acknowledged, and a key that returns to a previous value re-reads a cached answer.
    if (fresh > 0) setRevision((count) => count + fresh);
  }, [unread]);

  return revision;
}

/**
 * The wearer: the panel that lets the rest of the page tell the truth.
 *
 * Three renderings, because these are the three places a look is actually seen — the pair
 * at working size, the 24px chip the ladder and match history draw, and a ladder row. A
 * store that shows a chassis at 200px and never at 24px is lying about the product.
 */
function Wearer({
  bots,
  wearer,
  onWearer,
  current,
  onAccent,
  signedIn,
  ownedSelection,
  saving,
  error,
  saved,
  onApply,
}: {
  bots: readonly MyBot[];
  wearer: MyBot | null;
  onWearer: (id: string) => void;
  current: Draft;
  onAccent: (accent: string) => void;
  signedIn: boolean;
  ownedSelection: boolean;
  saving: boolean;
  error: string | null;
  saved: boolean;
  onApply: () => void;
}) {
  const chassis = botLook(current.lookId);
  const shot = projectileLook(current.projectileLookId);
  const dirty =
    wearer !== null &&
    (current.lookId !== wearer.lookId ||
      current.projectileLookId !== wearer.projectileLookId ||
      current.accent !== wearer.accent);

  // Disabled with the reason named, rather than greyed out and silent.
  const blocked =
    wearer === null
      ? 'Ship a bot and it can wear this.'
      : !ownedSelection
        ? 'That look is not unlocked yet.'
        : !dirty
          ? `${wearer.name} already wears this.`
          : null;

  return (
    <section className="panel pad">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
        <p className="lab shrink-0">Wearing</p>
        {/* Wraps when there is room and scrolls inside itself when there is not. Never a
            two-up grid and never truncated: an account with thirty bots has thirty, and
            the page body still must not scroll sideways at 390. */}
        <div className="flex min-w-0 flex-1 gap-1.5 overflow-x-auto sm:flex-wrap sm:overflow-visible">
          {bots.map((bot) => (
            <button
              key={bot.id}
              type="button"
              onClick={() => onWearer(bot.id)}
              aria-pressed={bot.id === wearer?.id}
              className={clsx('btn shrink-0', bot.id === wearer?.id && 'btn-on')}
            >
              <IdentityChip
                name={bot.name}
                accent={bot.accent}
                lookId={bot.lookId}
                size={24}
                nameClassName="t-body"
              />
            </button>
          ))}
        </div>
      </div>

      {!signedIn && (
        <p className="t-micro mt-2">
          Signed out, so nothing shows as yours.{' '}
          <Link to="/login" className="text-arena-accent hover:underline">
            Sign in
          </Link>
          . Colours below are each chassis's suggested accent, not anybody's.
        </p>
      )}
      {signedIn && bots.length === 0 && (
        <p className="t-micro mt-2">
          No bots yet, so nothing here is worn.{' '}
          <code className="val">nilbots submit</code> ships one; until then the colours
          are each chassis's suggested accent.
        </p>
      )}

      <div className="mt-3 flex flex-wrap items-center gap-x-5 gap-y-3">
        <div className="flex items-center gap-3">
          <img
            src={chassis.imageUrl}
            alt={`${chassis.label} chassis`}
            className="size-18 object-contain sm:size-24"
          />
          <ProjectilePreview
            look={shot}
            accent={current.accent}
            className="h-3 w-8 sm:h-4 sm:w-14"
          />
        </div>
        <div className="min-w-0">
          {wearer && <p className="t-body truncate">{wearer.name}</p>}
          {/* Chassis and shot are names a player chose, not values a machine wrote. */}
          <p className="t-meta">
            {chassis.label} · {shot.label}
          </p>
          {wearer ? (
            <label className="t-meta mt-1.5 flex items-center gap-2">
              accent
              <input
                type="color"
                value={current.accent}
                onChange={(event) => onAccent(event.target.value)}
                aria-label={`Accent for ${wearer.name}`}
                className="h-7 w-11 cursor-pointer rounded-[3px] border border-arena-edge bg-arena-bg"
              />
              <span className="val">{current.accent}</span>
            </label>
          ) : (
            <p className="t-micro mt-1.5">
              suggested accent <span className="val">{current.accent}</span>
            </p>
          )}
        </div>
      </div>

      <p className="lab mt-4 border-t border-arena-edge pt-3">As it appears</p>
      <div className="mt-2 flex flex-col gap-2">
        <IdentityChip
          name={wearer?.name ?? chassis.label}
          accent={current.accent}
          lookId={current.lookId}
          size={24}
          nameClassName="t-body"
        />
        {wearer && <LadderPreview wearer={wearer} current={current} />}
      </div>

      {error && <p className="t-body mt-3 text-arena-hot">{error}</p>}
      {/* Success survives only until the next edit — `dirty` going true means the
          confirmation is describing a state the panel has already left. */}
      {saved && !dirty && wearer && (
        <p className="t-body mt-3 text-arena-ok">
          Applied. Future matches use it; existing replays keep the look they were fought
          in.
        </p>
      )}
      <div className="mt-3 flex justify-end">
        <button
          type="button"
          onClick={onApply}
          disabled={blocked !== null || saving}
          title={blocked ?? undefined}
          className="btn w-full sm:w-auto"
        >
          {saving ? 'Applying…' : `Apply to ${wearer?.name ?? 'a bot'}`}
        </button>
      </div>
    </section>
  );
}

/**
 * The one place a player's colour competes with somebody else's.
 *
 * Real rows from the real ladder, with the reader's own row drawn in the *draft* look —
 * that is the preview. Movement, trend and W–L are the seams `SeasonPage` already
 * documents (no rank snapshot, no rating history, no ranked record on this endpoint), so
 * they are absent here rather than invented.
 *
 * Mounted only when there is a wearer, which is what keeps the ladder request off this
 * page for a visitor who has nothing to preview.
 */
function LadderPreview({ wearer, current }: { wearer: MyBot; current: Draft }) {
  const { data: board } = useLeaderboard(null);
  const entries = board?.entries ?? [];
  const index = entries.findIndex((entry) => entry.id === wearer.id);

  if (index < 0)
    return (
      <>
        <div className="flex items-center justify-between gap-3 border-t border-arena-edge pt-2">
          <BotIdentity
            name={wearer.name}
            accent={current.accent}
            lookId={current.lookId}
            size="sm"
            className="min-w-0"
          />
          <span className="type-display tabular shrink-0 text-[22px] text-arena-text">
            —
          </span>
        </div>
        <p className="t-micro">
          Not on the ladder yet — this is the row it takes once it has played a ranked
          set.
        </p>
      </>
    );

  const around = entries.slice(Math.max(0, index - 1), index + 2);

  return (
    <>
      {/* The desktop ladder row, and below 640 the phone's `row spread` instead —
          previewing a row shape the phone never draws would defeat the point. */}
      <table className="t-body hidden w-full border-collapse sm:table">
        <tbody>
          {around.map((entry) => {
            const mine = entry.id === wearer.id;
            const accent = mine ? current.accent : entry.accent;
            return (
              <tr
                key={entry.id}
                className={clsx(
                  'border-b border-arena-edge last:border-b-0',
                  mine && 'bg-arena-text/[0.028]',
                )}
              >
                <td
                  className="type-display tabular w-[42px] p-2 align-middle text-[22px] text-arena-text"
                  style={mine ? { boxShadow: `inset 2px 0 0 ${accent}` } : undefined}
                >
                  {entry.rank}
                </td>
                <td className="p-2 align-middle">
                  <BotIdentity
                    name={entry.name}
                    accent={accent}
                    lookId={mine ? current.lookId : entry.lookId}
                    size="sm"
                  />
                </td>
                <td className="val w-20 p-2 text-right align-middle text-arena-text">
                  {Math.round(entry.rating)}
                </td>
                <td className="val w-16 p-2 text-right align-middle">
                  {entry.rankedSets}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      <ul className="flex flex-col gap-1 sm:hidden">
        {around.map((entry) => {
          const mine = entry.id === wearer.id;
          const accent = mine ? current.accent : entry.accent;
          return (
            <li
              key={entry.id}
              className={clsx(
                'flex items-center justify-between gap-2 py-1',
                mine && 'bg-arena-text/[0.028]',
              )}
              style={mine ? { boxShadow: `inset 2px 0 0 ${accent}` } : undefined}
            >
              <BotIdentity
                name={entry.name}
                accent={accent}
                lookId={mine ? current.lookId : entry.lookId}
                size="sm"
                className="min-w-0 pl-2"
              />
              <span className="flex shrink-0 items-center gap-2.5">
                <span className="val">{Math.round(entry.rating)}</span>
                <span className="type-display tabular text-[18px] text-arena-text">
                  {entry.rank}
                </span>
              </span>
            </li>
          );
        })}
      </ul>
    </>
  );
}

/**
 * The shelf.
 *
 * Two things are true at once and the section has to hold both: nothing can be bought yet,
 * and the packs are real and worth looking at. A shop that hides until checkout works is a
 * shop nobody can give feedback on, and Paddle's domain review wants to see the product
 * before it approves selling it. So the goods render properly and the button is honest.
 *
 * One panel with a hairline between rows, not three cards in a grid — three cards in a
 * 3-column grid at 1200px is a shape the rest of the site never makes.
 */
function ForSale({
  packs,
  pending,
  error,
  onRetry,
  open,
  signedIn,
  accent,
}: {
  packs: readonly StorePack[];
  pending: boolean;
  error: unknown;
  onRetry: () => void;
  open: boolean;
  signedIn: boolean;
  accent: string;
}) {
  // An empty shelf with a heading over it is worse than no shelf. Note this is the *empty*
  // case only: a closed store is not empty, and renders in full with the reason said.
  if (!pending && error === null && packs.length === 0) return null;

  return (
    <section id="for-sale">
      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <p className="lab">
          For sale{packs.length > 0 && ` · ${packs.length} packs`}
        </p>
        {!pending && error === null && !open && (
          <span className="pill">Not selling yet</span>
        )}
      </div>
      {pending && <p className="t-micro">Loading what's for sale…</p>}
      {/* The shop failing is not the page failing: "what have I earned" is answerable
          without it, so the library below still renders. */}
      {error !== null && (
        <p className="t-meta flex flex-wrap items-center gap-3 text-arena-hot">
          {errorMessage(error, 'Could not load what is for sale.')}
          <button type="button" onClick={onRetry} className="btn">
            Try again
          </button>
        </p>
      )}
      {packs.length > 0 && (
        <ul className="panel">
          {packs.map((pack) => (
            <Pack
              key={pack.id}
              pack={pack}
              open={open}
              signedIn={signedIn}
              accent={accent}
            />
          ))}
        </ul>
      )}
    </section>
  );
}

function Pack({
  pack,
  open,
  signedIn,
  accent,
}: {
  pack: StorePack;
  open: boolean;
  signedIn: boolean;
  accent: string;
}) {
  // Owned and repeatable are different states: appearance is owned once and then done
  // with, while capacity stacks — so "Owned" must not read as "nothing more to get".
  const settled = pack.owned && !pack.repeatable;
  const price = packPrice(pack);
  const checkout = checkoutFor(pack);
  const reason = settled
    ? 'You already own this.'
    : !open
      ? 'The store is not open yet.'
      : !signedIn
        ? 'Sign in to buy.'
        : checkout === null
          ? 'Checkout is not wired up yet.'
          : undefined;

  return (
    <li className="flex flex-col gap-2 border-b border-arena-edge p-3 last:border-b-0 sm:flex-row sm:items-start sm:gap-4">
      {/* Iterated, never indexed: every appearance pack is a chassis plus a shot today,
          but the type is a list and a three-item pack must not render as a broken image.
          Drawn in the wearer's accent, so the shop and the wearer panel agree. */}
      <span className="flex shrink-0 items-center gap-2">
        {pack.items.map((item) => (
          <LookMark key={item.key} kind={item.kind} id={item.id} accent={accent} />
        ))}
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-2">
          <span className="t-body">{pack.label}</span>
          {/* A pack is not worn, so the accent rule does not apply to it. */}
          {settled && <span className="pill">Owned</span>}
        </span>
        <span className="t-meta mt-0.5 block">{pack.description}</span>
        <span className="t-micro mt-1 block">
          {pack.items.map((item) => item.label).join(' · ')}
        </span>
      </span>
      <span className="flex shrink-0 flex-col items-stretch gap-1.5 sm:items-end">
        {/* The price slot: shaped, and empty until an endpoint fills it. Not "—". */}
        {price !== null && <span className="val text-arena-text">{price}</span>}
        <button
          type="button"
          // Disabled rather than hidden, and the title says which of the several reasons
          // applies. A greyed button with no explanation is the most annoying possible
          // version of every one of these states.
          disabled={settled || !open || !signedIn || checkout === null}
          title={reason}
          className="btn w-full sm:w-auto"
        >
          {settled
            ? 'Owned'
            : !open
              ? 'Coming soon'
              : !signedIn
                ? 'Sign in to buy'
                : 'Buy'}
        </button>
      </span>
    </li>
  );
}

/**
 * What a pack costs.
 *
 * `StorePackResponse` carries `Id, Label, Description, Items, Owned, Repeatable` and no
 * amount, currency or formatted string: prices live with the merchant of record behind
 * `IStorePaymentProvider` and nothing projects one into the contract. Read off the
 * response optionally rather than added to `schema.d.ts`, which is generated from the
 * server and must not be hand-edited — so production reads null, the slot renders nothing,
 * and the day the field exists regenerating the client deletes this seam.
 */
function packPrice(pack: StorePack): string | null {
  const priced = pack as StorePack & { price?: string | null };
  return priced.price ?? null;
}

/**
 * Where buying this pack would go. Today: nowhere.
 *
 * `IStorePaymentProvider.CreateCheckoutAsync` exists, no endpoint maps it, and
 * `ClosedStore.IsConfigured` is false in every environment — so `store.open` is false and
 * this is null on top of it. The button stays disabled with the reason named; the day
 * checkout lands, this returns a URL and nothing else on the page moves.
 */
function checkoutFor(pack: StorePack): { href: string } | null {
  const wired = pack as StorePack & { checkoutUrl?: string | null };
  return wired.checkoutUrl ? { href: wired.checkoutUrl } : null;
}

/**
 * The line above the library, of which there are three and sometimes none.
 *
 * A wall of unmarked rows with no explanation is the state this exists to prevent. Every
 * sentence is computed from the catalog rather than written into the page, so none of them
 * can go stale when the catalog moves. It sits above both tables rather than inside one
 * because it counts chassis *and* shots, which no single table can say.
 */
function LibraryNote({
  signedIn,
  catalog,
  chassis,
  shots,
}: {
  signedIn: boolean;
  catalog: CosmeticCatalog;
  chassis: readonly CosmeticCatalogItem[];
  shots: readonly CosmeticCatalogItem[];
}) {
  // The design's line is "Signed out, so nothing here shows as yours" — but the server
  // reports starters as owned for everybody (`CosmeticEntitlementService.CatalogForAsync`
  // returns `Availability == Starter || …`), so a signed-out visitor genuinely does see
  // eleven marked rows. Ownership is not derived client-side to make a sentence come true;
  // the sentence says what the marks actually mean.
  if (!signedIn)
    return (
      <p className="panel-quiet pad t-body">
        Signed out, so the marked rows are what every account starts with rather than
        anything of yours.{' '}
        <Link to="/login" className="text-arena-accent hover:underline">
          Sign in
        </Link>{' '}
        to see what you've unlocked.
      </p>
    );

  const looks = [...chassis, ...shots];
  const owned = looks.filter((item) => item.owned);
  // Anything earned, and the tables say it better than a sentence could. Nothing owned at
  // all is not a state this account can be in, but it is not this line's job to explain it.
  if (owned.length === 0 || owned.some((item) => item.availability !== 'starter'))
    return null;

  const ownedShots = shots.filter((item) => item.owned).length;
  const starters = `${capitalise(
    numeral(chassis.filter((item) => item.owned).length),
  )} chassis and ${numeral(ownedShots)} ${
    ownedShots === 1 ? 'shot' : 'shots'
  } come with the account.`;

  // The closest thing to earning, by how far along it is. Units are not comparable to each
  // other, so "closest" is a fraction and the distance is said in that item's own unit.
  const nearest = looks
    .filter((item) => !item.owned && item.progress)
    .sort(
      (a, b) =>
        b.progress!.current / Math.max(1, b.progress!.target) -
        a.progress!.current / Math.max(1, a.progress!.target),
    )[0];
  if (nearest?.progress)
    return (
      <p className="panel-quiet pad t-body">
        {starters} The next one is {remainingPhrase(nearest.progress)} away.
      </p>
    );

  // Nothing has measurable progress yet — a new account with no completed builds. The
  // first milestone's own sentence stands in, taken from the catalog so it cannot rot.
  const first = catalog.items.find(
    (item) =>
      !item.owned &&
      item.unlock !== null &&
      item.unlock.sourceKind !== 'purchase' &&
      (item.kind === BOT_LOOK_KIND || item.kind === PROJECTILE_LOOK_KIND),
  );
  return (
    <p className="panel-quiet pad t-body">
      {starters} Nothing earned yet
      {first?.unlock ? `. The first one: ${first.unlock.hint}` : '.'}
    </p>
  );
}

/**
 * Small counts as words.
 *
 * These are prose, not values a machine produced — mono and `.val` are reserved for the
 * latter, and "6 chassis come with the account" would be spending that signal on a number
 * nothing computed.
 */
const NUMERALS = [
  'no',
  'one',
  'two',
  'three',
  'four',
  'five',
  'six',
  'seven',
  'eight',
  'nine',
  'ten',
  'eleven',
  'twelve',
];

function numeral(count: number): string {
  return NUMERALS[count] ?? String(count);
}

function capitalise(word: string): string {
  return word.charAt(0).toUpperCase() + word.slice(1);
}
