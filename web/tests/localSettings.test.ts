import assert from 'node:assert/strict';
import test from 'node:test';
import {
  readLocalSetting,
  writeLocalSetting,
} from '../src/audio/localSettings.ts';

test('restricted webviews cannot crash optional audio persistence', () => {
  const denied = {
    getItem(): string | null {
      throw new DOMException('Denied', 'SecurityError');
    },
    setItem(): void {
      throw new DOMException('Denied', 'SecurityError');
    },
  };

  assert.equal(readLocalSetting('candidate', denied), null);
  assert.doesNotThrow(() => writeLocalSetting('candidate', 'aegis', denied));
});
