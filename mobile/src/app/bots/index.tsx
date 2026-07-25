import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, StyleSheet, View } from 'react-native';

import { BotRow } from '@/components/BotRow';
import { EmptyState, ErrorState, LoadingState } from '@/components/ui/StateView';
import { BottomSheet } from '@/components/ui/BottomSheet';
import { FilterBar } from '@/components/ui/FilterBar';
import { FilterChip } from '@/components/ui/FilterChip';
import { Screen } from '@/components/ui/Screen';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useBots } from '@/hooks/useBots';
import { Space } from '@/theme/arena';

export default function BotsScreen() {
  const { data, isPending, error, refetch, isRefetching } = useBots();
  const [query, setQuery] = useState('');
  const [rankedOnly, setRankedOnly] = useState(false);
  const [filtersOpen, setFiltersOpen] = useState(false);

  // Name and owner are what people actually search for, and both are on the row — the
  // site filters the same two fields.
  const shown = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const matches = (data ?? []).filter((bot) => {
      if (rankedOnly && !bot.currentStanding) return false;
      if (needle === '') return true;
      return (
        bot.name.toLowerCase().includes(needle) || bot.owner.toLowerCase().includes(needle)
      );
    });

    // Standing first, then name. The API returns creation order, which was fine when a
    // row showed nothing to compare — with elo and rank on every row it just looks
    // shuffled. Ties share a rank (competition ranking), so name breaks them stably.
    return matches.sort((a, b) => {
      const ra = a.currentStanding?.rank;
      const rb = b.currentStanding?.rank;
      if (ra !== undefined && rb !== undefined && ra !== rb) return ra - rb;
      if (ra !== undefined && rb === undefined) return -1;
      if (ra === undefined && rb !== undefined) return 1;
      return a.name.localeCompare(b.name);
    });
  }, [data, query, rankedOnly]);

  const filtering = query.trim() !== '' || rankedOnly;

  return (
    <Screen>
      <ScreenHeader
        title="Bots"
        status={
          data
            ? filtering
              ? `${shown.length} of ${data.length}`
              : `${data.length} registered`
            : undefined
        }
      />

      {isPending ? (
        <LoadingState label="Loading bots…" />
      ) : error ? (
        <ErrorState error={error} onRetry={refetch} />
      ) : (
        <>
          <View style={styles.filters}>
            <FilterBar
              query={query}
              onQueryChange={setQuery}
              placeholder="Filter by bot or owner…"
              accessibilityLabel="Filter bots by name or owner"
              activeCount={rankedOnly ? 1 : 0}
              onOpenFilters={() => setFiltersOpen(true)}
            />
          </View>

          <BottomSheet
            visible={filtersOpen}
            onClose={() => setFiltersOpen(false)}
            title="FILTERS">
            <View style={styles.chips}>
              <FilterChip
                label="Ranked only"
                active={rankedOnly}
                onToggle={() => setRankedOnly((on) => !on)}
              />
            </View>
          </BottomSheet>

          {shown.length === 0 ? (
            <EmptyState
              title={filtering ? 'Nothing matches' : 'No bots yet'}
              detail={
                filtering
                  ? 'Try a different name, or clear the filters.'
                  : 'Submit one with the CLI: nilbots submit .'
              }
            />
          ) : (
            <FlatList
              data={shown}
              keyExtractor={(bot) => bot.id}
              contentContainerStyle={styles.list}
              onRefresh={refetch}
              refreshing={isRefetching}
              keyboardDismissMode="on-drag"
              keyboardShouldPersistTaps="handled"
              renderItem={({ item }) => (
                <BotRow bot={item} onPress={() => router.push(`/bots/${item.slug}`)} />
              )}
            />
          )}
        </>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  filters: { gap: Space.sm, marginBottom: Space.lg },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: Space.sm },
  list: { gap: Space.md, paddingBottom: 96 },
});
