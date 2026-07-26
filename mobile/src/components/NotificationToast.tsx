import { useEffect, useRef } from 'react';
import { Animated, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { BotSprite } from '@/components/BotSprite';
import type { UserNotification } from '@/api/client';
import { Arena, Mono, Radius, Space } from '@/theme/arena';

/** Long enough to read and feel, short enough not to sit in the way. */
const VISIBLE_MS = 5_000;

/**
 * The moment the game pays the player back.
 *
 * A push can be plain; this cannot. It slides in over whatever is on screen, says the one
 * thing worth saying, and leaves. Tapping dismisses; so does waiting.
 *
 * Losses look exactly like wins here, in weight and prominence — only the colour differs
 * (DECISIONS #119). The ladder already shows the rating, so a silent or shrunken loss
 * reads as the app hiding something rather than sparing anyone.
 */
export function NotificationToast({
  notification,
  queued,
  onDismiss,
}: {
  notification: UserNotification;
  queued: number;
  onDismiss: () => void;
}) {
  const slide = useRef(new Animated.Value(-140)).current;

  useEffect(() => {
    slide.setValue(-140);
    Animated.spring(slide, {
      toValue: 0,
      useNativeDriver: true,
      damping: 16,
      stiffness: 180,
    }).start();
    const timer = setTimeout(onDismiss, VISIBLE_MS);
    return () => clearTimeout(timer);
    // Keyed on the notification so a queued second one re-runs the entrance.
  }, [notification.id, slide, onDismiss]);

  const content = describe(notification);
  if (!content) return null;

  return (
    <SafeAreaView style={styles.host} edges={['top']} pointerEvents="box-none">
      <Animated.View style={[styles.animated, { transform: [{ translateY: slide }] }]}>
        <Pressable
          onPress={onDismiss}
          accessibilityRole="button"
          accessibilityLabel={`${content.title}. ${content.detail}`}
          style={({ pressed }) => [styles.toast, pressed && styles.pressed]}>
          <View style={styles.row}>
            {content.lookId !== undefined ? (
              <BotSprite lookId={content.lookId} accent={content.accent} size="md" />
            ) : null}
            <View style={styles.text}>
              <Text style={styles.title} numberOfLines={1}>
                {content.title}
              </Text>
              <Text style={styles.detail} numberOfLines={2}>
                {content.detail}
              </Text>
            </View>
            {content.headline ? (
              <Text style={[styles.headline, { color: content.headlineColor }]}>
                {content.headline}
              </Text>
            ) : null}
          </View>
          {queued > 1 ? <Text style={styles.queued}>+{queued - 1} more</Text> : null}
        </Pressable>
      </Animated.View>
    </SafeAreaView>
  );
}

type Content = {
  title: string;
  detail: string;
  /** The signed rating delta, or the outcome — whatever the reader came for. */
  headline?: string;
  headlineColor?: string;
  lookId?: string;
  accent?: string;
};

/**
 * Narrowed on the payload's own `kind`, not the notification's — they carry the same
 * string, but TypeScript cannot narrow a sibling property from it.
 *
 * An unrecognised kind renders nothing rather than guessing. It is still acknowledged, so
 * a client that has not shipped support for a new kind does not accumulate an inbox it
 * cannot show.
 */
function describe(notification: UserNotification): Content | null {
  const payload = notification.payload;
  switch (payload.kind) {
    case 'entitlement-earned': {
      const first = payload.items[0];
      if (!first) return null;
      return {
        title: payload.items.length === 1 ? `${first.label} unlocked` : 'New loadout unlocked',
        detail: payload.reason ?? 'A new look is yours.',
        lookId: first.kind === 'bot-look' ? first.id : undefined,
        headline: '★',
        headlineColor: Arena.zone,
      };
    }
    case 'set-settled': {
      const gain = payload.ratingChange >= 0;
      return {
        title: `${payload.botName} ${outcomeVerb(payload.outcome)}`,
        detail: `${payload.score}–${payload.opponentScore} against ${payload.opponentName}`,
        headline: `${gain ? '+' : ''}${Math.round(payload.ratingChange)}`,
        headlineColor: gain ? Arena.ok : Arena.live,
        lookId: payload.botLookId,
        accent: payload.botAccent,
      };
    }
    case 'match-settled':
      return {
        title: `${payload.botName} ${outcomeVerb(payload.outcome)}`,
        detail: `against ${payload.opponentName} on ${payload.mapId}`,
        lookId: payload.botLookId,
        accent: payload.botAccent,
        headline: payload.outcome === 'Win' ? 'W' : payload.outcome === 'Loss' ? 'L' : 'D',
        headlineColor:
          payload.outcome === 'Win'
            ? Arena.ok
            : payload.outcome === 'Loss'
              ? Arena.live
              : Arena.dim,
      };
    default:
      return null;
  }
}

function outcomeVerb(outcome: string) {
  return outcome === 'Win' ? 'won' : outcome === 'Loss' ? 'lost' : 'drew';
}

const styles = StyleSheet.create({
  host: { position: 'absolute', top: 0, left: 0, right: 0, zIndex: 50 },
  animated: { paddingHorizontal: Space.md },
  toast: {
    backgroundColor: Arena.panel,
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: Radius.lg,
    padding: Space.md,
    gap: Space.xs,
    // The one place the app uses a shadow: this sits over other content and needs to
    // read as above it rather than part of it.
    shadowColor: '#000',
    shadowOpacity: 0.5,
    shadowRadius: 18,
    shadowOffset: { width: 0, height: 8 },
    elevation: 8,
  },
  pressed: { opacity: 0.85 },
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  text: { flex: 1, minWidth: 0, gap: 1 },
  title: { color: Arena.text, fontSize: 15, fontWeight: '700' },
  detail: { color: Arena.dim, fontSize: 12, lineHeight: 16 },
  headline: { ...Mono, fontSize: 22, fontWeight: '800' },
  queued: { ...Mono, color: Arena.dim, fontSize: 10, textAlign: 'right' },
});
