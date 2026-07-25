import { Stack, useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet, Text } from 'react-native';

import { BotHeaderCard } from '@/components/BotHeaderCard';
import { BotMatchRow } from '@/components/BotMatchRow';
import { BotVersionRow } from '@/components/BotVersionRow';
import { Card } from '@/components/ui/Card';
import { ErrorState, LoadingState } from '@/components/ui/StateView';
import { Screen } from '@/components/ui/Screen';
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
        <BotHeaderCard bot={bot} record={history} />

        {history ? (
          <>
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
  none: { color: Arena.dim, fontSize: 13, lineHeight: 18 },
  sectionTitle: { ...SectionLabelText, marginTop: Space.md },
});
