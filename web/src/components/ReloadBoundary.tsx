import { Component, type ReactNode } from 'react';

/**
 * The last line against a blank page: any uncaught render error — a stale
 * hashed chunk after a redeploy under an open tab, a bad frame — shows a
 * plain reload prompt instead of unmounting the whole app into white.
 * Deliberately dependency-free (no router, no site chrome) so both the
 * site and the standalone viewer can sit inside it.
 */
export default class ReloadBoundary extends Component<
  { children: ReactNode },
  { failed: boolean }
> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  render() {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="flex min-h-dvh flex-col items-center justify-center gap-3 bg-arena-bg p-6 text-center">
        <p className="t-body text-arena-text">Something broke while drawing this page.</p>
        <p className="t-micro text-arena-dim">
          Usually a fresh build replaced the one this tab had loaded.
        </p>
        <button
          type="button"
          className="btn"
          onClick={() => window.location.reload()}
        >
          Reload
        </button>
      </div>
    );
  }
}
