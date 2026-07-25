import { StyleSheet, Text, View } from 'react-native';

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
}: {
  title: string;
  status?: string;
  showWordmark?: boolean;
}) {
  return (
    <View style={styles.header}>
      {showWordmark ? <Wordmark size={22} /> : null}
      <Text style={styles.title}>{title}</Text>
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
  title: { color: Arena.text, fontSize: 26, fontWeight: '800', marginTop: Space.xs },
  status: { ...Mono, color: Arena.dim, fontSize: 11 },
});
