import { Stack } from 'expo-router';

import { Arena } from '@/theme/arena';

/** The Bots tab is a stack so a bot row can push its detail screen. */
export default function BotsLayout() {
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: Arena.bg },
        headerTintColor: Arena.accent,
        headerTitleStyle: { color: Arena.text },
        contentStyle: { backgroundColor: Arena.bg },
      }}>
      <Stack.Screen name="index" options={{ headerShown: false }} />
      <Stack.Screen name="[key]" options={{ title: '' }} />
    </Stack>
  );
}
