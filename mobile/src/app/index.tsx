import { router } from "expo-router";
import { FlatList, StyleSheet, Text, View } from "react-native";

import { FilterChip } from "@/components/ui/FilterChip";
import { LeaderboardRow } from "@/components/LeaderboardRow";
import {
  EmptyState,
  ErrorState,
  LoadingState,
} from "@/components/ui/StateView";
import { Screen } from "@/components/ui/Screen";
import { ScreenHeader } from "@/components/ui/ScreenHeader";
import { useLeaderboard } from "@/hooks/useLeaderboard";
import { useState } from "react";
import { Arena, Space } from "@/theme/arena";

export default function LadderScreen() {
  // null = the server's current ladder; a value pins a historical one.
  const [rules, setRules] = useState<string | null>(null);
  const { data, isPending, error, refetch, isRefetching } = useLeaderboard(
    rules ?? undefined,
  );

  return (
    <Screen>
      <ScreenHeader
        title="Ladder"
        status={
          data
            ? `rules ${data.rulesVersion}${
                data.activeRulesVersion !== data.rulesVersion
                  ? " · closed, historical"
                  : " · live"
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
        <>
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
              This ladder is closed — rules {data.rulesVersion} no longer
              accepts sets, so these standings are final.
            </Text>
          ) : null}
          <FlatList
            data={data.entries}
            keyExtractor={(entry) => entry.id}
            contentContainerStyle={styles.list}
            onRefresh={refetch}
            refreshing={isRefetching}
            renderItem={({ item }) => (
              <LeaderboardRow
                entry={item}
                onPress={() => router.push(`/bots/${item.slug}`)}
              />
            )}
          />
        </>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: { gap: Space.md, paddingBottom: 96 },
  ladders: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: Space.sm,
    marginBottom: Space.md,
  },
  closed: {
    color: Arena.dim,
    fontSize: 12,
    marginBottom: Space.md,
    lineHeight: 17,
  },
});
