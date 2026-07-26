import { router } from 'expo-router';
import { FlatList, Pressable, StyleSheet, Text, View } from 'react-native';

import { MyBotRow } from '@/components/MyBotRow';
import { EmptyState, ErrorState, LoadingState } from '@/components/ui/StateView';
import { Screen } from '@/components/ui/Screen';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useAuth } from '@/auth/AuthProvider';
import { useMyBots } from '@/hooks/useMyBots';
import { Arena, Mono, Radius, Space } from '@/theme/arena';

export default function GarageScreen() {
  const { status, signIn, signOut, error } = useAuth();
  const { data: bots, isPending, error: loadError, refetch, isRefetching } = useMyBots();

  if (status === 'loading')
    return (
      <Screen>
        <LoadingState />
      </Screen>
    );

  if (status === 'signed-out') return <SignedOut onSignIn={signIn} error={error} />;

  return (
    <Screen>
      <ScreenHeader
        title="Garage"
        status={bots ? `${bots.length} bot${bots.length === 1 ? '' : 's'}` : undefined}
        action={{ label: 'sign out', onPress: signOut }}
      />

      {isPending ? (
        <LoadingState label="Loading your bots…" />
      ) : loadError ? (
        <ErrorState error={loadError} onRetry={refetch} />
      ) : bots.length === 0 ? (
        <EmptyState
          title="No bots yet"
          detail="Bots are written and submitted with the CLI: nilbots new, then nilbots submit."
        />
      ) : (
        <FlatList
          data={bots}
          keyExtractor={(bot) => bot.id}
          contentContainerStyle={styles.list}
          onRefresh={refetch}
          refreshing={isRefetching}
          renderItem={({ item }) => (
            <MyBotRow bot={item} onPress={() => router.push(`/bots/${item.slug}`)} />
          )}
        />
      )}
    </Screen>
  );
}

/**
 * The signed-out garage. It says what signing in is *for* rather than only offering a
 * button — the rest of the app works without an account, so a bare login prompt in one
 * tab reads as a wall rather than an invitation.
 */
function SignedOut({ onSignIn, error }: { onSignIn: () => void; error: string | null }) {
  return (
    <Screen>
      <ScreenHeader title="Garage" />
      <View style={styles.intro}>
        <Text style={styles.blurb}>
          Sign in to see your bots, their build state, and how they are doing on the ladder.
        </Text>
        <Text style={styles.detail}>
          Writing and submitting bots stays in the CLI — this is the companion to it, not a
          replacement.
        </Text>

        <Pressable
          onPress={onSignIn}
          accessibilityRole="button"
          accessibilityLabel="Sign in to nilbots"
          style={({ pressed }) => [styles.button, pressed && styles.buttonPressed]}>
          <Text style={styles.buttonLabel}>Sign in</Text>
        </Pressable>

        {error ? <Text style={styles.error}>{error}</Text> : null}
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  list: { gap: Space.md, paddingBottom: 96 },
  intro: { gap: Space.md, paddingTop: Space.lg },
  blurb: { color: Arena.text, fontSize: 15, lineHeight: 21 },
  detail: { color: Arena.dim, fontSize: 13, lineHeight: 19 },
  button: {
    marginTop: Space.sm,
    borderWidth: 1,
    borderColor: Arena.accent,
    borderRadius: Radius.md,
    paddingVertical: Space.md,
    alignItems: 'center',
  },
  buttonPressed: { opacity: 0.7 },
  buttonLabel: { ...Mono, color: Arena.accent, fontSize: 15, fontWeight: '700' },
  error: { color: Arena.live, fontSize: 13 },
});
