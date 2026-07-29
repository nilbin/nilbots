import clsx from 'clsx';
import { Link } from 'react-router-dom';
import AccentRing from '../../components/AccentRing';
import ProjectilePreview from '../../components/ProjectilePreview';
import { playerAccent } from '../../presentation/playerAccent';
import { styleVariables } from '../../presentation/styleVariables';
import { botLook, projectileLook } from '../../render/arenaThemes';
import type { CosmeticCatalog, CosmeticCatalogItem } from '../api';
import { BOT_LOOK_KIND, PROJECTILE_LOOK_KIND } from '../cosmetics';
import Th from './TableHeader';

/**
 * The look library: a tactile wardrobe grid when choosing for one bot, and a compact
 * progress table when the same catalogue is shown read-only elsewhere.
 *
 * Two things were wrong with the list this replaces and both were the same mistake —
 * rendering an entitlement without asking what kind of thing it is. It filtered on
 * `availability === 'entitlement'`, which includes the two `capacity:` packs, and sent
 * everything that was not a `bot-look` through `ProjectilePreview` — so the garage drew
 * "Extra daily builds" as a projectile sprite. And it printed `RANKED MATCHES` above every
 * progress bar, which labelled the 1300-rating milestone `1284/1300 ranked matches`.
 *
 * So: only the two look kinds are ever drawn — an unknown `kind` is skipped rather than
 * guessed at — and `CosmeticProgress.unit` is mapped rather than assumed.
 *
 * There are no state pills here. Owned is a quiet achromatic inset rule; player colour
 * stays inside the identity artwork instead of being reused as a system-state signal.
 * Locked choices remain fully previewable and say where they come from; selected state
 * uses only the wearer's accent rail.
 */

/**
 * Machine tokens from `CosmeticProgress.Unit`, in words a person would say.
 *
 * The server sends `ranked-matches` or `rating` (`CosmeticAchievementService`). Anything
 * else prints raw rather than being guessed at: a confidently wrong unit is worse than an
 * ugly one, which is exactly how `1284/1300 ranked matches` happened.
 */
const UNITS: Record<string, { one: string; many: string }> = {
  'ranked-matches': { one: 'ranked match', many: 'ranked matches' },
  rating: { one: 'rating point', many: 'rating points' },
};

export function unitPhrase(unit: string, count: number): string {
  const known = UNITS[unit];
  if (!known) return `${count} ${unit}`;
  return `${count} ${count === 1 ? known.one : known.many}`;
}

/** What is left, said out loud: "53 ranked matches". */
export function remainingPhrase(progress: {
  current: number;
  target: number;
  unit: string;
}): string {
  return unitPhrase(progress.unit, Math.max(0, progress.target - progress.current));
}

/** "1284 of 1300 rating points" — the count and its unit, for a title and a label. */
function progressPhrase(progress: {
  current: number;
  target: number;
  unit: string;
}): string {
  return `${progress.current} of ${unitPhrase(progress.unit, progress.target)}`;
}

/**
 * A look as it will be worn: the chassis ringed in the accent, or the shot drawn in it.
 *
 * A chassis has no colour of its own, so it needs the ring to carry the accent — the same
 * ring `IdentityChip` puts around a bot. A projectile is *masked* in the accent, so
 * ringing it too would spend the colour twice; its disc is a plain hairline.
 *
 * `IdentityChip` is deliberately not reused here: that is a *bot's* identity and always
 * draws a name beside the chassis. A catalogue row is not a bot, and its name is a column.
 */
export function LookMark({
  kind,
  id,
  accent,
  large = false,
}: {
  kind: string;
  id: string;
  accent: string;
  large?: boolean;
}) {
  if (kind === BOT_LOOK_KIND) {
    return (
      <AccentRing accent={accent} size={large ? 42 : 24}>
        <img
          src={botLook(id).imageUrl}
          alt=""
          loading="lazy"
          className="size-full object-contain"
        />
      </AccentRing>
    );
  }
  if (kind === PROJECTILE_LOOK_KIND) {
    return (
      <span
        className={clsx(
          'flex shrink-0 items-center justify-center rounded-full border border-arena-edge bg-arena-bg',
          large ? 'size-[55px]' : 'size-[31px]',
        )}
      >
        <ProjectilePreview
          look={projectileLook(id)}
          accent={accent}
          className="block h-1/2 w-3/4"
        />
      </span>
    );
  }
  // Capacity, and whatever a future catalog adds: not a look, so not drawn as one.
  return null;
}

/**
 * Where a row sits.
 *
 * Owned first — the top of the table is your wardrobe, which is what you came to change.
 * Then what you are closest to earning, then what is sold, then what has no measurable
 * route yet. `CosmeticAchievementService.ProgressForAsync` returns null for purchase
 * sources by design, so a pack row genuinely has no bar rather than a missing one.
 */
function bucket(item: CosmeticCatalogItem): number {
  if (item.owned) return 0;
  if (item.progress) return 1;
  if (item.unlock?.sourceKind === 'purchase') return 2;
  return 3;
}

function fraction(item: CosmeticCatalogItem): number {
  const progress = item.progress;
  return progress ? progress.current / Math.max(1, progress.target) : 0;
}

function ordered(items: readonly CosmeticCatalogItem[]): CosmeticCatalogItem[] {
  // Sort is stable, so inside a bucket the catalog's own order survives.
  return [...items].sort(
    (a, b) =>
      bucket(a) - bucket(b) || (bucket(a) === 1 ? fraction(b) - fraction(a) : 0),
  );
}

export interface LookLibraryProps {
  /** "Chassis" / "Projectiles". The count is appended from the items given. */
  title: string;
  /** Already filtered to one kind by the caller; an unknown kind never reaches here. */
  items: readonly CosmeticCatalogItem[];
  /** The wearer's accent — the only saturated colour here, and it is their data. */
  accent: string;
  /** Which bots of yours wear this look. Real data or nothing; never a guess. */
  wornBy?: (item: CosmeticCatalogItem) => readonly string[];
  /** The pack a purchase-gated look comes in, resolved from `useStore()`. */
  packLabel?: (item: CosmeticCatalogItem) => string | null;
  /** Where this item's pack is sold, when the caller has loaded the shop catalogue. */
  packHref?: (item: CosmeticCatalogItem) => string | null;
  /** Optional context the Shop can use to return to the bot being dressed. */
  packState?: { returnTo: string; returnLabel: string };
  selectedId?: string | null;
  onSelect?: (item: CosmeticCatalogItem) => void;
}

export function LookLibrary({
  title,
  items,
  accent,
  wornBy,
  packLabel,
  packHref,
  packState,
  selectedId = null,
  onSelect,
}: LookLibraryProps) {
  if (items.length === 0) return null;

  const rows = ordered(items);
  const owned = items.filter((item) => item.owned).length;
  const band = `${owned} of ${items.length} unlocked`;
  if (onSelect) {
    return (
      <LookPicker
        title={title}
        band={band}
        items={rows}
        accent={accent}
        packLabel={packLabel}
        packHref={packHref}
        packState={packState}
        selectedId={selectedId}
        onSelect={onSelect}
      />
    );
  }
  // The band is a boundary between two blocks, so it exists only when there are two.
  const bandAfter = owned > 0 && owned < items.length ? owned : -1;

  return (
    <section>
      <p className="lab mb-2">
        {title} · {band}
      </p>
      <div className="panel">
        {/* A table above 640px and a `row spread` list below it. Not a table that drags
            sideways: there is exactly one numeric column, so nothing needs to line up and
            a horizontal scrollbar would be scroll for its own sake. The narrow form is the
            one the design's phone mock actually draws. */}
        <div className="pad hidden sm:block">
          <table className="t-body w-full border-collapse">
            <thead>
              <tr>
                <Th>Look</Th>
                <Th>Where it comes from</Th>
                <Th className="w-[190px]">Progress</Th>
                <Th className="w-32">
                  <span className="sr-only">Worn by</span>
                </Th>
              </tr>
            </thead>
            <tbody>
              {rows.map((item, index) => (
                <Row
                  key={item.key}
                  item={item}
                  accent={accent}
                  worn={wornBy?.(item) ?? []}
                  pack={packLabel?.(item) ?? null}
                  packHref={packHref?.(item) ?? undefined}
                  packState={packState}
                  selected={item.id === selectedId}
                  onSelect={onSelect}
                  band={index + 1 === bandAfter ? band : null}
                />
              ))}
            </tbody>
          </table>
        </div>

        <ul className="sm:hidden">
          {rows.map((item, index) => (
            <PhoneRow
              key={item.key}
              item={item}
              accent={accent}
              worn={wornBy?.(item) ?? []}
              pack={packLabel?.(item) ?? null}
              packHref={packHref?.(item) ?? undefined}
              packState={packState}
              selected={item.id === selectedId}
              onSelect={onSelect}
              band={index + 1 === bandAfter ? band : null}
            />
          ))}
        </ul>
      </div>
    </section>
  );
}

function LookPicker({
  title,
  band,
  items,
  accent,
  packLabel,
  packHref,
  packState,
  selectedId,
  onSelect,
}: {
  title: string;
  band: string;
  items: readonly CosmeticCatalogItem[];
  accent: string;
  packLabel?: (item: CosmeticCatalogItem) => string | null;
  packHref?: (item: CosmeticCatalogItem) => string | null;
  packState?: { returnTo: string; returnLabel: string };
  selectedId: string | null;
  onSelect: (item: CosmeticCatalogItem) => void;
}) {
  return (
    <section>
      <div className="mb-2 flex items-baseline justify-between gap-3">
        <h2 className="lab">{title}</h2>
        <span className="t-micro">{band}</span>
      </div>
      <ul
        className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4"
        style={styleVariables({
          '--player-accent': playerAccent(accent, 'panel'),
        })}
      >
        {items.map((item) => {
          const selected = item.id === selectedId;
          return (
            <li
              key={item.key}
              data-selected={selected}
              className="choice-card flex min-h-0 cursor-default flex-col p-0"
            >
              <button
                type="button"
                onClick={() => onSelect(item)}
                aria-pressed={selected}
                aria-label={`${item.label}, ${item.owned ? 'unlocked' : 'locked'}`}
                className="flex min-h-[132px] w-full flex-1 flex-col items-start gap-2.5 p-3 text-left focus-visible:outline-2 focus-visible:outline-offset-[-3px] focus-visible:outline-arena-text"
              >
                <LookMark
                  kind={item.kind}
                  id={item.id}
                  accent={accent}
                  large
                />
                <span className="t-body line-clamp-2 font-semibold">
                  {item.label}
                </span>
                <span className="t-micro mt-auto">
                  {selected
                    ? 'Selected preview'
                    : item.owned
                      ? 'Unlocked'
                      : 'Locked · preview'}
                </span>
              </button>
              <div className="t-micro flex min-h-[52px] flex-col justify-center gap-2 border-t border-arena-edge px-3 py-2.5">
                <Source
                  item={item}
                  pack={packLabel?.(item) ?? null}
                  packHref={packHref?.(item) ?? undefined}
                  packState={packState}
                  linkClassName="-mx-3 -my-2.5 flex min-h-11 items-center px-3 py-2.5"
                />
                <Progress item={item} />
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

interface RowProps {
  item: CosmeticCatalogItem;
  accent: string;
  worn: readonly string[];
  pack: string | null;
  packHref?: string;
  packState?: { returnTo: string; returnLabel: string };
  selected: boolean;
  onSelect?: (item: CosmeticCatalogItem) => void;
  /** The dashed count rule, when this row is the last of the owned block. */
  band: string | null;
}

function Row({
  item,
  accent,
  worn,
  pack,
  packHref,
  packState,
  selected,
  onSelect,
  band,
}: RowProps) {
  return (
    <>
      <tr
        className={clsx(
          'border-b border-arena-edge last:border-b-0',
          item.owned && 'bg-arena-text/[0.028]',
        )}
      >
        <td
          className={clsx(
            'p-2 align-middle',
            item.owned && 'border-l-2 border-l-arena-edge2',
          )}
        >
          <Face item={item} accent={accent} selected={selected} onSelect={onSelect} />
        </td>
        <td className="t-meta p-2 align-middle">
          <Source
            item={item}
            pack={pack}
            packHref={packHref}
            packState={packState}
          />
        </td>
        <td className="p-2 align-middle">
          <Progress item={item} />
        </td>
        <td className="t-micro p-2 text-right align-middle">
          <WornBy names={worn} />
        </td>
      </tr>
      {band && (
        <tr>
          <td colSpan={4} className="px-2 py-[3px]">
            <span className="lab block border-t border-dashed border-arena-edge pt-1">
              {band}
            </span>
          </td>
        </tr>
      )}
    </>
  );
}

function PhoneRow({
  item,
  accent,
  worn,
  pack,
  packHref,
  packState,
  selected,
  onSelect,
  band,
}: RowProps) {
  return (
    <>
      <li
        className={clsx(
          'flex flex-col gap-1.5 border-b border-arena-edge px-3 py-2.5',
          item.owned && 'bg-arena-text/[0.028]',
          item.owned && 'border-l-2 border-l-arena-edge2',
        )}
      >
        <div className="flex items-center justify-between gap-2">
          <Face item={item} accent={accent} selected={selected} onSelect={onSelect} />
          <span className="t-micro min-w-0 truncate text-right">
            <WornBy names={worn} />
          </span>
        </div>
        <span className="t-micro">
          <Source
            item={item}
            pack={pack}
            packHref={packHref}
            packState={packState}
          />
        </span>
        <Progress item={item} />
      </li>
      {band && (
        <li className="px-3 pb-1.5">
          <span className="lab block border-t border-dashed border-arena-edge pt-1.5">
            {band}
          </span>
        </li>
      )}
    </>
  );
}

/**
 * The look, and — where the page has a bot to wear it — the control that picks it.
 *
 * Pressed is `.btn-on`'s form with the resting border suppressed: a bordered box on every
 * one of twelve catalogue rows is exactly the noise the state pills were removed for.
 */
function Face({
  item,
  accent,
  selected,
  onSelect,
}: {
  item: CosmeticCatalogItem;
  accent: string;
  selected: boolean;
  onSelect?: (item: CosmeticCatalogItem) => void;
}) {
  const face = (
    <>
      <LookMark kind={item.kind} id={item.id} accent={accent} />
      <span className="t-body truncate">{item.label}</span>
    </>
  );
  if (!onSelect) return <span className="flex min-w-0 items-center gap-2.5">{face}</span>;
  return (
    <button
      type="button"
      onClick={() => onSelect(item)}
      aria-pressed={selected}
      className={clsx(
        'flex min-w-0 items-center gap-2.5 rounded-[3px] border px-1.5 py-1 text-left transition-colors',
        selected
          ? 'border-arena-edge2 bg-arena-raise'
          : 'border-transparent hover:border-arena-edge',
      )}
    >
      {face}
    </button>
  );
}

/** Starter, an achievement's own sentence, or the pack that sells it. */
function Source({
  item,
  pack,
  packHref,
  packState,
  linkClassName,
}: {
  item: CosmeticCatalogItem;
  pack: string | null;
  packHref?: string;
  packState?: { returnTo: string; returnLabel: string };
  linkClassName?: string;
}) {
  if (item.availability === 'starter') return <>Starter</>;
  if (item.unlock?.sourceKind === 'purchase') {
    if (pack === null) return <>{item.unlock.hint}</>;
    return packHref ? (
      <Link
        to={packHref}
        state={packState}
        className={clsx('text-link', linkClassName)}
      >
        {pack} pack ↑
      </Link>
    ) : (
      <>{pack} pack</>
    );
  }
  return <>{item.unlock?.hint ?? ''}</>;
}

/**
 * A hairline bar and the count that produced it.
 *
 * The unit is the accessible name rather than a heading over the bar: *Where it comes
 * from* already reads "Complete 100 ranked matches", and a second copy above every bar is
 * what made the old one wrong about the rating milestone.
 */
function Progress({ item }: { item: CosmeticCatalogItem }) {
  const progress = item.progress;
  if (!progress || item.owned) return null;
  const pct = Math.max(
    0,
    Math.min(100, (progress.current / Math.max(1, progress.target)) * 100),
  );
  const said = progressPhrase(progress);
  return (
    <span className="flex items-center gap-2" title={said}>
      <span
        role="progressbar"
        aria-label={said}
        aria-valuemin={0}
        aria-valuemax={progress.target}
        aria-valuenow={progress.current}
        className="block h-1 min-w-0 flex-1 overflow-hidden rounded-full bg-arena-edge2"
      >
        <span
          className="runtime-progress block h-full rounded-full bg-arena-dim"
          style={styleVariables({
            '--runtime-progress': `${pct}%`,
          })}
        />
      </span>
      <span className="val shrink-0">
        {progress.current}/{progress.target}
      </span>
    </span>
  );
}

function WornBy({ names }: { names: readonly string[] }) {
  if (names.length === 0) return null;
  return <>worn by {names.join(', ')}</>;
}

interface CosmeticUnlocksProps {
  catalog: CosmeticCatalog | null;
  accent: string;
  error?: string | null;
}

/**
 * The garage's slice: the looks this account can still *earn*.
 *
 * The whole catalogue — starters, earned and sold — is available from each owned bot's
 * appearance picker. This stays narrow on purpose: the garage asks "what am I close to",
 * not "what could this particular bot look like". It renders through the rows above, so
 * progress and unlock wording cannot drift from the picker.
 */
export default function CosmeticUnlocks({
  catalog,
  accent,
  error,
}: CosmeticUnlocksProps) {
  const earnable = (catalog?.items ?? []).filter(
    (item) =>
      !item.owned &&
      item.availability === 'entitlement' &&
      item.unlock?.sourceKind !== 'purchase',
  );

  return (
    <section className="flex flex-col gap-3.5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="lab">Unlocks</h2>
        <Link to="/store" className="btn">
          Shop
        </Link>
      </div>
      {!catalog && !error && <p className="t-micro">Loading cosmetic progress…</p>}
      {error && <p className="t-body text-arena-hot">{error}</p>}
      <LookLibrary
        title="Chassis to earn"
        items={earnable.filter((item) => item.kind === BOT_LOOK_KIND)}
        accent={accent}
      />
      <LookLibrary
        title="Shots to earn"
        items={earnable.filter((item) => item.kind === PROJECTILE_LOOK_KIND)}
        accent={accent}
      />
    </section>
  );
}
