import { StyleSheet, Text, View } from 'react-native';

import type { ArenaObjective } from '@/components/arena/protocol';
import { Arena, Mono, Radius, Space } from '@/theme/arena';

/**
 * The match objective, already reduced to presentation data by the WebView.
 *
 * Legacy pressure uses a centred signed marker. Frontline uses ordinary
 * zero-to-threshold progress and also exposes its current authored position.
 * No capture or winner rule is implemented here; this draws the values and
 * phase wording sent over bridge v3.
 */
export function ArenaControlBar({ objective }: { objective: ArenaObjective }) {
  if (objective.kind === 'frontline') {
    const offset = Math.max(
      0,
      Math.min(
        100,
        (100 * objective.captureProgress) /
          Math.max(1, objective.captureThreshold),
      ),
    );

    return (
      <View style={styles.container}>
        <View style={styles.frontlineHeader}>
          <Text style={styles.frontlineTitle}>
            FRONTLINE {objective.activePositionIndex + 1}/{objective.positionCount}
          </Text>
          <Text style={styles.reading}>
            {objective.claimingTeamId === null
              ? 'NEUTRAL'
              : `TEAM ${objective.claimingTeamId}`}
            {' · '}
            {objective.captureProgress} / {objective.captureThreshold}
          </Text>
        </View>

        <View style={styles.positions}>
          {Array.from(
            { length: objective.positionCount },
            (_, positionIndex) => (
              <View
                key={positionIndex}
                style={[
                  styles.position,
                  positionIndex === objective.activePositionIndex &&
                    styles.positionActive,
                ]}
              />
            ),
          )}
        </View>

        <View style={styles.track}>
          <View style={[styles.marker, { left: `${offset}%` }]} />
        </View>

        <Text style={styles.phase} numberOfLines={2}>
          {objective.phase}
        </Text>
      </View>
    );
  }

  const offset = Math.max(
    0,
    Math.min(
      100,
      50 + (50 * objective.pressure) / Math.max(1, objective.limit),
    ),
  );

  return (
    <View style={styles.container}>
      <View style={styles.labels}>
        <Text style={styles.name} numberOfLines={1}>
          {objective.names[0]}
        </Text>
        <Text style={styles.reading}>
          {objective.overtime ? 'OVERTIME ' : ''}
          {objective.pressure > 0 ? '+' : ''}
          {objective.pressure} / ±{objective.limit}
        </Text>
        <Text style={[styles.name, styles.nameRight]} numberOfLines={1}>
          {objective.names[1]}
        </Text>
      </View>

      <View style={styles.track}>
        <View style={styles.centre} />
        <View style={[styles.marker, { left: `${offset}%` }]} />
      </View>

      {objective.phase ? (
        <Text style={styles.phase} numberOfLines={1}>
          {objective.phase}
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
  frontlineHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: Space.sm,
  },
  frontlineTitle: {
    ...Mono,
    color: Arena.zone,
    fontSize: 11,
    fontWeight: '700',
  },
  positions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Space.xs,
  },
  position: {
    flex: 1,
    height: 4,
    borderRadius: 2,
    backgroundColor: Arena.edge,
  },
  positionActive: { backgroundColor: Arena.zone },
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
