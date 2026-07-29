import { botLook, botLookOptions, projectileLookOptions } from '../../render/arenaThemes';
import type { CosmeticCatalog } from '../api';
import {
  BOT_LOOK_KIND,
  cosmeticItem,
  PROJECTILE_LOOK_KIND,
} from '../cosmetics';

const botLooks = botLookOptions();
const projectileLooks = projectileLookOptions();

interface AppearanceFieldsProps {
  catalog: CosmeticCatalog | null;
  accent: string;
  lookId: string;
  projectileLookId: string;
  accentLabel?: string;
  onAccentChange: (accent: string) => void;
  onLookChange: (lookId: string) => void;
  onProjectileLookChange: (lookId: string) => void;
}

/** The single picker contract shared by new-bot and existing-bot forms. */
export default function AppearanceFields({
  catalog,
  accent,
  lookId,
  projectileLookId,
  accentLabel = 'Accent',
  onAccentChange,
  onLookChange,
  onProjectileLookChange,
}: AppearanceFieldsProps) {
  const selectLook = (nextLookId: string) => {
    onLookChange(nextLookId);
    const defaultProjectile = botLook(nextLookId).defaultProjectileLookId;
    if (
      defaultProjectile &&
      cosmeticItem(
        catalog,
        PROJECTILE_LOOK_KIND,
        defaultProjectile,
      )?.owned === true
    ) {
      onProjectileLookChange(defaultProjectile);
    }
  };

  return (
    <>
      <label className="t-meta flex items-center gap-3">
        {accentLabel}
        <input
          type="color"
          value={accent}
          onChange={(event) => onAccentChange(event.target.value)}
          className="field h-8 w-14 cursor-pointer p-0.5"
        />
        <span className="val">{accent}</span>
      </label>
      <label className="t-meta flex flex-col gap-1">
        Chassis
        <select
          value={lookId}
          onChange={(event) => selectLook(event.target.value)}
          className="field"
        >
          {botLooks.map((look) => {
            const item = cosmeticItem(catalog, BOT_LOOK_KIND, look.id);
            return (
              <option
                key={look.id}
                value={look.id}
                disabled={item?.owned !== true}
              >
                {look.label}
                {item?.owned
                  ? ''
                  : ` — locked: ${item?.unlock?.hint ?? 'Unlock required'}`}
              </option>
            );
          })}
        </select>
      </label>
      <label className="t-meta flex flex-col gap-1">
        Projectile
        <select
          value={projectileLookId}
          onChange={(event) => onProjectileLookChange(event.target.value)}
          className="field"
        >
          {projectileLooks.map((look) => {
            const item = cosmeticItem(
              catalog,
              PROJECTILE_LOOK_KIND,
              look.id,
            );
            return (
              <option
                key={look.id}
                value={look.id}
                disabled={item?.owned !== true}
              >
                {look.label}
                {item?.owned
                  ? ''
                  : ` — locked: ${item?.unlock?.hint ?? 'Unlock required'}`}
              </option>
            );
          })}
        </select>
      </label>
    </>
  );
}

export function appearanceSelectionOwned(
  catalog: CosmeticCatalog | null,
  lookId: string,
  projectileLookId: string,
): boolean {
  return (
    cosmeticItem(catalog, BOT_LOOK_KIND, lookId)?.owned === true &&
    cosmeticItem(catalog, PROJECTILE_LOOK_KIND, projectileLookId)?.owned ===
      true
  );
}
