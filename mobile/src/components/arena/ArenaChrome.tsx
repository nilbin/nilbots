import type { ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { ArenaOutcome } from '@/components/arena/ArenaOutcome';
import type { ArenaHeader, ArenaResult } from '@/components/arena/protocol';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * The bar above the arena in portrait: what is being watched, and the way out.
 */
export function ArenaTitleBar({
  title,
  header,
  onClose,
}: {
  title: string | undefined;
  header: ArenaHeader | null;
  onClose: () => void;
}) {
  return (
    <SafeAreaView style={styles.safe} edges={['top', 'left', 'right']}>
      <View style={styles.bar}>
        <View style={styles.titleBlock}>
          <Text style={styles.title} numberOfLines={1}>
            {title ?? 'Replay'}
          </Text>
          {header ? (
            <Text style={styles.subtitle} numberOfLines={1}>
              {header.mapId} · rules {header.rulesVersion}
              {header.partial ? ' · broadcasting' : ''}
            </Text>
          ) : null}
        </View>
        <CloseButton onPress={onClose} />
      </View>
    </SafeAreaView>
  );
}

/**
 * The chrome that floats over a landscape arena, and fades when it is not wanted.
 *
 * Floating rather than a row of its own: a row of controls under a landscape canvas is the
 * layout that made full screen pointless on the site, and the arena is already
 * height-constrained sideways — a row would come straight out of it.
 *
 * Hidden by opacity rather than unmounting, so the transport keeps its scrubber position
 * and the WebView underneath is never relaid out. Nothing jumps when it comes back.
 */
export function ArenaFloatingChrome({
  visible,
  transport,
  result,
  header,
  atEnd,
  onClose,
}: {
  visible: boolean;
  transport: ReactNode;
  result: ArenaResult;
  header: ArenaHeader | null;
  atEnd: boolean;
  onClose: () => void;
}) {
  const hidden = visible ? undefined : styles.faded;
  const touchable = visible ? 'box-none' : 'none';

  return (
    <>
      <SafeAreaView
        style={styles.floating}
        edges={['bottom', 'left', 'right']}
        pointerEvents={touchable}>
        <View style={hidden}>{transport}</View>
      </SafeAreaView>

      {/* The only way out sideways, since the title bar is gone. */}
      <SafeAreaView
        style={styles.floatingTop}
        edges={['top', 'right']}
        pointerEvents={touchable}>
        <CloseButton onPress={onClose} style={[styles.floatingClose, hidden]} />
      </SafeAreaView>

      {/* Not tied to the fading chrome: the result is the point of watching, and it should
          not time out three seconds after arriving. */}
      {result && atEnd ? (
        <View style={styles.outcomeOverlay} pointerEvents="none">
          <ArenaOutcome result={result} header={header} framed />
        </View>
      ) : null}
    </>
  );
}

function CloseButton({
  onPress,
  style,
}: {
  onPress: () => void;
  style?: React.ComponentProps<typeof Pressable>['style'];
}) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel="Close the replay"
      hitSlop={12}
      style={style}>
      <Text style={styles.close}>close</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  safe: { backgroundColor: Arena.bg },
  bar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: Space.md,
    paddingHorizontal: Space.lg,
    paddingVertical: Space.sm,
  },
  titleBlock: { flexShrink: 1 },
  title: { color: Arena.text, fontSize: 16, fontWeight: '600' },
  subtitle: { ...Mono, color: Arena.dim, fontSize: 11 },
  close: { ...Mono, color: Arena.accent, fontSize: 13 },
  floating: { position: 'absolute', left: 0, right: 0, bottom: 0, padding: Space.sm },
  floatingTop: { position: 'absolute', top: 0, right: 0, alignItems: 'flex-end' },
  floatingClose: {
    margin: Space.sm,
    borderWidth: 1,
    borderColor: Arena.edge,
    borderRadius: 6,
    backgroundColor: Arena.panel,
    paddingHorizontal: Space.sm,
    paddingVertical: Space.xs,
  },
  faded: { opacity: 0 },
  outcomeOverlay: {
    position: 'absolute',
    top: 0,
    right: 0,
    bottom: 0,
    left: 0,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
