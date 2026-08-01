import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const classRoot = join(repositoryRoot, 'web', 'src', 'assets', 'class-looks');
const projectileRoot = join(
  repositoryRoot,
  'web',
  'src',
  'assets',
  'class-projectile-looks',
);

const looks = [
  {
    id: 'kestrel',
    label: 'Kestrel',
    scale: 1.08,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M76 256 176 174 286 112 438 210 470 256 438 302 286 400 176 338Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="12" d="m128 256 96-50 118-76-42 104 118 22-118 22 42 104-118-76Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="10" d="m250 212 94 44-94 44-82-44Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m302 220 94 36-94 36 24-36Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m178 202 64-46-28 72Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m178 310 64 46-28-72Z"/>
      <path fill="#f6b73c" d="m93 244 58 12-58 12Z"/>`,
  },
  {
    id: 'palisade',
    label: 'Palisade',
    scale: 1.1,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M90 170 224 104 382 126 456 190 456 322 382 386 224 408 90 342 58 256Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="12" d="m278 126 104 22 52 56v104l-52 56-104 22 34-82v-96Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="10" d="M116 198 232 146 300 190v132l-68 44-116-52-28-58Z"/>
      <rect data-team-accent="true" fill="#38bdf8" x="374" y="176" width="34" height="160" rx="14"/>
      <path data-team-accent="true" fill="#38bdf8" d="m130 190 70-28 16 30-72 30Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m130 322 70 28 16-30-72-30Z"/>
      <circle fill="#f6b73c" cx="266" cy="256" r="34"/>`,
  },
  {
    id: 'towline',
    label: 'Towline',
    scale: 1.07,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M78 184 170 126 318 138 392 194 392 222 464 222 464 290 392 290 392 318 318 374 170 386 78 328Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="M126 190 214 156 326 176 370 218v76l-44 42-112 20-88-34-28-66Z"/>
      <circle fill="#8a6547" stroke="#0b1118" stroke-width="11" cx="244" cy="256" r="72"/>
      <circle fill="#18212b" stroke="#0b1118" stroke-width="9" cx="244" cy="256" r="34"/>
      <path fill="#a58b6c" stroke="#0b1118" stroke-width="10" d="M370 220h86v34h-54v38h54v34h-86Z"/>
      <rect data-team-accent="true" fill="#38bdf8" x="102" y="202" width="42" height="108" rx="18"/>
      <path data-team-accent="true" fill="#38bdf8" d="m286 182 60 26-14 30-64-24Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m286 330 60-26-14-30-64 24Z"/>
      <circle fill="#f6b73c" cx="244" cy="256" r="14"/>`,
  },
  {
    id: 'patchbay',
    label: 'Patchbay',
    scale: 1.04,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M88 212 150 146 226 146 256 92 286 146 366 146 430 212 430 300 366 366 286 366 256 420 226 366 150 366 88 300Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="M136 224 184 176h144l54 48v64l-54 48H184l-48-48Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="10" d="M220 174h72v50h50v72h-50v50h-72v-50h-50v-72h50Z"/>
      <circle fill="#18212b" stroke="#0b1118" stroke-width="9" cx="256" cy="260" r="42"/>
      <path data-team-accent="true" fill="#38bdf8" d="M238 186h36v52h52v36h-52v52h-36v-52h-52v-36h52Z"/>
      <rect data-team-accent="true" fill="#38bdf8" x="112" y="238" width="44" height="44" rx="10"/>
      <rect data-team-accent="true" fill="#38bdf8" x="356" y="238" width="50" height="44" rx="10"/>
      <circle fill="#f6b73c" cx="256" cy="260" r="18"/>`,
  },
  {
    id: 'lantern',
    label: 'Lantern',
    scale: 1.03,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M64 230 126 196 166 126 230 142 270 74 310 142 374 126 414 196 476 230 446 256 476 282 414 316 374 386 310 370 270 438 230 370 166 386 126 316 64 282 94 256Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="M112 232 174 200 208 160 270 150 332 160 366 200 428 232 404 256 428 280 366 312 332 352 270 362 208 352 174 312 112 280 136 256Z"/>
      <circle fill="#8a6547" stroke="#0b1118" stroke-width="12" cx="270" cy="256" r="82"/>
      <circle fill="#101820" stroke="#0b1118" stroke-width="10" cx="270" cy="256" r="50"/>
      <path data-team-accent="true" fill="#38bdf8" d="M270 178a78 78 0 0 1 64 34l-30 18a42 42 0 0 0-34-18Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="M270 334a78 78 0 0 0 64-34l-30-18a42 42 0 0 1-34 18Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m390 226 58 30-58 30 18-30Z"/>
      <circle fill="#f6b73c" cx="270" cy="256" r="26"/>`,
  },
  {
    id: 'mortar',
    label: 'Mortar',
    scale: 1.09,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M72 194 164 132 316 132 406 188 450 256 406 324 316 380 164 380 72 318Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="M112 210 188 168h114l64 42 30 46-30 46-64 42H188l-76-42Z"/>
      <ellipse fill="#8a6547" stroke="#0b1118" stroke-width="12" cx="260" cy="256" rx="98" ry="74"/>
      <ellipse fill="#0e151c" stroke="#0b1118" stroke-width="11" cx="318" cy="256" rx="58" ry="48"/>
      <ellipse fill="#556273" stroke="#0b1118" stroke-width="8" cx="336" cy="256" rx="28" ry="24"/>
      <rect data-team-accent="true" fill="#38bdf8" x="106" y="220" width="38" height="72" rx="14"/>
      <path data-team-accent="true" fill="#38bdf8" d="m204 174 74-12 8 32-76 16Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m204 338 74 12 8-32-76-16Z"/>
      <circle fill="#f6b73c" cx="336" cy="256" r="12"/>`,
  },
  {
    id: 'minesmith',
    label: 'Minesmith',
    scale: 1.06,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M84 204 146 138 250 126 356 154 430 218 430 294 356 358 250 386 146 374 84 308Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="M132 218 176 172 252 164 328 184 382 228v56l-54 44-76 20-76-8-44-46Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="10" d="m194 190 70-26 54 48-18 76-70 26-54-48Z"/>
      <circle fill="#111820" stroke="#0b1118" stroke-width="10" cx="188" cy="326" r="48"/>
      <circle fill="#111820" stroke="#0b1118" stroke-width="10" cx="334" cy="318" r="42"/>
      <path data-team-accent="true" fill="#38bdf8" d="m110 218 42-44 24 26-40 46Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m330 184 52 42-20 28-54-40Z"/>
      <circle data-team-accent="true" fill="#38bdf8" cx="188" cy="326" r="18"/>
      <circle fill="#f6b73c" cx="247" cy="239" r="18"/>`,
  },
  {
    id: 'hush',
    label: 'Hush',
    scale: 1.08,
    body: `
      <path fill="#111820" stroke="#0b1118" stroke-width="18" d="M62 256 150 132 250 190 338 94 454 204 482 256 454 308 338 418 250 322 150 380Z"/>
      <path fill="#283543" stroke="#0b1118" stroke-width="12" d="M116 256 172 180 252 220 334 144 414 218 444 256 414 294 334 368 252 292 172 332Z"/>
      <path fill="#090e14" stroke="#0b1118" stroke-width="10" d="M170 256c48-76 148-92 232-24l28 24-28 24c-84 68-184 52-232-24Z"/>
      <path fill="#8a6547" d="M226 256c34-34 88-40 140-12l20 12-20 12c-52 28-106 22-140-12Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m126 206 70-48 18 32-68 48Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m126 306 70 48 18-32-68-48Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m382 224 52 32-52 32 14-32Z"/>
      <ellipse fill="#f6b73c" cx="292" cy="256" rx="24" ry="12"/>`,
  },
  {
    id: 'relay',
    label: 'Relay',
    scale: 1.05,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M82 194 168 134 288 134 376 176 446 232 446 280 376 336 288 378 168 378 82 318Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="M126 212 190 170h90l66 32 52 42v24l-52 42-66 32h-90l-64-42Z"/>
      <circle fill="#8a6547" stroke="#0b1118" stroke-width="11" cx="244" cy="256" r="84"/>
      <circle fill="#101820" stroke="#0b1118" stroke-width="10" cx="244" cy="256" r="48"/>
      <path fill="none" stroke="#a58b6c" stroke-width="22" stroke-linecap="round" d="M280 204c54-34 104-16 136 34M280 308c54 34 104 16 136-34"/>
      <path data-team-accent="true" fill="#38bdf8" d="m128 204 62-38 16 32-62 40Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m128 308 62 38 16-32-62-40Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m380 224 62 32-62 32 18-32Z"/>
      <circle fill="#f6b73c" cx="244" cy="256" r="22"/>`,
  },
  {
    id: 'switchback',
    label: 'Switchback',
    scale: 1.06,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M80 182 180 118 274 154 342 126 438 194 470 256 438 318 342 386 274 358 180 394 80 330 116 256Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="m134 192 54-34 76 30-46 68 46 68-76 30-54-34 28-64Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="12" d="m378 192-54-34-76 30 46 68-46 68 76 30 54-34-28-64Z"/>
      <path fill="#101820" stroke="#0b1118" stroke-width="9" d="m216 256 40-58 40 58-40 58Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m104 188 70-44 16 34-66 44Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m408 324-70 44-16-34 66-44Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m392 212 54 44-54 44 14-44Z"/>
      <circle fill="#f6b73c" cx="256" cy="256" r="18"/>`,
  },
  {
    id: 'longshot',
    label: 'Longshot',
    scale: 1.1,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M64 214 150 154 302 176 366 214 474 224 474 288 366 298 302 336 150 358 64 298Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="m106 222 64-34 122 18 58 34h112v32H350l-58 34-122 18-64-34Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="10" d="M188 208h126l62 34v28l-62 34H188l44-48Z"/>
      <rect fill="#0d141b" stroke="#0b1118" stroke-width="9" x="298" y="238" width="164" height="36" rx="14"/>
      <path data-team-accent="true" fill="#38bdf8" d="m102 216 62-34 18 30-60 34Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m102 296 62 34 18-30-60-34Z"/>
      <rect data-team-accent="true" fill="#38bdf8" x="330" y="244" width="110" height="24" rx="10"/>
      <rect fill="#f6b73c" x="444" y="248" width="26" height="16" rx="8"/>`,
  },
  {
    id: 'mason',
    label: 'Mason',
    scale: 1.09,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M78 164 178 112 334 112 434 164 466 220 466 292 434 348 334 400 178 400 78 348 46 292 46 220Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="M112 188 194 148h124l82 40 28 48v40l-28 48-82 40H194l-82-40-28-48v-40Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="10" d="M164 184h176v144H164Z"/>
      <path fill="#18212b" stroke="#0b1118" stroke-width="9" d="M212 216h80v80h-80Z"/>
      <path fill="#a58b6c" stroke="#0b1118" stroke-width="10" d="M338 198h88v44h-46v28h46v44h-88Z"/>
      <rect data-team-accent="true" fill="#38bdf8" x="98" y="202" width="38" height="108" rx="10"/>
      <rect data-team-accent="true" fill="#38bdf8" x="176" y="154" width="144" height="26" rx="10"/>
      <rect data-team-accent="true" fill="#38bdf8" x="176" y="332" width="144" height="26" rx="10"/>
      <circle fill="#f6b73c" cx="252" cy="256" r="18"/>`,
  },
  {
    id: 'sunder',
    label: 'Sunder',
    scale: 1.07,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="m54 256 74-54-22-84 92 40 58-86 58 86 92-40-22 84 86 54-86 54 22 84-92-40-58 86-58-86-92 40 22-84Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="m116 256 54-38-14-48 58 24 42-62 42 62 58-24-14 48 54 38-54 38 14 48-58-24-42 62-42-62-58 24 14-48Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="10" d="m184 256 88-68 102 68-102 68Z"/>
      <path fill="#101820" stroke="#0b1118" stroke-width="9" d="m276 212 100 44-100 44 28-44Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m130 204 62-50 22 30-62 50Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m130 308 62 50 22-30-62-50Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m362 224 82 32-82 32 20-32Z"/>
      <circle fill="none" stroke="#f6b73c" stroke-width="16" cx="286" cy="256" r="38"/>`,
  },
  {
    id: 'repulsor',
    label: 'Repulsor',
    scale: 1.08,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M94 158 190 88h132l96 70 62 98-62 98-96 70H190l-96-70-62-98Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="m132 178 76-50h96l76 50 46 78-46 78-76 50h-96l-76-50-46-78Z"/>
      <circle fill="#8a6547" stroke="#0b1118" stroke-width="12" cx="278" cy="256" r="112"/>
      <circle fill="#18212b" stroke="#0b1118" stroke-width="10" cx="278" cy="256" r="72"/>
      <circle fill="#556273" stroke="#0b1118" stroke-width="8" cx="278" cy="256" r="34"/>
      <path data-team-accent="true" fill="#38bdf8" d="m278 146 34 6-8 48-26-6Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m278 366 34-6-8-48-26 6Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m384 224 70 32-70 32 18-32Z"/>
      <circle fill="#f6b73c" cx="278" cy="256" r="16"/>`,
  },
  {
    id: 'veil',
    label: 'Veil',
    scale: 1.06,
    body: `
      <path fill="#111820" stroke="#0b1118" stroke-width="18" d="M68 256 148 188 224 174 260 102 346 122 388 190 458 216 486 256 458 296 388 322 346 390 260 410 224 338 148 324Z"/>
      <path fill="#2b3744" stroke="#0b1118" stroke-width="12" d="M122 256 176 216 242 210 276 152 330 166 360 216 424 232 448 256 424 280 360 296 330 346 276 360 242 302 176 296Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="10" d="m202 256 66-70 92 22 38 48-38 48-92 22Z"/>
      <ellipse fill="#101820" stroke="#0b1118" stroke-width="9" cx="292" cy="216" rx="42" ry="28"/>
      <ellipse fill="#101820" stroke="#0b1118" stroke-width="9" cx="292" cy="296" rx="42" ry="28"/>
      <path data-team-accent="true" fill="#38bdf8" d="m136 206 64-48 18 32-62 48Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m136 306 64 48 18-32-62-48Z"/>
      <path data-team-accent="true" fill="#38bdf8" d="m392 226 62 30-62 30 18-30Z"/>
      <ellipse fill="#f6b73c" cx="292" cy="216" rx="16" ry="10"/>
      <ellipse fill="#f6b73c" cx="292" cy="296" rx="16" ry="10"/>`,
  },
  {
    id: 'nest',
    label: 'Nest',
    scale: 1.08,
    body: `
      <path fill="#18212b" stroke="#0b1118" stroke-width="18" d="M72 198 154 126 278 116 382 154 446 214 446 298 382 358 278 396 154 386 72 314Z"/>
      <path fill="#344252" stroke="#0b1118" stroke-width="12" d="M118 216 176 166 272 156 354 186 400 230v52l-46 44-82 30-96-10-58-50Z"/>
      <path fill="#8a6547" stroke="#0b1118" stroke-width="10" d="M174 212 250 172 330 206 348 276 292 336 208 328 160 270Z"/>
      <circle fill="#101820" stroke="#0b1118" stroke-width="9" cx="230" cy="232" r="30"/>
      <circle fill="#101820" stroke="#0b1118" stroke-width="9" cx="298" cy="260" r="30"/>
      <circle fill="#101820" stroke="#0b1118" stroke-width="9" cx="236" cy="302" r="30"/>
      <path fill="none" stroke="#a58b6c" stroke-width="20" stroke-linecap="round" d="M342 218 424 178M350 256h88M342 294l82 40"/>
      <rect data-team-accent="true" fill="#38bdf8" x="98" y="214" width="36" height="84" rx="14"/>
      <circle data-team-accent="true" fill="#38bdf8" cx="230" cy="232" r="13"/>
      <circle data-team-accent="true" fill="#38bdf8" cx="236" cy="302" r="13"/>
      <circle fill="#f6b73c" cx="298" cy="260" r="13"/>`,
  },
];

function sprite(body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" role="img">
  <g stroke-linejoin="round" stroke-linecap="round">
${body.trimEnd()}
  </g>
</svg>
`;
}

for (const look of looks) {
  const id = `arc-${look.id}`;
  const root = join(classRoot, id);
  await mkdir(root, { recursive: true });
  await writeFile(join(root, 'sprite.svg'), sprite(look.body), 'utf8');
  await writeFile(
    join(root, 'look.json'),
    `${JSON.stringify(
      {
        id,
        label: `Arc Relay ${look.label}`,
        sprite: 'sprite.svg',
        suggestedAccent: '#38bdf8',
        defaultProjectile: 'arc-pulse',
        classId: look.id,
        locomotionCue: 'low-hover',
        scale: look.scale,
      },
      null,
      2,
    )}\n`,
    'utf8',
  );
}

const projectileDirectory = join(projectileRoot, 'arc-pulse');
await mkdir(projectileDirectory, { recursive: true });
await writeFile(
  join(projectileDirectory, 'look.json'),
  `${JSON.stringify(
    {
      id: 'arc-pulse',
      label: 'Arc Pulse',
      sprite: 'sprite.svg',
      scale: 0.48,
    },
    null,
    2,
  )}\n`,
  'utf8',
);
await writeFile(
  join(projectileDirectory, 'sprite.svg'),
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" role="img">
  <path fill="#fff" fill-opacity="0.22" d="M30 128 90 70l126 58L90 186Z"/>
  <path fill="#fff" fill-opacity="0.58" d="m62 128 54-38 104 38-104 38Z"/>
  <path fill="#fff" d="m96 128 48-20 76 20-76 20Z"/>
  <circle fill="#fff" cx="142" cy="128" r="18"/>
</svg>
`,
  'utf8',
);
