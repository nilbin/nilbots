import { Pressable, StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import type { ArenaBot } from '@/components/arena/protocol';
import { Arena, Mono, Radius, Space } from '@/theme/arena';

const STATUS_COLOR: Record<string, string> = {
  Active: Arena.ok,
  Destroyed: Arena.live,
  Disqualified: Arena.warn,
};

/**
 * One bot's state at the current tick: health, cooldown, zone hold, and what it chose to
 * do. Tapping selects it, which highlights it in the arena and reveals its debug line.
 *
 * The accent is the one the renderer resolved for this bot against the panel background,
 * not the raw `bot.accent` from the API — so hearts here are the colour the chassis is
 * drawn with, including for looks that override their bot's accent.
 */
export function ArenaBotCard({
  bot,
  lookId,
  showZone,
  selected,
  onPress,
}: {
  bot: ArenaBot;
  lookId?: string;
  /** Whether this match has a zone at all — the site shows an idle marker when it does. */
  showZone: boolean;
  selected: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      accessibilityLabel={`${bot.name}, ${bot.status}, ${bot.health} of ${bot.maxHealth} health`}
      style={({ pressed }) => [
        styles.card,
        selected && styles.selected,
        pressed && styles.pressed,
      ]}>
      <View style={styles.head}>
        <BotSprite lookId={lookId} accent={bot.accent} size="sm" />
        <View style={styles.identity}>
          <Text style={styles.name} numberOfLines={1}>
            {bot.name}
          </Text>
          <Text style={styles.meta} numberOfLines={1}>
            {bot.lookLabel} · slot {bot.slot} · {bot.runtimeKind}
          </Text>
        </View>
        <Text style={[styles.status, { color: STATUS_COLOR[bot.status] ?? Arena.dim }]}>
          {bot.status.toUpperCase()}
        </Text>
      </View>

      <View style={styles.stats}>
        <Text style={styles.hearts} numberOfLines={1}>
          {Array.from({ length: bot.maxHealth }, (_, index) => (
            <Text key={index} style={{ color: index < bot.health ? bot.accent : Arena.edge }}>
              ♥
            </Text>
          ))}
        </Text>

        <Text style={styles.stat}>
          CD <Text style={styles.statValue}>{bot.cooldown}</Text>
        </Text>

        {bot.energy !== undefined ? (
          <Text style={styles.stat}>
            ⚡ <Text style={styles.statValue}>{bot.energy}</Text>
          </Text>
        ) : null}

        {showZone ? (
          <Text style={[styles.stat, bot.holdingZone && styles.holding]}>
            ⬢{' '}
            <Text style={bot.holdingZone ? styles.holding : styles.statValue}>
              {bot.zoneTicks ?? (bot.holdingZone ? 'HOLD' : 'idle')}
            </Text>
          </Text>
        ) : null}
      </View>

      {bot.action ? (
        <Text style={styles.action} numberOfLines={1}>
          → <Text style={styles.statValue}>{bot.action}</Text>
          {bot.actionResult && bot.actionResult !== 'Success' ? (
            <Text style={styles.rejected}> ({bot.actionResult})</Text>
          ) : null}
        </Text>
      ) : null}

      {selected ? (
        <View style={styles.detail}>
          <Text style={styles.vision}>
            sees {bot.visibleTiles} tiles ·{' '}
            {bot.visibleEnemies.length > 0
              ? `enemy at ${bot.visibleEnemies.map((e) => `(${e.x},${e.y})`).join(' ')}`
              : 'no enemies visible'}
          </Text>
          {bot.debug ? <Text style={styles.debug}>{bot.debug}</Text> : null}
        </View>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: Radius.md,
    backgroundColor: Arena.panel,
    padding: Space.md,
    gap: Space.sm,
  },
  selected: { borderColor: Arena.accent },
  pressed: { opacity: 0.8 },
  head: { flexDirection: 'row', alignItems: 'center', gap: Space.sm },
  identity: { flex: 1, minWidth: 0 },
  name: { color: Arena.text, fontSize: 14, fontWeight: '600' },
  meta: { ...Mono, color: Arena.dim, fontSize: 10 },
  status: { ...Mono, fontSize: 10 },
  stats: { flexDirection: 'row', alignItems: 'center', gap: Space.md, flexWrap: 'wrap' },
  hearts: { fontSize: 13, letterSpacing: 1 },
  stat: { ...Mono, color: Arena.dim, fontSize: 11 },
  statValue: { color: Arena.text },
  holding: { color: Arena.zone },
  action: { ...Mono, color: Arena.dim, fontSize: 11 },
  rejected: { color: Arena.warn },
  detail: { gap: Space.xs, borderTopWidth: 1, borderTopColor: Arena.edge, paddingTop: Space.sm },
  vision: { ...Mono, color: Arena.dim, fontSize: 10 },
  debug: {
    ...Mono,
    color: Arena.dim,
    fontSize: 10,
    backgroundColor: Arena.bg,
    borderRadius: Radius.sm,
    padding: Space.sm,
  },
});
