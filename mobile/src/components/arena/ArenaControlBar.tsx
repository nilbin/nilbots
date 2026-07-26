import { StyleSheet, Text, View } from 'react-native';

import type { ArenaControl } from '@/components/arena/protocol';
import { Arena, Mono, Radius, Space } from '@/theme/arena';

/**
 * Zone-control pressure: a centred bar leaning toward whichever bot is gaining.
 *
 * Every number and the phase wording arrive already derived from the replay — which
 * limit applies in overtime, whether a hold counts, why pressure is decaying. None of
 * that is recomputed here; this draws what it is given.
 */
export function ArenaControlBar({ control }: { control: ArenaControl }) {
  const offset = Math.max(
    0,
    Math.min(100, 50 + (50 * control.pressure) / Math.max(1, control.limit)),
  );

  return (
    <View style={styles.container}>
      <View style={styles.labels}>
        <Text style={styles.name} numberOfLines={1}>
          {control.names[0]}
        </Text>
        <Text style={styles.reading}>
          {control.overtime ? 'OVERTIME ' : ''}
          {control.pressure > 0 ? '+' : ''}
          {control.pressure} / ±{control.limit}
        </Text>
        <Text style={[styles.name, styles.nameRight]} numberOfLines={1}>
          {control.names[1]}
        </Text>
      </View>

      <View style={styles.track}>
        <View style={styles.centre} />
        <View style={[styles.marker, { left: `${offset}%` }]} />
      </View>

      {control.phase ? (
        <Text style={styles.phase} numberOfLines={1}>
          {control.phase}
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: Radius.md,
    backgroundColor: Arena.panel,
    padding: Space.md,
    gap: Space.sm,
  },
  labels: { flexDirection: 'row', alignItems: 'center', gap: Space.sm },
  name: { ...Mono, color: Arena.dim, fontSize: 10, flex: 1 },
  nameRight: { textAlign: 'right' },
  reading: { ...Mono, color: Arena.dim, fontSize: 10 },
  track: { height: 8, borderRadius: 4, backgroundColor: Arena.bg, justifyContent: 'center' },
  centre: {
    position: 'absolute',
    left: '50%',
    width: 1,
    top: 0,
    bottom: 0,
    backgroundColor: Arena.dim,
  },
  // Pulled back by half its width so the marker centres on its position rather than
  // starting at it — at ±limit it would otherwise hang off the end of the track.
  marker: {
    position: 'absolute',
    width: 4,
    marginLeft: -2,
    top: 0,
    bottom: 0,
    borderRadius: 2,
    backgroundColor: Arena.zone,
  },
  phase: { ...Mono, color: Arena.dim, fontSize: 10, textAlign: 'center', letterSpacing: 0.5 },
});
