import { StyleSheet, Text, View } from 'react-native';

import { BotSprite } from '@/components/BotSprite';
import { OutcomeText } from '@/components/OutcomeText';
import { Card } from '@/components/ui/Card';
import type { MatchSet } from '@/api/client';
import { Arena, Mono, Space } from '@/theme/arena';

type Game = MatchSet['games'][number];

/**
 * One game of a ranked set, told from `bot`'s side so a column of these reads as that
 * bot's run through the set rather than a list of unattributed results.
 *
 * A game that is still broadcasting reports no winner, so it says LIVE. `winnerBotId` is
 * also null for a genuine draw, which `draw` distinguishes — without that check a draw
 * would be indistinguishable from a result being withheld.
 */
export function SetGameRow({
  game,
  botId,
  onPress,
}: {
  game: Game;
  botId: string | undefined;
  onPress?: () => void;
}) {
  const outcome = game.broadcasting
    ? null
    : game.draw
      ? 'Draw'
      : game.winnerBotId === null
        ? null
        : game.winnerBotId === botId
          ? 'Win'
          : 'Loss';

  const opponent = game.participants.find((participant) => participant.botId !== botId);

  return (
    <Card
      onPress={onPress}
      accessibilityLabel={`Game ${game.game ?? '?'} on ${game.mapId}, ${
        game.broadcasting ? 'broadcasting' : (outcome ?? game.status.toLowerCase())
      }`}>
      <View style={styles.row}>
        <Text style={styles.game}>g{game.game ?? '?'}</Text>
        <OutcomeText
          outcome={outcome}
          broadcasting={game.broadcasting}
          status={game.status}
          style={styles.outcome}
        />
        <BotSprite
          lookId={opponent?.lookIdSnapshot}
          accent={opponent?.accentSnapshot}
          size="xs"
        />
        <Text style={styles.opponent} numberOfLines={1}>
          {opponent?.nameSnapshot ?? 'a removed bot'}
        </Text>
        <Text style={styles.map} numberOfLines={1}>
          {game.mapId}
        </Text>
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: Space.sm },
  game: { ...Mono, color: Arena.text, fontSize: 12, fontWeight: '700', minWidth: 20 },
  outcome: { minWidth: 30 },
  opponent: { color: Arena.text, fontSize: 14, flex: 1 },
  map: { ...Mono, color: Arena.dim, fontSize: 11, flexShrink: 0 },
});
