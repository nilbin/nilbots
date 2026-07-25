import { DarkTheme, Stack, ThemeProvider } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import { Arena } from '@/theme/arena';

SplashScreen.preventAutoHideAsync();

/**
 * Anchor deep links to the tabs. Opening nilbots://bots/<slug> cold would otherwise make
 * the detail screen the root of the stack — no back button, and no way to reach anything
 * else. With this the tab bar is always beneath it.
 */
export const unstable_settings = { initialRouteName: '(tabs)' };

/**
 * The arena is a dark surface on the web and reads badly inverted, so the app stays dark
 * regardless of the system scheme rather than following it.
 */
const Theme = {
  ...DarkTheme,
  colors: {
    ...DarkTheme.colors,
    background: Arena.bg,
    card: Arena.panel,
    text: Arena.text,
    border: Arena.edge,
    primary: Arena.accent,
  },
};

export default function RootLayout() {
  // Per-mount, not module-level: a module-level client survives fast-refresh and holds
  // stale query state across reloads.
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false },
        },
      }),
  );

  // preventAutoHideAsync above holds the splash until we say so; without this the app
  // never appears at all. Hiding on first render (rather than after the first query)
  // means the ladder's own loading state does the waiting, which is the honest place
  // for it — a splash that lingers on a slow network looks like a hang.
  useEffect(() => {
    void SplashScreen.hideAsync();
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider value={Theme}>
        <Stack
          screenOptions={{
            headerStyle: { backgroundColor: Arena.bg },
            headerTintColor: Arena.accent,
            headerTitleStyle: { color: Arena.text },
            contentStyle: { backgroundColor: Arena.bg },
            headerShadowVisible: false,
            // Without this the back button falls back to the previous *route name*, which
            // for a file-based router reads "(tabs)". It stays generic on purpose: these
            // screens push from the ladder, from the roster and from each other, so
            // naming one origin would be wrong from the others.
            headerBackTitle: 'Back',
          }}>
          <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
          {/* Detail screens live above the tab bar, not inside a tab's own stack. A bot
              opens from the ladder and from the roster; a match opens from a bot and
              from a set. Nested in one tab, every one of those pushes would silently
              switch tabs and then "back" would return somewhere you had never been. */}
          <Stack.Screen name="bots/[key]" options={{ title: '' }} />
          <Stack.Screen name="matches/[id]" options={{ title: 'Match' }} />
          <Stack.Screen name="sets/[id]" options={{ title: 'Ranked set' }} />
        </Stack>
      </ThemeProvider>
    </QueryClientProvider>
  );
}
