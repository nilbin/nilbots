import { Stack, useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import { BotIdentity } from '@/components/BotIdentity';
import { BotVersionRow } from '@/components/BotVersionRow';
import { Card } from '@/components/ui/Card';
import { ErrorState, LoadingState } from '@/components/ui/StateView';
import { Screen } from '@/components/ui/Screen';
import { useBot } from '@/hooks/useBots';
import { Arena, Mono, Space } from '@/theme/arena';

export default function BotDetailScreen() {
  const { key } = useLocalSearchParams<{ key: string }>();
  const { data: bot, isPending, error, refetch } = useBot(key);

  if (isPending) return <Screen><LoadingState /></Screen>;
  if (error) return <Screen><ErrorState error={error} onRetry={refetch} /></Screen>;

  return (
    <Screen>
      <Stack.Screen options={{ title: bot.name }} />
      <ScrollView contentContainerStyle={styles.body} showsVerticalScrollIndicator={false}>
        <Card>
          <BotIdentity name={bot.name} accent={bot.accent} lookId={bot.lookId} size="lg" />
          <Text style={styles.owner}>by {bot.owner}</Text>
          {bot.currentStanding ? (
            <View style={styles.standing}>
              <Stat label="rating" value={Math.round(bot.currentStanding.rating)} />
              <Stat label="rank" value={bot.currentStanding.rank} />
              <Stat label="sets" value={bot.currentStanding.rankedSets} />
            </View>
          ) : (
            <Text style={styles.unranked}>Unranked — no ranked sets on the current ladder.</Text>
          )}
        </Card>

        <Text style={styles.sectionTitle}>VERSIONS</Text>
        {bot.versions.map((version) => (
          <BotVersionRow key={version.id} version={version} />
        ))}
      </ScrollView>
    </Screen>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <View style={styles.stat}>
      <Text style={styles.statValue}>{value}</Text>
      <Text style={styles.statLabel}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  body: { gap: Space.md, paddingBottom: Space.xl, paddingTop: Space.sm },
  owner: { color: Arena.dim, fontSize: 13, marginTop: Space.xs },
  standing: { flexDirection: 'row', gap: Space.xl, marginTop: Space.lg },
  stat: { alignItems: 'flex-start' },
  statValue: { ...Mono, color: Arena.text, fontSize: 20, fontWeight: '800' },
  statLabel: { color: Arena.dim, fontSize: 11, textTransform: 'uppercase' },
  unranked: { color: Arena.dim, fontSize: 13, marginTop: Space.md },
  sectionTitle: {
    ...Mono,
    color: Arena.dim,
    fontSize: 11,
    letterSpacing: 2,
    marginTop: Space.md,
  },
});
