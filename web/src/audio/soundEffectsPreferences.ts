import {
  readLocalSetting,
  writeLocalSetting,
} from './localSettings';

export const SOUND_EFFECTS_VOLUME_STORAGE_KEY =
  'nilbots.sound-effects.volume.v1';
export const SOUND_EFFECTS_MUTED_STORAGE_KEY =
  'nilbots.sound-effects.muted.v1';
export const DEFAULT_SOUND_EFFECTS_VOLUME = 0.72;

export function soundEffectsMutedPreference(
  storedValue: string | null,
): boolean {
  return storedValue === 'true';
}

export function soundEffectsVolumePreference(
  storedValue: string | null,
): number {
  if (storedValue === null) return DEFAULT_SOUND_EFFECTS_VOLUME;
  const stored = Number(storedValue);
  return Number.isFinite(stored) && stored >= 0 && stored <= 1
    ? stored
    : DEFAULT_SOUND_EFFECTS_VOLUME;
}

export function readSoundEffectsMutedPreference(): boolean {
  return soundEffectsMutedPreference(
    readLocalSetting(SOUND_EFFECTS_MUTED_STORAGE_KEY),
  );
}

export function readSoundEffectsVolumePreference(): number {
  return soundEffectsVolumePreference(
    readLocalSetting(SOUND_EFFECTS_VOLUME_STORAGE_KEY),
  );
}

export function writeSoundEffectsMutedPreference(muted: boolean): void {
  writeLocalSetting(
    SOUND_EFFECTS_MUTED_STORAGE_KEY,
    muted ? 'true' : 'false',
  );
}

export function writeSoundEffectsVolumePreference(volume: number): void {
  writeLocalSetting(SOUND_EFFECTS_VOLUME_STORAGE_KEY, String(volume));
}
