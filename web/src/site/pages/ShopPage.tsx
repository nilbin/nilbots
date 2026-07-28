import { Link } from 'react-router-dom';
import { botLook } from '../../render/arenaThemes';
import type { StoreCategory, StorePack } from '../api';
import { useAuth } from '../auth';
import { BOT_LOOK_KIND, PROJECTILE_LOOK_KIND } from '../cosmetics';
import { useStore } from '../queries';
import { LookMark } from '../components/CosmeticUnlocks';
import { ErrorState, LoadingState } from '../components/StateView';

/**
 * The commercial catalogue.
 *
 * Choosing and equipping a look belongs to a bot. This page has the narrower job its
 * name promises: show every pack the server says is for sale, grouped by the server's
 * categories. Prices and checkout are intentionally absent until the API projects them.
 */
export default function ShopPage() {
  const { user } = useAuth();
  const store = useStore();

  if (store.isPending) return <LoadingState label="Loading the shop…" />;
  if (store.isError)
    return <ErrorState error={store.error} onRetry={() => void store.refetch()} />;
  const packCount = store.data.categories.reduce(
    (total, category) => total + category.packs.length,
    0,
  );

  return (
    <div className="mx-auto flex max-w-4xl flex-col gap-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="type-display text-[26px]">Shop</h1>
          <p className="t-meta mt-1 max-w-[62ch]">
            Cosmetic packs and account upgrades. Nothing here changes what happens
            inside a fight.
          </p>
        </div>
        {!store.data.open && <span className="pill">Checkout coming soon</span>}
      </header>

      {!store.data.open && (
        <p className="panel-quiet pad t-body">
          The shelves are ready to browse, but purchases are not open yet. You can
          still inspect every owned and locked skin from an owned bot's appearance
          picker.{' '}
          <Link
            to={
              user
                ? '/garage'
                : `/login?returnUrl=${encodeURIComponent('/garage')}`
            }
            className="text-link"
          >
            {user ? 'Choose a bot in your Garage' : 'Sign in to open your Garage'}
          </Link>
          .
        </p>
      )}

      {packCount === 0 ? (
        <section className="panel pad">
          <h2 className="lab">Nothing on the shelves</h2>
          <p className="t-meta mt-1">No packs are listed by this server.</p>
        </section>
      ) : (
        store.data.categories.map((category) => (
          <Shelf
            key={category.id}
            category={category}
            open={store.data.open}
            signedIn={Boolean(user)}
          />
        ))
      )}
    </div>
  );
}

function Shelf({
  category,
  open,
  signedIn,
}: {
  category: StoreCategory;
  open: boolean;
  signedIn: boolean;
}) {
  if (category.packs.length === 0) return null;

  return (
    <section aria-labelledby={`shop-${category.id}`}>
      <h2 id={`shop-${category.id}`} className="lab mb-2">
        {category.label} · {category.packs.length}{' '}
        {category.packs.length === 1 ? 'pack' : 'packs'}
      </h2>
      <ul className="panel">
        {category.packs.map((pack) => (
          <Pack
            key={pack.id}
            pack={pack}
            open={open}
            signedIn={signedIn}
          />
        ))}
      </ul>
    </section>
  );
}

function Pack({
  pack,
  open,
  signedIn,
}: {
  pack: StorePack;
  open: boolean;
  signedIn: boolean;
}) {
  const settled = pack.owned && !pack.repeatable;
  const checkout = checkoutFor(pack);
  const reason = settled
    ? 'You already own this.'
    : !open
      ? 'The shop is not open yet.'
      : !signedIn
        ? 'Sign in to buy.'
        : checkout === null
          ? 'Checkout is not wired up yet.'
          : null;

  return (
    <li
      id={`pack-${pack.id}`}
      className="flex scroll-mt-4 flex-col gap-2 border-b border-arena-edge p-3 last:border-b-0 sm:flex-row sm:items-start sm:gap-4"
    >
      <PackArtwork pack={pack} />
      <span className="min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-2">
          <span className="t-body">{pack.label}</span>
          {settled && <span className="pill">Owned</span>}
        </span>
        <span className="t-meta mt-0.5 block">{pack.description}</span>
        <span className="t-micro mt-1 block">
          {pack.items.map((item) => item.label).join(' · ')}
        </span>
      </span>
      <span className="flex shrink-0 flex-col items-stretch gap-1.5 sm:items-end">
        {checkout ? (
          <a href={checkout.href} className="btn btn-on w-full sm:w-auto">
            Buy
          </a>
        ) : open && !signedIn ? (
          <Link
            to={`/login?returnUrl=${encodeURIComponent(`/store#pack-${pack.id}`)}`}
            className="btn w-full sm:w-auto"
          >
            Sign in to buy
          </Link>
        ) : (
          <button
            type="button"
            disabled
            aria-describedby={reason ? `pack-${pack.id}-reason` : undefined}
            className="btn w-full disabled:opacity-60 sm:w-auto"
          >
            {settled ? 'Owned' : open ? 'Unavailable' : 'Coming soon'}
          </button>
        )}
        {reason && (
          <span
            id={`pack-${pack.id}-reason`}
            className="t-micro max-w-52 text-right"
          >
            {reason}
          </span>
        )}
      </span>
    </li>
  );
}

function PackArtwork({ pack }: { pack: StorePack }) {
  const looks = pack.items.filter(
    (item) =>
      item.kind === BOT_LOOK_KIND || item.kind === PROJECTILE_LOOK_KIND,
  );
  if (looks.length === 0) {
    return (
      <span
        aria-hidden="true"
        className="lab flex h-8 min-w-20 shrink-0 items-center justify-center rounded-[3px] border border-arena-edge bg-arena-bg px-2"
      >
        Account
      </span>
    );
  }

  const chassis = looks.find((item) => item.kind === BOT_LOOK_KIND);
  const accent = chassis
    ? botLook(chassis.id).suggestedAccent
    : botLook().suggestedAccent;
  return (
    <span className="flex shrink-0 items-center gap-2" aria-hidden="true">
      {looks.map((item) => (
        <LookMark
          key={item.key}
          kind={item.kind}
          id={item.id}
          accent={accent}
        />
      ))}
    </span>
  );
}

/**
 * The server does not expose checkout yet. Keeping this seam typed and empty makes the
 * disabled state honest without inventing a URL or hand-editing the generated contract.
 */
function checkoutFor(pack: StorePack): { href: string } | null {
  void pack;
  return null;
}
