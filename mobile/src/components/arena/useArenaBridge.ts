import { useCallback, useRef, useState } from 'react';
import type { WebView, WebViewMessageEvent } from 'react-native-webview';

import type {
  ArenaHeader,
  ArenaMessage,
  ArenaResult,
  ArenaTick,
  ArenaTransport,
} from '@/components/arena/protocol';
import { API_BASE_URL } from '@/api/config';

/**
 * The half of the arena that talks to the page.
 *
 * Everything crossing the bridge lives here: the injected `__BOTARENA_LOAD__` /
 * `__BOTARENA_CONTROL__` calls going out, and the `postMessage` stream coming back. The
 * viewer above it deals in `header`, `tick`, `transport` and a `control()` function, and
 * never sees a string of JavaScript.
 *
 * This is the hand-mirrored contract described in `web/CLAUDE.md` — changing
 * `HostedViewer`'s protocol means changing `protocol.ts` and this file in the same commit.
 */
export interface ArenaBridge {
  webViewRef: React.RefObject<WebView | null>;
  onMessage: (event: WebViewMessageEvent) => void;
  /** Call a method on the page's control surface: play/pause/seek/… */
  control: (method: string, ...args: unknown[]) => void;
  /** Point the page at a replay, optionally anchored to a broadcast's clock. */
  load: (matchId: string, anchor?: BroadcastAnchor) => void;
  /** Queue a replay for when the page announces itself, or load it now if it already has. */
  loadWhenReady: (matchId: string) => void;
  /** Forget the current fight's readouts. */
  reset: () => void;
  selectSlot: (slot: number) => void;
  header: ArenaHeader | null;
  result: ArenaResult;
  tick: ArenaTick | null;
  transport: ArenaTransport | null;
  selectedSlot: number | null;
  failed: boolean;
}

export interface BroadcastAnchor {
  tick: number;
  ticksPerSecond: number;
}

export function useArenaBridge(): ArenaBridge {
  const webViewRef = useRef<WebView>(null);
  const [header, setHeader] = useState<ArenaHeader | null>(null);
  const [result, setResult] = useState<ArenaResult>(null);
  const [tick, setTick] = useState<ArenaTick | null>(null);
  const [transport, setTransport] = useState<ArenaTransport | null>(null);
  const [selectedSlot, setSelectedSlot] = useState<number | null>(null);
  const [failed, setFailed] = useState(false);

  // The page announces readiness once. A load before that has nowhere to land, so it
  // waits here and is replayed when the announcement arrives.
  const ready = useRef(false);
  const pending = useRef<string | null>(null);

  const call = useCallback((expression: string) => {
    webViewRef.current?.injectJavaScript(`${expression}; true;`);
  }, []);

  const control = useCallback(
    (method: string, ...args: unknown[]) => {
      const serialized = args.map((arg) => JSON.stringify(arg)).join(', ');
      call(
        `window.__BOTARENA_CONTROL__ && window.__BOTARENA_CONTROL__.${method}(${serialized})`,
      );
    },
    [call],
  );

  const load = useCallback(
    (matchId: string, anchor?: BroadcastAnchor) => {
      const source = {
        url: `${API_BASE_URL}/api/matches/${matchId}/replay`,
        ...(anchor ? { live: anchor } : {}),
      };
      call(`window.__BOTARENA_LOAD__ && window.__BOTARENA_LOAD__(${JSON.stringify(source)})`);
    },
    [call],
  );

  const loadWhenReady = useCallback(
    (matchId: string) => {
      if (ready.current) load(matchId);
      else pending.current = matchId;
    },
    [load],
  );

  const reset = useCallback(() => {
    // Cleared rather than kept: leaving the previous match's readouts up while the next
    // one loads shows numbers that belong to a different fight.
    setHeader(null);
    setResult(null);
    setTick(null);
    setTransport(null);
    setSelectedSlot(null);
    setFailed(false);
  }, []);

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
          setTick({ tick: message.tick, control: message.control, bots: message.bots });
          break;
        case 'transport':
          setTransport({
            playing: message.playing,
            speed: message.speed,
            tick: message.tick,
            tickCount: message.tickCount,
            atEnd: message.atEnd,
            following: message.following,
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

  // Deliberately not a functional update: the updater would have to send the message to
  // the page, and React may invoke it more than once. Selection is driven by taps, so
  // reading the rendered value is correct here.
  const selectSlot = useCallback(
    (slot: number) => {
      const next = selectedSlot === slot ? null : slot;
      setSelectedSlot(next);
      control('selectSlot', next);
    },
    [control, selectedSlot],
  );

  return {
    webViewRef,
    onMessage,
    control,
    load,
    loadWhenReady,
    reset,
    selectSlot,
    header,
    result,
    tick,
    transport,
    selectedSlot,
    failed,
  };
}
