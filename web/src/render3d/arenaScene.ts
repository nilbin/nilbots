import * as THREE from 'three';
import type { ReplayDocument } from '../types';
import { arenaTheme, type ArenaTheme } from '../render/arenaThemes';
import { WallLayout } from '../render/wallTopology';

/**
 * The arena as actual geometry.
 *
 * The Canvas2D renderer draws a plan view and fakes depth with offsets and gradients; this
 * one builds the room and points a camera at it. Walls are boxes with height, the floor is
 * a plane, and the shadows are cast rather than painted — which is the whole reason it
 * exists, because a painted shadow cannot fall across another wall or shorten as a light
 * moves.
 *
 * **It uses the textures that are already shipped**, which is what makes it affordable.
 * Wall tops take their sprite from the same 16-column topology atlas the 2D renderer
 * indexes, so every baked rivet and panel edge is preserved; the sides take the 1024²
 * tiling albedo that until now only filled a flat silhouette. Nothing new had to be drawn.
 */

/** Wall height, in tiles. Tall enough to read as a room, low enough to see over. */
const WALL_HEIGHT = 0.62;

/** How far the floor plane extends past the map, so the horizon is not a cliff edge. */
const FLOOR_MARGIN = 6;

export interface ArenaScene {
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
  dispose: () => void;
}

/**
 * Build the static half of the scene: floor, walls, lights.
 *
 * Static for the life of a replay — the map does not change — so this runs once and the
 * per-frame work is only moving bots and projectiles.
 */
export function buildArena(replay: ReplayDocument): ArenaScene {
  const theme = arenaTheme(replay.header.themeId);
  const { mapWidth, mapHeight } = replay.header;

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(theme.palette.canvas);
  // Fog hides the floor plane's edge and gives distance a cost, which is most of what
  // makes a flat grid read as a space.
  scene.fog = new THREE.Fog(theme.palette.canvas, mapWidth * 0.9, mapWidth * 2.2);

  const disposables: { dispose: () => void }[] = [];

  scene.add(...lights(mapWidth, mapHeight, disposables));
  scene.add(floor(theme, mapWidth, mapHeight, disposables));
  for (const mesh of walls(replay, theme, disposables)) scene.add(mesh);

  const camera = new THREE.PerspectiveCamera(42, 1, 0.1, 200);

  return {
    scene,
    camera,
    dispose: () => {
      for (const item of disposables) item.dispose();
    },
  };
}

/**
 * One directional light with a shadow map, plus enough ambient that the unlit faces are
 * dark rather than black.
 *
 * Angled across the map rather than straight down: a light directly overhead casts almost
 * no visible shadow on a top-down-ish view, which would waste the entire point of building
 * real geometry.
 */
function lights(
  mapWidth: number,
  mapHeight: number,
  disposables: { dispose: () => void }[],
): THREE.Object3D[] {
  const key = new THREE.DirectionalLight(0xe8f1ff, 4.4);
  key.position.set(mapWidth * 0.55, mapWidth * 0.85, mapHeight * 0.75);
  key.castShadow = true;
  key.shadow.mapSize.set(2048, 2048);
  // The shadow camera has to contain the whole map or walls at the edge stop casting.
  const extent = Math.max(mapWidth, mapHeight) * 0.8;
  key.shadow.camera.left = -extent;
  key.shadow.camera.right = extent;
  key.shadow.camera.top = extent;
  key.shadow.camera.bottom = -extent;
  key.shadow.camera.far = mapWidth * 3;
  // Without a bias the floor shadows itself in stripes; too much and shadows detach from
  // the walls casting them.
  key.shadow.bias = -0.0015;
  key.shadow.normalBias = 0.02;

  const ambient = new THREE.AmbientLight(0x6f8bb0, 2.4);
  // A dim fill from the opposite side so wall faces turned away from the key are readable
  // rather than silhouettes.
  const fill = new THREE.DirectionalLight(0x4d7099, 1.5);
  fill.position.set(-mapWidth * 0.6, mapWidth * 0.4, -mapHeight * 0.5);

  disposables.push(key, ambient, fill);
  return [key, key.target, ambient, fill];
}

function floor(
  theme: ArenaTheme,
  mapWidth: number,
  mapHeight: number,
  disposables: { dispose: () => void }[],
): THREE.Mesh {
  const geometry = new THREE.PlaneGeometry(
    mapWidth + FLOOR_MARGIN * 2,
    mapHeight + FLOOR_MARGIN * 2,
  );
  geometry.rotateX(-Math.PI / 2);
  geometry.translate(mapWidth / 2, 0, mapHeight / 2);

  // One copy stretched across the arena, exactly as `drawTextureField` does — the 2D
  // renderer maps a material once over the whole map and reveals it through geometry,
  // never slicing it per tile. Repeating it here was the single biggest reason this did
  // not look like the flat viewer: same texture, completely different texel density and no
  // continuity from one tile to the next.
  //
  // The plane overhangs the map, so the repeat is the overhang ratio and the offset pulls
  // the arena's copy back into place.
  const span = mapWidth + FLOOR_MARGIN * 2;
  const texture = fromImage(theme.floorTexture);
  if (texture) {
    texture.wrapS = THREE.RepeatWrapping;
    texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(span / mapWidth, (mapHeight + FLOOR_MARGIN * 2) / mapHeight);
    texture.offset.set(
      -FLOOR_MARGIN / mapWidth,
      -FLOOR_MARGIN / mapHeight,
    );
  }
  const material = new THREE.MeshStandardMaterial({
    map: texture,
    color: tintMultiplier(theme.palette.floorTint),
    roughness: 0.86,
    metalness: 0.12,
  });

  const mesh = new THREE.Mesh(geometry, material);
  mesh.receiveShadow = true;
  disposables.push(geometry, material);
  if (texture) disposables.push(texture);
  return mesh;
}

/**
 * Every wall, merged into one geometry per family.
 *
 * Merged rather than instanced because the top faces need per-tile UVs into the topology
 * atlas, and instancing that means patching a shader with a per-instance attribute. A few
 * hundred boxes is nothing as geometry, so baking the UVs into one buffer costs a loop and
 * saves a custom material — and it still ends up as one draw call per family.
 */
function walls(
  replay: ReplayDocument,
  theme: ArenaTheme,
  disposables: { dispose: () => void }[],
): THREE.Mesh[] {
  const layout = new WallLayout(
    replay,
    validFamily(replay.header.presentation?.boundaryWall, theme, theme.walls.defaults.boundary),
    validFamily(replay.header.presentation?.interiorWall, theme, theme.walls.defaults.interior),
  );

  const byFamily = new Map<string, THREE.BufferGeometry[]>();
  const capsByFamily = new Map<string, THREE.BufferGeometry[]>();
  for (const wall of layout.walls()) {
    const box = new THREE.BoxGeometry(1, WALL_HEIGHT, 1);
    box.translate(wall.x + 0.5, WALL_HEIGHT / 2, wall.y + 0.5);
    projectWorldUvs(box, replay.header.mapWidth, replay.header.mapHeight);
    (byFamily.get(wall.family) ?? byFamily.set(wall.family, []).get(wall.family)!).push(box);

    // The overlay quad. Deliberately wider than its tile: the atlas art overhangs its
    // logical cell by the manifest's gutter, which is what lets a wall's edge trim sit over
    // its neighbour — the same reason the 2D renderer draws it oversized.
    const gutter = theme.walls.atlas.gutterPixels / theme.walls.atlas.contentPixels;
    const span = 1 + gutter * 2;
    const cap = new THREE.PlaneGeometry(span, span);
    cap.rotateX(-Math.PI / 2);
    applyAtlasUvs(cap, wall.mask, theme.walls.atlas.columns);
    cap.translate(wall.x + 0.5, WALL_HEIGHT + 0.004, wall.y + 0.5);
    (capsByFamily.get(wall.family) ?? capsByFamily.set(wall.family, []).get(wall.family)!)
      .push(cap);
  }

  const meshes: THREE.Mesh[] = [];
  for (const [familyId, parts] of byFamily) {
    const family = theme.walls.families.get(familyId);
    const geometry = mergeGeometries(parts);
    for (const part of parts) part.dispose();

    // Albedo on every face, including the top. The topology atlas is *not* a standalone
    // texture — the 2D renderer fills with the material and then draws the atlas over it as
    // a transparent overlay — so using it alone here produced dark outlines floating on
    // nothing. It goes on as a cap below, in the same order.
    const body = new THREE.MeshStandardMaterial({
      map: sprite(family?.materialTexture ?? null, THREE.RepeatWrapping),
      color: tintMultiplier(theme.palette.wallTint),
      roughness: 0.88,
      metalness: 0.2,
    });

    const mesh = new THREE.Mesh(geometry, body);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    meshes.push(mesh);
    disposables.push(geometry, body);

    const caps = capsByFamily.get(familyId) ?? [];
    if (caps.length > 0 && family?.edgeAtlasTexture) {
      const capGeometry = mergeGeometries(caps);
      for (const part of caps) part.dispose();
      const capMaterial = new THREE.MeshStandardMaterial({
        map: sprite(family.edgeAtlasTexture),
        transparent: true,
        // Sits a fraction above the wall top, so depth writes would fight the box beneath
        // it at grazing angles. Reading depth without writing it settles that.
        depthWrite: false,
        roughness: 0.7,
        metalness: 0.25,
      });
      const capMesh = new THREE.Mesh(capGeometry, capMaterial);
      capMesh.receiveShadow = true;
      meshes.push(capMesh);
      disposables.push(capGeometry, capMaterial);
    }
  }
  return meshes;
}

/**
 * Re-map a box's UVs from world position, so the material is continuous across every wall.
 *
 * BoxGeometry gives each face its own 0..1 square, which restarts the texture at every
 * tile — the tell that gives away a grid built from repeated boxes. The 2D renderer has no
 * such seam because it stretches one copy over the arena and masks it, so this reproduces
 * that: horizontal faces take the material's arena-wide coordinates, vertical faces take
 * the horizontal axis they run along plus height, at the same texels per world unit.
 */
function projectWorldUvs(box: THREE.BoxGeometry, mapWidth: number, mapHeight: number): void {
  const position = box.attributes.position as THREE.BufferAttribute;
  const normal = box.attributes.normal as THREE.BufferAttribute;
  const uv = box.attributes.uv as THREE.BufferAttribute;

  for (let vertex = 0; vertex < position.count; vertex++) {
    const x = position.getX(vertex);
    const y = position.getY(vertex);
    const z = position.getZ(vertex);
    if (Math.abs(normal.getY(vertex)) > 0.5) {
      uv.setXY(vertex, x / mapWidth, 1 - z / mapHeight);
    } else if (Math.abs(normal.getX(vertex)) > 0.5) {
      uv.setXY(vertex, z / mapHeight, y / mapHeight);
    } else {
      uv.setXY(vertex, x / mapWidth, y / mapHeight);
    }
  }
  uv.needsUpdate = true;
}

/**
 * Approximate a palette tint as a colour multiplier.
 *
 * The 2D renderer lays `rgba(4, 8, 13, 0.20)` over the material as a flat composite. There
 * is no equivalent pass here, but the tints are near-black at low alpha, so compositing
 * against them is within a rounding error of scaling the texture down by `1 - alpha` — and
 * a multiply costs nothing where an extra transparent layer would cost a draw and a sort.
 */
function tintMultiplier(tint: string): THREE.Color {
  const match = /rgba?\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*([\d.]+)\s*\)/.exec(tint);
  const alpha = match ? Number(match[1]) : 0;
  const scale = Math.max(0, Math.min(1, 1 - alpha));
  return new THREE.Color(scale, scale, scale);
}

/** Point a quad's UVs at one cell of the 16-column topology atlas. */
function applyAtlasUvs(geometry: THREE.BufferGeometry, mask: number, columns: number): void {
  const uv = geometry.attributes.uv as THREE.BufferAttribute;
  const cell = 1 / columns;
  const column = mask % columns;
  const row = Math.floor(mask / columns);

  for (let vertex = 0; vertex < uv.count; vertex++) {
    const u = uv.getX(vertex);
    const v = uv.getY(vertex);
    uv.setXY(vertex, (column + u) * cell, 1 - (row + 1 - v) * cell);
  }
  uv.needsUpdate = true;
}

/** Concatenate geometries that share an attribute layout, preserving material groups. */
function mergeGeometries(parts: readonly THREE.BufferGeometry[]): THREE.BufferGeometry {
  const merged = new THREE.BufferGeometry();
  const names = ['position', 'normal', 'uv'] as const;
  for (const name of names) {
    const arrays = parts.map((part) => part.attributes[name].array as Float32Array);
    const total = arrays.reduce((sum, array) => sum + array.length, 0);
    const combined = new Float32Array(total);
    let offset = 0;
    for (const array of arrays) {
      combined.set(array, offset);
      offset += array.length;
    }
    merged.setAttribute(
      name,
      new THREE.BufferAttribute(combined, parts[0].attributes[name].itemSize),
    );
  }

  const indices: number[] = [];
  let vertexOffset = 0;
  for (const part of parts) {
    const index = part.getIndex()!;
    for (let i = 0; i < index.count; i++) indices.push(index.getX(i) + vertexOffset);
    vertexOffset += part.attributes.position.count;
  }
  merged.setIndex(indices);

  return merged;
}

/** A texture sampled directly, clamped by default so atlas cells cannot bleed. */
function sprite(
  image: HTMLImageElement | null,
  wrap: THREE.Wrapping = THREE.ClampToEdgeWrapping,
): THREE.Texture | null {
  const texture = fromImage(image);
  if (!texture) return null;
  texture.wrapS = wrap;
  texture.wrapT = wrap;
  return texture;
}

/**
 * Wrap an image, whether or not it has finished loading.
 *
 * The theme's images are shared with the Canvas2D renderer, which redraws every frame and
 * simply skips one that is not `complete` yet. This renderer builds its materials once, so
 * the same check bakes "not loaded" in permanently — which is exactly what happened the
 * first time: every wall came out flat grey because the scene was built while the atlases
 * were still decoding, and nothing ever revisited them.
 *
 * One listener per texture makes the timing irrelevant.
 */
function fromImage(image: HTMLImageElement | null): THREE.Texture | null {
  if (!image) return null;
  const texture = new THREE.Texture(image);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.anisotropy = 8;
  if (image.complete && image.naturalWidth > 0) {
    texture.needsUpdate = true;
  } else {
    image.addEventListener('load', () => { texture.needsUpdate = true; }, { once: true });
  }
  return texture;
}

function validFamily(candidate: string | undefined, theme: ArenaTheme, fallback: string): string {
  return candidate && theme.walls.families.has(candidate) ? candidate : fallback;
}
