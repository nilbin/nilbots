import assert from 'node:assert/strict';
import test from 'node:test';
import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import {
  TacticsIconAudition, TacticsIconButton,
} from './.harness/harness.entry.js';

test('icon buttons expose labels and visible interaction states without emoji assets', () => {
  const normal = renderToStaticMarkup(createElement(TacticsIconButton, {
    icon: 'posts', label: 'Exact Posts',
  }));
  const pressed = renderToStaticMarkup(createElement(TacticsIconButton, {
    icon: 'patrol', label: 'Patrol route', pressed: true,
  }));
  const configured = renderToStaticMarkup(createElement(TacticsIconButton, {
    icon: 'transition', label: 'Continue conditions configured', configured: true,
  }));
  const disabled = renderToStaticMarkup(createElement(TacticsIconButton, {
    icon: 'signature', label: 'No spatial signature', disabled: true,
  }));
  assert.match(normal, /aria-label="Exact Posts"/);
  assert.match(normal, /<svg[^>]+viewBox="0 0 24 24"/);
  assert.match(pressed, /aria-pressed="true"/);
  assert.match(configured, /data-configured="true"/);
  assert.match(disabled, /disabled=""/);
});
test('the development audition renders the coherent icon vocabulary together', () => {
  const markup = renderToStaticMarkup(createElement(TacticsIconAudition));
  assert.match(markup, /data-testid="tactics-icon-audition"/);
  for (const name of ['posts', 'patrol', 'vision', 'attack-range', 'transition', 'signature']) {
    assert.match(markup, new RegExp(`aria-label="${name}"`));
  }
});
