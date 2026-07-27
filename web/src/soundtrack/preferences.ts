import {
  readLocalSetting,
  writeLocalSetting,
} from '../audio/localSettings';
import type { SoundtrackPlaybackMode } from './types';

export const SOUNDTRACK_ENABLED_STORAGE_KEY =
  'nilbots.soundtrack.enabled.v1';
// V2 intentionally retires the louder review calibration so existing preview
// sessions receive the production mix instead of carrying 0.62 forward.
export const SOUNDTRACK_VOLUME_STORAGE_KEY =
  'nilbots.soundtrack.volume.v2';
export const DEFAULT_SOUNDTRACK_VOLUME = 0.4;

/**
 * Straight-through playback is the default. Adaptive scoring remains an
 * explicit viewer experiment selected with `?score=adaptive`.
 */
export function soundtrackPlaybackMode(
  search: string,
): SoundtrackPlaybackMode {
  return new URLSearchParams(search).get('score') === 'adaptive'
    ? 'adaptive'
    : 'straight';
}

/**
 * Music is opted in by default. Only an explicit persisted opt-out disables
 * it, so missing or stale preference values do not silently mute the viewer.
 */
export function soundtrackEnabledPreference(
  storedValue: string | null,
): boolean {
  return storedValue !== 'false';
}

export function readSoundtrackEnabledPreference(): boolean {
  return soundtrackEnabledPreference(
    readLocalSetting(SOUNDTRACK_ENABLED_STORAGE_KEY),
  );
}

export function writeSoundtrackEnabledPreference(enabled: boolean): void {
  writeLocalSetting(
    SOUNDTRACK_ENABLED_STORAGE_KEY,
    enabled ? 'true' : 'false',
  );
}

export function soundtrackVolumePreference(
  storedValue: string | null,
): number {
  if (storedValue === null) return DEFAULT_SOUNDTRACK_VOLUME;
  const stored = Number(storedValue);
  return Number.isFinite(stored) && stored >= 0 && stored <= 1
    ? stored
    : DEFAULT_SOUNDTRACK_VOLUME;
}

export function readSoundtrackVolumePreference(): number {
  return soundtrackVolumePreference(
    readLocalSetting(SOUNDTRACK_VOLUME_STORAGE_KEY),
  );
}

export function writeSoundtrackVolumePreference(volume: number): void {
  writeLocalSetting(SOUNDTRACK_VOLUME_STORAGE_KEY, String(volume));
}
