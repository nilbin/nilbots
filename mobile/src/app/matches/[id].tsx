import { Stack, router, useLocalSearchParams } from 'expo-router';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import { useArenaViewer } from '@/components/ArenaViewer';
import { MatchParticipant } from '@/components/MatchParticipant';
import { Card } from '@/components/ui/Card';
import { DetailRow } from '@/components/ui/DetailRow';
import { ErrorState, LoadingState } from '@/components/ui/StateView';
import { Screen } from '@/components/ui/Screen';
import { useMatch } from '@/hooks/useMatch';
import { Arena, SectionLabelText, Space } from '@/theme/arena';

export default function MatchScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { data: match, isPending, error, refetch } = useMatch(id);
  const { watch } = useArenaViewer();

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
      <Stack.Screen options={{ title: match.setGame ? `Game ${match.setGame}` : 'Match' }} />
      <ScrollView contentContainerStyle={styles.body} showsVerticalScrollIndicator={false}>
        <Card>
          <View style={styles.fight}>
            {match.participants.map((participant) => (
              <MatchParticipant
                key={participant.slot}
                participant={participant}
                broadcasting={match.broadcasting}
              />
            ))}
          </View>
        </Card>

        {/* A replay only exists once the match has run. A broadcasting match has one, but
            truncated to the ticks the server has released — watching it is the point. */}
        {match.status === 'Completed' ? (
          <Card
            onPress={() =>
              watch(match.id, `${match.participants.map((p) => p.nameSnapshot).join(' vs ')}`)
            }>
            <Text style={styles.watch}>▶ Watch the replay</Text>
          </Card>
        ) : null}

        {match.broadcasting ? (
          <Text style={styles.live}>
            Broadcasting. The result is withheld until the replay finishes playing out.
          </Text>
        ) : null}

        <Text style={styles.sectionTitle}>THE FIGHT</Text>
        <Card>
          <DetailRow label="map" value={match.mapId} />
          {/* Seed and hash are the determinism claim made checkable: same versions, map
              and seed always replay to the same hash. Mono because a machine wrote them. */}
          <DetailRow label="seed" value={String(match.seed)} mono />
          {match.endReason ? <DetailRow label="ended" value={match.endReason} /> : null}
          {match.endTick !== null ? (
            <DetailRow label="ticks" value={String(match.endTick)} mono />
          ) : null}
          {match.replayHash ? (
            <DetailRow label="replay hash" value={match.replayHash} mono truncate />
          ) : null}
          {match.error ? <DetailRow label="error" value={match.error} tone="error" /> : null}
        </Card>

        {match.matchSetId ? (
          <Card onPress={() => router.push(`/sets/${match.matchSetId}`)}>
            <Text style={styles.link}>
              Part of a ranked set{match.setGame ? ` — game ${match.setGame} of six` : ''} ›
            </Text>
          </Card>
        ) : null}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  body: { gap: Space.md, paddingBottom: Space.xxl, paddingTop: Space.md },
  fight: { gap: Space.lg },
  live: { color: Arena.dim, fontSize: 12, lineHeight: 17 },
  watch: { color: Arena.accent, fontSize: 15, fontWeight: '600', textAlign: 'center' },
  sectionTitle: { ...SectionLabelText, marginTop: Space.md },
  link: { color: Arena.accent, fontSize: 14 },
});
