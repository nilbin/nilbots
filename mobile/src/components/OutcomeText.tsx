import { StyleSheet, Text, type StyleProp, type TextStyle } from 'react-native';

import { Arena, Mono } from '@/theme/arena';

export type OutcomeTextSize = 'sm' | 'lg';

const FONT_SIZE: Record<OutcomeTextSize, number> = { sm: 11, lg: 15 };

/**
 * How a match ended, in the app's one set of result colours.
 *
 * A broadcasting match has no outcome — the server withholds winners until the broadcast
 * completes — so this takes `broadcasting` and says LIVE. Falling back to the status
 * there would print "completed" and leak that the match is already decided, which is the
 * whole thing broadcast secrecy exists to prevent.
 */
export function OutcomeText({
  outcome,
  broadcasting,
  status,
  size = 'sm',
  style,
}: {
  outcome?: string | null;
  broadcasting?: boolean;
  status?: string | null;
  size?: OutcomeTextSize;
  style?: StyleProp<TextStyle>;
}) {
  const label = broadcasting ? 'LIVE' : (outcome ?? status?.toLowerCase() ?? '—');

  return (
    <Text
      style={[
        styles.base,
        { fontSize: FONT_SIZE[size] },
        broadcasting && styles.live,
        outcome === 'Win' && styles.win,
        outcome === 'Loss' && styles.loss,
        style,
      ]}>
      {label}
    </Text>
  );
}

const styles = StyleSheet.create({
  base: { ...Mono, color: Arena.dim },
  live: { color: Arena.live, fontWeight: '700' },
  win: { color: Arena.ok, fontWeight: '700' },
  loss: { color: Arena.live },
});
