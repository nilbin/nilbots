import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import {
  REVIEW_BASTILLE_ID,
  REVIEW_FAILED_MATCH_ID,
  REVIEW_PINCER_ID,
  REVIEW_SET_ID,
  arenaCapabilitiesFixture,
  botMatchHistoryFixture,
  botsFixture,
  completedSetMatchDetailFixtures,
  currentLeaderboardEntries,
  cosmeticCatalogFixture,
  labsCatalogFixture,
  matchSetFixture,
  matchesFixture,
  metaFixture,
  myBotsFixture,
  rankedCappedArenaCapabilitiesFixture,
  reviewSetGameSpecs,
  storeFixture,
} from '../src/site/review/fixtures.ts';
import { siteReviewApiResponse } from '../src/site/review/routes.ts';

test('binds an exact review route to its typed fixture', () => {
  const response = siteReviewApiResponse('GET', '/api/meta');
  assert.ok(response);
  assert.deepEqual(response.body, metaFixture);
});

test('serves the hosted Labs catalog used by the owner bot review', () => {
  const response = siteReviewApiResponse('GET', '/api/labs');
  assert.ok(response);
  assert.deepEqual(response.body, labsCatalogFixture);
});

test('keeps Arena authority coherent with review rules, sets and ownership', () => {
  const response = siteReviewApiResponse('GET', '/api/arena');
  assert.ok(response);
  assert.deepEqual(response.body, arenaCapabilitiesFixture);
  assert.equal(
    arenaCapabilitiesFixture.format.rulesVersion,
    metaFixture.gameRulesVersion,
  );
  assert.equal(
    arenaCapabilitiesFixture.format.ranked.gamesPerSet,
    reviewSetGameSpecs.length,
  );
  assert.ok(
    reviewSetGameSpecs.every((game) =>
      arenaCapabilitiesFixture.format.ranked.mapPool.includes(
        game.mapId,
      ),
    ),
  );
  assert.ok(
    arenaCapabilitiesFixture.format.ranked.mapPool.every((mapId) =>
      metaFixture.maps.some((map) => map.id === mapId),
    ),
  );
  assert.ok(
    metaFixture.maps.some(
      (map) =>
        map.id ===
        arenaCapabilitiesFixture.format.unranked.defaultMapId,
    ),
  );
  assert.deepEqual(
    arenaCapabilitiesFixture.bots.map((bot) => bot.botId),
    botsFixture.map((bot) => bot.id),
  );
  assert.deepEqual(
    arenaCapabilitiesFixture.bots
      .filter((bot) => bot.isOwned)
      .map((bot) => bot.botId),
    myBotsFixture.map((bot) => bot.id),
  );

  const firstRun = siteReviewApiResponse('GET', '/api/arena', {
    referer: 'http://127.0.0.1:4181/garage?review=first-run',
  });
  assert.ok(firstRun);
  assert.equal(
    'bots' in firstRun.body
      ? firstRun.body.bots.some((bot) => bot.isOwned)
      : true,
    false,
  );

  const capped = siteReviewApiResponse('GET', '/api/arena', {
    referer:
      'http://127.0.0.1:4181/bots/pincer-gen-10?review=ranked-capped',
  });
  assert.ok(capped);
  assert.deepEqual(
    capped.body,
    rankedCappedArenaCapabilitiesFixture,
  );
});

test('keeps review cosmetics aligned with the production catalogue', () => {
  const production = JSON.parse(
    readFileSync(
      new URL('../../cosmetics/catalog.json', import.meta.url),
      'utf8',
    ),
  ) as {
    version: number;
    items: Array<{
      key: string;
      kind: string;
      id: string;
      label: string;
      availability: string;
      unlock?: { sourceKind: string; sourceId: string; hint: string };
    }>;
    packs: Array<{
      id: string;
      label: string;
      description: string;
      items: string[];
      category: string;
    }>;
  };

  assert.equal(cosmeticCatalogFixture.version, production.version);
  assert.deepEqual(
    cosmeticCatalogFixture.items.map((item) => ({
      key: item.key,
      kind: item.kind,
      id: item.id,
      label: item.label,
      availability: item.availability,
      ...(item.unlock ? { unlock: item.unlock } : {}),
    })),
    production.items,
  );
  assert.deepEqual(
    storeFixture.categories.flatMap((category) =>
      category.packs.map((pack) => ({
        id: pack.id,
        label: pack.label,
        description: pack.description,
        items: pack.items.map((item) => item.key),
        category: category.id,
      })),
    ),
    production.packs,
  );

  const rankedCapacityItem = cosmeticCatalogFixture.items.find(
    (item) => item.key === 'capacity:extra-daily-ranked-sets',
  );
  const rankedCapacityPack = storeFixture.categories
    .flatMap((category) => category.packs)
    .find((pack) => pack.id === 'extra-daily-ranked-sets');
  assert.equal(rankedCapacityItem?.owned, rankedCapacityPack?.owned);
  assert.equal(
    arenaCapabilitiesFixture.rankedAllowance.limit,
    rankedCapacityPack?.owned ? 15 : 10,
  );
});

test('keeps the default account and first-run account internally coherent', () => {
  const owned = siteReviewApiResponse('GET', '/api/bots/mine');
  assert.ok(owned);
  assert.ok(Array.isArray(owned.body));
  assert.equal(owned.body[0]?.id, REVIEW_PINCER_ID);

  const empty = siteReviewApiResponse('GET', '/api/bots/mine', {
    referer: 'http://127.0.0.1:4181/garage?review=first-run',
  });
  assert.ok(empty);
  assert.deepEqual(empty.body, []);
  assert.equal(
    siteReviewApiResponse('GET', '/api/bots/mine?unexpected=1', {
      referer: 'http://127.0.0.1:4181/garage?review=first-run',
    }),
    undefined,
  );
});

test('uses the real anonymous HTTP state for the login scenario', () => {
  const response = siteReviewApiResponse('GET', '/api/accounts/me', {
    referer: 'http://127.0.0.1:4181/login',
  });
  assert.ok(response);
  assert.equal(response.status, 401);
});

test('supports the Watch controls without relaxing unknown queries', () => {
  const filtered = siteReviewApiResponse(
    'GET',
    '/api/matches?take=30&bot=pincer-gen-10&map=arena-01&ranked=true',
  );
  assert.ok(filtered);
  assert.ok(Array.isArray(filtered.body));
  assert.equal(filtered.body.length, 2);

  assert.equal(
    siteReviewApiResponse('GET', '/api/matches?take=30&surprise=true'),
    undefined,
  );
  assert.equal(
    siteReviewApiResponse('POST', '/api/matches?take=30'),
    undefined,
  );
});

test('keeps every rendered bot link inside the controlled API', () => {
  assert.deepEqual(
    botsFixture.map((bot) => bot.id),
    currentLeaderboardEntries.map((entry) => entry.id),
  );

  for (const bot of botsFixture) {
    for (const key of [bot.slug, bot.id]) {
      const detail = siteReviewApiResponse('GET', `/api/bots/${key}`);
      assert.equal(detail?.status, 200, `missing bot detail for ${key}`);
      assert.equal(
        'id' in (detail?.body ?? {}) ? detail?.body.id : null,
        bot.id,
      );
    }
    assert.equal(
      siteReviewApiResponse('GET', `/api/bots/${bot.id}/stats`)?.status,
      200,
      `missing bot stats for ${bot.name}`,
    );
    assert.equal(
      siteReviewApiResponse('GET', `/api/bots/${bot.id}/matches`)?.status,
      200,
      `missing bot history for ${bot.name}`,
    );
  }

  assert.equal(
    siteReviewApiResponse('GET', `/api/bots/${REVIEW_PINCER_ID}?loose=1`),
    undefined,
  );
});

test('keeps every rendered set-game link inside the controlled API', () => {
  const match = siteReviewApiResponse(
    'GET',
    `/api/matches/${REVIEW_FAILED_MATCH_ID}`,
  );
  const live = siteReviewApiResponse(
    'GET',
    `/api/matches/${REVIEW_FAILED_MATCH_ID}/live`,
  );
  const set = siteReviewApiResponse(
    'GET',
    `/api/matchsets/${REVIEW_SET_ID}`,
  );

  assert.equal(match?.status, 200);
  assert.equal(live?.status, 200);
  assert.equal(set?.status, 200);
  assert.equal(
    'id' in (match?.body ?? {}) ? match?.body.id : null,
    REVIEW_FAILED_MATCH_ID,
  );
  assert.equal(
    'id' in (set?.body ?? {}) ? set?.body.id : null,
    REVIEW_SET_ID,
  );

  for (const game of matchSetFixture.games) {
    assert.equal(
      siteReviewApiResponse('GET', `/api/matches/${game.id}`)?.status,
      200,
      `missing set game ${game.game} detail`,
    );
    assert.equal(
      siteReviewApiResponse('GET', `/api/matches/${game.id}/live`)?.status,
      200,
      `missing set game ${game.game} live state`,
    );
    assert.ok(
      matchesFixture.some((summary) => summary.id === game.id),
      `set game ${game.game} is missing from Watch`,
    );
  }
});

test('keeps ranked-set records, replays, histories, and map metadata coherent', () => {
  assert.equal(matchSetFixture.games.length, reviewSetGameSpecs.length);
  assert.equal(
    matchSetFixture.scoreA,
    matchSetFixture.games.filter(
      (game) => game.winnerBotId === REVIEW_PINCER_ID,
    ).length,
  );
  assert.equal(
    matchSetFixture.scoreB,
    matchSetFixture.games.filter(
      (game) => game.winnerBotId === REVIEW_BASTILLE_ID,
    ).length,
  );

  for (const [index, spec] of reviewSetGameSpecs.entries()) {
    const detail = completedSetMatchDetailFixtures[index];
    assert.ok(detail);
    assert.equal(detail.id, spec.id);
    assert.equal(detail.mapId, spec.mapId);
    assert.equal(detail.replayHash, spec.replayHash);
    assert.equal(detail.winnerSlot, 0);

    const map = metaFixture.maps.find((candidate) => candidate.id === spec.mapId);
    assert.ok(map, `missing metadata for ${spec.mapId}`);
    const productionMap = JSON.parse(
      readFileSync(
        new URL(`../../maps/${spec.mapId}.json`, import.meta.url),
        'utf8',
      ),
    ) as {
      width: number;
      height: number;
      theme: string;
    };
    // Review replays retain the canonical golden Arena topology while varying
    // only identity/theme; their metadata must describe that replay payload.
    assert.equal(map.width, 24);
    assert.equal(map.height, 18);
    assert.equal(map.themeId, productionMap.theme);
    assert.equal(map.themeId, spec.themeId);
  }

  const pincerSetGames = botMatchHistoryFixture.matches.filter(
    (match) => match.matchSetId === REVIEW_SET_ID,
  );
  assert.equal(pincerSetGames.length, reviewSetGameSpecs.length);
  assert.equal(pincerSetGames[0]?.id, reviewSetGameSpecs.at(-1)?.id);
});
