import type { ReactNode } from 'react';
import { StyleSheet, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { Arena, Space } from '@/theme/arena';

/**
 * Page chrome: arena background, safe-area insets, consistent gutters. Every route
 * renders inside one so padding never drifts screen to screen.
 */
export function Screen({ children, padded = true }: { children: ReactNode; padded?: boolean }) {
  return (
    <SafeAreaView style={styles.safe} edges={['top', 'left', 'right']}>
      <View style={[styles.body, padded && styles.padded]}>{children}</View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Arena.bg },
  body: { flex: 1 },
  padded: { paddingHorizontal: Space.lg },
});
