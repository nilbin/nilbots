import { StyleSheet, Text, View } from 'react-native';

import { BotRecord } from '@/components/BotRecord';
import { BotSprite } from '@/components/BotSprite';
import { Card } from '@/components/ui/Card';
import { StatRow, type Stat } from '@/components/ui/StatRow';
import type { BotDetail, BotMatchHistory } from '@/api/client';
import { Arena, Space } from '@/theme/arena';

/**
 * Who a bot is and how it is doing — the whole top of its detail screen.
 *
 * Sprite beside a name-over-owner column, not above a left-edge owner line: hanging
 * "by …" under the sprite breaks the text block in two and leaves the name orphaned out
 * to the right. Same shape as a roster row, one size up.
 *
 * Every number the bot has lives in one band under a rule. The record used to be its own
 * card below, which spent a section heading and most of a phone screen on three small
 * integers before you reached the games. As a fourth column it costs nothing, so long as
 * it collapses to a `BotRecord` triple — three more labelled columns would not fit.
 *
 * `record` is optional because its history is a second request: the band renders with
 * whatever has arrived. It is also a wider scope than the rest — a record counts
 * individual games including unranked ones, while rating, rank and sets are facts about
 * the current ladder — so it sits at the end rather than interleaved.
 */
export function BotHeaderCard({
  bot,
  record,
}: {
  bot: BotDetail;
  record?: Pick<BotMatchHistory, 'wins' | 'losses' | 'draws'>;
}) {
  const standing = bot.currentStanding;
  const stats: Stat[] = [
    ...(standing
      ? [
          { label: 'rating', value: Math.round(standing.rating) },
          { label: 'rank', value: `#${standing.rank}` },
          { label: 'sets', value: standing.rankedSets },
        ]
      : []),
    ...(record
      ? [
          {
            label: 'w/l/d',
            value: (
              <BotRecord wins={record.wins} losses={record.losses} draws={record.draws} size="lg" />
            ),
          },
        ]
      : []),
  ];

  return (
    <Card>
      <View style={styles.identity}>
        <BotSprite lookId={bot.lookId} accent={bot.accent} size="lg" />
        <View style={styles.identityText}>
          <Text style={styles.name} numberOfLines={1}>
            {bot.name}
          </Text>
          <Text style={styles.owner} numberOfLines={1}>
            by {bot.owner}
          </Text>
        </View>
      </View>

      {stats.length > 0 ? (
        <>
          {/* The rule is what makes this a band. Without it the numbers read as a third
              line of the identity block that failed to line up with it. */}
          <View style={styles.divider} />
          <StatRow layout="spread" stats={stats} />
        </>
      ) : null}

      {standing ? null : (
        <Text style={styles.unranked}>Unranked — no ranked sets on the current ladder.</Text>
      )}
    </Card>
  );
}

const styles = StyleSheet.create({
  identity: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  identityText: { flex: 1, minWidth: 0, gap: 1 },
  name: { color: Arena.text, fontSize: 24, fontWeight: '700' },
  owner: { color: Arena.dim, fontSize: 13 },
  divider: { height: 1, backgroundColor: Arena.edge, marginVertical: Space.lg },
  unranked: { color: Arena.dim, fontSize: 13, marginTop: Space.md },
});
