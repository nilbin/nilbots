import { router } from 'expo-router';
import { FlatList, StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { Card } from '@/components/ui/Card';
import { EmptyState, ErrorState, LoadingState } from '@/components/ui/StateView';
import { Screen } from '@/components/ui/Screen';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useBots } from '@/hooks/useBots';
import { Arena, Mono, Space } from '@/theme/arena';

export default function BotsScreen() {
  const { data, isPending, error, refetch, isRefetching } = useBots();

  return (
    <Screen>
      <ScreenHeader
        title="Bots"
        status={data ? `${data.length} registered · compiled to wasm` : undefined}
      />

      {isPending ? (
        <LoadingState label="Loading bots…" />
      ) : error ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : data.length === 0 ? (
        <EmptyState title="No bots yet" detail="Submit one with the CLI: nilbots submit ." />
      ) : (
        <FlatList
          data={data}
          keyExtractor={(bot) => bot.id}
          contentContainerStyle={styles.list}
          onRefresh={refetch}
          refreshing={isRefetching}
          renderItem={({ item }) => {
            // One rating per rules-version ladder, newest first; an unranked bot has none.
            const current = item.ratings[0];
            return (
              <Card
                onPress={() => router.push(`/bots/${item.slug}`)}
                accessibilityLabel={`${item.name} by ${item.owner}`}>
                <View style={styles.row}>
                  <BotSprite lookId={item.lookId} accent={item.accent} size="md" />
                  <View style={styles.identity}>
                    <Text style={styles.name} numberOfLines={1}>
                      {item.name}
                    </Text>
                    <Text style={styles.owner} numberOfLines={1}>
                      {item.owner}
                    </Text>
                  </View>
                  <View style={styles.stats}>
                    <Text style={styles.rating}>
                      {current ? Math.round(current.rating) : '—'}
                    </Text>
                    <Text style={styles.versions}>
                      {item.versionCount} {item.versionCount === 1 ? 'version' : 'versions'}
                    </Text>
                  </View>
                </View>
              </Card>
            );
          }}
        />
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: { gap: Space.md, paddingBottom: 96 },
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  identity: { flex: 1, gap: 1 },
  name: { color: Arena.text, fontSize: 16, fontWeight: '600' },
  owner: { color: Arena.dim, fontSize: 12 },
  stats: { alignItems: 'flex-end' },
  rating: { ...Mono, color: Arena.text, fontSize: 16, fontWeight: '700' },
  versions: { color: Arena.dim, fontSize: 11 },
});
