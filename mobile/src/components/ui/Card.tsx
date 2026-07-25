import type { ReactNode } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';

import { Arena, Radius, Space } from '@/theme/arena';

/**
 * A panel on the arena background. Pass `onPress` to make it a row in a list — the
 * pressed state is handled here so every tappable surface behaves identically.
 */
export function Card({
  children,
  onPress,
  accessibilityLabel,
}: {
  children: ReactNode;
  onPress?: () => void;
  accessibilityLabel?: string;
}) {
  if (!onPress) return <View style={styles.card}>{children}</View>;

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      style={({ pressed }) => [styles.card, pressed && styles.pressed]}>
      {children}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: Arena.panel,
    borderColor: Arena.edge,
    borderWidth: 1,
    borderRadius: Radius.md,
    padding: Space.lg,
  },
  pressed: { borderColor: Arena.accent, opacity: 0.85 },
});
