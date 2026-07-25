import { StyleSheet, Text } from 'react-native';

import { Arena, Mono } from '@/theme/arena';

/** Matches the value scale of the surface it sits on: `lg` for a headline stat band. */
export type BotRecordSize = 'sm' | 'lg';

const FONT_SIZE: Record<BotRecordSize, number> = { sm: 14, lg: 20 };

/**
 * A bot's wins, losses and draws as one tinted triple: `2-6-1`.
 *
 * The colours are the same ones a result carries everywhere else in the app — green for
 * a win, the broadcast red for a loss, dim for a draw — so the balance of a record is
 * legible before any of the digits are read.
 *
 * Colour is never the only signal. The digits stay in a fixed W-L-D order, callers label
 * it, and the whole thing reads as words to a screen reader, so the triple still works
 * for someone who cannot tell the two hues apart.
 */
export function BotRecord({
  wins,
  losses,
  draws,
  size = 'sm',
}: {
  wins: number;
  losses: number;
  draws: number;
  size?: BotRecordSize;
}) {
  return (
    <Text
      style={[styles.record, { fontSize: FONT_SIZE[size] }]}
      numberOfLines={1}
      accessibilityLabel={
        `${wins} ${wins === 1 ? 'win' : 'wins'}, ` +
        `${losses} ${losses === 1 ? 'loss' : 'losses'}, ` +
        `${draws} ${draws === 1 ? 'draw' : 'draws'}`
      }>
      <Text style={styles.win}>{wins}</Text>
      <Text style={styles.separator}>-</Text>
      <Text style={styles.loss}>{losses}</Text>
      <Text style={styles.separator}>-</Text>
      <Text style={styles.draw}>{draws}</Text>
    </Text>
  );
}

const styles = StyleSheet.create({
  record: { ...Mono, fontWeight: '800' },
  win: { color: Arena.ok },
  loss: { color: Arena.live },
  draw: { color: Arena.dim },
  // Punctuation, not data — dim enough that the eye lands on the digits.
  separator: { color: Arena.edge },
});
