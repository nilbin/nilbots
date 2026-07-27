import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import type { StorePack, StorePackItem } from '../api';
import { ErrorState, LoadingState } from '../components/StateView';
import { useAuth } from '../auth';
import { useStore } from '../queries';

/**
 * The shop.
 *
 * Two things are true at once and the page has to hold both: nothing can be bought yet
 * (no payment provider is configured), and the packs are real and worth looking at. So it
 * renders the goods properly and is honest about the button — a store that hides itself
 * until checkout works is a store nobody can give feedback on, and Paddle's domain review
 * wants to see the product before it approves selling it.
 */
export default function StorePage() {
  const { user } = useAuth();
  const { data: store, isPending, isError, error, refetch } = useStore();

  if (isPending) return <LoadingState label="Loading the store…" />;
  if (isError) return <ErrorState error={error} onRetry={() => void refetch()} />;

  return (
    <div className="flex flex-col gap-8">
      <section className="rounded-xl border border-arena-edge bg-arena-panel p-6">
        <h1 className="text-2xl font-black tracking-wide">Store</h1>
        <p className="mt-2 max-w-xl text-sm text-arena-dim">
          Chassis and their matching shot, sold together. Everything here is cosmetic or
          capacity — nothing sold changes how a bot fights, and every ladder cosmetic
          earned by playing stays earned by playing.
        </p>
        {!store.open && (
          <p className="mt-3 inline-block rounded-md border border-amber-400/40 bg-amber-400/10 px-3 py-1.5 font-mono text-[11px] tracking-wide text-amber-300">
            NOT OPEN YET — browsing only
          </p>
        )}
      </section>

      {store.categories.map((category) => (
        <section key={category.id}>
          <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">
            {category.label.toUpperCase()}
          </h2>
          <ul className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
            {category.packs.map((pack) => (
              <PackCard key={pack.id} pack={pack} open={store.open} signedIn={Boolean(user)} />
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}

function PackCard({
  pack,
  open,
  signedIn,
}: {
  pack: StorePack;
  open: boolean;
  signedIn: boolean;
}) {
  // Owned and repeatable are different states: an appearance pack is owned once and then
  // done with, while capacity stacks — so "Owned" must not read as "nothing more to get".
  const settled = pack.owned && !pack.repeatable;

  return (
    <li
      className={
        'flex flex-col gap-3 rounded-xl border bg-arena-panel/60 p-4 ' +
        (settled ? 'border-emerald-400/35' : 'border-arena-edge')
      }
    >
      <div className="flex items-start gap-3">
        <PackArtwork items={pack.items} />
        <div className="min-w-0 flex-1">
          <h3 className="truncate font-semibold text-arena-text">{pack.label}</h3>
          <p className="mt-0.5 text-xs leading-relaxed text-arena-dim">{pack.description}</p>
        </div>
        {settled && (
          <span className="shrink-0 rounded bg-emerald-400/15 px-2 py-0.5 font-mono text-[10px] tracking-wider text-emerald-300">
            OWNED
          </span>
        )}
      </div>

      <ul className="flex flex-wrap gap-1.5">
        {pack.items.map((item) => (
          <li
            key={item.key}
            className="rounded border border-arena-edge px-2 py-0.5 font-mono text-[10px] text-arena-dim"
          >
            {item.label}
          </li>
        ))}
      </ul>

      <button
        type="button"
        // Disabled rather than hidden, and the label says which of the several reasons
        // applies. A greyed button with no explanation is the most annoying possible
        // version of every one of these states.
        disabled
        title={
          settled
            ? 'You already own this.'
            : !open
              ? 'The store is not open yet.'
              : !signedIn
                ? 'Sign in to buy.'
                : undefined
        }
        className="mt-auto rounded-md border border-arena-edge px-3 py-2 text-sm font-semibold text-arena-dim disabled:cursor-not-allowed"
      >
        {settled ? 'Owned' : !open ? 'Coming soon' : !signedIn ? 'Sign in to buy' : 'Buy'}
      </button>
    </li>
  );
}

/**
 * A pack's contents, drawn from the client's own assets.
 *
 * Catalog ids rather than image URLs, matching the unlock toast (DECISIONS #108): the
 * server names what a thing *is* and the client decides how it looks, so nothing about
 * rendering has to round-trip when a sprite changes.
 */
function PackArtwork({ items }: { items: readonly StorePackItem[] }) {
  const chassis = items.find((item) => item.kind === 'bot-look');
  const projectile = items.find((item) => item.kind === 'projectile-look');

  if (!chassis && !projectile) {
    // A capacity pack has nothing to draw. A generic glyph beats an empty box that reads
    // as a failed image load.
    return (
      <div className="flex size-16 shrink-0 items-center justify-center rounded-lg border border-arena-edge bg-arena-bg font-mono text-lg text-arena-accent">
        +
      </div>
    );
  }

  const look = chassis ? botLook(chassis.id) : null;
  return (
    <div className="flex size-16 shrink-0 flex-col items-center justify-center gap-1 rounded-lg border border-arena-edge bg-arena-bg p-1">
      {look?.imageUrl && (
        <img src={look.imageUrl} alt="" className="size-9 object-contain" />
      )}
      {projectile && (
        <ProjectilePreview
          look={projectileLook(projectile.id)}
          accent={look?.suggestedAccent ?? '#22d3ee'}
          className="h-3 w-10"
        />
      )}
    </div>
  );
}
