import { useCallback, useEffect, useState } from 'react';
import type { ReplayDocument } from './types';
import Viewer from './components/Viewer';

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

  const notifyHost = useCallback((message: Record<string, unknown>) => {
    window.ReactNativeWebView?.postMessage(JSON.stringify(message));
  }, []);

  useEffect(() => {
    window.__BOTARENA_LOAD__ = (source) => {
      setLoadError(null);
      if ('replay' in source) {
        setReplay(source.replay);
        notifyHost({ type: 'loaded' });
        return;
      }
      // Fetched here rather than handed over as JSON: the host and this page are the
      // same origin, and pushing a whole replay document across the native bridge as a
      // string is both slower and bounded in size.
      setReplay(null);
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

  useEffect(() => {
    if (replay || hosted) return;
    fetch('replay.json')
      .then((response) => (response.ok ? response.json() : Promise.reject(response.status)))
      .then((data: ReplayDocument) => setReplay(data))
      .catch(() =>
        setLoadError(
          'No replay embedded and no replay.json found. Generate one with: nilbots play',
        ),
      );
  }, [replay, hosted]);

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
  return <Viewer replay={replay} />;
}
