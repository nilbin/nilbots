import { StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { Card } from '@/components/ui/Card';
import type { MyBot } from '@/api/client';
import { Arena, Mono, Space } from '@/theme/arena';

const STATUS_COLOR: Record<string, string> = {
  Succeeded: Arena.ok,
  Failed: Arena.live,
  Building: Arena.accent,
  Queued: Arena.dim,
};

/**
 * One of the player's own bots, in the garage.
 *
 * Deliberately about *build state* rather than standing — the roster and ladder already
 * rank bots, and what you come here for is whether the version you just submitted
 * compiled. A bot with no version at all has never been submitted, which is a different
 * thing from a build that failed and says so.
 */
export function MyBotRow({ bot, onPress }: { bot: MyBot; onPress?: () => void }) {
  const version = bot.latestVersion;

  return (
    <Card
      onPress={onPress}
      accessibilityLabel={
        version
          ? `${bot.name}, version ${version.versionNumber}, ${version.status}`
          : `${bot.name}, never submitted`
      }>
      <View style={styles.row}>
        <BotSprite lookId={bot.lookId} accent={bot.accent} size="md" />
        <View style={styles.identity}>
          <Text style={styles.name} numberOfLines={1}>
            {bot.name}
          </Text>
          <Text style={styles.slug} numberOfLines={1}>
            {bot.slug}
          </Text>
        </View>
        <View style={styles.build}>
          {version ? (
            <>
              <Text style={[styles.status, { color: STATUS_COLOR[version.status] ?? Arena.dim }]}>
                {version.status.toLowerCase()}
              </Text>
              <Text style={styles.version}>
                v{version.versionNumber}
                {version.isActive ? ' · active' : ''}
              </Text>
            </>
          ) : (
            <Text style={styles.none}>never submitted</Text>
          )}
        </View>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.md },
  identity: { flex: 1, minWidth: 0, gap: 1 },
  name: { color: Arena.text, fontSize: 16, fontWeight: '600' },
  slug: { ...Mono, color: Arena.dim, fontSize: 11 },
  build: { alignItems: 'flex-end', gap: 1 },
  status: { ...Mono, fontSize: 12, fontWeight: '700' },
  version: { ...Mono, color: Arena.dim, fontSize: 11 },
  none: { color: Arena.dim, fontSize: 12 },
});
