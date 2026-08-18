import assert from 'node:assert/strict';
import test from 'node:test';
import {
  mapRegionPresentation,
  nearestSpawnTeam,
  relationForPhysicalSide,
  relationForSpawn,
} from '../src/site/tactics/sheetMapPresentation';

test('every Tapline landmark gets an explicit player-facing role', () => {
  assert.deepEqual(mapRegionPresentation('well-north'), {
    kind: 'well', label: 'North Well', symbol: 'N', physicalSide: null,
  });
  assert.equal(mapRegionPresentation('heal-south').kind, 'heal');
  assert.deepEqual(mapRegionPresentation('reactor-west'), {
    kind: 'reactor', label: 'West reactor', symbol: 'R', physicalSide: 'west',
  });
  assert.deepEqual(mapRegionPresentation('home-east'), {
    kind: 'delivery', label: 'East delivery zone', symbol: '↓', physicalSide: 'east',
  });
});

test('team landmarks stay own/enemy-relative when the binding preview rotates', () => {
  assert.equal(relationForPhysicalSide('west', 'west'), 'own');
  assert.equal(relationForPhysicalSide('east', 'west'), 'enemy');
  assert.equal(relationForPhysicalSide('east', 'east'), 'own');
  assert.equal(relationForSpawn('team-1-unit-3', 'east'), 'own');
  assert.equal(relationForSpawn('team-0-unit-3', 'east'), 'enemy');
});

test('a combined spawn-pad tag resolves from its authoritative spawn clusters', () => {
  const spawns = [
    { id: 'team-0-unit-0', position: { x: 1, y: 4 } },
    { id: 'team-0-unit-1', position: { x: 3, y: 4 } },
    { id: 'team-1-unit-0', position: { x: 29, y: 22 } },
    { id: 'team-1-unit-1', position: { x: 27, y: 22 } },
  ];
  assert.equal(nearestSpawnTeam({ x: 2, y: 6 }, spawns), 0);
  assert.equal(nearestSpawnTeam({ x: 28, y: 20 }, spawns), 1);
  assert.equal(nearestSpawnTeam({ x: 15, y: 13 }, spawns), null);
});
