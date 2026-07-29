import assert from 'node:assert/strict';
import test from 'node:test';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { MemoryRouter } from 'react-router-dom';
import {
  ArenaActionProvider,
  LabsPanel,
} from './.harness/harness.entry.js';

const GENERIC_PROFILE = 'generic-actor-match-2';

test('an eligible Labs panel opens match setup through the shared Play composer', () => {
  const markup = renderPanel({
    labs: {
      enabled: true,
      playlists: [
        {
          playlistVersionId: '10000000-0000-0000-0000-000000000001',
          key: 'frontline-labs',
          displayName: 'Frontline Labs',
          version: 1,
          gameModeId: 'frontline',
          rulesetId: 'frontline-labs-1',
          matchFormatId: 'head-to-head',
          participantCount: 2,
          scoringTeamCount: 2,
          participantsPerTeam: 1,
          requiredContractProfileId: GENERIC_PROFILE,
        },
      ],
    },
    bots: [
      botSummary(
        '20000000-0000-0000-0000-000000000002',
        'Compatible',
        [GENERIC_PROFILE],
      ),
      botSummary(
        '20000000-0000-0000-0000-000000000003',
        'Legacy only',
        ['legacy-duel-0.1'],
      ),
    ],
  });

  assert.match(markup, /Labs experiments · unranked/);
  assert.match(markup, /Frontline Labs/);
  assert.match(markup, /Run lab match/);
  assert.doesNotMatch(markup, /Ranked set|FIGHT FOR RATING/);
});

test('the Labs panel explains disabled and incompatible experiment states', () => {
  assert.match(
    renderPanel({
      labs: { enabled: false, playlists: [] },
      bots: [],
    }),
    /No experiments are running/,
  );
  assert.match(
    renderPanel({
      labs: {
        enabled: true,
        playlists: [
          {
            playlistVersionId: '10000000-0000-0000-0000-000000000001',
            key: 'frontline-labs',
            displayName: 'Frontline Labs',
            version: 1,
            gameModeId: 'frontline',
            rulesetId: 'frontline-labs-1',
            matchFormatId: 'head-to-head',
            participantCount: 2,
            scoringTeamCount: 2,
            participantsPerTeam: 1,
            requiredContractProfileId: 'another-profile',
          },
        ],
      },
      bots: [],
    }),
    /No compatible experiment/,
  );
});

test('the Labs panel exposes its catalog loading state', () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  const markup = renderToStaticMarkup(
    createElement(
      QueryClientProvider,
      { client },
      createElement(
        MemoryRouter,
        null,
        createElement(
          ArenaActionProvider,
          null,
          createElement(LabsPanel, { bot: ownedGenericBot() }),
        ),
      ),
    ),
  );

  assert.match(markup, /Checking experiments/);
});

function renderPanel(data: { labs: unknown; bots: unknown }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  client.setQueryData(['labs'], data.labs);
  client.setQueryData(['bots'], data.bots);

  return renderToStaticMarkup(
    createElement(
      QueryClientProvider,
      { client },
      createElement(
        MemoryRouter,
        null,
        createElement(
          ArenaActionProvider,
          null,
          createElement(LabsPanel, { bot: ownedGenericBot() }),
        ),
      ),
    ),
  );
}

function ownedGenericBot() {
  return {
    id: '20000000-0000-0000-0000-000000000001',
    name: 'Entrant',
    slug: 'entrant',
    accent: '#00ffff',
    lookId: 'default',
    projectileLookId: 'default',
    createdAt: '2026-07-28T00:00:00Z',
    owner: 'Owner',
    isOwner: true,
    currentStanding: null,
    versions: [
      {
        id: '40000000-0000-0000-0000-000000000001',
        versionNumber: 1,
        status: 'Built',
        artifactHash: 'abc',
        isActive: true,
        createdAt: '2026-07-28T00:00:00Z',
        buildReceipt: null,
        buildLog: null,
        entryType: null,
        sources: null,
        supportedContractProfiles: [GENERIC_PROFILE],
      },
    ],
  };
}

function botSummary(
  id: string,
  name: string,
  supportedContractProfiles: string[],
) {
  return {
    id,
    name,
    slug: name.toLowerCase().replaceAll(' ', '-'),
    accent: '#00ffff',
    lookId: 'default',
    projectileLookId: 'default',
    createdAt: '2026-07-28T00:00:00Z',
    ratings: [],
    owner: `${name} owner`,
    activeVersion: {
      id: crypto.randomUUID(),
      versionNumber: 1,
      artifactHash: 'abc',
      supportedContractProfiles,
    },
    versionCount: 1,
    currentStanding: null,
  };
}
