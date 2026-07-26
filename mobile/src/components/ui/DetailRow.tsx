import { StyleSheet, Text, View } from 'react-native';

import { Arena, Mono, Space } from '@/theme/arena';

/**
 * A labelled fact: dim label left, value right. Several stacked inside one card read as
 * a specification table, which is what a match's map, seed and hash are.
 *
 * `mono` for anything a machine produced — seeds, hashes, tick counts — matching the
 * site's rule that prose is sans and data is mono.
 *
 * `truncate` for values with no meaningful end, like a hash. Off by default, because
 * silently clipping something a reader needs whole is worse than letting it wrap.
 */
export function DetailRow({
  label,
  value,
  mono = false,
  truncate = false,
  tone,
}: {
  label: string;
  value: string;
  mono?: boolean;
  truncate?: boolean;
  tone?: 'error';
}) {
  return (
    <View style={styles.row}>
      <Text style={styles.label}>{label}</Text>
      <Text
        style={[styles.value, mono && styles.mono, tone === 'error' && styles.error]}
        numberOfLines={truncate ? 1 : undefined}
        ellipsizeMode="middle">
        {value}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'space-between',
    gap: Space.lg,
    paddingVertical: Space.xs,
  },
  label: { color: Arena.dim, fontSize: 12, textTransform: 'uppercase', letterSpacing: 1 },
  value: { color: Arena.text, fontSize: 13, flexShrink: 1, textAlign: 'right' },
  mono: { ...Mono, fontSize: 12 },
  error: { color: Arena.live },
});
