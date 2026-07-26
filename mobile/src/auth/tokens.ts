import * as SecureStore from 'expo-secure-store';

/**
 * Where the session lives between launches: the OS keychain, not AsyncStorage.
 *
 * A refresh token is a long-lived credential for the account — with `offline_access` it
 * mints access tokens until it is revoked. AsyncStorage is a plain file in the app
 * container, readable on a jailbroken or backed-up device; SecureStore is the keychain.
 *
 * Stored as one blob rather than three keys so a partial write cannot leave an access
 * token without the refresh token that renews it.
 */
export type StoredSession = {
  accessToken: string;
  refreshToken?: string;
  /** Epoch milliseconds. Absolute, because a relative lifetime cannot survive a relaunch. */
  expiresAt?: number;
};

const KEY = 'nilbots.session';

export async function readSession(): Promise<StoredSession | null> {
  const raw = await SecureStore.getItemAsync(KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as StoredSession;
  } catch {
    // A blob we cannot parse is a blob we cannot use. Drop it rather than wedging every
    // launch on the same failure.
    await clearSession();
    return null;
  }
}

export async function writeSession(session: StoredSession): Promise<void> {
  await SecureStore.setItemAsync(KEY, JSON.stringify(session));
}

export async function clearSession(): Promise<void> {
  await SecureStore.deleteItemAsync(KEY);
}
