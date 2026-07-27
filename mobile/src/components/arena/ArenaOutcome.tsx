import { StyleSheet, Text, View } from 'react-native';

import type {
  ArenaHeader,
  ArenaResult,
} from '@/components/arena/protocol';
import { Arena, Mono, Radius, Space } from '@/theme/arena';

/**
 * The team result, without projecting a participant winner onto a team game.
 *
 * Exact aggregate values arrive over bridge v2. The app labels health as a team aggregate
 * and keeps canonical decimal damage as text rather than coercing it through a number.
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
    result.winnerTeamId === null
      ? 'DRAW'
      : `TEAM ${result.winnerTeamId} WINS`;

  return (
    <View style={framed ? styles.card : styles.inline}>
      <Text style={styles.line}>{winner}</Text>
      <Text style={styles.reason}>
        {result.reason} · tick {result.endTick}
        {result.territorialScore === null
          ? ''
          : ` · territory ${result.territorialScore}`}
      </Text>
      <View style={styles.teams}>
        {result.teams.map((team) => {
          const participantNames =
            header?.participants
              .filter(
                (participant) => participant.teamId === team.teamId,
              )
              .map((participant) => participant.name) ?? [];
          return (
            <View key={team.teamId} style={styles.team}>
              <View style={styles.teamHeading}>
                <Text style={styles.teamName}>TEAM {team.teamId}</Text>
                <Text style={styles.teamOutcome}>
                  {team.outcome.toUpperCase()}
                </Text>
              </View>
              {participantNames.length > 0 ? (
                <Text style={styles.participants} numberOfLines={1}>
                  {[...new Set(participantNames)].join(', ')}
                </Text>
              ) : null}
              <Text style={styles.aggregate}>
                active HP {team.activeHealth} · damage {team.damageDealt}
              </Text>
              <View style={styles.units}>
                {team.units.map((unit) => (
                  <Text key={unit.unitKey} style={styles.unit}>
                    U{unit.unitId} · {unit.formId} ·{' '}
                    {unit.lifecycleStatus.replaceAll('-', ' ')} · HP{' '}
                    {unit.health} · damage {unit.damageDealt}
                  </Text>
                ))}
              </View>
            </View>
          );
        })}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  inline: { alignItems: 'center', paddingVertical: Space.sm, gap: Space.sm },
  card: {
    maxWidth: '90%',
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: Radius.lg,
    backgroundColor: Arena.panel,
    paddingHorizontal: Space.xl,
    paddingVertical: Space.lg,
    alignItems: 'center',
    gap: Space.sm,
  },
  line: {
    color: Arena.text,
    fontSize: 20,
    fontWeight: '800',
    letterSpacing: 1,
  },
  reason: { ...Mono, color: Arena.dim, fontSize: 11, textAlign: 'center' },
  teams: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'center',
    gap: Space.sm,
  },
  team: {
    minWidth: 150,
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: Radius.sm,
    padding: Space.sm,
    gap: 2,
  },
  teamHeading: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: Space.sm,
  },
  teamName: { ...Mono, color: Arena.text, fontSize: 10 },
  teamOutcome: { ...Mono, color: Arena.accent, fontSize: 10 },
  participants: { color: Arena.dim, fontSize: 10 },
  aggregate: { ...Mono, color: Arena.dim, fontSize: 9 },
  units: {
    borderTopWidth: 1,
    borderTopColor: Arena.edge,
    paddingTop: Space.xs,
    gap: 2,
  },
  unit: { ...Mono, color: Arena.dim, fontSize: 8 },
});
