import { StyleSheet, Text, View } from 'react-native';

import { Arena, Radius, Space } from '@/theme/arena';

/**
 * The nilbots wordmark: a targeting reticle beside `nil` + accented `bots`.
 *
 * The site draws the mark as SVG (web/src/components/Logo.tsx). Here it is composed from
 * Views — a ring, a bright core, and two ticks at 12 and 3 o'clock — which matches the
 * geometry without pulling react-native-svg in for one glyph. When bot look sprites land
 * (they are genuinely SVG) that dependency arrives and this can switch to the shared path.
 */
export function Wordmark({ size = 26 }: { size?: number }) {
  const ring = size * 0.58;
  const core = size * 0.19;
  const tick = size * 0.14;

  return (
    <View style={styles.row}>
      <View style={[styles.mark, { width: size, height: size, borderRadius: size * 0.22 }]}>
        <View
          style={[
            styles.ring,
            { width: ring, height: ring, borderRadius: ring / 2, borderWidth: size * 0.08 },
          ]}
        />
        <View
          style={[styles.core, { width: core, height: core, borderRadius: core / 2 }]}
        />
        {/* 12 o'clock and 3 o'clock ticks, as in the SVG. */}
        <View
          style={[
            styles.tick,
            { width: size * 0.08, height: tick, top: 0, borderRadius: size * 0.04 },
          ]}
        />
        <View
          style={[
            styles.tick,
            { width: tick, height: size * 0.08, right: 0, borderRadius: size * 0.04 },
          ]}
        />
      </View>
      <Text style={[styles.word, { fontSize: size * 0.62 }]}>
        nil<Text style={styles.accent}>bots</Text>
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.sm },
  mark: {
    backgroundColor: Arena.bg,
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: Radius.sm,
    alignItems: 'center',
    justifyContent: 'center',
  },
  ring: { borderColor: Arena.accent, position: 'absolute' },
  core: { backgroundColor: Arena.spark },
  tick: { position: 'absolute', backgroundColor: Arena.accent },
  word: { color: Arena.text, fontWeight: '900', letterSpacing: 2 },
  accent: { color: Arena.accent },
});
