import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, StyleSheet, Text, View } from 'react-native';

import { LeaderboardRow } from '@/components/LeaderboardRow';
import { EmptyState, ErrorState, LoadingState } from '@/components/ui/StateView';
import { FilterBar } from '@/components/ui/FilterBar';
import { FilterChip } from '@/components/ui/FilterChip';
import { Screen } from '@/components/ui/Screen';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useLeaderboard } from '@/hooks/useLeaderboard';
import { Arena, Space } from '@/theme/arena';

export default function LadderScreen() {
  // null = the server's current ladder; a value pins a historical one.
  const [rules, setRules] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const { data, isPending, error, refetch, isRefetching } = useLeaderboard(rules ?? undefined);

  // Filtering hides rows; it never renumbers them. Each entry carries the rank the
  // server computed for the whole ladder, so #7 still reads #7 with everything else
  // filtered out — deriving rank from the visible array would quietly lie.
  const shown = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const entries = data?.entries ?? [];
    if (needle === '') return entries;
    return entries.filter(
      (entry) =>
        entry.name.toLowerCase().includes(needle) || entry.owner.toLowerCase().includes(needle),
    );
  }, [data, query]);

  return (
    <Screen>
      <ScreenHeader
        title="Ladder"
        status={
          data
            ? `rules ${data.rulesVersion}${
                data.activeRulesVersion !== data.rulesVersion ? ' · closed' : ' · live'
              }${query.trim() ? ` · ${shown.length} of ${data.entries.length}` : ''}`
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
        <>
          <View style={styles.filter}>
            <FilterBar
              query={query}
              onQueryChange={setQuery}
              placeholder="Filter by bot or owner…"
              accessibilityLabel="Filter standings by bot or owner"
            />
          </View>

          {/* Every rules version keeps its own ladder — a new era never erases old
              standings (DECISIONS #97), so past ladders stay readable. */}
          {data.ladders.length > 1 ? (
            <View style={styles.ladders}>
              {data.ladders.map((ladder) => (
                <FilterChip
                  key={ladder}
                  label={`rules ${ladder}`}
                  active={ladder === data.rulesVersion}
                  onToggle={() => setRules(ladder)}
                />
              ))}
            </View>
          ) : null}

          {data.activeRulesVersion !== data.rulesVersion ? (
            <Text style={styles.closed}>
              This ladder is closed — rules {data.rulesVersion} no longer accepts sets, so these
              standings are final.
            </Text>
          ) : null}

          {shown.length === 0 ? (
            <EmptyState title="Nothing matches" detail="Try a different name, or clear the filter." />
          ) : (
            <FlatList
              data={shown}
              keyExtractor={(entry) => entry.id}
              contentContainerStyle={styles.list}
              onRefresh={refetch}
              refreshing={isRefetching}
              keyboardDismissMode="on-drag"
              keyboardShouldPersistTaps="handled"
              renderItem={({ item }) => (
                <LeaderboardRow entry={item} onPress={() => router.push(`/bots/${item.slug}`)} />
              )}
            />
          )}
        </>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  filter: { marginBottom: Space.md },
  list: { gap: Space.md, paddingBottom: 96 },
  ladders: { flexDirection: 'row', flexWrap: 'wrap', gap: Space.sm, marginBottom: Space.md },
  closed: { color: Arena.dim, fontSize: 12, marginBottom: Space.md, lineHeight: 17 },
});
