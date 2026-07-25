import { router, useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { SetGameRow } from '@/components/SetGameRow';
import { Card } from '@/components/ui/Card';
import { DetailRow } from '@/components/ui/DetailRow';
import { ErrorState, LoadingState } from '@/components/ui/StateView';
import { Screen } from '@/components/ui/Screen';
import { useMatchSet } from '@/hooks/useMatchSet';
import { Arena, Mono, SectionLabelText, Space } from '@/theme/arena';

export default function MatchSetScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: set, isPending, error, refetch } = useMatchSet(id);

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
      <ScrollView contentContainerStyle={styles.body} showsVerticalScrollIndicator={false}>
        <Card>
          <View style={styles.matchup}>
            <SetBot bot={set.botA} score={set.scoreA} ratingChange={set.ratingChangeA} />
            <Text style={styles.versus}>vs</Text>
            <SetBot bot={set.botB} score={set.scoreB} ratingChange={set.ratingChangeB} align="end" />
          </View>
        </Card>

        {/* A set withholds its score, rating change and winner until every game has
            finished broadcasting — the games below reveal one at a time, and the set
            total would give away the ones still playing. */}
        {set.revealed ? null : (
          <Text style={styles.pending}>
            Scores stay hidden until every game in the set has finished broadcasting.
          </Text>
        )}

        <Text style={styles.sectionTitle}>GAMES</Text>
        {set.games.map((game) => (
          <SetGameRow
            key={game.id}
            game={game}
            botId={set.botA.id}
            onPress={() => router.push(`/matches/${game.id}`)}
          />
        ))}

        <Text style={styles.sectionTitle}>SET</Text>
        <Card>
          <DetailRow label="rules" value={set.rulesVersion} mono />
          <DetailRow label="status" value={set.status} />
          <DetailRow label="games" value={`${set.games.length} of 6`} />
        </Card>
      </ScrollView>
    </Screen>
  );
}

/**
 * One side of the matchup. Score and rating change are null until the set is revealed,
 * and name/accent/look are null for a bot deleted since it fought — the projection has
 * no snapshot to fall back on at set level, unlike the per-game participants.
 */
function SetBot({
  bot,
  score,
  ratingChange,
  align = 'start',
}: {
  bot: { id: string; name?: string | null; accent?: string | null; lookId?: string | null };
  score?: number | null;
  ratingChange?: number | null;
  align?: 'start' | 'end';
}) {
  return (
    <View style={[styles.side, align === 'end' && styles.sideEnd]}>
      <BotSprite lookId={bot.lookId} accent={bot.accent} size="md" />
      <Text style={styles.name} numberOfLines={1}>
        {bot.name ?? 'a removed bot'}
      </Text>
      <Text style={styles.score}>{score ?? '—'}</Text>
      {ratingChange !== null && ratingChange !== undefined ? (
        <Text style={[styles.delta, ratingChange >= 0 ? styles.up : styles.down]}>
          {ratingChange >= 0 ? '+' : ''}
          {Math.round(ratingChange)}
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  body: { gap: Space.md, paddingBottom: Space.xxl, paddingTop: Space.md },
  matchup: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  side: { flex: 1, alignItems: 'center', gap: Space.xs },
  sideEnd: {},
  versus: { color: Arena.dim, fontSize: 12 },
  name: { color: Arena.text, fontSize: 14, fontWeight: '600', textAlign: 'center' },
  score: { ...Mono, color: Arena.text, fontSize: 28, fontWeight: '800' },
  delta: { ...Mono, fontSize: 12 },
  up: { color: Arena.ok },
  down: { color: Arena.live },
  pending: { color: Arena.dim, fontSize: 12, lineHeight: 17 },
  sectionTitle: { ...SectionLabelText, marginTop: Space.md },
});
