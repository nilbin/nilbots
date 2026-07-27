import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { SPEEDS, type ArenaTransport as Transport } from '@/components/arena/protocol';
import { Arena, Mono, Radius, Space } from '@/theme/arena';

/**
 * Native playback transport. The clock lives in the WebView, so these are requests
 * against it — the button state comes back as a `transport` message rather than being
 * assumed locally, which keeps the two from disagreeing when playback ends on its own.
 *
 * A live broadcast has no transport at all: every viewer sees the same moment by design,
 * so seeking would break the one property broadcasting has.
 */
export function ArenaTransportBar({
  transport,
  onToggle,
  onStep,
  onRestart,
  onSpeed,
  onSeek,
}: {
  transport: Transport | null;
  onToggle: () => void;
  onStep: (delta: number) => void;
  onRestart: () => void;
  onSpeed: (speed: number) => void;
  onSeek: (tick: number) => void;
}) {
  const tickCount = transport?.tickCount ?? 0;
  const tick = transport?.tick ?? 0;
  const loading = transport?.loading ?? true;
  const progress =
    tickCount > 1 ? Math.min(1, tick / (tickCount - 1)) : 0;

  return (
    <View style={styles.container}>
      <Scrubber
        progress={progress}
        tickCount={tickCount}
        disabled={loading}
        onSeek={onSeek}
      />

      <View style={styles.row}>
        <Text style={styles.counter}>
          {loading
            ? `loading${transport?.pendingAssets ? ` ${transport.pendingAssets}` : ''}`
            : `${String(tick).padStart(3, '0')}/${String(
                Math.max(0, tickCount - 1),
              ).padStart(3, '0')}`}
        </Text>

        <View style={styles.buttons}>
          <TransportButton
            label="⏮"
            hint="Restart"
            disabled={loading}
            onPress={onRestart}
          />
          <TransportButton
            label="◀"
            hint="Step back one tick"
            disabled={loading}
            onPress={() => onStep(-1)}
          />
          <TransportButton
            label={transport?.atEnd ? '⟲' : transport?.playing ? '❚❚' : '▶'}
            hint={transport?.playing ? 'Pause' : 'Play'}
            onPress={transport?.atEnd ? onRestart : onToggle}
            disabled={loading}
            primary
          />
          <TransportButton
            label="▶"
            hint="Step forward one tick"
            disabled={loading}
            onPress={() => onStep(1)}
          />
        </View>

        <View style={styles.speeds}>
          {SPEEDS.map((speed) => (
            <Pressable
              key={speed}
              onPress={() => onSpeed(speed)}
              disabled={loading}
              accessibilityRole="button"
              accessibilityLabel={`Play at ${speed} times speed`}
              accessibilityState={{
                disabled: loading,
                selected: transport?.speed === speed,
              }}
              hitSlop={6}>
              <Text
                style={[
                  styles.speed,
                  transport?.speed === speed && styles.speedOn,
                  loading && styles.disabled,
                ]}>
                {speed}x
              </Text>
            </Pressable>
          ))}
        </View>
      </View>
    </View>
  );
}

/**
 * Tap-to-seek across the timeline. The bar is full-width, so its pixel width has to be
 * measured before a touch position means anything — once per layout, not per touch.
 */
function Scrubber({
  progress,
  tickCount,
  disabled,
  onSeek,
}: {
  progress: number;
  tickCount: number;
  disabled: boolean;
  onSeek: (tick: number) => void;
}) {
  const [width, setWidth] = useState(0);

  return (
    <Pressable
      disabled={disabled}
      accessibilityRole="adjustable"
      accessibilityLabel="Playback position"
      accessibilityState={{ disabled }}
      onLayout={(event) => setWidth(event.nativeEvent.layout.width)}
      onPress={(event) => {
        if (width <= 0 || tickCount === 0) return;
        const fraction = Math.max(0, Math.min(1, event.nativeEvent.locationX / width));
        onSeek(Math.round(fraction * (tickCount - 1)));
      }}
      // The bar itself is 4px; the touch target has to be a finger.
      hitSlop={{ top: 12, bottom: 12 }}
      style={styles.track}>
      <View style={[styles.fill, { width: `${progress * 100}%` }]} />
    </Pressable>
  );
}

function TransportButton({
  label,
  hint,
  onPress,
  disabled = false,
  primary = false,
}: {
  label: string;
  hint: string;
  onPress: () => void;
  disabled?: boolean;
  primary?: boolean;
}) {
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      accessibilityRole="button"
      accessibilityLabel={hint}
      accessibilityState={{ disabled }}
      hitSlop={8}
      style={({ pressed }) => [
        styles.button,
        primary && styles.buttonPrimary,
        disabled && styles.disabled,
        pressed && styles.buttonPressed,
      ]}>
      <Text style={[styles.buttonLabel, primary && styles.buttonLabelPrimary]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  container: {
    borderTopWidth: 1,
    borderTopColor: Arena.edge,
    backgroundColor: Arena.panel,
    paddingHorizontal: Space.lg,
    paddingTop: Space.md,
    paddingBottom: Space.sm,
    gap: Space.md,
  },
  track: { height: 4, borderRadius: 2, backgroundColor: Arena.bg, overflow: 'hidden' },
  fill: { height: '100%', backgroundColor: Arena.accent },
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  counter: { ...Mono, color: Arena.dim, fontSize: 11, minWidth: 62 },
  buttons: { flexDirection: 'row', alignItems: 'center', gap: Space.xs },
  button: {
    minWidth: 40,
    paddingVertical: 6,
    borderRadius: Radius.sm,
    alignItems: 'center',
  },
  buttonPrimary: { backgroundColor: Arena.edge, minWidth: 52 },
  buttonPressed: { opacity: 0.6 },
  buttonLabel: { color: Arena.dim, fontSize: 13 },
  buttonLabelPrimary: { color: Arena.accent, fontSize: 14 },
  speeds: { flexDirection: 'row', gap: Space.sm, minWidth: 62, justifyContent: 'flex-end' },
  speed: { ...Mono, color: Arena.dim, fontSize: 11 },
  speedOn: { color: Arena.accent, fontWeight: '700' },
  disabled: { opacity: 0.4 },
});
