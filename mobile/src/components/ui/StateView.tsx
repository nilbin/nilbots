import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';

import { Arena, Space } from '@/theme/arena';

/**
 * The loading / error / empty branches every screen owes its user, in one place.
 *
 * Emptiness is a legitimate state here rather than a bug — a fresh ladder genuinely has
 * no entries — so `empty` takes a sentence explaining what would fill it. Silence would
 * read as breakage.
 */
export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return (
    <View style={styles.centre}>
      <ActivityIndicator color={Arena.accent} />
      <Text style={styles.dim}>{label}</Text>
    </View>
  );
}

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const message = error instanceof Error ? error.message : 'Something went wrong.';
  return (
    <View style={styles.centre}>
      <Text style={styles.errorTitle}>Couldn’t load that</Text>
      <Text style={styles.dim}>{message}</Text>
      {onRetry ? (
        <Pressable onPress={onRetry} style={styles.retry} accessibilityRole="button">
          <Text style={styles.retryLabel}>Try again</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

export function EmptyState({ title, detail }: { title: string; detail?: string }) {
  return (
    <View style={styles.centre}>
      <Text style={styles.emptyTitle}>{title}</Text>
      {detail ? <Text style={styles.dim}>{detail}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  centre: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: Space.sm,
    padding: Space.xl,
  },
  dim: { color: Arena.dim, fontSize: 13, textAlign: 'center' },
  errorTitle: { color: Arena.text, fontSize: 15, fontWeight: '700' },
  emptyTitle: { color: Arena.text, fontSize: 15, fontWeight: '700', textAlign: 'center' },
  retry: {
    marginTop: Space.sm,
    paddingHorizontal: Space.lg,
    paddingVertical: Space.sm,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: Arena.edge,
    backgroundColor: Arena.panel,
  },
  retryLabel: { color: Arena.accent, fontSize: 13, fontWeight: '600' },
});
