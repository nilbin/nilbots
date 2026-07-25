import type { ReactNode } from 'react';
import { StyleSheet, View } from 'react-native';
import { SafeAreaView, type Edge } from 'react-native-safe-area-context';

import { Arena, Space } from '@/theme/arena';

/**
 * Page chrome: arena background, safe-area insets, consistent gutters. Every route
 * renders inside one so padding never drifts screen to screen.
 *
 * `hasHeader` matters more than it looks. A navigation header already consumes the top
 * safe-area inset, so a screen pushed inside a Stack must not claim it a second time —
 * doing so leaves an empty band the height of the status bar above the first card.
 * Tab roots have no header and do need it.
 */
export function Screen({
  children,
  padded = true,
  hasHeader = false,
}: {
  children: ReactNode;
  padded?: boolean;
  hasHeader?: boolean;
}) {
  const edges: Edge[] = hasHeader ? ['left', 'right'] : ['top', 'left', 'right'];

  return (
    <SafeAreaView style={styles.safe} edges={edges}>
      <View style={[styles.body, padded && styles.padded]}>{children}</View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: Arena.bg },
  body: { flex: 1 },
  padded: { paddingHorizontal: Space.lg },
});
