import Constants from 'expo-constants';
import { Platform } from 'react-native';

/**
 * Where the API lives.
 *
 * Resolution order:
 *  1. EXPO_PUBLIC_NILBOTS_API — set it to point anywhere (staging, a colleague's box).
 *  2. In development, the host running Metro. The iOS Simulator shares the host's
 *     network so 127.0.0.1 resolves, but a physical device over Expo Go does not —
 *     it needs the LAN address, which Expo already knows as the Metro host.
 *  3. Production.
 */
function resolveBaseUrl(): string {
  const explicit = process.env.EXPO_PUBLIC_NILBOTS_API;
  if (explicit) return explicit.replace(/\/$/, '');

  if (__DEV__) {
    // e.g. "192.168.1.24:8081" on a device, "127.0.0.1:8081" in the simulator.
    const metroHost = Constants.expoConfig?.hostUri?.split(':')[0];
    if (metroHost) return `http://${metroHost}:8080`;
    // Android emulators alias the host as 10.0.2.2; every other target can use loopback.
    return Platform.OS === 'android' ? 'http://10.0.2.2:8080' : 'http://127.0.0.1:8080';
  }

  return 'https://nilbots.com';
}

export const API_BASE_URL = resolveBaseUrl();
