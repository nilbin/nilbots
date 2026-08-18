import { useCallback, useEffect, useRef, useState } from 'react';
import type { TacticsDraft } from './tacticsDocument';

/**
 * One draft in memory, an 80-deep undo ring, and a debounced IndexedDB
 * autosave so a reload (or a tab crash mid-drag) never loses drawn geometry.
 * The stored copy also carries the server revision it grew from, which is how
 * opening a sheet can prefer this device's newer unsent work.
 */

const DB_NAME = 'nilbots-tactics-drafts-v1';
const STORE = 'drafts';
const UNDO_DEPTH = 80;

interface StoredDraft {
  draft: TacticsDraft;
  savedAt: number;
}

function withStore<T>(
  mode: IDBTransactionMode,
  run: (store: IDBObjectStore) => IDBRequest<T>,
): Promise<T | null> {
  return new Promise((resolve) => {
    if (typeof indexedDB === 'undefined') return resolve(null);
    const open = indexedDB.open(DB_NAME, 1);
    open.onupgradeneeded = () => open.result.createObjectStore(STORE);
    open.onerror = () => resolve(null);
    open.onsuccess = () => {
      const db = open.result;
      try {
        const request = run(db.transaction(STORE, mode).objectStore(STORE));
        request.onsuccess = () => { resolve(request.result); db.close(); };
        request.onerror = () => { resolve(null); db.close(); };
      } catch {
        resolve(null);
        db.close();
      }
    };
  });
}

export function saveLocalDraft(draft: TacticsDraft): Promise<unknown> {
  const stored: StoredDraft = { draft, savedAt: Date.now() };
  return withStore('readwrite', (store) => store.put(stored, `draft:${draft.localId}`));
}

export async function loadLocalDraft(localId: string): Promise<StoredDraft | null> {
  const value = await withStore<StoredDraft | undefined>(
    'readonly', (store) => store.get(`draft:${localId}`) as IDBRequest<StoredDraft | undefined>);
  return value ?? null;
}

export function deleteLocalDraft(localId: string): Promise<unknown> {
  return withStore('readwrite', (store) => store.delete(`draft:${localId}`));
}

export function useSheetDraft(initial: TacticsDraft | null) {
  const [draft, setDraft] = useState<TacticsDraft | null>(initial);
  const undoStack = useRef<TacticsDraft[]>([]);
  const redoStack = useRef<TacticsDraft[]>([]);
  const draftRef = useRef(draft);
  draftRef.current = draft;
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const persistSoon = useCallback((value: TacticsDraft) => {
    if (saveTimer.current) clearTimeout(saveTimer.current);
    saveTimer.current = setTimeout(() => void saveLocalDraft(value), 180);
  }, []);

  useEffect(() => () => {
    if (saveTimer.current) clearTimeout(saveTimer.current);
    if (draftRef.current) void saveLocalDraft(draftRef.current);
  }, []);

  /** Apply a change through a mutable working copy; the previous draft goes
   * on the undo ring and redo clears. */
  const mutate = useCallback((change: (draft: TacticsDraft) => void) => {
    setDraft((current) => {
      if (!current) return current;
      const next = structuredClone(current);
      change(next);
      undoStack.current.push(current);
      if (undoStack.current.length > UNDO_DEPTH) undoStack.current.shift();
      redoStack.current = [];
      persistSoon(next);
      return next;
    });
  }, [persistSoon]);

  /** Like mutate, but coalesces with the previous entry — for drags, where
   * sixty frames of movement should undo as one gesture. */
  const mutateLive = useCallback((change: (draft: TacticsDraft) => void) => {
    setDraft((current) => {
      if (!current) return current;
      const next = structuredClone(current);
      change(next);
      persistSoon(next);
      return next;
    });
  }, [persistSoon]);

  const beginGesture = useCallback(() => {
    const current = draftRef.current;
    if (!current) return;
    undoStack.current.push(current);
    if (undoStack.current.length > UNDO_DEPTH) undoStack.current.shift();
    redoStack.current = [];
  }, []);

  const undo = useCallback(() => {
    setDraft((current) => {
      const previous = undoStack.current.pop();
      if (!previous || !current) return current;
      redoStack.current.push(current);
      persistSoon(previous);
      return previous;
    });
  }, [persistSoon]);

  const redo = useCallback(() => {
    setDraft((current) => {
      const next = redoStack.current.pop();
      if (!next || !current) return current;
      undoStack.current.push(current);
      persistSoon(next);
      return next;
    });
  }, [persistSoon]);

  const replaceDraft = useCallback((next: TacticsDraft) => {
    undoStack.current = [];
    redoStack.current = [];
    setDraft(next);
    persistSoon(next);
  }, [persistSoon]);

  return { draft, mutate, mutateLive, beginGesture, undo, redo, replaceDraft };
}
