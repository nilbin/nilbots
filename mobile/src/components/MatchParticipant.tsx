import { StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { OutcomeText } from '@/components/OutcomeText';
import { StatRow } from '@/components/ui/StatRow';
import type { MatchDetail } from '@/api/client';
import { Arena, Space } from '@/theme/arena';

type Participant = MatchDetail['participants'][number];

/**
 * One side of a match: who fought, how it went, and what it did.
 *
 * Every per-fight number here is null until the broadcast completes, so the stat band is
 * absent rather than zeroed while a match is live. Zeros would read as "dealt no damage"
 * instead of "not saying yet", which is a different and wrong claim.
 *
 * Names are snapshots taken when the match ran, so a bot renamed or deleted since still
 * appears as it fought.
 */
export function MatchParticipant({
  participant,
  broadcasting,
}: {
  participant: Participant;
  broadcasting: boolean;
}) {
  const { damageDealt, finalHealth, faults } = participant;
  const hasStats = damageDealt !== null || finalHealth !== null || faults !== null;

  return (
    <View style={styles.container}>
      <View style={styles.identity}>
        <BotSprite
          lookId={participant.lookIdSnapshot}
          accent={participant.accentSnapshot}
          size="md"
        />
        <View style={styles.text}>
          <Text style={styles.name} numberOfLines={1}>
            {participant.nameSnapshot}
          </Text>
          <Text style={styles.owner} numberOfLines={1}>
            by {participant.ownerDisplayNameSnapshot}
          </Text>
        </View>
        <OutcomeText outcome={participant.outcome} broadcasting={broadcasting} size="lg" />
      </View>

      {hasStats ? (
        <View style={styles.stats}>
          <StatRow
            layout="spread"
            stats={[
              { label: 'damage', value: damageDealt ?? '—' },
              { label: 'health left', value: finalHealth ?? '—' },
              { label: 'faults', value: faults ?? '—' },
            ]}
          />
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: Space.md },
  identity: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  text: { flex: 1, minWidth: 0, gap: 1 },
  name: { color: Arena.text, fontSize: 16, fontWeight: '600' },
  owner: { color: Arena.dim, fontSize: 12 },
  stats: { paddingLeft: 40 + Space.md },
});
