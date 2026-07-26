import { type CosmeticCatalog, type CosmeticCatalogItem } from './api';
import { errorMessage } from './errorMessage';
import { useCosmetics } from './queries';

export const BOT_LOOK_KIND = 'bot-look';
export const PROJECTILE_LOOK_KIND = 'projectile-look';

/**
 * The unlock catalog, in the shape its two callers already render.
 *
 * They both want `{ catalog, error }` — a null catalog disables the pickers, and the
 * error prints beside them — so the query is adapted here rather than at each call site.
 */
export function useCosmeticCatalog(revision = 0) {
  const query = useCosmetics(revision);
  return {
    catalog: query.data ?? null,
    error: query.isError
      ? errorMessage(query.error, 'Could not load cosmetic unlocks.')
      : null,
  };
}

export function cosmeticItem(
  catalog: CosmeticCatalog | null,
  kind: CosmeticCatalogItem['kind'],
  id: string,
): CosmeticCatalogItem | undefined {
  return catalog?.items.find((item) => item.kind === kind && item.id === id);
}
