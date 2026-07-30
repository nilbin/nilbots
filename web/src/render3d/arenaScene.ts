import * as THREE from 'three';
import type { ReplayModel } from '../replayModel';
import { arenaTheme, type ArenaTheme } from '../render/arenaThemes';
import { WallLayout } from '../render/wallTopology';
import { wallShapes } from './wallSolids';

/**
 * The arena as actual geometry.
 *
 * The Canvas2D renderer draws a plan view and fakes depth with offsets and gradients; this
 * one builds the room and points a camera at it. Walls are chamfered solids traced from the
 * tile grid, the floor is a plane, and the shadows are cast rather than painted — which is
 * the whole reason it exists, because a painted shadow cannot fall across another wall or
 * shorten as a light moves.
 *
 * **It uses the textures that are already shipped**, which is what makes it affordable.
 * Wall tops take their sprite from the same 16-column topology atlas the 2D renderer
 * indexes, so every baked rivet and panel edge is preserved; the sides take the 1024²
 * tiling albedo that until now only filled a flat silhouette. Nothing new had to be drawn.
 *
 * Those same albedos are also used as **bump maps**, which is the cheapest honest trick
 * here: the art already contains its own relief, drawn as light and shade by whoever baked
 * it, so reading its luminance as height makes a plate edge catch this scene's key light
 * rather than carrying a highlight from a light that was never in this room.
 */

/**
 * How far above horizontal the camera sits.
 *
 * Exported because it is not only the camera's business: anything that has to face the
 * viewer squarely — health pips, any future label — needs the same angle, and a second copy
 * of it is a thing that silently stops matching the day the framing is adjusted.
 */
export const CAMERA_PITCH = (58 * Math.PI) / 180;

/** The chamfer along a wall's top edge, in tiles. */
const WALL_CHAMFER = 0.055;

/**
 * Static visual room around the largest approved live chassis.
 *
 * Trident Wasp's approved GLB spans 1.12 tiles and the actor renderer applies
 * `max(0.82, 1.18 * 0.9) = 1.062`, for a live span of 1.18944. Its farthest
 * measured planform vertex has a rotation-safe live radius of 0.62462; adding
 * 0.02 tile safety and subtracting the authoritative half-tile corridor gives
 * 0.14462. Width / 2 is insufficient for diagonal headings.
 * Cosmetic idle/recoil motion remains actor-owned and is deliberately not paid for by
 * hollowing every wall out further.
 */
export const WALL_OPEN_EDGE_INSET = 0.14462;

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
export function buildArena(replay: ReplayModel): ArenaScene {
  const theme = arenaTheme(replay.map.presentation?.themeId ?? undefined);
  const mapWidth = replay.map.width;
  const mapHeight = replay.map.height;

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

/**
 * The arena floor, and **only** the arena floor.
 *
 * It used to overhang the map by six tiles so the horizon was not a cliff edge. That is a
 * real problem and this is not the fix: the overhang read as arena — same texture, same
 * lighting — so the room appeared to have no walls at its edge, just floor continuing into
 * the dark past the boundary. The map is the world here; outside it is background, and the
 * fog reaches the edge before the eye does.
 */
function floor(
  theme: ArenaTheme,
  mapWidth: number,
  mapHeight: number,
  disposables: { dispose: () => void }[],
): THREE.Mesh {
  const geometry = new THREE.PlaneGeometry(mapWidth, mapHeight);
  geometry.rotateX(-Math.PI / 2);
  geometry.translate(mapWidth / 2, 0, mapHeight / 2);

  // One copy stretched across the arena, exactly as `drawTextureField` does — the 2D
  // renderer maps a material once over the whole map and reveals it through geometry,
  // never slicing it per tile. Repeating it here was the single biggest reason this did
  // not look like the flat viewer: same texture, completely different texel density and no
  // continuity from one tile to the next.
  const texture = fromImage(theme.floorTexture);
  const material = new THREE.MeshStandardMaterial({
    map: texture,
    // The albedo doubles as relief. Every one of these textures is a photographed or baked
    // metal surface whose plates, rivets and grime are already *drawn* as light and shade —
    // so reading its luminance as height gives the real thing back a shape that responds to
    // the key light, instead of a photograph of shading lying flat under a different one.
    bumpMap: texture,
    bumpScale: 1.6,
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
 * Every wall, as one extruded solid per family.
 *
 * The tiles are traced into outlines by `wallShapes` rather than stamped as a box each, so
 * a run of wall is a single chamfered block instead of a row of cubes with hidden faces
 * pressed together. One extrusion per family is also one draw call per family.
 *
 * The topology caps stay per tile and stay merged. They need per-tile UVs into the 16-column
 * atlas, and instancing that means patching a shader with a per-instance attribute — so
 * baking the UVs into one buffer costs a loop and saves a custom material.
 */
function walls(
  replay: ReplayModel,
  theme: ArenaTheme,
  disposables: { dispose: () => void }[],
): THREE.Mesh[] {
  const layout = new WallLayout(
    replay,
    validFamily(
      replay.map.presentation?.boundaryWall ?? undefined,
      theme,
      theme.walls.defaults.boundary,
    ),
    validFamily(
      replay.map.presentation?.interiorWall ?? undefined,
      theme,
      theme.walls.defaults.interior,
    ),
    (family) =>
      validFamily(
        family,
        theme,
        theme.walls.defaults.interior,
      ),
  );

  const byFamily = new Map<string, { x: number; y: number }[]>();
  const capsByFamily = new Map<string, THREE.BufferGeometry[]>();
  for (const wall of layout.walls()) {
    (byFamily.get(wall.family) ?? byFamily.set(wall.family, []).get(wall.family)!)
      .push({ x: wall.x, y: wall.y });

    const family = theme.walls.families.get(wall.family)!;

    // Preserve atlas gutter only across same-family joins. A different wall family meets
    // on the grid boundary; open floor receives the universal live-bot clearance.
    const { contentPixels, gutterPixels } = theme.walls.atlas;
    const gutter = gutterPixels / contentPixels;
    const cellGutter = gutterPixels / (contentPixels + gutterPixels * 2);
    const connectedHalf = 0.5 + gutter - WALL_CHAMFER;
    const openHalf = 0.5 - WALL_OPEN_EDGE_INSET - WALL_CHAMFER;
    const edge = (
      dx: number,
      dy: number,
      lowUv: boolean,
    ): { extent: number; uv: number } => {
      const neighbour = layout.familyAt(wall.x + dx, wall.y + dy);
      if (neighbour === wall.family)
        return { extent: connectedHalf, uv: lowUv ? 0 : 1 };
      return {
        extent: neighbour === null ? openHalf : 0.5,
        // The atlas cell's outer gutter lies beyond the authored wall rim. Move that rim
        // to a newly inset edge instead of cropping progressively deeper into the art.
        uv: lowUv ? cellGutter : 1 - cellGutter,
      };
    };
    const west = edge(-1, 0, true);
    const east = edge(1, 0, false);
    // Plane V runs from south to north after it is laid onto XZ.
    const southEdge = edge(0, 1, true);
    const northEdge = edge(0, -1, false);
    const left = -west.extent;
    const right = east.extent;
    const north = -northEdge.extent;
    const south = southEdge.extent;
    const cap = new THREE.PlaneGeometry(right - left, south - north);
    cap.rotateX(-Math.PI / 2);
    applyAtlasUvs(
      cap,
      wall.mask,
      theme.walls.atlas.columns,
      {
        uMin: west.uv,
        uMax: east.uv,
        vMin: southEdge.uv,
        vMax: northEdge.uv,
      },
    );
    cap.translate(
      wall.x + 0.5 + (left + right) / 2,
      family.geometry3d.height + 0.004,
      wall.y + 0.5 + (north + south) / 2,
    );
    (capsByFamily.get(wall.family) ?? capsByFamily.set(wall.family, []).get(wall.family)!)
      .push(cap);
  }

  const meshes: THREE.Mesh[] = [];
  for (const [familyId, tiles] of byFamily) {
    const family = theme.walls.families.get(familyId);
    if (!family) continue;
    const height = family.geometry3d.height;
    const shapes = wallShapes(tiles, {
      cornerRadius: family.geometry3d.cornerRadius,
      // ExtrudeGeometry's bevel reaches outside its source outline. Compensate here so the
      // widest generated vertex, not merely the nominal outline, honours the contract.
      openEdgeInset: WALL_OPEN_EDGE_INSET + WALL_CHAMFER,
      isWall: (x, y) => layout.familyAt(x, y) !== null,
    });
    if (shapes.length === 0) continue;

    // Extruded from the traced outline rather than assembled from cubes, with a chamfer
    // along the top edge. `curveSegments` is what the corner arcs are drawn with; three is
    // enough to round a corner at this scale and cheap enough to spend on every wall.
    const geometry = new THREE.ExtrudeGeometry(shapes, {
      depth: height - WALL_CHAMFER,
      bevelEnabled: true,
      bevelThickness: WALL_CHAMFER,
      bevelSize: WALL_CHAMFER,
      bevelSegments: 2,
      curveSegments: 3,
    });
    // Stand it up: the shape's Y became −Z when it was traced, so this lands each wall on
    // the tile it came from, with the extrusion axis vertical and the top at WALL_HEIGHT.
    geometry.rotateX(-Math.PI / 2);
    projectWorldUvs(geometry, replay.map.width, replay.map.height);

    const texture = sprite(family?.materialTexture ?? null, THREE.RepeatWrapping);
    // Albedo on every face, including the top. The topology atlas is *not* a standalone
    // texture — the 2D renderer fills with the material and then draws the atlas over it as
    // a transparent overlay — so using it alone here produced dark outlines floating on
    // nothing. It goes on as a cap below, in the same order.
    const body = new THREE.MeshStandardMaterial({
      map: texture,
      // Same trick as the floor: the wall material's own plates and seams become relief,
      // so the surface has form under the light instead of a picture of form.
      bumpMap: texture,
      bumpScale: 2.2,
      color: tintMultiplier(theme.palette.wallTint),
      roughness: 0.88,
      metalness: 0.2,
    });

    const mesh = new THREE.Mesh(geometry, body);
    mesh.userData.kind = 'arena-wall-body';
    mesh.userData.family = familyId;
    mesh.userData.height = height;
    mesh.userData.cornerRadius = family.geometry3d.cornerRadius;
    mesh.userData.openEdgeInset = WALL_OPEN_EDGE_INSET;
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
      capMesh.userData.kind = 'arena-wall-caps';
      capMesh.userData.family = familyId;
      capMesh.userData.height = height;
      capMesh.userData.openEdgeInset = WALL_OPEN_EDGE_INSET;
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
function projectWorldUvs(
  geometry: THREE.BufferGeometry,
  mapWidth: number,
  mapHeight: number,
): void {
  const position = geometry.attributes.position as THREE.BufferAttribute;
  const normal = geometry.attributes.normal as THREE.BufferAttribute;
  const uv = geometry.attributes.uv as THREE.BufferAttribute;

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
function applyAtlasUvs(
  geometry: THREE.BufferGeometry,
  mask: number,
  columns: number,
  crop = { uMin: 0, uMax: 1, vMin: 0, vMax: 1 },
): void {
  const uv = geometry.attributes.uv as THREE.BufferAttribute;
  const cell = 1 / columns;
  const column = mask % columns;
  const row = Math.floor(mask / columns);

  for (let vertex = 0; vertex < uv.count; vertex++) {
    const u =
      crop.uMin + uv.getX(vertex) * (crop.uMax - crop.uMin);
    const v =
      crop.vMin + uv.getY(vertex) * (crop.vMax - crop.vMin);
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
