import { register } from 'node:module';
import * as canvas from '@napi-rs/canvas';

register('./resolve.mjs', import.meta.url);

/**
 * The renderer reaches for browser globals — Path2D for wall outlines, DOMMatrix for
 * transforms. @napi-rs/canvas provides them as exports rather than globals, so they are
 * installed here, before any test imports the renderer.
 *
 * Only what is missing: if a future Node ships these natively, it wins.
 */
for (const name of ['Path2D', 'DOMMatrix', 'ImageData', 'Image']) {
  if (!(name in globalThis) && name in canvas) {
    globalThis[name] = canvas[name];
  }
}
