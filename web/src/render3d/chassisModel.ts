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

/** How tall a chassis stands, as a fraction of how wide it is. */
const DEPTH = 0.32;

/** Sprites are authored on a 512 viewBox; this normalises whatever one arrives. */
const TARGET_SPAN = 1;

const cache = new Map<string, Promise<THREE.Group | null>>();

/**
 * Build (or reuse) the model for a sprite URL.
 *
 * Cached by URL because both bots may share a look, a replay may be reopened, and parsing
 * plus triangulating a few hundred path segments is not something to repeat per match.
 *
 * **The owner's accent is deliberately not an input.** The flat renderer draws these
 * sprites untinted — accent reaches the screen as health pips, the facing cone, the
 * selection ring and the pool of light on the floor, never as a wash over the chassis — so
 * a model that took one would not be the same bot in a different renderer. It was tried:
 * a trace of accent as emission, which on hull greys this dark is not a trace but the
 * entire colour, and every chassis arrived as a solid lozenge of team colour.
 *
 * `paint` is the exception, and projectiles are why: the flat renderer *does* recolour a
 * bolt, wholesale, keeping only its silhouette. Passing it here says "this is a bolt", and
 * the rule stays one rule — match the flat renderer, whichever way it goes.
 */
export function chassisModel(url: string, paint?: THREE.Color): Promise<THREE.Group | null> {
  const key = paint ? `${url}|${paint.getHexString()}` : url;
  const existing = cache.get(key);
  if (existing) return existing;

  const built = load(url, paint).catch(() => null);
  cache.set(key, built);
  return built;
}

async function load(url: string, paint?: THREE.Color): Promise<THREE.Group | null> {
  const response = await fetch(url);
  if (!response.ok) return null;
  const markup = await response.text();

  const parsed = new SVGLoader().parse(markup);
  if (parsed.paths.length === 0) return null;

  // Draw order is height.
  //
  // A plan-view illustration is layered the way the object is built: hull, then plating,
  // then the cockpit and highlights on top. So a path drawn later is, in the thing being
  // drawn, further from the floor — which means the artist has already described the relief
  // and the extruder only has to believe them. Every path at one depth is a flat cut-out;
  // this is what makes it read as a machine instead of a sticker.
  //
  // Rises are a fraction of full height rather than a length, so `orient` can set the
  // vertical scale from the chassis' proportions without knowing what units the sprite was
  // drawn in — the two SVGs here are 512-unit, but nothing should depend on that.
  const paints = gradientPaints(markup);
  const layers = parsed.paths.length;
  const cutouts: { shape: THREE.Shape; fill: string; colour: THREE.Color; rise: number }[] = [];
  for (const [index, path] of parsed.paths.entries()) {
    // Fill colour comes from the SVG itself, so the model is coloured by the same source
    // the sprite is. A path with no fill is a stroke-only decoration and has no volume.
    const fill = (path.userData as { style?: { fill?: string } } | undefined)?.style?.fill;
    if (!fill || fill === 'none') continue;
    const colour = resolveFill(fill, paints);
    if (!colour) continue;

    const rise = 0.45 + 0.55 * ((index + 1) / layers);
    for (const shape of path.toShapes()) cutouts.push({ shape, fill, colour, rise });
  }
  if (cutouts.length === 0) return null;

  // The bevel has to be a proportion of the drawing, not a constant. Authored as one it was
  // 0.02 against a 385-unit-wide sprite — arithmetically present, invisible on screen — and
  // the catchlight along a chassis edge is most of what separates a model from a slab.
  const span = silhouetteSpan(cutouts);
  const bevel = span * 0.006;

  // One material per fill colour rather than per path: a chassis draws the same dozen
  // greys over and over, and there is no reason for twenty-eight copies of each.
  // A painted model throws its own colours away and becomes one glowing silhouette. This
  // is not a simplification for the sake of it — it is what the flat renderer does to a
  // bolt: `source-in` over the sprite, which keeps the alpha and replaces every pixel with
  // the owner's accent. A projectile is a bolt of that player's energy, not a little
  // painted object, and it reads as one in both renderers or in neither.
  const painted =
    paint &&
    new THREE.MeshStandardMaterial({
      color: paint,
      emissive: paint,
      emissiveIntensity: 1.8,
      roughness: 0.35,
      metalness: 0.1,
    });

  const palette = new Map<string, THREE.MeshStandardMaterial>();
  const materialFor = (fill: string, colour: THREE.Color) => {
    if (painted) return painted;
    const existing = palette.get(fill);
    if (existing) return existing;
    // A layer lights itself in proportion to how bright it was drawn. Every one of these
    // sprites is a dark hull carrying a few vivid trim colours — the Vanguard's cyan
    // strut, the Warden's gold spine — and in the flat renderer those read as lit because
    // a canvas draws them at full strength with no lighting model at all. Under real
    // lights they would just be pale paint. Squaring the luminance keeps the hull greys
    // matte and lets only the trim glow, which is the distinction the artist drew.
    const glow = luminance(colour) ** 2;
    const material = new THREE.MeshStandardMaterial({
      color: colour,
      roughness: 0.42,
      metalness: 0.62,
      emissive: colour,
      emissiveIntensity: glow * 1.4,
    });
    palette.set(fill, material);
    return material;
  };

  const group = new THREE.Group();
  for (const { shape, fill, colour, rise } of cutouts) {
    // Every layer starts at the floor and rises to its own height, so a shape is a solid
    // standing on the base rather than a slab floating over a gap.
    const geometry = new THREE.ExtrudeGeometry(shape, {
      depth: rise,
      bevelEnabled: true,
      // Thickness is along the extrusion axis, where a rise is at most 1 — sharing the
      // in-plane bevel's units here would bury the whole layer inside its own chamfer.
      bevelThickness: rise * 0.12,
      bevelSize: bevel,
      bevelSegments: 1,
    });
    const mesh = new THREE.Mesh(geometry, materialFor(fill, colour));
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    group.add(mesh);
  }

  return orient(group);
}

/**
 * Flatten every gradient in the document to the one colour that best stands for it.
 *
 * **Not an optimisation — without this the chassis have no colour at all.** All twelve
 * looks paint their hull with `fill="url(#nd-hull)"`, and `THREE.Color.setStyle` cannot
 * read a paint server: it warns, leaves the colour white, and every bot arrives as a pale
 * blank. A lit solid cannot carry a gradient the way a flat sprite does anyway, since the
 * shading is now the light's job, so one representative colour is the honest answer rather
 * than a lossy one.
 *
 * Representative means *area-weighted*: the mean colour along the gradient, so a stop
 * occupying half the sweep counts for half. A plain average would let the Warden's thin
 * `#fff0a6` rim wash its gold out as much as the gold itself.
 */
function gradientPaints(markup: string): Map<string, THREE.Color> {
  const paints = new Map<string, THREE.Color>();
  if (typeof DOMParser === 'undefined') return paints;

  const document = new DOMParser().parseFromString(markup, 'image/svg+xml');
  for (const gradient of document.querySelectorAll('linearGradient, radialGradient')) {
    const stops = [...gradient.querySelectorAll('stop')].map((stop, index, all) => ({
      // An omitted offset is 0, except on the last stop where the sweep ends at 1 — which
      // is how every one of these sprites is authored.
      offset: Number(stop.getAttribute('offset') ?? (index === all.length - 1 ? 1 : 0)),
      colour: new THREE.Color().setStyle(stop.getAttribute('stop-color') ?? '#ffffff'),
    }));
    if (stops.length === 0 || !gradient.id) continue;

    const mixed = new THREE.Color(0, 0, 0);
    let covered = 0;
    for (let index = 1; index < stops.length; index++) {
      const width = stops[index].offset - stops[index - 1].offset;
      if (width <= 0) continue;
      const midpoint = stops[index].colour.clone().lerp(stops[index - 1].colour, 0.5);
      mixed.add(midpoint.multiplyScalar(width));
      covered += width;
    }
    // A single stop, or every stop at the same offset: a flat colour wearing a gradient's
    // clothes, so take it literally.
    paints.set(gradient.id, covered > 0 ? mixed.multiplyScalar(1 / covered) : stops[0].colour);
  }
  return paints;
}

/** The colour to build a layer from, or null if the paint is one we cannot stand in for. */
function resolveFill(fill: string, paints: Map<string, THREE.Color>): THREE.Color | null {
  const reference = /^url\(['"]?#(.+?)['"]?\)$/.exec(fill.trim());
  if (!reference) return new THREE.Color().setStyle(fill);
  // A missing paint server means a malformed sprite. Dropping the layer loses a piece of
  // the model, which is the visible failure — better than a white patch that reads as art.
  return paints.get(reference[1]) ?? null;
}

/** Perceived brightness, on the linear colours three.js actually shades with. */
function luminance(colour: THREE.Color): number {
  return 0.2126 * colour.r + 0.7152 * colour.g + 0.0722 * colour.b;
}

/** How wide the drawing is, in whatever units it was drawn in. */
function silhouetteSpan(cutouts: { shape: THREE.Shape }[]): number {
  let minimum = Infinity;
  let maximum = -Infinity;
  for (const { shape } of cutouts) {
    for (const point of shape.getPoints()) {
      minimum = Math.min(minimum, point.x, point.y);
      maximum = Math.max(maximum, point.x, point.y);
    }
  }
  return Number.isFinite(minimum) && maximum > minimum ? maximum - minimum : 1;
}

/**
 * Put the model where the renderer expects it: one tile across, flat on the floor, facing
 * east — and hand back a **wrapper** holding it, not the model.
 *
 * SVG is Y-down and extrudes along Z, so without this a chassis arrives upside down,
 * standing on its nose, and roughly five hundred tiles across.
 *
 * The wrapper is the whole point of the return value. Every one of those corrections lives
 * in the inner group's own transform, so a caller that scales the model to a bot's size
 * overwrites the fit and gets the sprite back at its authored five hundred units — one bot
 * filling the arena and the camera inside it. Callers scale the wrapper; the fit is sealed
 * underneath where nothing has to remember it exists.
 */
function orient(group: THREE.Group): THREE.Group {
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

  // Height is set independently of the silhouette. Rises are fractions of one, so scaling
  // the extrusion axis by DEPTH makes the tallest layer exactly that fraction of the
  // chassis' width — a proportion, which is what "how tall should a bot be" actually is,
  // and it holds whether the sprite was drawn on a 512 grid or a 24 one.
  group.scale.z = TARGET_SPAN * DEPTH;

  // Re-measure after every transform and sit the model on the floor rather than through it.
  const placed = new THREE.Box3().setFromObject(wrapper);
  group.position.y = -placed.min.y;
  return wrapper;
}
