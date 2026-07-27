import { useCallback, useRef, useState } from 'react';
import type { WebView, WebViewMessageEvent } from 'react-native-webview';

import type {
  ArenaControlMethod,
  ArenaHeader,
  ArenaMessage,
  ArenaResult,
  ArenaTick,
  ArenaTransport,
  ArenaUnitKey,
} from '@/components/arena/protocol';
import { ARENA_BRIDGE_VERSION } from '@/components/arena/protocol';
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
  control: (method: ArenaControlMethod, ...args: unknown[]) => void;
  /** Point the page at a replay, optionally anchored to a broadcast's clock. */
  load: (matchId: string, anchor?: BroadcastAnchor) => void;
  /** Queue a replay for when the page announces itself, or load it now if it already has. */
  loadWhenReady: (matchId: string) => void;
  /** Forget the current fight's readouts. */
  reset: () => void;
  selectUnit: (unitKey: ArenaUnitKey) => void;
  header: ArenaHeader | null;
  result: ArenaResult;
  tick: ArenaTick | null;
  transport: ArenaTransport | null;
  selectedUnitKey: ArenaUnitKey | null;
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
  const [selectedUnitKey, setSelectedUnitKey] =
    useState<ArenaUnitKey | null>(null);
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
    setSelectedUnitKey(null);
    setFailed(false);
  }, []);

  const onMessage = useCallback(
    (event: WebViewMessageEvent) => {
      let decoded: unknown;
      try {
        decoded = JSON.parse(event.nativeEvent.data);
      } catch {
        return;
      }
      if (!isArenaMessage(decoded)) return;
      const message = decoded;

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
          setTick(message);
          break;
        case 'transport':
          setTransport(message);
          break;
        case 'selected':
          setSelectedUnitKey(message.unitKey);
          break;
        case 'loaded':
          setFailed(false);
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
  const selectUnit = useCallback(
    (unitKey: ArenaUnitKey) => {
      const next = selectedUnitKey === unitKey ? null : unitKey;
      setSelectedUnitKey(next);
      control('selectUnit', next);
    },
    [control, selectedUnitKey],
  );

  return {
    webViewRef,
    onMessage,
    control,
    load,
    loadWhenReady,
    reset,
    selectUnit,
    header,
    result,
    tick,
    transport,
    selectedUnitKey,
    failed,
  };
}

function isArenaMessage(value: unknown): value is ArenaMessage {
  if (typeof value !== 'object' || value === null) return false;
  const candidate = value as Record<string, unknown>;
  if (candidate.bridgeVersion !== ARENA_BRIDGE_VERSION) return false;
  return (
    candidate.type === 'ready' ||
    candidate.type === 'loaded' ||
    candidate.type === 'error' ||
    candidate.type === 'replay' ||
    candidate.type === 'tick' ||
    candidate.type === 'transport' ||
    candidate.type === 'selected'
  );
}
