import assert from 'node:assert/strict';
import test from 'node:test';
import { ApiError, api } from '../src/site/api';

test('API errors retain stable problem codes and retry metadata', async () => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () =>
    new Response(
      JSON.stringify({
        type: 'about:blank',
        title: 'Too Many Requests',
        status: 429,
        detail: 'The rolling allowance is used.',
        code: 'matches.ranked_daily_limit',
        traceId: 'trace-review',
        retryAfterSeconds: 90,
      }),
      {
        status: 429,
        headers: { 'Content-Type': 'application/problem+json' },
      },
    );

  try {
    await assert.rejects(
      api.get('/api/test-problem'),
      (error: unknown) => {
        assert.ok(error instanceof ApiError);
        assert.equal(error.status, 429);
        assert.equal(error.message, 'The rolling allowance is used.');
        assert.equal(error.code, 'matches.ranked_daily_limit');
        assert.equal(error.traceId, 'trace-review');
        assert.equal(error.retryAfterSeconds, 90);
        return true;
      },
    );
  } finally {
    globalThis.fetch = originalFetch;
  }
});
