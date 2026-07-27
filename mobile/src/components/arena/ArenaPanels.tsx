import { ScrollView, StyleSheet, Switch, Text, View } from 'react-native';

import { ArenaBotCard } from '@/components/arena/ArenaBotCard';
import { ArenaControlBar } from '@/components/arena/ArenaControlBar';
import { ArenaOutcome } from '@/components/arena/ArenaOutcome';
import type { ArenaBridge } from '@/components/arena/useArenaBridge';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * Everything the arena is *not*: the outcome, control pressure, the bot cards, the
 * field-of-view switch and the fight's provenance.
 *
 * Portrait only. Sideways these are what full screen removes — a third of the screen given
 * to text is not "mainly the game" — so the whole scroll view is dropped rather than
 * squeezed. Nothing here is derived locally: every rules-dependent value arrives already
 * computed by `web/src/replayPresentation.ts`, which the site's own panels share.
 */
export function ArenaPanels({
  bridge,
  showVisibility,
  onToggleVisibility,
}: {
  bridge: ArenaBridge;
  showVisibility: boolean;
  onToggleVisibility: (next: boolean) => void;
}) {
  const {
    header,
    result,
    tick,
    transport,
    selectedUnitKey,
    failed,
    selectUnit,
  } = bridge;
  const lookFor = (participantId: number) =>
    header?.participants.find(
      (participant) => participant.participantId === participantId,
    )?.lookId ?? undefined;

  return (
    <ScrollView style={styles.panels} contentContainerStyle={styles.body}>
      {failed ? (
        <Text style={styles.error}>
          That replay could not be loaded. It may still be building.
        </Text>
      ) : null}

      {transport?.loading ? (
        <Text style={styles.loading}>
          Loading arena assets
          {transport.pendingAssets > 0
            ? ` · ${transport.pendingAssets} remaining`
            : '…'}
        </Text>
      ) : null}

      {result && transport?.atEnd ? <ArenaOutcome result={result} header={header} /> : null}

      {tick?.objective ? (
        <ArenaControlBar objective={tick.objective} />
      ) : null}

      {tick?.units.map((unit) => (
        <ArenaBotCard
          key={unit.unitKey}
          unit={unit}
          lookId={lookFor(unit.participantId)}
          // A pressure-mode match can have an objective without a per-unit tally, so
          // cannot decide whether the marker belongs on the card.
          showObjective={
            tick.objective !== null ||
            unit.zoneTicks !== null ||
            unit.holdingObjective
          }
          selected={selectedUnitKey === unit.unitKey}
          onPress={() => selectUnit(unit.unitKey)}
        />
      ))}

      <View style={styles.toggle}>
        <Switch
          value={showVisibility}
          onValueChange={onToggleVisibility}
          trackColor={{ true: Arena.accent, false: Arena.edge }}
        />
        <Text style={styles.toggleLabel}>
          Show selected unit&apos;s field of view
        </Text>
      </View>

      {header ? (
        <View style={styles.provenance}>
          <Text style={styles.provenanceLine} numberOfLines={1}>
            seed {header.seed}
            {header.seedExact ? '' : ' · inexact legacy value'}
          </Text>
          <Text style={styles.provenanceLine} numberOfLines={1}>
            replay v{header.replayVersion}
            {header.replayHash
              ? ` · #${header.replayHash.slice(0, 12)}`
              : ''}
          </Text>
        </View>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  panels: { flex: 1 },
  body: { gap: Space.sm, padding: Space.lg, paddingBottom: Space.xxl },
  error: { color: Arena.dim, fontSize: 13 },
  loading: { ...Mono, color: Arena.accent, fontSize: 11 },
  toggle: { flexDirection: 'row', alignItems: 'center', gap: Space.sm, paddingTop: Space.xs },
  toggleLabel: { color: Arena.dim, fontSize: 12, flexShrink: 1 },
  provenance: { gap: 2, paddingTop: Space.xs },
  provenanceLine: { ...Mono, color: Arena.dim, fontSize: 10 },
});
