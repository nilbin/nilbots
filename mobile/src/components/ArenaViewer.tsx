import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { WebView } from 'react-native-webview';

import { ArenaFloatingChrome, ArenaTitleBar } from '@/components/arena/ArenaChrome';
import { ArenaPanels } from '@/components/arena/ArenaPanels';
import { ArenaTransportBar } from '@/components/arena/ArenaTransport';
import { useArenaBridge } from '@/components/arena/useArenaBridge';
import { useArenaPresentation } from '@/components/arena/useArenaPresentation';
import { useBroadcastFollower } from '@/components/arena/useBroadcastFollower';
import { API_BASE_URL } from '@/api/config';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * Match playback: the site's renderer for the arena, native everything else.
 *
 * The WebView draws the canvas and nothing more. That part is ~950 lines of Canvas2D over
 * megabytes of wall atlases, it is still moving, and a second implementation would drift
 * from it. The transport, control bar and bot cards are lists and buttons — native ones
 * scroll and scrub properly, and they let the app show the match's own metadata instead of
 * the viewer's duplicate header.
 *
 * Three rules hold this together:
 *
 * **The clock stays in the page.** The app asks for play/pause/seek and is told what
 * happened. Driving `time` from here would be a bridge crossing per animation frame for
 * something requestAnimationFrame already does locally.
 *
 * **Rules-derived values are never computed here.** Control pressure, overtime limits,
 * zone tallies and hold phrasing all arrive already derived by
 * `web/src/replayPresentation.ts`, which the site's own panels share. Re-deriving any of it
 * would make this a rules surface that goes stale the first time the rules move.
 *
 * **There is exactly one WebView**, mounted for the life of the app and hidden with
 * `display: none` rather than unmounted — decoding those atlases is paid per instance, so a
 * viewer per screen would pay the whole bake on every open. That is what this file owns;
 * the protocol lives in `useArenaBridge`, the orientation rules in `useArenaPresentation`,
 * and the two layouts in `arena/`.
 */

type ArenaViewerApi = {
  /**
   * Show the arena and play a match's replay. Safe to call before the page is ready.
   *
   * A broadcasting match is *followed*, not played: pass `live` and the viewer anchors to
   * the server's presentation clock, re-reading the replay as more ticks are released,
   * with no transport. Playing one as a replay instead would show whichever ticks happened
   * to be public at open, stop at that edge, and drift from every other viewer.
   */
  watch: (matchId: string, options?: { title?: string; live?: boolean }) => void;
};

const ArenaViewerContext = createContext<ArenaViewerApi | null>(null);

export function useArenaViewer() {
  const api = useContext(ArenaViewerContext);
  if (!api) throw new Error('useArenaViewer must be used inside an ArenaViewerProvider');
  return api;
}

export function ArenaViewerProvider({ children }: { children: ReactNode }) {
  const [visible, setVisible] = useState(false);
  const [title, setTitle] = useState<string | undefined>();
  const [watching, setWatching] = useState<{ matchId: string; live: boolean } | null>(null);
  const [showVisibility, setShowVisibility] = useState(true);

  const bridge = useArenaBridge();
  const { control, load, loadWhenReady, reset, transport, result, header } = bridge;
  const presentation = useArenaPresentation({
    visible,
    playing: transport?.playing ?? false,
  });

  const watch = useCallback(
    (matchId: string, options?: { title?: string; live?: boolean }) => {
      setTitle(options?.title);
      setWatching({ matchId, live: options?.live ?? false });
      reset();
      setVisible(true);
      // A live match waits for its first clock poll rather than loading now: without an
      // anchor the page would play the released ticks as an ordinary replay.
      if (!options?.live) loadWhenReady(matchId);
    },
    [loadWhenReady, reset],
  );

  const close = useCallback(() => {
    control('pause');
    setVisible(false);
  }, [control]);

  // Broadcast over: reload without an anchor so the local transport takes over and the
  // full replay — outcome included — becomes seekable.
  const onBroadcastEnded = useCallback(
    (matchId: string) => {
      setWatching({ matchId, live: false });
      load(matchId);
    },
    [load],
  );

  useBroadcastFollower({
    matchId: visible && watching?.live ? watching.matchId : undefined,
    active: visible,
    load,
    onBroadcastEnded,
  });

  const api = useMemo<ArenaViewerApi>(() => ({ watch }), [watch]);
  const { landscape, chromeVisible, revealChrome } = presentation;

  // The same control in both layouts — a row of its own in portrait, floating over the
  // arena sideways — so it is built once rather than written twice.
  //
  // Absent while following: seeking a broadcast would put this viewer on a different tick
  // from everyone else watching the same fight.
  const transportNode = transport?.following ? (
    <View style={styles.broadcasting}>
      <Text style={styles.broadcastingText}>
        Broadcasting · tick {String(transport.tick).padStart(3, '0')} — every viewer sees this
        moment.
      </Text>
    </View>
  ) : (
    <ArenaTransportBar
      transport={transport}
      onToggle={() => control('toggle')}
      onStep={(delta) => control('step', delta)}
      onRestart={() => control('restart')}
      onSpeed={(speed) => control('setSpeed', speed)}
      onSeek={(tick) => control('seek', tick)}
    />
  );

  return (
    <ArenaViewerContext.Provider value={api}>
      {children}

      <View
        style={[StyleSheet.absoluteFill, styles.overlay, !visible && styles.hidden]}
        pointerEvents={visible ? 'auto' : 'none'}
        // Normally passive: returning false declines the responder, so the touch still
        // reaches the WebView and a tap on a bot selects it. Capturing here instead would
        // make the arena unclickable in exactly the mode built around looking at it.
        //
        // The ONE exception is the touch that wakes hidden chrome. That tap is the user
        // asking for the controls, not for a different body, and letting it through cost
        // them the selection they were following (owner, 2026-08-09). Swallowing it makes
        // the first tap wake the chrome and the second act — the same rule the web viewer
        // applies in `Viewer.selectFromArena`, arrived at from the opposite side because
        // here the chrome is native and the selection lives in the WebView.
        onStartShouldSetResponderCapture={
          landscape
            ? () => {
                const waking = !chromeVisible;
                revealChrome();
                return waking;
              }
            : undefined
        }>
        {landscape ? null : (
          <ArenaTitleBar title={title} header={header} onClose={close} />
        )}

        {/* In portrait the canvas keeps the arena's own aspect rather than stretching to
            fill: the renderer letterboxes inside whatever box it is given, so a tall box
            just adds dead bars above and below. Sideways there is nothing to share the
            screen with, so it takes all of it. */}
        <View style={landscape ? styles.canvasFull : styles.canvas}>
          <WebView
            ref={bridge.webViewRef}
            source={{ uri: `${API_BASE_URL}/?standalone&bridge=3` }}
            onMessage={bridge.onMessage}
            style={styles.web}
            containerStyle={styles.web}
            // The page is a single canvas now — nothing here scrolls, and letting it would
            // fight the native scroll view below.
            scrollEnabled={false}
            bounces={false}
            cacheEnabled
          />
        </View>

        {landscape ? (
          <ArenaFloatingChrome
            visible={chromeVisible}
            transport={transportNode}
            result={result}
            header={header}
            atEnd={transport?.atEnd ?? false}
            onClose={close}
          />
        ) : (
          <>
            {transportNode}
            <ArenaPanels
              bridge={bridge}
              showVisibility={showVisibility}
              onToggleVisibility={(next) => {
                setShowVisibility(next);
                control('setVisibility', next);
              }}
            />
          </>
        )}
      </View>
    </ArenaViewerContext.Provider>
  );
}

const styles = StyleSheet.create({
  overlay: { backgroundColor: Arena.bg, zIndex: 10 },
  hidden: { display: 'none' },
  canvas: { aspectRatio: 4 / 3, backgroundColor: Arena.bg },
  canvasFull: { flex: 1, backgroundColor: Arena.bg },
  web: { flex: 1, backgroundColor: Arena.bg },
  broadcasting: {
    borderTopWidth: 1,
    borderTopColor: Arena.edge,
    backgroundColor: Arena.panel,
    paddingHorizontal: Space.lg,
    paddingVertical: Space.md,
  },
  broadcastingText: { ...Mono, color: Arena.live, fontSize: 11 },
});
