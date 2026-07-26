import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Pressable, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { WebView, type WebViewMessageEvent } from 'react-native-webview';

import { ArenaBotCard } from '@/components/arena/ArenaBotCard';
import { ArenaControlBar } from '@/components/arena/ArenaControlBar';
import { ArenaTransportBar } from '@/components/arena/ArenaTransport';
import type {
  ArenaHeader,
  ArenaMessage,
  ArenaResult,
  ArenaTick,
  ArenaTransport,
} from '@/components/arena/protocol';
import { API_BASE_URL } from '@/api/config';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * Match playback: the site's renderer for the arena, native everything else.
 *
 * The WebView draws the canvas and nothing more. That part is ~950 lines of Canvas2D
 * over megabytes of wall atlases, it is still moving, and a second implementation would
 * drift from it. The transport, control bar and bot cards are lists and buttons — native
 * ones scroll and scrub properly, and they let the app show the match's own metadata
 * instead of the viewer's duplicate header.
 *
 * Two rules hold this together:
 *
 * **The clock stays in the page.** The app asks for play/pause/seek and is told what
 * happened. Driving `time` from here would be a bridge crossing per animation frame for
 * something requestAnimationFrame already does locally.
 *
 * **Rules-derived values are never computed here.** Control pressure, overtime limits,
 * zone tallies and hold phrasing all arrive already derived by
 * `web/src/replayPresentation.ts`, which the site's own panels share. Re-deriving any of
 * it would make this a rules surface that goes stale the first time the rules move.
 *
 * There is exactly one WebView, mounted for the life of the app and hidden with
 * `display: none` rather than unmounted — decoding those atlases is paid per instance,
 * so a viewer per screen would pay the whole bake on every open.
 */

type ArenaViewerApi = {
  /** Show the arena and play a match's replay. Safe to call before the page is ready. */
  watch: (matchId: string, title?: string) => void;
};

const ArenaViewerContext = createContext<ArenaViewerApi | null>(null);

export function useArenaViewer() {
  const api = useContext(ArenaViewerContext);
  if (!api) throw new Error('useArenaViewer must be used inside an ArenaViewerProvider');
  return api;
}

export function ArenaViewerProvider({ children }: { children: ReactNode }) {
  const webViewRef = useRef<WebView>(null);
  const [visible, setVisible] = useState(false);
  const [title, setTitle] = useState<string | undefined>();
  const [failed, setFailed] = useState(false);

  const [header, setHeader] = useState<ArenaHeader | null>(null);
  const [result, setResult] = useState<ArenaResult>(null);
  const [tickState, setTickState] = useState<ArenaTick | null>(null);
  const [transport, setTransport] = useState<ArenaTransport | null>(null);
  const [selectedSlot, setSelectedSlot] = useState<number | null>(null);
  const [showVisibility, setShowVisibility] = useState(true);

  // The page announces readiness once. A watch() before that has nowhere to land, so it
  // waits here and is replayed when the announcement arrives.
  const ready = useRef(false);
  const pending = useRef<string | null>(null);

  const call = useCallback((expression: string) => {
    webViewRef.current?.injectJavaScript(`${expression}; true;`);
  }, []);

  const control = useCallback(
    (method: string, ...args: unknown[]) => {
      const serialized = args.map((arg) => JSON.stringify(arg)).join(', ');
      call(`window.__BOTARENA_CONTROL__ && window.__BOTARENA_CONTROL__.${method}(${serialized})`);
    },
    [call],
  );

  const load = useCallback(
    (matchId: string) => {
      const url = `${API_BASE_URL}/api/matches/${matchId}/replay`;
      call(`window.__BOTARENA_LOAD__ && window.__BOTARENA_LOAD__({ url: ${JSON.stringify(url)} })`);
    },
    [call],
  );

  const watch = useCallback(
    (matchId: string, watchTitle?: string) => {
      setTitle(watchTitle);
      setFailed(false);
      // Cleared rather than kept: leaving the previous match's readouts up while the next
      // one loads shows numbers that belong to a different fight.
      setHeader(null);
      setResult(null);
      setTickState(null);
      setTransport(null);
      setSelectedSlot(null);
      setVisible(true);
      if (ready.current) load(matchId);
      else pending.current = matchId;
    },
    [load],
  );

  const onMessage = useCallback(
    (event: WebViewMessageEvent) => {
      let message: ArenaMessage;
      try {
        message = JSON.parse(event.nativeEvent.data) as ArenaMessage;
      } catch {
        return;
      }
      switch (message.type) {
        case 'ready':
          ready.current = true;
          if (pending.current) {
            load(pending.current);
            pending.current = null;
          }
          break;
        case 'replay':
          setHeader(message.header);
          setResult(message.result);
          break;
        case 'tick':
          setTickState({ tick: message.tick, control: message.control, bots: message.bots });
          break;
        case 'transport':
          setTransport({
            playing: message.playing,
            speed: message.speed,
            tick: message.tick,
            tickCount: message.tickCount,
            atEnd: message.atEnd,
          });
          break;
        case 'selected':
          setSelectedSlot(message.slot);
          break;
        case 'error':
          setFailed(true);
          break;
      }
    },
    [load],
  );

  const selectSlot = useCallback(
    (slot: number) => {
      const next = selectedSlot === slot ? null : slot;
      setSelectedSlot(next);
      control('selectSlot', next);
    },
    [control, selectedSlot],
  );

  const api = useMemo<ArenaViewerApi>(() => ({ watch }), [watch]);
  const lookFor = (slot: number) =>
    header?.participants.find((participant) => participant.slot === slot)?.lookId;

  return (
    <ArenaViewerContext.Provider value={api}>
      {children}

      <View
        style={[StyleSheet.absoluteFill, styles.overlay, !visible && styles.hidden]}
        pointerEvents={visible ? 'auto' : 'none'}>
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
            <Pressable
              onPress={() => {
                control('pause');
                setVisible(false);
              }}
              accessibilityRole="button"
              accessibilityLabel="Close the replay"
              hitSlop={12}>
              <Text style={styles.close}>close</Text>
            </Pressable>
          </View>
        </SafeAreaView>

        {/* The canvas keeps the arena's own aspect rather than stretching to fill: the
            renderer letterboxes inside whatever box it is given, so a tall box just adds
            dead bars above and below. */}
        <View style={styles.canvas}>
          <WebView
            ref={webViewRef}
            source={{ uri: `${API_BASE_URL}/?standalone` }}
            onMessage={onMessage}
            style={styles.web}
            containerStyle={styles.web}
            // The page is a single canvas now — nothing here scrolls, and letting it
            // would fight the native scroll view below.
            scrollEnabled={false}
            bounces={false}
            cacheEnabled
          />
        </View>

        <ArenaTransportBar
          transport={transport}
          onToggle={() => control('toggle')}
          onStep={(delta) => control('step', delta)}
          onRestart={() => control('restart')}
          onSpeed={(speed) => control('setSpeed', speed)}
          onSeek={(tick) => control('seek', tick)}
        />

        <ScrollView style={styles.panels} contentContainerStyle={styles.panelsBody}>
          {failed ? (
            <Text style={styles.error}>
              That replay could not be loaded. It may still be building.
            </Text>
          ) : null}

          {result && transport?.atEnd ? (
            <View style={styles.outcome}>
              <Text style={styles.outcomeLine}>
                {result.winnerSlot === null
                  ? 'DRAW'
                  : `${header?.participants[result.winnerSlot]?.name ?? 'winner'} WINS`}
              </Text>
              <Text style={styles.outcomeReason}>
                {result.reason} · tick {result.endTick}
              </Text>
            </View>
          ) : null}

          {tickState?.control ? <ArenaControlBar control={tickState.control} /> : null}

          {tickState?.bots.map((bot) => (
            <ArenaBotCard
              key={bot.slot}
              bot={bot}
              lookId={lookFor(bot.slot)}
              // A control-mode match has a zone but no per-bot tally, so the tally alone
              // cannot decide whether the marker belongs on the card.
              showZone={tickState.control !== null || bot.zoneTicks !== null}
              selected={selectedSlot === bot.slot}
              onPress={() => selectSlot(bot.slot)}
            />
          ))}

          <View style={styles.toggle}>
            <Switch
              value={showVisibility}
              onValueChange={(next) => {
                setShowVisibility(next);
                control('setVisibility', next);
              }}
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
      </View>
    </ArenaViewerContext.Provider>
  );
}

const styles = StyleSheet.create({
  overlay: { backgroundColor: Arena.bg, zIndex: 10 },
  hidden: { display: 'none' },
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
  canvas: { aspectRatio: 4 / 3, backgroundColor: Arena.bg },
  web: { flex: 1, backgroundColor: Arena.bg },
  panels: { flex: 1 },
  panelsBody: { gap: Space.sm, padding: Space.lg, paddingBottom: Space.xxl },
  error: { color: Arena.dim, fontSize: 13 },
  outcome: { alignItems: 'center', paddingVertical: Space.sm, gap: 2 },
  outcomeLine: { color: Arena.text, fontSize: 20, fontWeight: '800', letterSpacing: 1 },
  outcomeReason: { ...Mono, color: Arena.dim, fontSize: 11 },
  toggle: { flexDirection: 'row', alignItems: 'center', gap: Space.sm, paddingTop: Space.xs },
  toggleLabel: { color: Arena.dim, fontSize: 12, flexShrink: 1 },
  provenance: { ...Mono, color: Arena.dim, fontSize: 10, paddingTop: Space.xs },
});
