import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import {
  mkdir,
  readFile,
  readdir,
  rm,
  writeFile,
} from "node:fs/promises";
import { join, relative, resolve } from "node:path";
import test from "node:test";

import { taskDirectorySegment } from "./run-meshy-smart-geometry.mjs";
import {
  artifactDefinitions,
  assertCreditCeiling,
  buildRequestPayload,
  buildSanitizedRequest,
  main,
  parseArguments,
  retextureSettings,
  validateTextStylePrompt,
} from "./run-meshy-retexture.mjs";

const PNG_SIGNATURE = Buffer.from([
  0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
]);

function pngHeader(width = 1_254, height = 1_254) {
  const buffer = Buffer.alloc(24);
  PNG_SIGNATURE.copy(buffer, 0);
  buffer.write("IHDR", 12, "ascii");
  buffer.writeUInt32BE(width, 16);
  buffer.writeUInt32BE(height, 20);
  return buffer;
}

function glbBuffer(length = 12) {
  const buffer = Buffer.alloc(length);
  buffer.write("glTF", 0, "ascii");
  buffer.writeUInt32LE(2, 4);
  buffer.writeUInt32LE(length, 8);
  return buffer;
}

function jsonResponse(value, status = 200) {
  return new Response(JSON.stringify(value), {
    status,
    headers: { "content-type": "application/json" },
  });
}

test("requires explicit paid intent and exactly one supported style source", () => {
  const text = parseArguments([
    "--input-task-id",
    "accepted-smart-task",
    "--look-id",
    "aegis-tortoise",
    "--purpose",
    "accepted geometry texture gate",
    "--text-style-prompt",
    "stylized matte game asset",
  ]);
  assert.equal(text.execute, false);
  assert.equal(text.inputTaskId, "accepted-smart-task");
  assert.equal(text.textStylePrompt, "stylized matte game asset");

  const image = parseArguments([
    "--input-task-id",
    "accepted-smart-task",
    "--look-id",
    "aegis-tortoise",
    "--purpose",
    "accepted geometry texture gate",
    "--image-style-url",
    "art/class-models/concept-targets/aegis/material.png",
    "--execute",
  ]);
  assert.equal(image.execute, true);
  assert.equal(
    image.imageStylePath,
    "art/class-models/concept-targets/aegis/material.png",
  );

  assert.throws(
    () =>
      parseArguments([
        "--input-task-id",
        "accepted-smart-task",
        "--look-id",
        "aegis-tortoise",
        "--purpose",
        "texture gate",
        "--image-style-url",
        "material.png",
        "--text-style-prompt",
        "matte",
      ]),
    /exactly one/u,
  );
  assert.throws(
    () =>
      parseArguments([
        "--input-task-id",
        "accepted-smart-task",
        "--look-id",
        "aegis-tortoise",
        "--purpose",
        "texture gate",
        "--image-style-url",
        "https://example.com/material.png",
      ]),
    /repository-local/u,
  );
  assert.throws(
    () =>
      parseArguments([
        "--resume",
        "retexture-task",
        "--input-task-id",
        "source-task",
        "--look-id",
        "aegis-tortoise",
      ]),
    /cannot be combined/u,
  );
});

test("pins the Meshy 6 game-texture request and redacts local data URIs", () => {
  const settings = retextureSettings();
  assert.deepEqual(settings, {
    ai_model: "meshy-6",
    enable_original_uv: true,
    enable_pbr: true,
    texture_resolution: "2k",
    remove_lighting: true,
    target_formats: ["glb"],
  });

  const payload = buildRequestPayload({
    inputTaskId: "accepted-smart-task",
    style: {
      kind: "image",
      mimeType: "image/png",
      buffer: pngHeader(),
    },
    settings,
  });
  assert.match(payload.image_style_url, /^data:image\/png;base64,/u);
  assert.equal(Object.hasOwn(payload, "text_style_prompt"), false);
  assert.deepEqual(buildSanitizedRequest(payload), {
    input_task_id: "accepted-smart-task",
    ...settings,
    image_style_url: "<redacted-data-uri>",
  });

  const textPayload = buildRequestPayload({
    inputTaskId: "accepted-smart-task",
    style: { kind: "text", prompt: "broad matte fortress materials" },
    settings,
  });
  assert.equal(Object.hasOwn(textPayload, "image_style_url"), false);
  assert.equal(
    textPayload.text_style_prompt,
    "broad matte fortress materials",
  );
});

test("enforces the documented prompt length and 10-credit ceiling", () => {
  assert.equal(validateTextStylePrompt("x".repeat(600)).length, 600);
  assert.throws(() => validateTextStylePrompt("x".repeat(601)), /1–600/u);
  assert.equal(assertCreditCeiling({ consumed_credits: 10 }), 10);
  assert.equal(assertCreditCeiling({ consumed_credits: 0 }), 0);
  assert.throws(
    () => assertCreditCeiling({ consumed_credits: 11 }),
    /0 through the 10-credit ceiling/u,
  );
  assert.throws(
    () => assertCreditCeiling({ consumed_credits: -1 }),
    /0 through the 10-credit ceiling/u,
  );
  assert.throws(
    () => assertCreditCeiling({}),
    /cannot be verified/u,
  );
});

test("plans the GLB, preview, and every documented PBR texture safely", () => {
  const definitions = artifactDefinitions({
    id: "retexture-task",
    model_urls: {
      glb: "https://assets.meshy.ai/account/tasks/id/output/model.glb",
    },
    thumbnail_url:
      "https://assets.meshy.ai/account/tasks/id/output/preview.png",
    texture_urls: [
      {
        base_color:
          "https://assets.meshy.ai/account/tasks/id/output/base.png",
        metallic:
          "https://assets.meshy.ai/account/tasks/id/output/metallic.png",
        roughness:
          "https://assets.meshy.ai/account/tasks/id/output/roughness.png",
        normal:
          "https://assets.meshy.ai/account/tasks/id/output/normal.png",
        emission:
          "https://assets.meshy.ai/account/tasks/id/output/emission.png",
      },
    ],
  });
  assert.deepEqual(
    definitions.map((definition) => definition.file),
    [
      "raw-retextured-model.glb",
      "provider-preview.png",
      "texture-0-base-color.png",
      "texture-0-metallic.png",
      "texture-0-roughness.png",
      "texture-0-normal.png",
      "texture-0-emission.png",
    ],
  );
});

test("executes one mocked paid POST and archives resumable sanitized provenance", async () => {
  const repositoryRoot = resolve(import.meta.dirname, "../..");
  const nonce = randomUUID();
  const conceptDirectory = join(
    repositoryRoot,
    "art/class-models/concept-targets",
    `.retexture-runner-test-${nonce}`,
  );
  const outputRootRelative =
    `art/class-models/provider-runs/.retexture-runner-test-${nonce}`;
  const outputRoot = join(repositoryRoot, outputRootRelative);
  const imagePath = join(conceptDirectory, "material.png");
  const inputTaskId = "accepted/source?opaque";
  const retextureTaskId = "retexture/result?opaque";
  const calls = [];
  const imageBytes = pngHeader();
  const modelBytes = glbBuffer();

  await mkdir(conceptDirectory, { recursive: true });
  await writeFile(imagePath, imageBytes);

  const fetchImpl = async (url, init = {}) => {
    const method = init.method ?? "GET";
    calls.push({ url: String(url), method, body: init.body });
    if (String(url).endsWith("/balance")) {
      const balanceCalls = calls.filter((call) =>
        call.url.endsWith("/balance"),
      ).length;
      return jsonResponse({ balance: balanceCalls === 1 ? 100 : 90 });
    }
    if (String(url).endsWith("/retexture") && method === "POST") {
      return jsonResponse({ result: retextureTaskId });
    }
    if (
      String(url).endsWith(
        `/retexture/${encodeURIComponent(retextureTaskId)}`,
      )
    ) {
      const base =
        "https://assets.meshy.ai/account/tasks/retexture/output";
      return jsonResponse({
        id: retextureTaskId,
        type: "retexture",
        status: "SUCCEEDED",
        progress: 100,
        consumed_credits: 10,
        model_urls: { glb: `${base}/model.glb?Signature=secret` },
        thumbnail_url: `${base}/preview.png?Signature=secret`,
        texture_urls: [
          {
            base_color: `${base}/base.png?Signature=secret`,
            metallic: `${base}/metallic.png?Signature=secret`,
            roughness: `${base}/roughness.png?Signature=secret`,
            normal: `${base}/normal.png?Signature=secret`,
            emission: `${base}/emission.png?Signature=secret`,
          },
        ],
      });
    }
    if (String(url).startsWith("https://assets.meshy.ai/")) {
      return new Response(
        String(url).includes("model.glb") ? modelBytes : imageBytes,
        {
          status: 200,
          headers: { "content-type": "application/octet-stream" },
        },
      );
    }
    throw new Error(`Unexpected mock request: ${method} ${url}`);
  };

  try {
    const record = await main(
      [
        "--input-task-id",
        inputTaskId,
        "--look-id",
        "runner-test",
        "--purpose",
        "offline mocked retexture gate",
        "--image-style-url",
        relative(repositoryRoot, imagePath),
        "--output-root",
        outputRootRelative,
        "--execute",
      ],
      {
        fetchImpl,
        readApiKeyImpl: () => "test-key-that-must-not-be-written",
      },
    );

    const posts = calls.filter((call) => call.method === "POST");
    assert.equal(posts.length, 1);
    const submitted = JSON.parse(posts[0].body);
    assert.equal(submitted.input_task_id, inputTaskId);
    assert.match(submitted.image_style_url, /^data:image\/png;base64,/u);
    assert.deepEqual(
      Object.fromEntries(
        Object.keys(retextureSettings()).map((key) => [
          key,
          submitted[key],
        ]),
      ),
      retextureSettings(),
    );

    assert.equal(record.balanceBefore, 100);
    assert.equal(record.balanceAfter, 90);
    assert.equal(record.consumedCredits, 10);
    assert.equal(record.creditCeilingVerified, true);
    assert.equal(record.observedBalanceDelta, 10);
    assert.equal(record.artifacts.length, 7);

    const runDirectory = join(
      outputRoot,
      "runner-test",
      taskDirectorySegment(retextureTaskId),
    );
    const requestRecordText = await readFile(
      join(runDirectory, "request-record.json"),
      "utf8",
    );
    const taskRecordText = await readFile(
      join(runDirectory, "task.sanitized.json"),
      "utf8",
    );
    assert.equal(requestRecordText.includes("test-key-that-must-not-be-written"), false);
    assert.equal(requestRecordText.includes(";base64,"), false);
    assert.equal(taskRecordText.includes("Signature=secret"), false);
    assert.match(requestRecordText, /<redacted-data-uri>/u);
    assert.deepEqual(
      (await readdir(runDirectory)).sort(),
      [
        "provider-preview.png",
        "raw-retextured-model.glb",
        "request-intent.json",
        "request-record.json",
        "task.sanitized.json",
        "texture-0-base-color.png",
        "texture-0-emission.png",
        "texture-0-metallic.png",
        "texture-0-normal.png",
        "texture-0-roughness.png",
      ],
    );
    await assert.rejects(
      readFile(join(outputRoot, ".meshy-paid-call.lock", "owner.json")),
      /ENOENT/u,
    );
  } finally {
    await rm(conceptDirectory, { recursive: true, force: true });
    await rm(outputRoot, { recursive: true, force: true });
  }
});

test("never retries an ambiguous paid POST and retains its reconciliation lock", async () => {
  const repositoryRoot = resolve(import.meta.dirname, "../..");
  const nonce = randomUUID();
  const outputRootRelative =
    `art/class-models/provider-runs/.retexture-runner-unknown-${nonce}`;
  const outputRoot = join(repositoryRoot, outputRootRelative);
  let postCount = 0;
  const fetchImpl = async (url, init = {}) => {
    if (String(url).endsWith("/balance")) {
      return jsonResponse({ balance: 100 });
    }
    if (String(url).endsWith("/retexture") && init.method === "POST") {
      postCount += 1;
      throw new TypeError("mock connection reset after send");
    }
    throw new Error(`Unexpected mock request: ${init.method ?? "GET"} ${url}`);
  };

  try {
    await assert.rejects(
      main(
        [
          "--input-task-id",
          "accepted-smart-task",
          "--look-id",
          "runner-test",
          "--purpose",
          "offline ambiguous submission proof",
          "--text-style-prompt",
          "stylized matte fortress game asset",
          "--output-root",
          outputRootRelative,
          "--execute",
        ],
        {
          fetchImpl,
          readApiKeyImpl: () => "test-key-that-must-not-be-written",
        },
      ),
      /Submission outcome is unknown/u,
    );
    assert.equal(postCount, 1);

    const owner = JSON.parse(
      await readFile(
        join(outputRoot, ".meshy-paid-call.lock", "owner.json"),
        "utf8",
      ),
    );
    const intent = JSON.parse(
      await readFile(join(owner.intentDirectory, "request-intent.json"), "utf8"),
    );
    assert.equal(intent.state, "SUBMISSION_UNKNOWN");
    assert.equal(
      JSON.stringify(intent).includes("test-key-that-must-not-be-written"),
      false,
    );
  } finally {
    await rm(outputRoot, { recursive: true, force: true });
  }
});

test("contains no provider task-deletion request", async () => {
  const source = await readFile(
    join(import.meta.dirname, "run-meshy-retexture.mjs"),
    "utf8",
  );
  assert.doesNotMatch(source, /method:\s*["']DELETE["']/u);
});
