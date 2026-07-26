type LocalSettingsStore = Pick<Storage, 'getItem' | 'setItem'>;

export function readLocalSetting(
  key: string,
  store: LocalSettingsStore | null = browserStore(),
): string | null {
  try {
    return store?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

export function writeLocalSetting(
  key: string,
  value: string,
  store: LocalSettingsStore | null = browserStore(),
): void {
  try {
    store?.setItem(key, value);
  } catch {
    // Persistence is optional; audio remains usable in restricted webviews.
  }
}

function browserStore(): Storage | null {
  try {
    return typeof window === 'undefined' ? null : window.localStorage;
  } catch {
    return null;
  }
}
