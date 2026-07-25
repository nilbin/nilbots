import { Stack, useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import { BotMatchRow } from '@/components/BotMatchRow';
import { BotSprite } from '@/components/BotSprite';
import { BotVersionRow } from '@/components/BotVersionRow';
import { Card } from '@/components/ui/Card';
import { ErrorState, LoadingState } from '@/components/ui/StateView';
import { Screen } from '@/components/ui/Screen';
import { StatRow } from '@/components/ui/StatRow';
import { useBot } from '@/hooks/useBots';
import { useBotMatches } from '@/hooks/useBotMatches';
import { Arena, SectionLabelText, Space } from '@/theme/arena';

export default function BotDetailScreen() {
  const { key } = useLocalSearchParams<{ key: string }>();
  const { data: bot, isPending, error, refetch } = useBot(key);
  const { data: history } = useBotMatches(bot?.id);

  if (isPending)
    return (
      <Screen hasHeader>
        <LoadingState />
      </Screen>
    );
  if (error)
    return (
      <Screen hasHeader>
        <ErrorState error={error} onRetry={refetch} />
      </Screen>
    );

  return (
    <Screen hasHeader>
      <Stack.Screen options={{ title: bot.name }} />
      <ScrollView contentContainerStyle={styles.body} showsVerticalScrollIndicator={false}>
        <Card>
          {/* Sprite beside a name-over-owner column, not above a left-edge owner line:
              hanging "by …" under the sprite breaks the text block in two and leaves the
              name orphaned out to the right. Same shape as a roster row, one size up. */}
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

          {bot.currentStanding ? (
            <>
              {/* The standing is a separate band, so say so. Without the rule it reads as
                  a third line of the identity block that failed to line up with it. */}
              <View style={styles.divider} />
              <StatRow
                layout="spread"
                stats={[
                  { label: 'rating', value: Math.round(bot.currentStanding.rating) },
                  { label: 'rank', value: `#${bot.currentStanding.rank}` },
                  { label: 'sets', value: bot.currentStanding.rankedSets },
                ]}
              />
            </>
          ) : (
            <Text style={styles.unranked}>Unranked — no ranked sets on the current ladder.</Text>
          )}
        </Card>

        {history ? (
          <>
            <Text style={styles.sectionTitle}>RECORD</Text>
            <Card>
              <StatRow
                stats={[
                  { label: 'wins', value: history.wins },
                  { label: 'losses', value: history.losses },
                  { label: 'draws', value: history.draws },
                ]}
              />
            </Card>

            <Text style={styles.sectionTitle}>LATEST GAMES</Text>
            {history.matches.length === 0 ? (
              <Card>
                <Text style={styles.none}>
                  No games yet. Ranked sets are six games across three map and seed pairs.
                </Text>
              </Card>
            ) : (
              history.matches.map((row) => (
                // Not tappable yet: a ranked row should open its set and an unranked one
                // its match, and neither route exists. Typed routes catch that at build
                // time, which is why these are inert rather than dead links.
                <BotMatchRow key={row.id} row={row} />
              ))
            )}
          </>
        ) : null}

        <Text style={styles.sectionTitle}>VERSIONS</Text>
        {bot.versions.map((version) => (
          <BotVersionRow key={version.id} version={version} />
        ))}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  body: { gap: Space.md, paddingBottom: Space.xxl, paddingTop: Space.md },
  identity: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  identityText: { flex: 1, minWidth: 0, gap: 1 },
  name: { color: Arena.text, fontSize: 24, fontWeight: '700' },
  owner: { color: Arena.dim, fontSize: 13 },
  divider: { height: 1, backgroundColor: Arena.edge, marginVertical: Space.lg },
  unranked: { color: Arena.dim, fontSize: 13, marginTop: Space.md },
  none: { color: Arena.dim, fontSize: 13, lineHeight: 18 },
  sectionTitle: { ...SectionLabelText, marginTop: Space.md },
});
