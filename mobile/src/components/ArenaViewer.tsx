import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { WebView, type WebViewMessageEvent } from 'react-native-webview';

import { API_BASE_URL } from '@/api/config';
import { Arena, Mono, Space } from '@/theme/arena';

/**
 * Match playback, in a WebView running the site's own viewer.
 *
 * The renderer is ~550 lines of Canvas2D over several megabytes of wall atlases and
 * sprite sheets. Porting that to Skia is a real project and it is still moving, so the
 * app runs the same code the site does rather than a second implementation that would
 * drift.
 *
 * The cost that matters is decoding those textures and baking the sprites, and that is
 * paid **per WebView instance, not per match**. So there is exactly one, mounted for the
 * life of the app by `ArenaViewerProvider`, hidden until asked for. Mounting one per
 * match screen would pay the whole bake on every open — the stutter this design exists
 * to avoid. Nothing here may be moved into a screen.
 *
 * The page is loaded from the server rather than bundled: it is a 14 MB single-file
 * build, and `?standalone` is already how that build switches from the site to the
 * viewer.
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

type HostMessage = { type: 'ready' } | { type: 'loaded' } | { type: 'error'; reason?: string };

export function ArenaViewerProvider({ children }: { children: ReactNode }) {
  const webViewRef = useRef<WebView>(null);
  const [visible, setVisible] = useState(false);
  const [title, setTitle] = useState<string | undefined>();
  const [status, setStatus] = useState<'loading' | 'playing' | 'error'>('loading');

  // The page announces readiness once, on first mount. A watch() before that has nowhere
  // to deliver to, so it is held here and replayed when the announcement arrives.
  const ready = useRef(false);
  const pending = useRef<string | null>(null);

  const load = useCallback((matchId: string) => {
    const url = `${API_BASE_URL}/api/matches/${matchId}/replay`;
    webViewRef.current?.injectJavaScript(
      `window.__BOTARENA_LOAD__ && window.__BOTARENA_LOAD__({ url: ${JSON.stringify(url)} }); true;`,
    );
  }, []);

  const watch = useCallback(
    (matchId: string, watchTitle?: string) => {
      setTitle(watchTitle);
      setStatus('loading');
      setVisible(true);
      if (ready.current) load(matchId);
      else pending.current = matchId;
    },
    [load],
  );

  const onMessage = useCallback(
    (event: WebViewMessageEvent) => {
      let message: HostMessage;
      try {
        message = JSON.parse(event.nativeEvent.data) as HostMessage;
      } catch {
        return;
      }
      if (message.type === 'ready') {
        ready.current = true;
        if (pending.current) {
          load(pending.current);
          pending.current = null;
        }
      } else if (message.type === 'loaded') {
        setStatus('playing');
      } else if (message.type === 'error') {
        setStatus('error');
      }
    },
    [load],
  );

  const api = useMemo<ArenaViewerApi>(() => ({ watch }), [watch]);

  return (
    <ArenaViewerContext.Provider value={api}>
      {children}

      {/* display:'none' rather than conditional rendering: unmounting the WebView would
          throw away the decoded textures and make the next open pay for them again. */}
      <View
        style={[StyleSheet.absoluteFill, styles.overlay, !visible && styles.hidden]}
        pointerEvents={visible ? 'auto' : 'none'}>
        <SafeAreaView style={styles.safe} edges={['top', 'left', 'right']}>
          <View style={styles.bar}>
            <Text style={styles.title} numberOfLines={1}>
              {title ?? 'Replay'}
            </Text>
            <Pressable
              onPress={() => setVisible(false)}
              accessibilityRole="button"
              accessibilityLabel="Close the replay"
              hitSlop={12}>
              <Text style={styles.close}>close</Text>
            </Pressable>
          </View>
          {status === 'error' ? (
            <Text style={styles.error}>
              That replay could not be loaded. It may still be building.
            </Text>
          ) : null}
        </SafeAreaView>

        <WebView
          ref={webViewRef}
          // ?standalone is the site build's own switch into viewer mode.
          source={{ uri: `${API_BASE_URL}/?standalone` }}
          onMessage={onMessage}
          // The arena draws its own dark field; the styles carry that through so there is
          // no white flash while the page loads.
          style={styles.web}
          containerStyle={styles.web}
          // Scrollable on purpose. The viewer is not just the arena canvas — the bot
          // cards, the field-of-view toggle and the transport controls all sit below it,
          // and on a phone they are off-screen. Locking the scroll makes them
          // unreachable.
          bounces={false}
          allowsInlineMediaPlayback
          // Keeps the JS context alive across shows on iOS.
          cacheEnabled
        />
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
    paddingVertical: Space.md,
  },
  title: { color: Arena.text, fontSize: 16, fontWeight: '600', flexShrink: 1 },
  close: { ...Mono, color: Arena.accent, fontSize: 13 },
  error: { color: Arena.dim, fontSize: 13, paddingHorizontal: Space.lg, paddingBottom: Space.md },
  web: { flex: 1, backgroundColor: Arena.bg },
});
