import { Pressable, StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import type { ArenaUnitPresentation } from '@/components/arena/protocol';
import { Arena, Mono, Radius, Space } from '@/theme/arena';

const STATUS_COLOR: Record<string, string> = {
  active: Arena.ok,
  destroyed: Arena.live,
  disqualified: Arena.warn,
  respawning: Arena.live,
  locked: Arena.dim,
  ready: Arena.accent,
  'fabrication-queued': Arena.zone,
  rebuilding: Arena.zone,
};

/**
 * One stable unit at the current tick.
 *
 * The card survives runtime-life changes: selection follows unitKey while actorKey and
 * lifeId are allowed to be null during locks, respawns, rebuilds and fabrication. Tapping
 * asks the WebView to select the stable unit and reveals observation/debug detail.
 */
export function ArenaBotCard({
  unit,
  lookId,
  showObjective,
  selected,
  onPress,
}: {
  unit: ArenaUnitPresentation;
  lookId?: string;
  /** Whether this match has an objective worth showing on the card. */
  showObjective: boolean;
  selected: boolean;
  onPress: () => void;
}) {
  const healthPercent =
    (100 * Math.max(0, Math.min(unit.health, unit.maxHealth))) /
    Math.max(1, unit.maxHealth);
  const hasLife = unit.actorKey !== null && unit.lifeId !== null;
  const statusLabel = unit.status.replaceAll('-', ' ').toUpperCase();
  const transition =
    unit.status === 'respawning' && unit.respawnAtTick !== null
      ? { label: 'RESPAWN', tick: unit.respawnAtTick }
      : unit.status === 'locked' && unit.unlockAtTick !== null
        ? { label: 'UNLOCK', tick: unit.unlockAtTick }
        : unit.status === 'rebuilding' &&
            unit.rebuildReadyAtTick !== null
          ? { label: 'READY', tick: unit.rebuildReadyAtTick }
          : unit.status === 'fabrication-queued' &&
              unit.fabricationAtTick !== null
            ? { label: 'SPAWN', tick: unit.fabricationAtTick }
            : null;
  const formTransition = unit.pendingFormTransition;

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      accessibilityLabel={`${unit.name}, team ${unit.teamId} unit ${unit.unitId}, ${statusLabel}, ${unit.health} of ${unit.maxHealth} health`}
      style={({ pressed }) => [
        styles.card,
        selected && styles.selected,
        pressed && styles.pressed,
      ]}>
      <View style={styles.head}>
        <BotSprite lookId={lookId} accent={unit.accent} size="sm" />
        <View style={styles.identity}>
          <Text style={styles.name} numberOfLines={1}>
            {unit.name}
          </Text>
          <Text style={styles.meta} numberOfLines={1}>
            team {unit.teamId} · unit {unit.unitId} · {unit.formId || 'no form'}
          </Text>
          <Text style={styles.meta} numberOfLines={1}>
            {unit.runtimeKind} · life {unit.lifeId ?? '—'}
            {unit.legacySlot === null ? '' : ` · legacy slot ${unit.legacySlot}`}
          </Text>
        </View>
        <Text
          style={[
            styles.status,
            { color: STATUS_COLOR[unit.status] ?? Arena.dim },
          ]}>
          {statusLabel}
        </Text>
      </View>

      <View style={styles.healthRow}>
        <Text style={styles.stat}>
          HP{' '}
          <Text style={styles.statValue}>
            {unit.health}/{unit.maxHealth}
          </Text>
        </Text>
        <View style={styles.healthTrack}>
          <View
            style={[
              styles.healthFill,
              {
                width: `${healthPercent}%`,
                backgroundColor: unit.accent,
              },
            ]}
          />
        </View>
      </View>

      <View style={styles.stats}>
        <Text style={styles.stat}>
          CD <Text style={styles.statValue}>{unit.cooldown}</Text>
        </Text>

        {unit.energy !== null ? (
          <Text style={styles.stat}>
            ⚡ <Text style={styles.statValue}>{unit.energy}</Text>
          </Text>
        ) : null}

        {showObjective ? (
          <Text style={[styles.stat, unit.holdingObjective && styles.holding]}>
            ⬢{' '}
            <Text
              style={
                unit.holdingObjective ? styles.holding : styles.statValue
              }>
              {unit.zoneTicks ??
                (unit.holdingObjective ? 'HOLDING' : 'idle')}
            </Text>
          </Text>
        ) : null}

        {transition ? (
          <Text style={styles.stat}>
            {transition.label}{' '}
            <Text style={styles.statValue}>T{transition.tick}</Text>
          </Text>
        ) : null}
        {formTransition ? (
          <Text style={[styles.stat, styles.anchoring]}>
            ANCHOR{' '}
            <Text style={styles.statValue}>
              {formTransition.fromFormId}→{formTransition.toFormId} · T
              {formTransition.completesAtTick}
            </Text>
          </Text>
        ) : null}
      </View>

      {formTransition ||
      !unit.canMove ||
      unit.omnidirectionalVision ||
      unit.omnidirectionalShooting ? (
        <View style={styles.signals}>
          {formTransition ? (
            <Text style={[styles.signal, styles.windup]}>WINDUP</Text>
          ) : null}
          {!unit.canMove ? (
            <Text style={styles.signal}>STATIONARY</Text>
          ) : null}
          {unit.omnidirectionalVision ? (
            <Text style={styles.signal}>360° VISION</Text>
          ) : null}
          {unit.omnidirectionalShooting ? (
            <Text style={styles.signal}>360° FIRE</Text>
          ) : null}
        </View>
      ) : null}

      {unit.actionId !== null ? (
        <Text style={styles.action} numberOfLines={1}>
          → <Text style={styles.statValue}>{unit.actionId}</Text>
          {unit.actionLaunchHeading
            ? ` · ${unit.actionLaunchHeading.toUpperCase()}`
            : ''}
          {unit.actionResult !== null &&
          unit.actionResult !== 'success' &&
          unit.actionResult !== 'none' ? (
            <Text style={styles.rejected}> ({unit.actionResult})</Text>
          ) : null}
        </Text>
      ) : null}

      {selected ? (
        <View style={styles.detail}>
          <Text style={styles.vision}>
            {hasLife ? unit.actorKey : 'no active life'} · sees{' '}
            {unit.visibleTiles} tiles ·{' '}
            {unit.visibleEnemies.length > 0
              ? `${unit.visibleEnemies.length} ${
                  unit.visibleEnemies.length === 1 ? 'enemy' : 'enemies'
                } at ${unit.visibleEnemies
                  .map((enemy) => `(${enemy.x},${enemy.y})`)
                  .join(' ')}`
              : 'no enemies visible'}
          </Text>
          {unit.debug ? <Text style={styles.debug}>{unit.debug}</Text> : null}
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
  status: {
    ...Mono,
    fontSize: 9,
    maxWidth: 92,
    textAlign: 'right',
  },
  healthRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Space.sm,
  },
  healthTrack: {
    flex: 1,
    height: 4,
    borderRadius: 2,
    backgroundColor: Arena.edge,
    overflow: 'hidden',
  },
  healthFill: { height: '100%' },
  stats: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Space.md,
    flexWrap: 'wrap',
  },
  stat: { ...Mono, color: Arena.dim, fontSize: 11 },
  statValue: { color: Arena.text },
  holding: { color: Arena.zone },
  anchoring: { color: Arena.zone },
  signals: { flexDirection: 'row', flexWrap: 'wrap', gap: Space.xs },
  signal: {
    ...Mono,
    color: Arena.accent,
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: Radius.sm,
    paddingHorizontal: Space.xs,
    paddingVertical: 2,
    fontSize: 9,
  },
  windup: { color: Arena.zone },
  action: { ...Mono, color: Arena.dim, fontSize: 11 },
  rejected: { color: Arena.warn },
  detail: {
    gap: Space.xs,
    borderTopWidth: 1,
    borderTopColor: Arena.edge,
    paddingTop: Space.sm,
  },
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
