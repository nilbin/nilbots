import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { mkdtemp, readFile, readdir, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import test from "node:test";

import {
  ArtifactValidationError,
  MeshyHttpError,
  atomicWriteJson,
  classifyTaskStatus,
  downloadArtifact,
  inspectInputImage,
  isPathWithin,
  isSafeRetryable,
  main,
  parseArguments,
  sanitizeTask,
  sha256,
  taskDirectorySegment,
  validateArtifactBuffer,
  validateArtifactUrl,
} from "./run-meshy-smart-geometry.mjs";

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

test("parses create and resume modes without weakening paid-call intent", () => {
  const create = parseArguments([
    "--input",
    "art/class-models/concept-targets/example/input.png",
    "--look-id",
    "aegis-tortoise",
    "--purpose",
    "geometry gate",
    "--target-polycount",
    "2500",
  ]);
  assert.equal(create.targetPolycount, 2_500);
  assert.equal(create.execute, false);
  assert.equal(create.resumeTaskId, undefined);

  const resume = parseArguments([
    "--resume",
    "opaque-task-id",
    "--look-id",
    "aegis-tortoise",
    "--execute",
  ]);
  assert.equal(resume.resumeTaskId, "opaque-task-id");
  assert.equal(resume.execute, true);

  assert.throws(
    () =>
      parseArguments([
        "--resume",
        "task",
        "--input",
        "input.png",
        "--look-id",
        "aegis-tortoise",
      ]),
    /cannot be combined/u,
  );
  assert.throws(
    () =>
      parseArguments([
        "--input",
        "input.png",
        "--look-id",
        "aegis-tortoise",
        "--purpose",
        "geometry gate",
        "--target-polycount",
        "4000junk",
      ]),
    /integer/u,
  );
});

test("validates image bytes and the Nilbots minimum source dimensions", () => {
  assert.deepEqual(inspectInputImage(pngHeader(), "input.png"), {
    mimeType: "image/png",
    width: 1_254,
    height: 1_254,
  });
  assert.throws(
    () => inspectInputImage(pngHeader(512, 512), "input.png"),
    /1040/u,
  );
  assert.throws(
    () => inspectInputImage(Buffer.from("not a png"), "input.png"),
    /valid PNG header/u,
  );
  assert.throws(
    () => inspectInputImage(pngHeader(), "input.jpg"),
    /valid JPEG header/u,
  );
});

test("keeps task IDs opaque without allowing filesystem traversal", () => {
  const ordinary = "019fb418-41b0-7a1b-a888-1e2f4756c64b";
  assert.equal(taskDirectorySegment(ordinary), ordinary);

  const hostile = "../../outside";
  const segment = taskDirectorySegment(hostile);
  assert.match(segment, /^task-[0-9a-f]{32}$/u);
  assert.equal(segment.includes("/"), false);
  assert.equal(isPathWithin("/safe/root", "/safe/root/child"), true);
  assert.equal(isPathWithin("/safe/root", "/safe/other"), false);
});

test("sanitizes data URIs, secrets, signed URLs, and provider account paths", () => {
  const sanitized = sanitizeTask({
    authorization: "Bearer secret",
    api_key: "secret",
    image_url: "data:image/png;base64,AAAA",
    model_urls: {
      glb:
        "https://assets.meshy.ai/account-id/tasks/task-id/output/model.glb" +
        "?Expires=123&Signature=secret",
    },
  });
  assert.equal(sanitized.authorization, "<redacted>");
  assert.equal(sanitized.api_key, "<redacted>");
  assert.equal(sanitized.image_url, "<redacted-data-uri>");
  assert.equal(
    sanitized.model_urls.glb,
    "https://assets.meshy.ai/<account>/tasks/task-id/output/model.glb",
  );
});

test("accepts only HTTPS artifacts on the pinned Meshy asset host", () => {
  assert.equal(
    validateArtifactUrl(
      "https://assets.meshy.ai/account/tasks/id/output/model.glb?Expires=1",
    ).hostname,
    "assets.meshy.ai",
  );
  assert.throws(
    () => validateArtifactUrl("http://assets.meshy.ai/model.glb"),
    /HTTPS/u,
  );
  assert.throws(
    () => validateArtifactUrl("https://example.com/model.glb"),
    /Unapproved/u,
  );
  assert.throws(
    () => validateArtifactUrl("https://user:pass@assets.meshy.ai/model.glb"),
    /user information/u,
  );
});

test("validates GLB version and declared length plus PNG signatures", () => {
  validateArtifactBuffer("glb", glbBuffer());
  validateArtifactBuffer("png", pngHeader());

  const wrongLength = glbBuffer();
  wrongLength.writeUInt32LE(99, 8);
  assert.throws(
    () => validateArtifactBuffer("glb", wrongLength),
    /declared 99 bytes/u,
  );

  const wrongVersion = glbBuffer();
  wrongVersion.writeUInt32LE(1, 4);
  assert.throws(
    () => validateArtifactBuffer("glb", wrongVersion),
    /version 2/u,
  );
});

test("classifies only safe read retries as retryable", () => {
  assert.equal(isSafeRetryable(new MeshyHttpError("rate", 429)), true);
  assert.equal(isSafeRetryable(new MeshyHttpError("server", 503)), true);
  assert.equal(isSafeRetryable(new MeshyHttpError("bad request", 400)), false);
  assert.equal(
    isSafeRetryable(new ArtifactValidationError("bad artifact")),
    false,
  );
});

test("recognizes only the five documented Meshy task statuses", () => {
  assert.equal(classifyTaskStatus("PENDING"), "active");
  assert.equal(classifyTaskStatus("IN_PROGRESS"), "active");
  assert.equal(classifyTaskStatus("SUCCEEDED"), "terminal");
  assert.equal(classifyTaskStatus("FAILED"), "terminal");
  assert.equal(classifyTaskStatus("CANCELED"), "terminal");
  assert.throws(() => classifyTaskStatus("EXPIRED"), /undocumented/u);
});

test("atomically writes JSON without leaving temporary files", async () => {
  const directory = await mkdtemp(join(tmpdir(), "nilbots-meshy-json-"));
  try {
    const output = join(directory, "record.json");
    await atomicWriteJson(output, { state: "PREPARED" });
    await atomicWriteJson(output, { state: "SUBMITTED", taskId: "task" });
    assert.deepEqual(JSON.parse(await readFile(output, "utf8")), {
      state: "SUBMITTED",
      taskId: "task",
    });
    assert.deepEqual(await readdir(directory), ["record.json"]);
  } finally {
    await rm(directory, { recursive: true });
  }
});

test("downloads a validated artifact atomically and reuses the archived file", async () => {
  const directory = await mkdtemp(join(tmpdir(), "nilbots-meshy-download-"));
  const bytes = glbBuffer();
  const outputPath = join(directory, "raw-model.glb");
  let fetchCount = 0;
  const fetchImpl = async () => {
    fetchCount += 1;
    return new Response(bytes, {
      status: 200,
      headers: {
        "content-length": String(bytes.length),
        "content-type": "application/octet-stream",
      },
    });
  };

  try {
    const artifact = await downloadArtifact({
      url: "https://assets.meshy.ai/account/tasks/id/output/model.glb?Expires=1",
      outputPath,
      kind: "glb",
      maxBytes: 1_024,
      fetchImpl,
    });
    assert.equal(artifact.bytes, bytes.length);
    assert.equal(artifact.sha256, sha256(bytes));
    assert.deepEqual(await readFile(outputPath), bytes);
    assert.equal(fetchCount, 1);

    const existing = await downloadArtifact({
      url: "https://assets.meshy.ai/account/tasks/id/output/model.glb?Expires=2",
      outputPath,
      kind: "glb",
      maxBytes: 1_024,
      fetchImpl: async () => {
        throw new Error("existing artifact should not fetch");
      },
    });
    assert.equal(existing.sha256, artifact.sha256);
    assert.equal(fetchCount, 1);
    assert.deepEqual(await readdir(directory), ["raw-model.glb"]);
    await assert.rejects(
      downloadArtifact({
        url: "https://assets.meshy.ai/account/tasks/id/output/model.glb",
        outputPath,
        kind: "glb",
        maxBytes: 1,
        fetchImpl,
      }),
      /exceeds 1 bytes/u,
    );
  } finally {
    await rm(directory, { recursive: true });
  }
});

test("rejects invalid artifact bytes without publishing a final or partial file", async () => {
  const directory = await mkdtemp(join(tmpdir(), "nilbots-meshy-reject-"));
  const outputPath = join(directory, "raw-model.glb");
  try {
    await assert.rejects(
      downloadArtifact({
        url: "https://assets.meshy.ai/account/tasks/id/output/model.glb",
        outputPath,
        kind: "glb",
        maxBytes: 1_024,
        fetchImpl: async () =>
          new Response(Buffer.from("<html>not a model</html>"), {
            status: 200,
          }),
      }),
      /not a GLB/u,
    );
    assert.deepEqual(await readdir(directory), []);
  } finally {
    await rm(directory, { recursive: true });
  }
});

test("rejects redirects away from the pinned provider host", async () => {
  const directory = await mkdtemp(join(tmpdir(), "nilbots-meshy-redirect-"));
  try {
    await assert.rejects(
      downloadArtifact({
        url: "https://assets.meshy.ai/account/tasks/id/output/model.glb",
        outputPath: join(directory, "raw-model.glb"),
        kind: "glb",
        maxBytes: 1_024,
        fetchImpl: async () =>
          new Response(null, {
            status: 302,
            headers: { location: "https://example.com/model.glb" },
          }),
      }),
      /Unapproved/u,
    );
    assert.deepEqual(await readdir(directory), []);
  } finally {
    await rm(directory, { recursive: true });
  }
});

test("archives a complete mocked Smart task without touching the provider", async () => {
  const repositoryRoot = resolve(import.meta.dirname, "../..");
  const outputRelative =
    `art/class-models/provider-runs/meshy-runner-test-${randomUUID()}`;
  const outputRoot = resolve(repositoryRoot, outputRelative);
  const taskId = "mock-smart-task";
  const lookId = "mock-aegis";
  const glb = glbBuffer();
  const png = pngHeader();
  let balanceCalls = 0;

  const jsonResponse = (value, status = 200) =>
    new Response(JSON.stringify(value), {
      status,
      headers: { "content-type": "application/json" },
    });
  const fetchImpl = async (input, init = {}) => {
    const url = String(input);
    if (url.endsWith("/openapi/v1/balance")) {
      balanceCalls += 1;
      return jsonResponse({ balance: balanceCalls === 1 ? 100 : 95 });
    }
    if (
      url.endsWith("/openapi/v1/image-to-3d") &&
      init.method === "POST"
    ) {
      assert.equal(init.headers.Authorization, "Bearer offline-test-key");
      const request = JSON.parse(init.body);
      assert.equal(request.model_type, "smart-topology");
      assert.equal(request.ai_model, "meshy-t2");
      assert.equal(request.should_texture, false);
      assert.match(request.image_url, /^data:image\/png;base64,/u);
      return jsonResponse({ result: taskId }, 202);
    }
    if (url.endsWith(`/openapi/v1/image-to-3d/${taskId}`)) {
      return jsonResponse({
        id: taskId,
        type: "image-to-3d",
        status: "SUCCEEDED",
        progress: 100,
        consumed_credits: 5,
        model_urls: {
          glb:
            `https://assets.meshy.ai/account-id/tasks/${taskId}/output/` +
            "model.glb?Expires=1",
        },
        thumbnail_url:
          `https://assets.meshy.ai/account-id/tasks/${taskId}/output/` +
          "preview.png?Expires=1",
        alpha_thumbnail_url:
          `https://assets.meshy.ai/account-id/tasks/${taskId}/output/` +
          "preview_alpha.png?Expires=1",
      });
    }
    if (url.includes("/output/model.glb")) {
      return new Response(glb, {
        status: 200,
        headers: {
          "content-length": String(glb.length),
          "content-type": "application/octet-stream",
        },
      });
    }
    if (url.includes("/output/preview")) {
      return new Response(png, {
        status: 200,
        headers: {
          "content-length": String(png.length),
          "content-type": "image/png",
        },
      });
    }
    throw new Error(`Unexpected offline URL: ${url}`);
  };

  try {
    const record = await main(
      [
        "--input",
        "art/class-models/concept-targets/meshy-aegis-smart-v1/" +
          "03-front-three-quarter.png",
        "--look-id",
        lookId,
        "--purpose",
        "offline integration test",
        "--output-root",
        outputRelative,
        "--execute",
      ],
      {
        fetchImpl,
        credentialReader: () => "offline-test-key",
      },
    );
    assert.equal(record.state, "ARCHIVED");
    assert.equal(record.consumedCredits, 5);
    assert.equal(record.observedBalanceDelta, 5);
    assert.equal(record.artifacts.length, 3);
    assert.equal(record.credentialStore.valueRecorded, false);
    assert.equal("account" in record.credentialStore, false);

    const runDirectory = join(outputRoot, lookId, taskId);
    const persistedRecord = JSON.parse(
      await readFile(join(runDirectory, "request-record.json"), "utf8"),
    );
    const persistedTask = JSON.parse(
      await readFile(join(runDirectory, "task.sanitized.json"), "utf8"),
    );
    const persistedIntent = JSON.parse(
      await readFile(join(runDirectory, "request-intent.json"), "utf8"),
    );
    assert.equal(persistedRecord.state, "ARCHIVED");
    assert.equal(persistedIntent.state, "ARCHIVED");
    assert.equal(
      persistedTask.model_urls.glb,
      `https://assets.meshy.ai/<account>/tasks/${taskId}/output/model.glb`,
    );
    assert.deepEqual(await readFile(join(runDirectory, "raw-model.glb")), glb);
    assert.deepEqual(
      await readdir(join(outputRoot, lookId, ".attempts")),
      [],
    );
    assert.equal(
      (await readdir(outputRoot)).includes(".meshy-paid-call.lock"),
      false,
    );
  } finally {
    await rm(outputRoot, { recursive: true });
  }
});

test("retains intent and paid-call lock after an ambiguous mocked POST", async () => {
  const repositoryRoot = resolve(import.meta.dirname, "../..");
  const outputRelative =
    `art/class-models/provider-runs/meshy-runner-unknown-${randomUUID()}`;
  const outputRoot = resolve(repositoryRoot, outputRelative);
  const lookId = "mock-aegis";

  const fetchImpl = async (input, init = {}) => {
    const url = String(input);
    if (url.endsWith("/openapi/v1/balance")) {
      return new Response(JSON.stringify({ balance: 100 }), {
        status: 200,
        headers: { "content-type": "application/json" },
      });
    }
    if (
      url.endsWith("/openapi/v1/image-to-3d") &&
      init.method === "POST"
    ) {
      const error = new TypeError("mock connection reset");
      error.code = "ECONNRESET";
      throw error;
    }
    throw new Error(`Unexpected offline URL: ${url}`);
  };

  try {
    await assert.rejects(
      main(
        [
          "--input",
          "art/class-models/concept-targets/meshy-aegis-smart-v1/" +
            "03-front-three-quarter.png",
          "--look-id",
          lookId,
          "--purpose",
          "offline ambiguous-submit test",
          "--output-root",
          outputRelative,
          "--execute",
        ],
        {
          fetchImpl,
          credentialReader: () => "offline-test-key",
        },
      ),
      /outcome is unknown/u,
    );

    const attemptsRoot = join(outputRoot, lookId, ".attempts");
    const attempts = await readdir(attemptsRoot);
    assert.equal(attempts.length, 1);
    const intent = JSON.parse(
      await readFile(
        join(attemptsRoot, attempts[0], "request-intent.json"),
        "utf8",
      ),
    );
    assert.equal(intent.state, "SUBMISSION_UNKNOWN");
    assert.equal(
      (await readdir(outputRoot)).includes(".meshy-paid-call.lock"),
      true,
    );
  } finally {
    await rm(outputRoot, { recursive: true });
  }
});
