import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { Arena, Mono, Space } from '@/theme/arena';

export type Stat = {
  label: string;
  /**
   * A string or number gets the standard headline treatment. Anything else renders as
   * given — a value that needs its own colours or spans supplies its own typography, so
   * this stays a layout component rather than growing a style prop per case.
   */
  value: ReactNode;
};

/** How the cells divide the row's width. */
export type StatRowLayout =
  /** Sized to their content, left to right. Right for a card the numbers are the whole of. */
  | 'packed'
  /** Equal columns spanning the full width. Right under a heading the row has to sit with. */
  | 'spread';

/**
 * A row of headline numbers — rating/rank/sets, wins/losses/draws.
 *
 * `packed` is the default because a card whose only content is its numbers has nothing
 * to align to, and content-sized cells keep them reading as one group.
 *
 * `spread` is for a row beneath a heading. Left-packed cells there stop at whatever
 * width the digits happen to need, which lands nowhere in particular relative to the
 * text above and reads as a layout mistake. Equal columns divide the card instead, so
 * the row spans the same width as the block above it whatever the digits are.
 *
 * Values are pre-formatted by the caller: this decides layout, not rounding.
 */
export function StatRow({
  stats,
  layout = 'packed',
}: {
  stats: readonly Stat[];
  layout?: StatRowLayout;
}) {
  return (
    <View style={[styles.row, layout === 'packed' && styles.packed]}>
      {stats.map((stat) => (
        <View key={stat.label} style={layout === 'spread' && styles.spreadCell}>
          {typeof stat.value === 'string' || typeof stat.value === 'number' ? (
            <Text style={styles.value} numberOfLines={1}>
              {stat.value}
            </Text>
          ) : (
            stat.value
          )}
          <Text style={styles.label} numberOfLines={1}>
            {stat.label}
          </Text>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row' },
  packed: { gap: Space.xl },
  spreadCell: { flex: 1, minWidth: 0 },
  value: { ...Mono, color: Arena.text, fontSize: 20, fontWeight: '800' },
  label: { color: Arena.dim, fontSize: 11, letterSpacing: 1, textTransform: 'uppercase' },
});
