import { useEffect, useState } from 'react';
import {
  api,
  type CosmeticCatalog,
  type CosmeticCatalogItem,
} from './api';

export const BOT_LOOK_KIND = 'bot-look';
export const PROJECTILE_LOOK_KIND = 'projectile-look';

export function useCosmeticCatalog(revision = 0) {
  const [catalog, setCatalog] = useState<CosmeticCatalog | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let current = true;
    setError(null);
    void api
      .get<CosmeticCatalog>('/api/cosmetics')
      .then((value) => {
        if (current) setCatalog(value);
      })
      .catch((reason) => {
        if (!current) return;
        setError(
          reason instanceof Error
            ? reason.message
            : 'Could not load cosmetic unlocks.',
        );
      });
    return () => {
      current = false;
    };
  }, [revision]);

  return { catalog, error };
}

export function cosmeticItem(
  catalog: CosmeticCatalog | null,
  kind: CosmeticCatalogItem['kind'],
  id: string,
): CosmeticCatalogItem | undefined {
  return catalog?.items.find((item) => item.kind === kind && item.id === id);
}
