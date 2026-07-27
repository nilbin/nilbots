import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import Constants from 'expo-constants';
import { useEffect } from 'react';
import { Platform } from 'react-native';
import * as SecureStore from 'expo-secure-store';

import { api } from '@/api/client';

const DEVICE_ID_KEY = 'nilbots.deviceId';

/**
 * Tell the server where to push, for as long as someone is signed in.
 *
 * Registration follows the *session*, not the app: two people sharing a phone must not
 * inherit each other's results, so signing out deletes the registration rather than
 * leaving the token pointed at whoever logged in first.
 *
 * Runs on every launch rather than once. Expo rotates push tokens, and a stale one is
 * indistinguishable from a live one until a send fails — refreshing on launch is what
 * keeps the server addressing a phone that still exists.
 */
export function usePushRegistration(signedIn: boolean) {
  useEffect(() => {
    let cancelled = false;

    const register = async () => {
      // A simulator has no push token to give — asking throws, and there is nothing
      // useful to register even if it did not.
      if (!Device.isDevice) return;

      const { status: existing } = await Notifications.getPermissionsAsync();
      // Only ask if we have never asked. Re-prompting a player who said no is both
      // impossible on iOS and rude to attempt.
      const status =
        existing === 'undetermined'
          ? (await Notifications.requestPermissionsAsync()).status
          : existing;
      if (status !== 'granted' || cancelled) return;

      const projectId =
        Constants.expoConfig?.extra?.eas?.projectId ??
        Constants.easConfig?.projectId;
      const token = await Notifications.getExpoPushTokenAsync(
        projectId ? { projectId } : undefined,
      );
      if (cancelled) return;

      await api.registerDevice({
        pushToken: token.data,
        deviceId: await deviceId(),
        platform: Platform.OS,
      });
    };

    const unregister = async () => {
      await api.unregisterDevice(await deviceId());
    };

    // Non-fatal, but not silent. Push is an enhancement — a player who cannot register
    // still gets everything in-app and in the inbox, so an error banner about notification
    // plumbing at launch would be noise. A swallowed failure with *no* trace is worse
    // though: this path only runs on real hardware, so the one place it can go wrong is
    // the one place nobody is watching a console. `warn` keeps it out of the player's way
    // and in the developer's.
    const report = (reason: unknown) =>
      console.warn('[push] registration failed', reason);

    if (signedIn) void register().catch(report);
    else void unregister().catch(report);

    return () => {
      cancelled = true;
    };
  }, [signedIn]);
}

/**
 * A stable id for this installation.
 *
 * Not hardware-derived — both platforms restrict that, and this only has to be stable
 * enough to recognise "same install, new token" so a reinstall replaces its registration
 * instead of adding a second one. Kept in the keychain so it survives app updates.
 */
async function deviceId(): Promise<string> {
  const stored = await SecureStore.getItemAsync(DEVICE_ID_KEY);
  if (stored) return stored;
  const created = `${Platform.OS}-${Math.random().toString(36).slice(2)}${Date.now().toString(36)}`;
  await SecureStore.setItemAsync(DEVICE_ID_KEY, created);
  return created;
}
