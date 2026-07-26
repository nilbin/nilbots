import { StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { Card } from '@/components/ui/Card';
import type { BotSummary } from '@/api/client';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * One bot in the roster: chassis, name, owner, and where it stands.
 *
 * Rating comes from the server's `currentStanding` (never reconstructed here from
 * `ratings`, and never by joining the leaderboard — rank is a property of the ladder).
 *
 * Rank itself is deliberately absent: this is a roster, and the Ladder tab is the ordered
 * ranking. Showing rank in both made the two tabs the same screen. Here the second line
 * is build state, which is what you actually want when browsing bots — an unbuilt bot
 * cannot fight.
 *
 * An unranked bot says "unranked" in words. The first version showed an em dash, which
 * reads as a rendering artefact rather than an absence of data.
 */
export function BotRow({ bot, onPress }: { bot: BotSummary; onPress?: () => void }) {
  const standing = bot.currentStanding;

  return (
    <Card
      onPress={onPress}
      accessibilityLabel={
        standing
          ? `${bot.name} by ${bot.owner}, ${Math.round(standing.rating)} elo, rank ${standing.rank}`
          : `${bot.name} by ${bot.owner}, unranked`
      }>
      <View style={styles.row}>
        <BotSprite lookId={bot.lookId} accent={bot.accent} size="md" />
        <View style={styles.identity}>
          <Text style={styles.name} numberOfLines={1}>
            {bot.name}
          </Text>
          <Text style={styles.owner} numberOfLines={1}>
            {bot.owner}
          </Text>
        </View>
        <View style={styles.standing}>
          {standing ? (
            <Text style={styles.elo}>{Math.round(standing.rating)}</Text>
          ) : (
            <Text style={styles.unranked}>unranked</Text>
          )}
          <Text style={styles.build} numberOfLines={1}>
            {bot.activeVersion ? `v${bot.activeVersion.versionNumber} active` : 'no build yet'}
          </Text>
        </View>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  identity: { flex: 1, gap: 1 },
  name: { color: Arena.text, fontSize: 16, fontWeight: '600' },
  owner: { color: Arena.dim, fontSize: 12 },
  standing: { alignItems: 'flex-end', minWidth: 78 },
  elo: { ...Mono, color: Arena.text, fontSize: 17, fontWeight: '700' },
  build: { ...Mono, color: Arena.dim, fontSize: 11 },
  unranked: { color: Arena.dim, fontSize: 12 },
});
