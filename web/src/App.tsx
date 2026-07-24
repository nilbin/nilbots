import { useEffect, useState } from 'react';
import type { ReplayDocument } from './types';
import Viewer from './components/Viewer';

/// Standalone viewer mode: replay embedded by the CLI or served as replay.json.
export default function App() {
  const [replay, setReplay] = useState<ReplayDocument | null>(
    window.__BOTARENA_REPLAY__ ?? null,
  );
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (replay) return;
    fetch('replay.json')
      .then((response) => (response.ok ? response.json() : Promise.reject(response.status)))
      .then((data: ReplayDocument) => setReplay(data))
      .catch(() =>
        setLoadError(
          'No replay embedded and no replay.json found. Generate one with: nilbots play',
        ),
      );
  }, [replay]);

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
