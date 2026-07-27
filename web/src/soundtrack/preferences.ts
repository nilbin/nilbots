import {
  readLocalSetting,
  writeLocalSetting,
} from '../audio/localSettings';
import type { SoundtrackPlaybackMode } from './types';

export const SOUNDTRACK_ENABLED_STORAGE_KEY =
  'nilbots.soundtrack.enabled.v1';

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
