import { useCallback, useEffect, useRef, useState } from 'react';
import HostedViewer from './components/HostedViewer';
import type { LiveFollow } from './playback';
import Viewer from './components/Viewer';
import ReplayPicker, {
  type ReplayChoice,
} from './components/ReplayPicker';
import {
  loadReplayJson,
  loadReplayObject,
  type LoadedReplay,
} from './replayIngress';
import {
  bridgeSupportsReplay,
  errorBridgeMessage,
  hostedBridgeVersion,
  loadedBridgeMessage,
  readyBridgeMessage,
} from './hostedBridge';

interface InitialReplay {
  loaded: LoadedReplay | null;
  error: string | null;
  unsupported: boolean;
}

export default function App() {
  const hosted = Boolean(window.ReactNativeWebView);
  const bridgeVersion = hostedBridgeVersion(window.location.search);
  const embeddedPresent = window.__BOTARENA_REPLAY__ !== undefined;
  const [initial] = useState<InitialReplay>(() =>
    decodeEmbeddedReplay(hosted, bridgeVersion),
  );
  const [loadedReplay, setLoadedReplay] = useState<LoadedReplay | null>(
    initial.loaded,
  );
  const [loadError, setLoadError] = useState<string | null>(initial.error);
  const [live, setLive] = useState<LiveFollow | undefined>(undefined);
  const loadedUrl = useRef<string | null>(null);
  const [choices, setChoices] = useState<readonly ReplayChoice[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);

  const notifyHost = useCallback((message: Record<string, unknown>) => {
    window.ReactNativeWebView?.postMessage(JSON.stringify(message));
  }, []);

  const acceptReplay = useCallback(
    (candidate: LoadedReplay, notifyLoaded: boolean) => {
      if (
        hosted &&
        !bridgeSupportsReplay(bridgeVersion, candidate.replay)
      ) {
        // Unmount the previous HostedViewer before reporting the rejection:
        // bridge 1 must never continue sending stale replay/tick messages after
        // it is handed a replay-v2 document.
        setLoadedReplay(null);
        setLive(undefined);
        notifyHost(
          errorBridgeMessage(
            bridgeVersion,
            'unsupported-replay-version',
          ),
        );
        return;
      }
      setLoadedReplay(candidate);
      if (notifyLoaded) {
        notifyHost(loadedBridgeMessage(bridgeVersion));
      }
    },
    [bridgeVersion, hosted, notifyHost],
  );

  const fetchReplay = useCallback(
    async (url: string): Promise<LoadedReplay> => {
      const response = await fetch(url);
      if (!response.ok) throw new Error(String(response.status));
      return loadReplayJson(await response.text());
    },
    [],
  );

  const loadChoice = useCallback(
    (choice: ReplayChoice) => {
      setActiveId(choice.id);
      setLoadError(null);
      setLoadedReplay(null);
      return fetchReplay(choice.url)
        .then((candidate) => acceptReplay(candidate, false))
        .catch(() => setLoadError('Could not load that replay.'));
    },
    [acceptReplay, fetchReplay],
  );

  useEffect(() => {
    window.__BOTARENA_LOAD__ = (source) => {
      setLoadError(null);
      setLive(source.live ?? undefined);
      if ('json' in source) {
        try {
          acceptReplay(loadReplayJson(source.json), true);
        } catch (reason) {
          setLoadedReplay(null);
          notifyHost(
            errorBridgeMessage(bridgeVersion, String(reason)),
          );
        }
        return;
      }
      if ('replay' in source) {
        try {
          acceptReplay(loadReplayObject(source.replay), true);
        } catch (reason) {
          setLoadedReplay(null);
          notifyHost(
            errorBridgeMessage(bridgeVersion, String(reason)),
          );
        }
        return;
      }

      if (loadedUrl.current !== source.url) {
        setLoadedReplay(null);
        loadedUrl.current = source.url;
      }
      void fetchReplay(source.url)
        .then((candidate) => acceptReplay(candidate, true))
        .catch((reason) => {
          setLoadError('Could not load that replay.');
          notifyHost(
            errorBridgeMessage(bridgeVersion, String(reason)),
          );
        });
    };

    notifyHost(readyBridgeMessage(bridgeVersion));
    if (initial.unsupported) {
      notifyHost(
        errorBridgeMessage(
          bridgeVersion,
          'unsupported-replay-version',
        ),
      );
    }
    return () => {
      delete window.__BOTARENA_LOAD__;
    };
  }, [
    acceptReplay,
    bridgeVersion,
    fetchReplay,
    initial.unsupported,
    notifyHost,
  ]);

  useEffect(() => {
    if (hosted) return;
    let cancelled = false;
    void fetch('replays.json')
      .then((response) =>
        response.ok
          ? response.json()
          : Promise.reject(response.status),
      )
      .then((entries: ReplayChoice[]) => {
        if (cancelled || entries.length === 0) return;
        setChoices(entries);
        void loadChoice(entries[0]);
      })
      .catch(() => {
        // No review index: the normal single-replay viewer.
      });
    return () => {
      cancelled = true;
    };
  }, [hosted, loadChoice]);

  useEffect(() => {
    if (
      loadedReplay ||
      hosted ||
      embeddedPresent ||
      choices.length > 0
    )
      return;
    void fetchReplay('replay.json')
      .then((candidate) => acceptReplay(candidate, false))
      .catch(() =>
        setLoadError(
          'No replay embedded and no replay.json found. Generate one with: nilbots play',
        ),
      );
  }, [
    acceptReplay,
    choices.length,
    embeddedPresent,
    fetchReplay,
    hosted,
    loadedReplay,
  ]);

  if (loadError) {
    return (
      <main className="flex min-h-[100dvh] items-center justify-center">
        <h1 className="sr-only">Replay could not be loaded</h1>
        <p
          role="alert"
          className="max-w-md font-mono text-sm text-arena-dim"
        >
          {loadError}
        </p>
      </main>
    );
  }
  if (!loadedReplay) {
    return (
      <main className="flex min-h-[100dvh] items-center justify-center">
        <h1 className="sr-only">Loading nilbots replay</h1>
        <p role="status" className="font-mono text-sm text-arena-dim">
          Loading replay…
        </p>
      </main>
    );
  }

  const replay = loadedReplay.replay;
  if (hosted) {
    return (
      <HostedViewer
        replay={replay}
        live={live}
        bridgeVersion={bridgeVersion}
      />
    );
  }
  if (choices.length < 2)
    return (
      <main className="flex min-h-[100dvh] flex-col lg:h-[100dvh]">
        <h1 className="sr-only">nilbots replay viewer</h1>
        <div className="min-h-0 flex-1">
          <Viewer replay={replay} />
        </div>
      </main>
    );
  return (
    <main className="flex min-h-[100dvh] flex-col gap-2 p-3 pb-0 md:p-5 md:pb-0 lg:h-[100dvh]">
      <h1 className="sr-only">nilbots replay viewer</h1>
      <ReplayPicker
        choices={choices}
        activeId={activeId}
        onSelect={loadChoice}
      />
      <div className="min-h-0 flex-1">
        <Viewer replay={replay} />
      </div>
    </main>
  );
}

function decodeEmbeddedReplay(
  hosted: boolean,
  bridgeVersion: ReturnType<typeof hostedBridgeVersion>,
): InitialReplay {
  const embedded = window.__BOTARENA_REPLAY__;
  if (embedded === undefined) {
    return { loaded: null, error: null, unsupported: false };
  }
  try {
    const loaded =
      typeof embedded === 'string'
        ? loadReplayJson(embedded)
        : loadReplayObject(embedded);
    if (hosted && !bridgeSupportsReplay(bridgeVersion, loaded.replay)) {
      return { loaded: null, error: null, unsupported: true };
    }
    return { loaded, error: null, unsupported: false };
  } catch {
    return {
      loaded: null,
      error: 'The embedded replay is not valid.',
      unsupported: false,
    };
  }
}
