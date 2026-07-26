import { useCallback, useEffect, useRef, useState } from 'react';
import type { ReplayDocument } from './types';
import HostedViewer from './components/HostedViewer';
import type { LiveFollow } from './playback';
import Viewer from './components/Viewer';
import ReplayPicker, { type ReplayChoice } from './components/ReplayPicker';

/// Standalone viewer mode: replay embedded by the CLI, served as replay.json, or pushed
/// in by an embedding host (the mobile app) through window.__BOTARENA_LOAD__.
export default function App() {
  const [replay, setReplay] = useState<ReplayDocument | null>(
    window.__BOTARENA_REPLAY__ ?? null,
  );
  const [loadError, setLoadError] = useState<string | null>(null);
  // An embedding host decides which replay is shown, so this page must not also go
  // looking for replay.json. Not a race to be won by ordering: served from the site that
  // request 404s, and its rejection would land *after* the host's replay and replace it
  // with an error. Detected up front rather than on first __BOTARENA_LOAD__ call, so the
  // fallback never starts.
  const hosted = typeof window !== 'undefined' && Boolean(window.ReactNativeWebView);
  // Present only while the host is following a broadcast; cleared when it stops, which
  // is what hands control back to the local transport once the broadcast completes.
  const [live, setLive] = useState<LiveFollow | undefined>(undefined);
  // The URL currently shown, so a re-load of the *same* replay refreshes it in place. A
  // broadcast grows tick by tick and the host re-requests it as it does; blanking the
  // canvas on each of those would strobe the arena several times a second.
  const loaded = useRef<string | null>(null);
  // A review build can carry several replays so treatments can be compared without
  // reloading. Absent everywhere else, which leaves the CLI viewer and the site alone.
  const [choices, setChoices] = useState<readonly ReplayChoice[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);

  const loadChoice = useCallback((choice: ReplayChoice) => {
    setActiveId(choice.id);
    setLoadError(null);
    setReplay(null);
    return fetch(choice.url)
      .then((response) => (response.ok ? response.json() : Promise.reject(response.status)))
      .then((data: ReplayDocument) => setReplay(data))
      .catch(() => setLoadError('Could not load that replay.'));
  }, []);

  const notifyHost = useCallback((message: Record<string, unknown>) => {
    window.ReactNativeWebView?.postMessage(JSON.stringify(message));
  }, []);

  useEffect(() => {
    window.__BOTARENA_LOAD__ = (source) => {
      setLoadError(null);
      setLive(source.live ?? undefined);
      if ('replay' in source) {
        setReplay(source.replay);
        notifyHost({ type: 'loaded' });
        return;
      }
      // Fetched here rather than handed over as JSON: the host and this page are the
      // same origin, and pushing a whole replay document across the native bridge as a
      // string is both slower and bounded in size.
      if (loaded.current !== source.url) {
        setReplay(null);
        loaded.current = source.url;
      }
      fetch(source.url)
        .then((response) => (response.ok ? response.json() : Promise.reject(response.status)))
        .then((data: ReplayDocument) => {
          setReplay(data);
          notifyHost({ type: 'loaded' });
        })
        .catch((reason) => {
          setLoadError('Could not load that replay.');
          notifyHost({ type: 'error', reason: String(reason) });
        });
    };

    // A host cannot call __BOTARENA_LOAD__ before this runs, so announce that it can.
    notifyHost({ type: 'ready' });
    return () => {
      delete window.__BOTARENA_LOAD__;
    };
  }, [notifyHost]);

  // Offer the review build's replay set, when there is one.
  useEffect(() => {
    if (hosted) return;
    let cancelled = false;
    fetch('replays.json')
      .then((response) => (response.ok ? response.json() : Promise.reject(response.status)))
      .then((entries: ReplayChoice[]) => {
        if (cancelled || entries.length === 0) return;
        setChoices(entries);
        void loadChoice(entries[0]);
      })
      .catch(() => {
        // No index: a single-replay viewer, which is the normal case.
      });
    return () => {
      cancelled = true;
    };
  }, [hosted]);

  useEffect(() => {
    if (replay || hosted || choices.length > 0) return;
    fetch('replay.json')
      .then((response) => (response.ok ? response.json() : Promise.reject(response.status)))
      .then((data: ReplayDocument) => setReplay(data))
      .catch(() =>
        setLoadError(
          'No replay embedded and no replay.json found. Generate one with: nilbots play',
        ),
      );
  }, [replay, hosted, choices.length]);

  if (loadError) {
    return (
      <div className="flex h-screen items-center justify-center">
        <p className="max-w-md font-mono text-sm text-arena-dim">{loadError}</p>
      </div>
    );
  }
  if (!replay) {
    return (
      <div className="flex h-screen items-center justify-center">
        <p className="font-mono text-sm text-arena-dim">Loading replay…</p>
      </div>
    );
  }
  // A host supplies its own header, transport and readouts, so it gets the canvas alone;
  // rendering the full viewer inside it would duplicate all three.
  if (hosted) return <HostedViewer replay={replay} live={live} />;
  if (choices.length < 2) return <Viewer replay={replay} />;
  return (
    <div className="flex h-full flex-col gap-2 p-3 pb-0 md:p-5 md:pb-0">
      <ReplayPicker choices={choices} activeId={activeId} onSelect={loadChoice} />
      <div className="min-h-0 flex-1">
        <Viewer replay={replay} />
      </div>
    </div>
  );
}
