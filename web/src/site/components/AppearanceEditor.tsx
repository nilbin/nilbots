import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import clsx from 'clsx';
import IdentityChip from '../../components/IdentityChip';
import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import { type BotDetail, type CosmeticCatalogItem } from '../api';
import {
  BOT_LOOK_KIND,
  cosmeticItem,
  PROJECTILE_LOOK_KIND,
} from '../cosmetics';
import { errorMessage } from '../errorMessage';
import { useCosmetics, useStore, useUpdateAppearance } from '../queries';
import { appearanceSelectionOwned } from './AppearanceFields';
import { LookLibrary } from './CosmeticUnlocks';
import { ErrorState, LoadingState } from './StateView';

interface AppearanceEditorProps {
  bot: Pick<
    BotDetail,
    'id' | 'name' | 'accent' | 'lookId' | 'projectileLookId'
  >;
  /** Slug or id, whichever the page was routed by — the bot query is keyed on it. */
  botKey: string;
  entitlementRevision?: number;
}

/**
 * The bot's wardrobe: preview every skin, including locked ones, then equip only a pair
 * the account owns. The catalogue remains visible while the shop is unavailable because
 * earning, owning and wearing a look do not depend on checkout.
 */
export default function AppearanceEditor({
  bot,
  botKey,
  entitlementRevision = 0,
}: AppearanceEditorProps) {
  const [accent, setAccent] = useState(bot.accent);
  const [lookId, setLookId] = useState(bot.lookId);
  const [projectileLookId, setProjectileLookId] = useState(
    bot.projectileLookId,
  );
  const appearance = useUpdateAppearance(botKey, bot.id);
  const cosmetics = useCosmetics(entitlementRevision);
  const store = useStore();

  useEffect(() => {
    setAccent(bot.accent);
    setLookId(bot.lookId);
    setProjectileLookId(bot.projectileLookId);
  }, [bot.accent, bot.lookId, bot.projectileLookId]);

  const packByItemKey = useMemo(() => {
    const result = new Map<string, { id: string; label: string }>();
    for (const category of store.data?.categories ?? []) {
      for (const pack of category.packs) {
        for (const item of pack.items) {
          result.set(item.key, { id: pack.id, label: pack.label });
        }
      }
    }
    return result;
  }, [store.data]);

  if (cosmetics.isPending) {
    return <LoadingState label="Loading the wardrobe…" />;
  }
  if (cosmetics.isError) {
    return (
      <ErrorState
        error={cosmetics.error}
        onRetry={() => void cosmetics.refetch()}
      />
    );
  }

  const catalog = cosmetics.data;
  const chassisItems = catalog.items.filter(
    (item) => item.kind === BOT_LOOK_KIND,
  );
  const projectileItems = catalog.items.filter(
    (item) => item.kind === PROJECTILE_LOOK_KIND,
  );
  const chassis = botLook(lookId);
  const projectile = projectileLook(projectileLookId);
  const selectionOwned = appearanceSelectionOwned(
    catalog,
    lookId,
    projectileLookId,
  );
  const dirty =
    accent !== bot.accent ||
    lookId !== bot.lookId ||
    projectileLookId !== bot.projectileLookId;
  const selectedItems = [
    cosmeticItem(catalog, BOT_LOOK_KIND, lookId),
    cosmeticItem(catalog, PROJECTILE_LOOK_KIND, projectileLookId),
  ].filter((item): item is CosmeticCatalogItem => item !== undefined);
  const locked = selectedItems.filter((item) => !item.owned);

  const select = (item: CosmeticCatalogItem) => {
    appearance.reset();
    if (item.kind === PROJECTILE_LOOK_KIND) {
      setProjectileLookId(item.id);
      return;
    }
    if (item.kind !== BOT_LOOK_KIND) return;
    setLookId(item.id);
    const paired = botLook(item.id).defaultProjectileLookId;
    if (
      paired &&
      cosmeticItem(catalog, PROJECTILE_LOOK_KIND, paired) !== undefined
    ) {
      // The matching shot follows the chassis even when the pair is locked: locked rows
      // are a preview, and the save guard below still prevents equipping either item.
      setProjectileLookId(paired);
    }
  };

  const save = (event: React.FormEvent) => {
    event.preventDefault();
    appearance.mutate({ accent, lookId, projectileLookId });
  };

  const packLabel = (item: CosmeticCatalogItem) =>
    packByItemKey.get(item.key)?.label ?? null;
  const packHref = (item: CosmeticCatalogItem) => {
    const pack = packByItemKey.get(item.key);
    return pack ? `/store#pack-${pack.id}` : null;
  };
  const shopReturnState = {
    returnTo: `/bots/${encodeURIComponent(botKey)}/appearance`,
    returnLabel: bot.name,
  };
  const selectedPurchasePack =
    locked
      .map((item) => packHref(item))
      .find((href): href is string => href !== null) ?? null;

  return (
    <form
      onSubmit={save}
      className={clsx(
        'flex flex-col gap-5',
        dirty && 'pb-20 sm:pb-0',
      )}
    >
      <section className="panel pad">
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center">
          <div className="flex min-h-28 shrink-0 items-center gap-4 rounded-[3px] border border-arena-edge bg-arena-bg px-4 py-3 sm:min-w-64">
            <img
              src={chassis.imageUrl}
              alt={`${chassis.label} chassis`}
              className="size-20 object-contain"
            />
            <ProjectilePreview
              look={projectile}
              accent={accent}
              className="h-5 w-16"
            />
          </div>
          <div className="min-w-0 flex-1">
            <p className="lab mb-2">Preview on {bot.name}</p>
            <IdentityChip
              name={bot.name}
              accent={accent}
              lookId={lookId}
              sub={`${chassis.label} · ${projectile.label}`}
              size={30}
              emphasized
              className="max-w-full"
              nameClassName="type-display text-base"
            />
            <label className="t-meta mt-3 flex min-h-11 flex-wrap items-center gap-2">
              Accent
              <input
                type="color"
                value={accent}
                onChange={(event) => {
                  appearance.reset();
                  setAccent(event.target.value);
                }}
                aria-label={`Accent for ${bot.name}`}
                className="h-11 w-14 cursor-pointer rounded-[3px] border border-arena-edge bg-arena-bg p-0.5"
              />
              <span className="val">{accent}</span>
            </label>
          </div>
        </div>

        <div className="mt-4 border-t border-arena-edge pt-3">
          <p className="t-micro">
            Changes apply to future matches. Existing replays retain their
            snapshotted appearance.
          </p>
          {locked.length > 0 && (
            <p id="appearance-save-note" className="t-body mt-2">
              Previewing a locked look.{' '}
              {locked.some(
                (item) => item.unlock?.sourceKind === 'purchase',
              ) ? (
                <Link
                  to={selectedPurchasePack ?? '/store'}
                  state={shopReturnState}
                  className="text-link"
                >
                  Find its pack in the Shop
                </Link>
              ) : (
                'Its row below shows how to earn it.'
              )}
            </p>
          )}
          {!selectionOwned && locked.length === 0 && (
            <p id="appearance-save-note" className="t-body mt-2">
              This selection is not available to equip.
            </p>
          )}
          {appearance.isError && (
            <p className="t-body mt-2 text-arena-hot">
              {errorMessage(appearance.error, 'Could not save appearance.')}
            </p>
          )}
          {store.isError && (
            <p className="t-micro mt-2">
              Shop pack links are temporarily unavailable; owned and earned
              looks are unaffected.
            </p>
          )}
          {appearance.isSuccess && !dirty && (
            <p className="t-body mt-2 text-arena-ok">
              Appearance saved for future matches.
            </p>
          )}
          <button
            type="submit"
            disabled={!dirty || appearance.isPending || !selectionOwned}
            aria-describedby={!selectionOwned ? 'appearance-save-note' : undefined}
            className="btn btn-strong mt-3 min-h-11 w-full disabled:opacity-40 sm:w-auto"
          >
            {appearance.isPending ? 'Saving…' : `Save for ${bot.name}`}
          </button>
        </div>
      </section>

      <LookLibrary
        title="Chassis"
        items={chassisItems}
        accent={accent}
        packLabel={packLabel}
        packHref={packHref}
        packState={shopReturnState}
        selectedId={lookId}
        onSelect={select}
      />
      <LookLibrary
        title="Projectiles"
        items={projectileItems}
        accent={accent}
        packLabel={packLabel}
        packHref={packHref}
        packState={shopReturnState}
        selectedId={projectileLookId}
        onSelect={select}
      />
      {dirty && (
        <section
          className="appearance-save-dock panel flex items-center gap-3 p-2.5"
          aria-live="polite"
        >
          <span className="min-w-0 grow">
            <span className="lab block">
              {selectionOwned ? 'Ready to equip' : 'Locked preview'}
            </span>
            <span className="t-micro mt-0.5 block truncate">
              {chassis.label} · {projectile.label}
            </span>
          </span>
          {selectionOwned ? (
            <button
              type="submit"
              disabled={appearance.isPending}
              className="btn btn-strong min-h-11 shrink-0"
            >
              {appearance.isPending ? 'Saving…' : 'Save'}
            </button>
          ) : selectedPurchasePack ? (
            <Link
              to={selectedPurchasePack}
              state={shopReturnState}
              className="btn inline-flex min-h-11 shrink-0 items-center"
            >
              Find pack
            </Link>
          ) : (
            <span className="pill shrink-0">Locked</span>
          )}
        </section>
      )}
    </form>
  );
}
