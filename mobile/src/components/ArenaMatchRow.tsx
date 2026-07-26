import { StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { OutcomeText } from '@/components/OutcomeText';
import { Card } from '@/components/ui/Card';
import type { MatchSummary } from '@/api/client';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * One match in the arena feed, told from neither side — unlike a bot's history row, this
 * has no "you", so it names both fighters and marks the winner rather than saying
 * Win/Loss.
 *
 * A broadcasting match reports no winner, so it reads LIVE and both names stay neutral.
 * Deciding a winner from anything else available here would leak the outcome early.
 */
export function ArenaMatchRow({ match, onPress }: { match: MatchSummary; onPress?: () => void }) {
  const settled = !match.broadcasting && match.status === 'Completed';
  const decided = settled && match.winnerSlot !== null;

  return (
    <Card
      onPress={onPress}
      accessibilityLabel={`${match.participants.map((p) => p.nameSnapshot).join(' versus ')} on ${
        match.mapId
      }, ${match.broadcasting ? 'broadcasting' : match.status.toLowerCase()}`}>
      <View style={styles.head}>
        {/* No Win/Loss here — a feed row has no "you" to win. The state is the label and
            the winner is the bolded name; a settled match with no winner is a draw, worth
            saying rather than leaving both names dim and unexplained. */}
        <OutcomeText
          outcome={settled && match.winnerSlot === null ? 'Draw' : null}
          broadcasting={match.broadcasting}
          status={match.status}
        />
        <Text style={styles.meta} numberOfLines={1}>
          {match.setGame ? `ranked · g${match.setGame}` : 'unranked'} · {match.mapId}
        </Text>
      </View>

      <View style={styles.fighters}>
        {match.participants.map((participant) => (
          <View key={participant.slot} style={styles.fighter}>
            <BotSprite
              lookId={participant.lookIdSnapshot}
              accent={participant.accentSnapshot}
              size="xs"
            />
            <Text
              style={[styles.name, decided && match.winnerSlot === participant.slot && styles.won]}
              numberOfLines={1}>
              {participant.nameSnapshot}
            </Text>
          </View>
        ))}
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  head: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: Space.sm,
    marginBottom: Space.sm,
  },
  meta: { ...Mono, color: Arena.dim, fontSize: 10, flexShrink: 1 },
  fighters: { gap: Space.xs },
  fighter: { flexDirection: 'row', alignItems: 'center', gap: Space.sm },
  name: { color: Arena.dim, fontSize: 14, flexShrink: 1 },
  won: { color: Arena.text, fontWeight: '700' },
});
