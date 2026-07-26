import { StyleSheet, Text, View } from 'react-native';

import type { ArenaHeader, ArenaResult } from '@/components/arena/protocol';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * Who won, and how it ended.
 *
 * One component for both layouts because it is the same statement either way — only the
 * framing differs: in portrait it sits at the top of the panels, sideways it floats
 * centred over the arena as a card.
 */
export function ArenaOutcome({
  result,
  header,
  framed = false,
}: {
  result: NonNullable<ArenaResult>;
  header: ArenaHeader | null;
  /** Draw it as a card over the arena, rather than inline above the panels. */
  framed?: boolean;
}) {
  const winner =
    result.winnerSlot === null
      ? 'DRAW'
      : `${header?.participants[result.winnerSlot]?.name ?? 'winner'} WINS`;

  return (
    <View style={framed ? styles.card : styles.inline}>
      <Text style={styles.line}>{winner}</Text>
      <Text style={styles.reason}>
        {result.reason} · tick {result.endTick}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  inline: { alignItems: 'center', paddingVertical: Space.sm, gap: 2 },
  card: {
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: 12,
    backgroundColor: Arena.panel,
    paddingHorizontal: Space.xl,
    paddingVertical: Space.lg,
    alignItems: 'center',
    gap: 2,
  },
  line: { color: Arena.text, fontSize: 20, fontWeight: '800', letterSpacing: 1 },
  reason: { ...Mono, color: Arena.dim, fontSize: 11 },
});
