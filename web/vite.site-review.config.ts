import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig, type Connect, type Plugin } from 'vite';
import {
  REVIEW_LIVE_MATCH_ID,
  reviewSetGameSpecs,
  type ReviewSetGameSpec,
} from './src/site/review/fixtures';
import {
  siteReviewApiResponse,
  siteReviewRequestKey,
} from './src/site/review/routes';

const SITE_REVIEW_HEADER = 'x-nilbots-site-review';
const sourceReplay = readFileSync(
  new URL('./tests/fixtures/golden-replay.json', import.meta.url),
  'utf8',
).trim();
const completedReviewReplays = new Map<string, string>(
  reviewSetGameSpecs.map((spec) => [
    `/api/matches/${spec.id}/replay`,
    makeCompletedReviewReplay(sourceReplay, spec),
  ]),
);
const liveReviewReplay = makeLiveReviewReplay(sourceReplay);

/**
 * The review API exists only in this config. Neither the normal Vite config nor the
 * browser entry point imports it, so a production build cannot silently acquire fixtures.
 */
function siteReviewApi(): Plugin {
  const middleware: Connect.NextHandleFunction = (request, response, next) => {
    const requestUrl = request.url ?? '/';
    const url = new URL(requestUrl, 'http://nilbots.site-review');

    if (url.pathname === '/__site-review/health') {
      if (
        request.method?.toUpperCase() === 'GET' &&
        url.search === ''
      ) {
        sendJson(response, 200, { ready: true });
        return;
      }
      response.setHeader(
        'x-nilbots-site-review-error',
        'invalid-health-request',
      );
      sendJson(response, 500, {
        ready: false,
        detail: 'Health accepts exactly GET /__site-review/health.',
      });
      return;
    }
    if (!url.pathname.startsWith('/api/')) {
      next();
      return;
    }

    if (
      request.method?.toUpperCase() === 'GET' &&
      url.pathname === '/api/accounts/external/google' &&
      url.search === ''
    ) {
      response.statusCode = 302;
      response.setHeader('location', '/login?review=google');
      response.setHeader('cache-control', 'no-store');
      response.setHeader(SITE_REVIEW_HEADER, '1');
      response.end();
      return;
    }

    if (request.method?.toUpperCase() !== 'GET') {
      response.setHeader('x-nilbots-site-review-expected-status', '409');
      response.setHeader('x-nilbots-site-review-error', 'read-only-api');
      sendJson(response, 409, {
        type: 'about:blank',
        title: 'Site review is read-only',
        status: 409,
        detail:
          'This public design-review build does not submit or change account data.',
      });
      return;
    }

    const replay = reviewReplay(request.method, url);
    if (replay !== null) {
      sendRawJson(response, 200, replay);
      return;
    }

    const key = siteReviewRequestKey(request.method, requestUrl);
    const fixture = siteReviewApiResponse(request.method, requestUrl, {
      referer: request.headers.referer,
    });
    if (fixture === undefined) {
      const detail = `Unmatched site-review API request: ${key}`;
      console.error(detail);
      response.setHeader('x-nilbots-site-review-error', 'unmatched-api-request');
      sendJson(response, 500, {
        type: 'about:blank',
        title: 'Unmatched site-review API request',
        status: 500,
        detail,
      });
      return;
    }

    if (fixture.status >= 400) {
      response.setHeader(
        'x-nilbots-site-review-expected-status',
        String(fixture.status),
      );
    }
    sendJson(response, fixture.status, fixture.body);
  };

  return {
    name: 'nilbots-site-review-api',
    configureServer(server) {
      server.middlewares.use(middleware);
    },
    configurePreviewServer(server) {
      server.middlewares.use(middleware);
    },
  };
}

function sendJson(
  response: Parameters<Connect.NextHandleFunction>[1],
  status: number,
  body: unknown,
) {
  response.statusCode = status;
  response.setHeader('content-type', 'application/json; charset=utf-8');
  response.setHeader('cache-control', 'no-store');
  response.setHeader(SITE_REVIEW_HEADER, '1');
  response.end(JSON.stringify(body));
}

function sendRawJson(
  response: Parameters<Connect.NextHandleFunction>[1],
  status: number,
  body: string,
) {
  response.statusCode = status;
  response.setHeader('content-type', 'application/json; charset=utf-8');
  response.setHeader('cache-control', 'no-store');
  response.setHeader(SITE_REVIEW_HEADER, '1');
  response.end(body);
}

function reviewReplay(method: string | undefined, url: URL): string | null {
  if (method?.toUpperCase() !== 'GET' || url.search !== '') return null;
  const completedReplay = completedReviewReplays.get(url.pathname);
  if (completedReplay !== undefined) return completedReplay;
  if (url.pathname === `/api/matches/${REVIEW_LIVE_MATCH_ID}/replay`) {
    return liveReviewReplay;
  }
  return null;
}

/**
 * Keep the checked-in engine fixture byte-for-byte canonical except for presentation
 * identity. Re-hashing the canonical payload means the completed review replay still
 * demonstrates the product's real verification contract.
 */
function makeCompletedReviewReplay(
  source: string,
  spec: ReviewSetGameSpec,
): string {
  const withoutHash = source.replace(
    /,"replayHash":"[0-9a-f]{64}"}$/,
    '}',
  );
  if (withoutHash === source) {
    throw new Error('The review replay fixture has no terminal replay hash.');
  }

  const pincer =
    '"name":"Pincer gen-10","runtimeKind":"wasm","artifactHash":"9f31c0a4b7de51aa","accent":"#22d3ee"';
  const bastille =
    '"name":"Bastille gen-5","runtimeKind":"wasm","artifactHash":"11b2b6bf82cf61e9","accent":"#ef4444"';
  let payload = replaceOnce(
    withoutHash,
    '"name":"hunter","runtimeKind":"wasm","artifactHash":"66d522119ded08eb14784f5939a3b9b278eeba927f54fa47090c970f3f14669e","accent":"#f97316"',
    spec.pincerSlot === 0 ? pincer : bastille,
  );
  payload = replaceOnce(
    payload,
    '"name":"Rampart gen-2","runtimeKind":"wasm","artifactHash":"8786634a943703ba456a2db7521afad86b8899c72f8c65ab3fdc3fa299b542a3","accent":"#a78bfa"',
    spec.pincerSlot === 0 ? bastille : pincer,
  );
  if (spec.pincerSlot === 0) {
    payload = replaceOnce(
      payload,
      '"lookId":"orbiter"',
      '"lookId":"bulwark"',
    );
  } else {
    payload = replaceOnce(
      payload,
      '"lookId":"vanguard"',
      '"lookId":"bulwark"',
    );
    payload = replaceOnce(
      payload,
      '"lookId":"orbiter"',
      '"lookId":"vanguard"',
    );
  }
  if (spec.mapId !== 'arena-01') {
    payload = replaceOnce(
      payload,
      '"mapId":"arena-01"',
      `"mapId":"${spec.mapId}"`,
    );
  }
  if (spec.themeId !== 'ember-forge') {
    payload = replaceOnce(
      payload,
      '"themeId":"ember-forge"',
      `"themeId":"${spec.themeId}"`,
    );
  }

  const hash = createHash('sha256').update(payload).digest('hex');
  if (hash !== spec.replayHash) {
    throw new Error(
      `Review replay hash drifted for game ${spec.game}: expected ${spec.replayHash}, received ${hash}.`,
    );
  }
  return `${payload.slice(0, -1)},"replayHash":"${hash}"}`;
}

function makeLiveReviewReplay(source: string): string {
  const document = JSON.parse(source) as {
    header: {
      participants: Array<{
        name: string;
        artifactHash: string;
        accent: string;
        lookId?: string;
      }>;
    };
    ticks: unknown[];
    result?: unknown;
    replayHash?: unknown;
    partial?: boolean;
  };
  const [warden, rampart] = document.header.participants;
  if (!warden || !rampart) {
    throw new Error('The review replay needs two participants.');
  }
  Object.assign(warden, {
    name: 'Warden gen-1',
    artifactHash: '4f9229f8eb7b7725',
    accent: '#7dd3fc',
    lookId: 'aureate-warden',
  });
  Object.assign(rampart, {
    name: 'Rampart gen-2',
    artifactHash: '77dba5d2fe1939ac',
    accent: '#bef264',
    lookId: 'orbiter',
  });
  document.ticks = document.ticks.slice(0, 25);
  delete document.result;
  delete document.replayHash;
  document.partial = true;
  return JSON.stringify(document);
}

function replaceOnce(source: string, from: string, to: string): string {
  const first = source.indexOf(from);
  if (first < 0 || source.indexOf(from, first + from.length) >= 0) {
    throw new Error(`Expected exactly one review replay fragment: ${from.slice(0, 48)}…`);
  }
  return `${source.slice(0, first)}${to}${source.slice(first + from.length)}`;
}

export default defineConfig({
  plugins: [siteReviewApi(), react(), tailwindcss()],
  define: {
    'import.meta.env.VITE_SITE_REVIEW': JSON.stringify('1'),
  },
  build: {
    outDir: 'dist-site-review',
  },
  server: {
    host: true,
    port: 4181,
    strictPort: true,
  },
  preview: {
    host: true,
    port: 4181,
    strictPort: true,
    allowedHosts: ['.trycloudflare.com', '.ngrok-free.app', '.ngrok.io', '.ts.net'],
  },
});
