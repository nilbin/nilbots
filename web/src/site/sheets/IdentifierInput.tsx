import { useEffect, useId, useState } from 'react';
import { identifierFromName } from './sheetAuthoring';

export default function IdentifierInput({
  value,
  fallback = value,
  maxLength = 48,
  isTaken,
  onCommit,
}: {
  value: string;
  fallback?: string;
  maxLength?: number;
  isTaken?: (candidate: string) => boolean;
  onCommit: (next: string) => void;
}) {
  const [draft, setDraft] = useState(value);
  const [error, setError] = useState<string | null>(null);
  const messageId = useId();

  useEffect(() => {
    setDraft(value);
    setError(null);
  }, [value]);

  const commit = () => {
    const next = identifierFromName(draft, fallback, maxLength);
    if (next !== value && isTaken?.(next)) {
      setError('That name is already in use.');
      return;
    }
    setDraft(next);
    setError(null);
    if (next !== value) onCommit(next);
  };

  const message = error ?? (draft !== value
    ? 'Spaces and punctuation become dashes when you finish editing.'
    : null);

  return <>
    <input className="field min-h-11" value={draft} maxLength={maxLength}
      aria-invalid={error ? true : undefined} aria-describedby={message ? messageId : undefined}
      onChange={(event) => { setDraft(event.target.value); setError(null); }}
      onBlur={commit}
      onKeyDown={(event) => {
        if (event.key === 'Enter') {
          event.preventDefault();
          event.currentTarget.blur();
        }
        if (event.key === 'Escape') {
          event.preventDefault();
          setDraft(value);
          setError(null);
        }
      }} />
    {message && <span id={messageId} className={error ? 't-micro mt-1 block text-arena-hot' : 't-micro mt-1 block'}>{message}</span>}
  </>;
}
