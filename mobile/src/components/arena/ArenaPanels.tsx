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
  const { header, result, tick, transport, selectedSlot, failed, selectSlot } = bridge;
  const lookFor = (slot: number) =>
    header?.participants.find((participant) => participant.slot === slot)?.lookId;

  return (
    <ScrollView style={styles.panels} contentContainerStyle={styles.body}>
      {failed ? (
        <Text style={styles.error}>
          That replay could not be loaded. It may still be building.
        </Text>
      ) : null}

      {result && transport?.atEnd ? <ArenaOutcome result={result} header={header} /> : null}

      {tick?.control ? <ArenaControlBar control={tick.control} /> : null}

      {tick?.bots.map((bot) => (
        <ArenaBotCard
          key={bot.slot}
          bot={bot}
          lookId={lookFor(bot.slot)}
          // A control-mode match has a zone but no per-bot tally, so the tally alone
          // cannot decide whether the marker belongs on the card.
          showZone={tick.control !== null || bot.zoneTicks !== null}
          selected={selectedSlot === bot.slot}
          onPress={() => selectSlot(bot.slot)}
        />
      ))}

      <View style={styles.toggle}>
        <Switch
          value={showVisibility}
          onValueChange={onToggleVisibility}
          trackColor={{ true: Arena.accent, false: Arena.edge }}
        />
        <Text style={styles.toggleLabel}>Show selected bot&apos;s field of view</Text>
      </View>

      {header ? (
        <Text style={styles.provenance} numberOfLines={2}>
          seed {header.seed}
          {header.replayHash ? ` · #${header.replayHash.slice(0, 12)}` : ''}
        </Text>
      ) : null}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  panels: { flex: 1 },
  body: { gap: Space.sm, padding: Space.lg, paddingBottom: Space.xxl },
  error: { color: Arena.dim, fontSize: 13 },
  toggle: { flexDirection: 'row', alignItems: 'center', gap: Space.sm, paddingTop: Space.xs },
  toggleLabel: { color: Arena.dim, fontSize: 12, flexShrink: 1 },
  provenance: { ...Mono, color: Arena.dim, fontSize: 10, paddingTop: Space.xs },
});
