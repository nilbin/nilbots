import ProjectilePreview from '../../components/ProjectilePreview';
import { botLook, projectileLook } from '../../render/arenaThemes';
import type { CosmeticCatalog } from '../api';

interface CosmeticUnlocksProps {
  catalog: CosmeticCatalog | null;
  accent: string;
  error?: string | null;
}

export default function CosmeticUnlocks({
  catalog,
  accent,
  error,
}: CosmeticUnlocksProps) {
  const unlocks =
    catalog?.items.filter((item) => item.availability === 'entitlement') ?? [];

  return (
    <section className="max-w-2xl rounded-xl border border-arena-edge bg-arena-panel p-5">
      <h2 className="mb-3 font-mono text-xs tracking-widest text-arena-dim">
        UNLOCKS
      </h2>
      {!catalog && !error && (
        <p className="text-xs text-arena-dim">Loading cosmetic progress…</p>
      )}
      {error && <p className="text-sm text-arena-hot">{error}</p>}
      <ul className="grid gap-3 sm:grid-cols-2">
        {unlocks.map((item) => (
          <li
            key={item.key}
            className="flex min-h-24 items-center gap-3 rounded-lg border border-arena-edge bg-arena-bg/60 p-3"
          >
            <span className="flex size-16 shrink-0 items-center justify-center rounded-md bg-arena-bg/50">
              {item.kind === 'bot-look' ? (
                <img
                  src={botLook(item.id).imageUrl}
                  alt=""
                  className="size-14 object-contain"
                />
              ) : (
                <ProjectilePreview
                  look={projectileLook(item.id)}
                  accent={accent}
                  className="h-9 w-14"
                />
              )}
            </span>
            <span className="min-w-0">
              <span className="flex flex-wrap items-center gap-2">
                <strong className="text-sm">{item.label}</strong>
                <span
                  className={
                    item.owned
                      ? 'rounded bg-arena-ok/15 px-1.5 py-0.5 font-mono text-[9px] text-arena-ok'
                      : 'rounded bg-arena-dim/15 px-1.5 py-0.5 font-mono text-[9px] text-arena-dim'
                  }
                >
                  {item.owned ? 'UNLOCKED' : 'LOCKED'}
                </span>
              </span>
              <span className="mt-1 block text-[11px] leading-relaxed text-arena-dim">
                {item.unlock?.hint}
              </span>
              {item.progress && (
                <span className="mt-2 block">
                  <span className="mb-1 flex justify-between font-mono text-[9px] text-arena-dim">
                    <span>RANKED MATCHES</span>
                    <span>
                      {item.progress.current}/{item.progress.target}
                    </span>
                  </span>
                  <span className="block h-1 overflow-hidden rounded-full bg-arena-edge2">
                    <span
                      className="block h-full rounded-full bg-arena-accent"
                      style={{
                        width: `${
                          (item.progress.current / item.progress.target) * 100
                        }%`,
                      }}
                    />
                  </span>
                </span>
              )}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
