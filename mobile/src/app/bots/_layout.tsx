import { Stack } from 'expo-router';

import { Arena } from '@/theme/arena';

/**
 * Anchor deep links to the list. Opening nilbots://bots/<slug> cold would otherwise make
 * the detail screen the root of the stack — no back button, and no way to reach the
 * roster except the tab bar. With this, the list is always beneath it.
 */
export const unstable_settings = { initialRouteName: 'index' };

/** The Bots tab is a stack so a bot row can push its detail screen. */
export default function BotsLayout() {
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: Arena.bg },
        headerTintColor: Arena.accent,
        headerTitleStyle: { color: Arena.text },
        contentStyle: { backgroundColor: Arena.bg },
        // Without this the back button falls back to the previous *route name* — which
        // for a file-based router means it literally reads "index".
        headerBackTitle: 'Bots',
        // A hairline instead of the default heavy divider, matching the site's panels.
        headerShadowVisible: false,
      }}>
      <Stack.Screen name="index" options={{ headerShown: false, title: 'Bots' }} />
      <Stack.Screen name="[key]" options={{ title: '' }} />
    </Stack>
  );
}
