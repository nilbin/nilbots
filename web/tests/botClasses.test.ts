import assert from 'node:assert/strict';
import test from 'node:test';
import {
  botClassPresentation,
  orderBotClassIds,
} from '../src/site/botClasses.ts';

test('orders the three launch classes by player-facing priority', () => {
  assert.deepEqual(
    orderBotClassIds(['fabricator', 'striker', 'bulwark']),
    ['striker', 'bulwark', 'fabricator'],
  );
});

test('describes Fabricator companions as separate instances', () => {
  assert.match(
    botClassPresentation('fabricator').description,
    /separate instances of itself/i,
  );
});

test('keeps an unknown future class renderable', () => {
  assert.equal(botClassPresentation('specter').label, 'specter');
});
