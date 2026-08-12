import { arcSignaturePresentationDefinitions } from '../../render/arcSignaturePresentation';
import type { ArcSignatureWebGLPainter } from './types';

const painterModules = import.meta.glob<ArcSignatureWebGLPainter>(
  './definitions/*.ts',
  { eager: true, import: 'default' },
);

const painters = new Map<string, ArcSignatureWebGLPainter>();
for (const [path, painter] of Object.entries(painterModules)) {
  if (painters.has(painter.signatureId))
    throw new Error(`Duplicate Arc WebGL painter '${painter.signatureId}'.`);
  painters.set(painter.signatureId, painter);
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(painter.signatureId))
    throw new Error(`Arc WebGL painter '${path}' has an invalid semantic ID.`);
}

const declaredIds = arcSignaturePresentationDefinitions()
  .map((definition) => definition.webglPainterId)
  .sort();
const painterIds = [...painters.keys()].sort();
if (declaredIds.join('\n') !== painterIds.join('\n'))
  throw new Error(
    `Arc WebGL painter coverage differs from presentation definitions.\n` +
      `Declared: ${declaredIds.join(', ')}\n` +
      `Painters: ${painterIds.join(', ')}`,
  );

export function arcSignatureWebGLPainter(
  id: string,
): ArcSignatureWebGLPainter | undefined {
  return painters.get(id);
}

export function arcSignatureWebGLPainterIds(): readonly string[] {
  return [...painters.keys()].sort();
}
