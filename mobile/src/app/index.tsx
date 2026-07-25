import { router } from 'expo-router';
import { FlatList, StyleSheet } from 'react-native';

import { LeaderboardRow } from '@/components/LeaderboardRow';
import { EmptyState, ErrorState, LoadingState } from '@/components/ui/StateView';
import { Screen } from '@/components/ui/Screen';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useLeaderboard } from '@/hooks/useLeaderboard';
import { Space } from '@/theme/arena';

export default function LadderScreen() {
  const { data, isPending, error, refetch, isRefetching } = useLeaderboard();

  return (
    <Screen>
      <ScreenHeader
        title="Ladder"
        status={
          data
            ? `rules ${data.rulesVersion}${
                data.activeRulesVersion !== data.rulesVersion
                  ? ' · closed, historical'
                  : ' · live'
              }`
            : undefined
        }
      />

      {isPending ? (
        <LoadingState label="Loading the ladder…" />
      ) : error ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : data.entries.length === 0 ? (
        <EmptyState
          title="No ranked sets yet"
          detail="Standings appear once bots have played a ranked set."
        />
      ) : (
        <FlatList
          data={data.entries}
          keyExtractor={(entry) => entry.id}
          contentContainerStyle={styles.list}
          onRefresh={refetch}
          refreshing={isRefetching}
          renderItem={({ item }) => (
            <LeaderboardRow entry={item} onPress={() => router.push(`/bots/${item.slug}`)} />
          )}
        />
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: { gap: Space.md, paddingBottom: 96 },
});
