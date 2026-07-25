import { useEffect, useState } from 'react';
import ProjectilePreview from '../../components/ProjectilePreview';
import {
  botLook,
  botLookOptions,
  projectileLook,
  projectileLookOptions,
} from '../../render/arenaThemes';
import { api, type BotDetail } from '../api';
import {
  BOT_LOOK_KIND,
  cosmeticItem,
  PROJECTILE_LOOK_KIND,
  useCosmeticCatalog,
} from '../cosmetics';

const botLooks = botLookOptions();
const projectileLooks = projectileLookOptions();

interface AppearanceEditorProps {
  bot: Pick<BotDetail, 'id' | 'accent' | 'lookId' | 'projectileLookId'>;
  onSaved: () => Promise<unknown> | void;
  entitlementRevision?: number;
}

export default function AppearanceEditor({
  bot,
  onSaved,
  entitlementRevision = 0,
}: AppearanceEditorProps) {
  const [accent, setAccent] = useState(bot.accent);
  const [lookId, setLookId] = useState(bot.lookId);
  const [projectileLookId, setProjectileLookId] = useState(
    bot.projectileLookId,
  );
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { catalog, error: catalogError } =
    useCosmeticCatalog(entitlementRevision);

  useEffect(() => {
    setAccent(bot.accent);
    setLookId(bot.lookId);
    setProjectileLookId(bot.projectileLookId);
  }, [bot.accent, bot.lookId, bot.projectileLookId]);

  const chassis = botLook(lookId);
  const projectile = projectileLook(projectileLookId);
  const selectedLook = cosmeticItem(catalog, BOT_LOOK_KIND, lookId);
  const selectedProjectile = cosmeticItem(
    catalog,
    PROJECTILE_LOOK_KIND,
    projectileLookId,
  );
  const selectionOwned =
    selectedLook?.owned === true && selectedProjectile?.owned === true;
  const dirty =
    accent !== bot.accent ||
    lookId !== bot.lookId ||
    projectileLookId !== bot.projectileLookId;

  const selectLook = (nextLookId: string) => {
    setLookId(nextLookId);
    const defaultProjectile = botLook(nextLookId).defaultProjectileLookId;
    if (
      defaultProjectile &&
      cosmeticItem(
        catalog,
        PROJECTILE_LOOK_KIND,
        defaultProjectile,
      )?.owned === true
    )
      setProjectileLookId(defaultProjectile);
  };

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setMessage(null);
    setError(null);
    try {
      await api.put(`/api/bots/${bot.id}/appearance`, {
        accent,
        lookId,
        projectileLookId,
      });
      await onSaved();
      setMessage('Appearance saved for future matches.');
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : 'Could not save appearance.',
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="max-w-2xl rounded-xl border border-arena-edge bg-arena-panel p-5">
      <h2 className="mb-4 font-mono text-xs tracking-widest text-arena-dim">
        APPEARANCE
      </h2>
      <form
        onSubmit={save}
        className="grid gap-5 sm:grid-cols-[150px_minmax(0,1fr)]"
      >
        <div className="flex min-h-36 flex-col items-center justify-center gap-3 rounded-lg border border-arena-edge bg-arena-bg/70 p-4">
          <img
            src={chassis.imageUrl}
            alt={`${chassis.label} chassis`}
            className="size-24 object-contain"
          />
          <div className="flex items-center gap-2 text-xs text-arena-dim">
            <ProjectilePreview
              look={projectile}
              accent={accent}
              className="h-7 w-14"
            />
            <span>{projectile.label}</span>
          </div>
        </div>
        <div className="flex flex-col gap-3">
          <label className="flex items-center gap-3 text-xs text-arena-dim">
            Accent
            <input
              type="color"
              value={accent}
              onChange={(event) => setAccent(event.target.value)}
              className="h-8 w-14 cursor-pointer rounded border border-arena-edge bg-arena-bg"
            />
            <span className="font-mono">{accent}</span>
          </label>
          <label className="flex flex-col gap-1 text-xs text-arena-dim">
            Chassis
            <select
              value={lookId}
              onChange={(event) => selectLook(event.target.value)}
              className="rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm text-arena-text outline-none focus:border-arena-accent"
            >
              {botLooks.map((look) => (
                <option
                  key={look.id}
                  value={look.id}
                  disabled={
                    cosmeticItem(catalog, BOT_LOOK_KIND, look.id)?.owned !== true
                  }
                >
                  {look.label}
                  {cosmeticItem(catalog, BOT_LOOK_KIND, look.id)?.owned
                    ? ''
                    : ` — locked: ${
                        cosmeticItem(catalog, BOT_LOOK_KIND, look.id)?.unlock
                          ?.hint ?? 'Unlock required'
                      }`}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-xs text-arena-dim">
            Projectile
            <select
              value={projectileLookId}
              onChange={(event) => setProjectileLookId(event.target.value)}
              className="rounded-md border border-arena-edge bg-arena-bg px-3 py-2 text-sm text-arena-text outline-none focus:border-arena-accent"
            >
              {projectileLooks.map((look) => (
                <option
                  key={look.id}
                  value={look.id}
                  disabled={
                    cosmeticItem(
                      catalog,
                      PROJECTILE_LOOK_KIND,
                      look.id,
                    )?.owned !== true
                  }
                >
                  {look.label}
                  {cosmeticItem(catalog, PROJECTILE_LOOK_KIND, look.id)?.owned
                    ? ''
                    : ` — locked: ${
                        cosmeticItem(
                          catalog,
                          PROJECTILE_LOOK_KIND,
                          look.id,
                        )?.unlock?.hint ?? 'Unlock required'
                      }`}
                </option>
              ))}
            </select>
          </label>
          <p className="text-[11px] leading-relaxed text-arena-dim">
            Changes apply to future matches. Existing replays retain their
            snapshotted appearance.
          </p>
          {error && <p className="text-sm text-red-400">{error}</p>}
          {catalogError && <p className="text-sm text-red-400">{catalogError}</p>}
          {message && <p className="text-sm text-emerald-400">{message}</p>}
          <button
            type="submit"
            disabled={!dirty || saving || !catalog || !selectionOwned}
            className="self-start rounded-md bg-arena-accent px-4 py-2 text-sm font-semibold text-slate-950 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {saving ? 'Saving…' : 'Save appearance'}
          </button>
        </div>
      </form>
    </section>
  );
}
