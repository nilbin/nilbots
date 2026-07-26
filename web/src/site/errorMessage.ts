/**
 * What to show a person when a write fails.
 *
 * Five call sites each wrote their own `e instanceof Error ? e.message : 'Something went
 * wrong.'`, with five different fallbacks — so the same failure read as "Challenge failed."
 * in one place and "Something went wrong." in another. The server's message is the useful
 * one whenever there is one; the fallback exists for the cases that carry no message at
 * all, which is why callers pass their own.
 */
export function errorMessage(cause: unknown, fallback: string): string {
  if (cause instanceof Error && cause.message.trim() !== '') return cause.message;
  return fallback;
}
