const { getDefaultConfig } = require('expo/metro-config');
const path = require('path');

const projectRoot = __dirname;
const repoRoot = path.resolve(projectRoot, '..');

const config = getDefaultConfig(projectRoot);

// Bot look sprites live in web/src/assets and are shared, not copied: they are the same
// art the site renders, and a second copy would drift the moment a look is added or
// retouched. Metro only watches the project root by default, so widen it to the repo.
config.watchFolders = [repoRoot];
config.resolver.nodeModulesPaths = [path.resolve(projectRoot, 'node_modules')];

// Treat SVG as an asset rather than a component. expo-image renders SVG directly, which
// avoids pulling in react-native-svg plus its transformer for what is, here, just an
// image. (If we ever need to recolour sprite internals at runtime, that trade flips.)
config.resolver.assetExts = [...config.resolver.assetExts, 'svg'];

module.exports = config;
