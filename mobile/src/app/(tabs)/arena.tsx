import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, StyleSheet, View } from 'react-native';

import { ArenaMatchRow } from '@/components/ArenaMatchRow';
import { EmptyState, ErrorState, LoadingState } from '@/components/ui/StateView';
import { FilterChip } from '@/components/ui/FilterChip';
import { Screen } from '@/components/ui/Screen';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useMatches } from '@/hooks/useMatches';
import { Arena, Space } from '@/theme/arena';

type Scope = 'all' | 'ranked' | 'unranked';

const SCOPES: { key: Scope; label: string; ranked?: boolean }[] = [
  { key: 'all', label: 'all' },
  { key: 'ranked', label: 'ranked', ranked: true },
  { key: 'unranked', label: 'unranked', ranked: false },
];

export default function ArenaScreen() {
  const [scope, setScope] = useState<Scope>('all');
  const ranked = SCOPES.find((option) => option.key === scope)?.ranked;
  const {
    data,
    isPending,
    error,
    refetch,
    isRefetching,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
  } = useMatches(ranked);

  const matches = useMemo(() => data?.pages.flat() ?? [], [data]);
  const live = matches.filter((match) => match.broadcasting).length;

  return (
    <Screen>
      <ScreenHeader
        title="Arena"
        status={live > 0 ? `${live} broadcasting now` : 'every match, newest first'}
      />

      {/* One line, per the control-bar rule. Scope is the only control this list has —
          there is no text to search, and filtering by bot is what a bot's own page is. */}
      <View style={styles.scopes}>
        {SCOPES.map((option) => (
          <FilterChip
            key={option.key}
            label={option.label}
            active={scope === option.key}
            onToggle={() => setScope(option.key)}
          />
        ))}
      </View>

      {isPending ? (
        <LoadingState label="Loading the arena…" />
      ) : error ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : matches.length === 0 ? (
        <EmptyState
          title="No matches yet"
          detail="Matches appear here as soon as bots start fighting."
        />
      ) : (
        <FlatList
          data={matches}
          keyExtractor={(match) => match.id}
          contentContainerStyle={styles.list}
          onRefresh={refetch}
          refreshing={isRefetching}
          // Fires once per threshold crossing rather than per frame, so a fast scroll
          // does not queue several pages of the same rows.
          onEndReachedThreshold={0.4}
          onEndReached={() => {
            if (hasNextPage && !isFetchingNextPage) void fetchNextPage();
          }}
          ListFooterComponent={
            isFetchingNextPage ? (
              <ActivityIndicator style={styles.more} color={Arena.dim} />
            ) : null
          }
          renderItem={({ item }) => (
            <ArenaMatchRow
              match={item}
              onPress={() =>
                router.push(
                  item.matchSetId ? `/sets/${item.matchSetId}` : `/matches/${item.id}`,
                )
              }
            />
          )}
        />
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  scopes: { flexDirection: 'row', gap: Space.sm, marginBottom: Space.md },
  list: { gap: Space.md, paddingBottom: 96 },
  more: { paddingVertical: Space.lg },
});
