/**
 * Let Node resolve the app's extensionless relative imports.
 *
 * Source is written for the bundler, which resolves `./arenaThemes` to `arenaThemes.ts`.
 * Node's ESM resolver does not, so importing renderer modules directly in a test fails
 * before any assertion runs. Rewriting every import to carry an extension would be a
 * large diff to app code purely to satisfy the test runner.
 *
 * Only extensionless *relative* specifiers are retried, and only after the real resolver
 * has already failed — so a genuinely missing module still reports as missing.
 */
export async function resolve(specifier, context, next) {
  try {
    return await next(specifier, context);
  } catch (error) {
    if (specifier.startsWith('.') && !/\.[a-z]+$/i.test(specifier)) {
      return next(`${specifier}.ts`, context);
    }
    throw error;
  }
}
