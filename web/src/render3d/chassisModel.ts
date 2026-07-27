import * as THREE from 'three';
import { SVGLoader } from 'three/examples/jsm/loaders/SVGLoader.js';

/**
 * A chassis as real volume, extruded from the sprite that draws it flat.
 *
 * The bots are SVGs, which means their outlines are already vector paths — so the model
 * does not have to be authored, it can be *derived*. Every one of the twelve chassis and
 * eleven projectile looks gets a solid whose silhouette matches its sprite exactly, and a
 * new look added as a folder and a manifest gets a 3D form for free, the same way it gets a
 * 2D one.
 *
 * That is the whole reason this approach was worth the trouble. A hand-modelled set would
 * be twenty-three models to draw, keep in step with the sprites, and redo whenever an
 * artist adjusts one — and the first cosmetic pack sold would put them out of date.
 *
 * The paths keep their own fill colours, so a Vanguard is still blue-grey and an Aureate
 * Warden is still gold without anything here knowing which is which.
 */

/** How thick a chassis stands, in tiles. */
const DEPTH = 0.2;

/** Sprites are authored on a 512 viewBox; this normalises whatever one arrives. */
const TARGET_SPAN = 1;

const cache = new Map<string, Promise<THREE.Group | null>>();

/**
 * Build (or reuse) the model for a sprite URL.
 *
 * Cached by URL because both bots may share a look, a replay may be reopened, and parsing
 * plus triangulating a few hundred path segments is not something to repeat per match.
 */
export function chassisModel(url: string, accent: THREE.Color): Promise<THREE.Group | null> {
  const key = `${url}|${accent.getHexString()}`;
  const existing = cache.get(key);
  if (existing) return existing;

  const built = load(url, accent).catch(() => null);
  cache.set(key, built);
  return built;
}

async function load(url: string, accent: THREE.Color): Promise<THREE.Group | null> {
  const response = await fetch(url);
  if (!response.ok) return null;
  const markup = await response.text();

  const parsed = new SVGLoader().parse(markup);
  if (parsed.paths.length === 0) return null;

  const group = new THREE.Group();
  // Draw order is height.
  //
  // A plan-view illustration is layered the way the object is built: hull, then plating,
  // then the cockpit and highlights on top. So a path drawn later is, in the thing being
  // drawn, further from the floor — which means the artist has already described the relief
  // and the extruder only has to believe them. Every path at one depth is a flat cut-out;
  // this is what makes it read as a machine instead of a sticker.
  const layers = parsed.paths.length;
  for (const [index, path] of parsed.paths.entries()) {
    // Fill colour comes from the SVG itself, so the model is coloured by the same source
    // the sprite is. A path with no fill is a stroke-only decoration and has no volume.
    const fill = (path.userData as { style?: { fill?: string } } | undefined)?.style?.fill;
    if (!fill || fill === 'none') continue;

    const colour = new THREE.Color().setStyle(fill);
    const material = new THREE.MeshStandardMaterial({
      color: colour,
      roughness: 0.42,
      metalness: 0.62,
      // A trace of the owner's accent so two bots wearing the same chassis are still
      // telling apart at a glance, which is the one thing the flat renderer does with a
      // tint and this would otherwise lose.
      emissive: accent,
      emissiveIntensity: 0.18,
    });

    // Every layer starts at the floor and rises to its own height, so a shape is a solid
    // standing on the base rather than a slab floating over a gap.
    const rise = 0.45 + 0.55 * ((index + 1) / layers);
    for (const shape of SVGLoader.createShapes(path)) {
      const geometry = new THREE.ExtrudeGeometry(shape, {
        depth: rise,
        bevelEnabled: true,
        bevelThickness: 0.02,
        bevelSize: 0.02,
        bevelSegments: 1,
      });
      const mesh = new THREE.Mesh(geometry, material);
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      group.add(mesh);
    }
  }
  if (group.children.length === 0) return null;

  orient(group);
  return group;
}

/**
 * Put the model where the renderer expects it: unit-sized, flat on the floor, facing east.
 *
 * SVG is Y-down and extrudes along Z, so without this a chassis arrives upside down,
 * standing on its nose, and roughly five hundred tiles across.
 */
function orient(group: THREE.Group): void {
  const bounds = new THREE.Box3().setFromObject(group);
  const size = new THREE.Vector3();
  const centre = new THREE.Vector3();
  bounds.getSize(size);
  bounds.getCenter(centre);

  const span = Math.max(size.x, size.y) || 1;
  const scale = TARGET_SPAN / span;

  // Centre on the origin first, so scaling and rotation happen about the middle of the
  // chassis rather than the corner of its viewBox.
  for (const child of group.children) child.position.sub(centre);

  const wrapper = new THREE.Group();
  // Y-down to Y-up, and lay the extrusion axis vertical.
  group.scale.set(scale, -scale, scale);
  group.rotation.x = -Math.PI / 2;
  wrapper.add(group);

  // Depth is authored in the same units the shapes are, so it scales with them; this only
  // flattens the stack to the height a bot should stand.
  group.scale.z = scale * span * DEPTH;

  // Re-measure after every transform and sit the model on the floor rather than through it.
  const placed = new THREE.Box3().setFromObject(wrapper);
  group.position.y = -placed.min.y;
}
