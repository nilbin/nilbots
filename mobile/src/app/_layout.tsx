import { DarkTheme, ThemeProvider } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState } from 'react';

import AppTabs from '@/components/app-tabs';
import { Arena } from '@/theme/arena';

SplashScreen.preventAutoHideAsync();

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
        <AppTabs />
      </ThemeProvider>
    </QueryClientProvider>
  );
}
