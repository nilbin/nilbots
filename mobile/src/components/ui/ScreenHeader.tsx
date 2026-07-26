import { Pressable, StyleSheet, Text, View } from 'react-native';

import { Wordmark } from '@/components/ui/Wordmark';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * Top-level screen chrome: the wordmark once per tab root, then the screen's own title
 * and a monospace status line. Keeps the header identical across tabs, which is most of
 * what makes an app feel like one product rather than a set of pages.
 */
export function ScreenHeader({
  title,
  status,
  showWordmark = true,
  action,
}: {
  title: string;
  status?: string;
  showWordmark?: boolean;
  /** A single screen-level affordance, beside the title rather than in a bar of its own. */
  action?: { label: string; onPress: () => void };
}) {
  return (
    <View style={styles.header}>
      {showWordmark ? <Wordmark size={22} /> : null}
      <View style={styles.titleRow}>
        <Text style={styles.title}>{title}</Text>
        {action ? (
          <Pressable
            onPress={action.onPress}
            accessibilityRole="button"
            accessibilityLabel={action.label}
            hitSlop={10}>
            <Text style={styles.action}>{action.label}</Text>
          </Pressable>
        ) : null}
      </View>
      {status ? <Text style={styles.status}>{status}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  header: {
    paddingTop: Space.sm,
    paddingBottom: Space.lg,
    gap: Space.sm,
    borderBottomWidth: 1,
    borderBottomColor: Arena.edge,
    marginBottom: Space.lg,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'space-between',
    gap: Space.md,
  },
  title: { color: Arena.text, fontSize: 26, fontWeight: '800', marginTop: Space.xs },
  action: { ...Mono, color: Arena.accent, fontSize: 12 },
  status: { ...Mono, color: Arena.dim, fontSize: 11 },
});
