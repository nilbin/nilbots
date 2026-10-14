#!/usr/bin/env node

import { randomUUID } from "node:crypto";
import { execFileSync } from "node:child_process";
import {
  mkdir,
  open,
  readFile,
  realpath,
  rename,
  rmdir,
  stat,
  unlink,
} from "node:fs/promises";
import { userInfo } from "node:os";
import {
  dirname,
  isAbsolute,
  join,
  relative,
  resolve,
  sep,
} from "node:path";
import { pathToFileURL } from "node:url";

import {
  MeshyHttpError,
  atomicWriteJson,
  classifyTaskStatus,
  downloadArtifact,
  inspectInputImage,
  isSafeRetryable,
  sanitizeTask,
  sha256,
  taskDirectorySegment,
  validateTaskId,
} from "./run-meshy-smart-geometry.mjs";

const API_ROOT = "https://api.meshy.ai/openapi/v1";
const RETEXTURE_CREDIT_CEILINGS = new Map([
  ["2k", 10],
  ["4k", 10],
  ["8k", 15],
]);
const DEFAULT_POLL_INTERVAL_MS = 5_000;
const DEFAULT_POLL_TIMEOUT_MS = 30 * 60_000;
const API_REQUEST_TIMEOUT_MS = 30_000;
const MAX_JSON_BYTES = 2 * 1024 * 1024;
const MAX_STYLE_IMAGE_BYTES = 20 * 1024 * 1024;
export const MAX_INPUT_MODEL_BYTES = 32 * 1024 * 1024;
const MAX_GLB_BYTES = 512 * 1024 * 1024;
const MAX_IMAGE_BYTES = 128 * 1024 * 1024;
const MAX_TEXTURE_SETS = 64;
const MAX_SAFE_ATTEMPTS = 5;
const TEXT_PROMPT_MAX_CHARACTERS = 600;
const DEFINITE_SUBMISSION_REJECTIONS = new Set([
  400, 401, 402, 403, 404, 429,
]);
const TEXTURE_FIELDS = new Map([
  ["base_color", "base-color"],
  ["metallic", "metallic"],
  ["roughness", "roughness"],
  ["normal", "normal"],
  ["emission", "emission"],
]);

class SubmissionUnknownError extends Error {
  constructor(message) {
    super(message);
    this.name = "SubmissionUnknownError";
  }
}

function usage() {
  return (
    "Create with image style: run-meshy-retexture.mjs " +
    "(--input-task-id <accepted-task-id> | --input-model <repo-glb>) " +
    "--look-id <id> " +
    "--purpose <text> --image-style-url <local-pinned-png> " +
    "[--texture-resolution 2k|4k|8k] [--output-root <path>] " +
    "[--keychain-service nilbots.meshy.api] --execute\n" +
    "Create with text style: run-meshy-retexture.mjs " +
    "(--input-task-id <accepted-task-id> | --input-model <repo-glb>) " +
    "--look-id <id> " +
    "--purpose <text> --text-style-prompt <text> " +
    "[--texture-resolution 2k|4k|8k] [--output-root <path>] " +
    "[--keychain-service nilbots.meshy.api] --execute\n" +
    "Resume: run-meshy-retexture.mjs --resume <retexture-task-id> " +
    "--look-id <id> [--purpose <text>] [--output-root <path>] " +
    "[--keychain-service nilbots.meshy.api] --execute"
  );
}

function takeArgumentValue(argv, state, argument) {
  state.index += 1;
  if (state.index >= argv.length) {
    throw new Error(`Missing value for ${argument}.\n${usage()}`);
  }
  return argv[state.index];
}

function validateLookId(value) {
  if (!value) {
    throw new Error(`--look-id is required.\n${usage()}`);
  }
  if (value.length > 128 || !/^[a-z0-9][a-z0-9-]*$/u.test(value)) {
    throw new Error("--look-id must contain lowercase letters, digits, or hyphens.");
  }
}

function validatePurpose(value, required) {
  if (!value) {
    if (required) {
      throw new Error(`--purpose is required.\n${usage()}`);
    }
    return;
  }
  if (value.length > 1_000 || /[\u0000\u007f]/u.test(value)) {
    throw new Error("--purpose must be at most 1000 characters without controls.");
  }
}

export function validateTextStylePrompt(value) {
  if (
    typeof value !== "string" ||
    value.trim().length === 0 ||
    value.length > TEXT_PROMPT_MAX_CHARACTERS ||
    /[\u0000\u007f]/u.test(value)
  ) {
    throw new Error(
      "--text-style-prompt must be 1–600 characters without NUL or DEL.",
    );
  }
  return value;
}

export function parseArguments(argv) {
  const options = {
    keychainService: "nilbots.meshy.api",
    outputRoot: "art/class-models/provider-runs/meshy",
    textureResolution: "2k",
    execute: false,
  };
  const state = { index: 0 };

  for (; state.index < argv.length; state.index += 1) {
    const argument = argv[state.index];
    switch (argument) {
      case "--input-task-id":
        options.inputTaskId = takeArgumentValue(argv, state, argument);
        break;
      case "--input-model":
        options.inputModelPath = takeArgumentValue(argv, state, argument);
        break;
      case "--look-id":
        options.lookId = takeArgumentValue(argv, state, argument);
        break;
      case "--purpose":
        options.purpose = takeArgumentValue(argv, state, argument);
        break;
      case "--image-style-url":
        options.imageStylePath = takeArgumentValue(argv, state, argument);
        break;
      case "--text-style-prompt":
        options.textStylePrompt = takeArgumentValue(argv, state, argument);
        break;
      case "--texture-resolution":
        options.textureResolution = takeArgumentValue(argv, state, argument);
        break;
      case "--output-root":
        options.outputRoot = takeArgumentValue(argv, state, argument);
        break;
      case "--keychain-service":
        options.keychainService = takeArgumentValue(argv, state, argument);
        break;
      case "--resume":
        options.resumeTaskId = takeArgumentValue(argv, state, argument);
        break;
      case "--execute":
        options.execute = true;
        break;
      default:
        throw new Error(`Unknown argument: ${argument}\n${usage()}`);
    }
  }

  validateLookId(options.lookId);
  validatePurpose(options.purpose, !options.resumeTaskId);
  if (!RETEXTURE_CREDIT_CEILINGS.has(options.textureResolution)) {
    throw new Error("--texture-resolution must be 2k, 4k, or 8k.");
  }
  if (!options.keychainService || /[\0\r\n]/u.test(options.keychainService)) {
    throw new Error("--keychain-service must be a non-empty single-line value.");
  }

  if (options.resumeTaskId) {
    validateTaskId(options.resumeTaskId);
    if (
      options.inputTaskId ||
      options.inputModelPath ||
      options.imageStylePath ||
      options.textStylePrompt
    ) {
      throw new Error(
        "--resume cannot be combined with model/task or style inputs.",
      );
    }
    return options;
  }

  const inputCount =
    Number(Boolean(options.inputTaskId)) +
    Number(Boolean(options.inputModelPath));
  if (inputCount !== 1) {
    throw new Error(
      "Choose exactly one of --input-task-id or --input-model.",
    );
  }
  if (options.inputTaskId) {
    validateTaskId(options.inputTaskId);
  }
  if (
    options.inputModelPath &&
    (isAbsolute(options.inputModelPath) ||
      /^https?:\/\//iu.test(options.inputModelPath) ||
      /^data:/iu.test(options.inputModelPath))
  ) {
    throw new Error(
      "--input-model must name a repository-relative GLB, not an absolute " +
        "path, URL, or caller-supplied data URI.",
    );
  }
  const styleCount =
    Number(Boolean(options.imageStylePath)) +
    Number(Boolean(options.textStylePrompt));
  if (styleCount !== 1) {
    throw new Error(
      "Choose exactly one of --image-style-url or --text-style-prompt.",
    );
  }
  if (options.textStylePrompt) {
    validateTextStylePrompt(options.textStylePrompt);
  }
  if (
    options.imageStylePath &&
    (/^https?:\/\//iu.test(options.imageStylePath) ||
      /^data:/iu.test(options.imageStylePath))
  ) {
    throw new Error(
      "--image-style-url must name a repository-local pinned PNG, not a URL " +
        "or caller-supplied data URI.",
    );
  }
  return options;
}

export function retextureSettings(textureResolution = "2k") {
  if (!RETEXTURE_CREDIT_CEILINGS.has(textureResolution)) {
    throw new Error("Texture resolution must be 2k, 4k, or 8k.");
  }
  return {
    ai_model: "meshy-6",
    enable_original_uv: true,
    enable_pbr: true,
    texture_resolution: textureResolution,
    remove_lighting: true,
    target_formats: ["glb"],
  };
}

export function retextureCreditCeiling(textureResolution = "2k") {
  const ceiling = RETEXTURE_CREDIT_CEILINGS.get(textureResolution);
  if (!ceiling) {
    throw new Error("Texture resolution must be 2k, 4k, or 8k.");
  }
  return ceiling;
}

export function inspectInputModel(buffer, label = "Meshy input model") {
  if (!Buffer.isBuffer(buffer)) {
    throw new Error(`${label} must be read as a Buffer.`);
  }
  if (buffer.length > MAX_INPUT_MODEL_BYTES) {
    throw new Error(
      `${label} exceeds the ${MAX_INPUT_MODEL_BYTES}-byte upload ceiling.`,
    );
  }
  if (
    buffer.length < 12 ||
    buffer.subarray(0, 4).toString("ascii") !== "glTF"
  ) {
    throw new Error(`${label} is not a GLB.`);
  }
  const version = buffer.readUInt32LE(4);
  if (version !== 2) {
    throw new Error(`${label} must be GLB version 2; received ${version}.`);
  }
  const declaredLength = buffer.readUInt32LE(8);
  if (declaredLength !== buffer.length) {
    throw new Error(
      `${label} declares ${declaredLength} bytes but contains ` +
        `${buffer.length}.`,
    );
  }
  return {
    mimeType: "application/octet-stream",
    version,
    declaredLength,
  };
}

export function buildRequestPayload({
  inputTaskId,
  inputModel,
  style,
  settings = retextureSettings(),
}) {
  const inputCount =
    Number(Boolean(inputTaskId)) + Number(Boolean(inputModel));
  if (inputCount !== 1) {
    throw new Error(
      "Retexture request must contain exactly one task or uploaded model.",
    );
  }
  if (settings?.enable_original_uv !== true) {
    throw new Error("Retexture requests for authored models must preserve UVs.");
  }
  const payload = { ...settings };
  if (inputTaskId) {
    payload.input_task_id = validateTaskId(inputTaskId);
  } else {
    inspectInputModel(inputModel.buffer);
    payload.model_url =
      "data:application/octet-stream;base64," +
      inputModel.buffer.toString("base64");
  }
  if (style.kind === "image") {
    if (
      style.mimeType !== "image/png" ||
      !Buffer.isBuffer(style.buffer) ||
      style.buffer.length === 0
    ) {
      throw new Error("Image style must be a non-empty validated PNG buffer.");
    }
    payload.image_style_url =
      `data:image/png;base64,${style.buffer.toString("base64")}`;
  } else if (style.kind === "text") {
    payload.text_style_prompt = validateTextStylePrompt(style.prompt);
  } else {
    throw new Error("Retexture style kind must be image or text.");
  }
  return payload;
}

export function buildSanitizedRequest(payload) {
  const sanitized = sanitizeTask(payload);
  if (
    Object.hasOwn(sanitized, "input_task_id") ===
    Object.hasOwn(sanitized, "model_url")
  ) {
    throw new Error(
      "Sanitized retexture request must contain exactly one model source.",
    );
  }
  if (
    Object.hasOwn(sanitized, "image_style_url") ===
    Object.hasOwn(sanitized, "text_style_prompt")
  ) {
    throw new Error("Sanitized retexture request must contain exactly one style.");
  }
  return sanitized;
}

function inputFingerprintRecord(input) {
  if (input.kind === "task") {
    return {
      kind: "task",
      taskId: input.taskId,
    };
  }
  return {
    kind: "model",
    sha256: input.record.sha256,
    bytes: input.record.bytes,
    glbVersion: input.record.glbVersion,
  };
}

function styleFingerprintRecord(style) {
  if (style.kind === "image") {
    return {
      kind: "image",
      sha256: style.record.sha256,
      mimeType: style.record.mimeType,
    };
  }
  return {
    kind: "text",
    prompt: style.prompt,
  };
}

export function requestFingerprint(input, lookId, style, settings) {
  return sha256(
    Buffer.from(
      JSON.stringify({
        endpoint: "POST /openapi/v1/retexture",
        ...(input.kind === "task"
          ? { inputTaskId: input.taskId }
          : { inputModel: inputFingerprintRecord(input) }),
        lookId,
        style: styleFingerprintRecord(style),
        settings,
      }),
      "utf8",
    ),
  );
}

function buildSanitizedRequestPreview(input, style, settings) {
  const payload = {
    ...settings,
    ...(input.kind === "task"
      ? { input_task_id: input.taskId }
      : { model_url: "<redacted-data-uri>" }),
    ...(style.kind === "image"
      ? { image_style_url: "<redacted-data-uri>" }
      : { text_style_prompt: style.prompt }),
  };
  return buildSanitizedRequest(payload);
}

function sanitizeErrorMessage(value) {
  return String(value ?? "unknown error")
    .replace(
      /data:[^,;\s]+(?:;[^,\s]+)*;base64,[A-Za-z0-9+/=_-]+/giu,
      "<redacted-data-uri>",
    )
    .slice(0, 2_000);
}

function delay(milliseconds) {
  return new Promise((resolvePromise) => {
    setTimeout(resolvePromise, milliseconds);
  });
}

async function withSafeRetry(
  operation,
  {
    attempts = MAX_SAFE_ATTEMPTS,
    sleep = delay,
    random = Math.random,
  } = {},
) {
  let lastError;
  for (let attempt = 0; attempt < attempts; attempt += 1) {
    try {
      return await operation();
    } catch (error) {
      lastError = error;
      if (!isSafeRetryable(error) || attempt === attempts - 1) {
        throw error;
      }
      const backoff = Math.min(1_000 * 2 ** attempt, 10_000);
      const jitter = Math.floor(random() * 250);
      await sleep(backoff + jitter);
    }
  }
  throw lastError;
}

async function readResponseWithLimit(response, maxBytes) {
  const contentLength = Number.parseInt(
    response.headers.get("content-length") ?? "",
    10,
  );
  if (Number.isFinite(contentLength) && contentLength > maxBytes) {
    throw new Error(`Response exceeds ${maxBytes} bytes.`);
  }
  if (!response.body) {
    return Buffer.alloc(0);
  }

  const chunks = [];
  let totalBytes = 0;
  const reader = response.body.getReader();
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      const chunk = Buffer.from(value);
      totalBytes += chunk.length;
      if (totalBytes > maxBytes) {
        await reader.cancel();
        throw new Error(`Response exceeds ${maxBytes} bytes.`);
      }
      chunks.push(chunk);
    }
  } finally {
    reader.releaseLock();
  }
  return Buffer.concat(chunks, totalBytes);
}

async function fetchWithTimeout(fetchImpl, url, init, timeoutMs) {
  const controller = new AbortController();
  const timeout = setTimeout(() => {
    controller.abort(new Error(`Request exceeded ${timeoutMs}ms.`));
  }, timeoutMs);
  try {
    return await fetchImpl(url, {
      ...init,
      signal: controller.signal,
    });
  } finally {
    clearTimeout(timeout);
  }
}

async function apiJson(
  path,
  apiKey,
  init = {},
  fetchImpl = globalThis.fetch,
) {
  const response = await fetchWithTimeout(
    fetchImpl,
    `${API_ROOT}${path}`,
    {
      ...init,
      headers: {
        Authorization: `Bearer ${apiKey}`,
        ...(init.body ? { "Content-Type": "application/json" } : {}),
        ...init.headers,
      },
    },
    API_REQUEST_TIMEOUT_MS,
  );
  const bytes = await readResponseWithLimit(response, MAX_JSON_BYTES);
  const text = bytes.toString("utf8");
  let payload;
  try {
    payload = text ? JSON.parse(text) : {};
  } catch {
    payload = {};
  }

  if (!response.ok) {
    const message = sanitizeErrorMessage(
      payload?.message ??
        payload?.error ??
        `${response.status} ${response.statusText}`,
    );
    throw new MeshyHttpError(
      `Meshy API ${path} failed: ${message}`,
      response.status,
    );
  }
  if (!text || !payload || typeof payload !== "object") {
    throw new Error(`Meshy API ${path} returned invalid JSON.`);
  }
  return payload;
}

async function safeApiJson(path, apiKey, options = {}) {
  return withSafeRetry(
    () => apiJson(path, apiKey, {}, options.fetchImpl),
    options,
  );
}

function numericBalance(payload) {
  if (!Number.isFinite(payload?.balance)) {
    throw new Error("Meshy balance response did not contain numeric balance.");
  }
  return payload.balance;
}

async function observeBalance(apiKey, fetchImpl) {
  return numericBalance(
    await safeApiJson("/balance", apiKey, { fetchImpl }),
  );
}

function readApiKey(service) {
  let value;
  try {
    value = execFileSync(
      "/usr/bin/security",
      [
        "find-generic-password",
        "-s",
        service,
        "-a",
        userInfo().username,
        "-w",
      ],
      {
        encoding: "utf8",
        maxBuffer: 16 * 1024,
        stdio: ["ignore", "pipe", "pipe"],
      },
    ).replace(/\r?\n$/u, "");
  } catch {
    throw new Error(
      `Unable to read Meshy credential from Keychain service ${service}.`,
    );
  }
  if (!value || /[\r\n]/u.test(value)) {
    throw new Error(`Keychain service ${service} returned an invalid credential.`);
  }
  return value;
}

function isPathWithin(rootPath, candidatePath) {
  const pathFromRoot = relative(rootPath, candidatePath);
  return (
    pathFromRoot === "" ||
    (!isAbsolute(pathFromRoot) &&
      pathFromRoot !== ".." &&
      !pathFromRoot.startsWith(`..${sep}`))
  );
}

function assertPathWithin(rootPath, candidatePath, label) {
  if (!isPathWithin(rootPath, candidatePath)) {
    throw new Error(`${label} must remain inside ${rootPath}.`);
  }
}

async function pathExists(filePath) {
  try {
    await stat(filePath);
    return true;
  } catch (error) {
    if (error?.code === "ENOENT") {
      return false;
    }
    throw error;
  }
}

async function readJsonIfPresent(filePath) {
  try {
    return JSON.parse(await readFile(filePath, "utf8"));
  } catch (error) {
    if (error?.code === "ENOENT") {
      return null;
    }
    throw error;
  }
}

async function syncDirectory(directoryPath) {
  let directoryHandle;
  try {
    directoryHandle = await open(directoryPath, "r");
    await directoryHandle.sync();
  } catch (error) {
    if (!["EINVAL", "ENOTSUP", "EBADF"].includes(error?.code)) {
      throw error;
    }
  } finally {
    await directoryHandle?.close();
  }
}

async function prepareImageStyle(repositoryRoot, imageArgument) {
  const allowedInputRoot = resolve(
    repositoryRoot,
    "art/class-models/concept-targets",
  );
  const requestedPath = resolve(repositoryRoot, imageArgument);
  assertPathWithin(allowedInputRoot, requestedPath, "Meshy style image");
  const [realAllowedRoot, inputPath] = await Promise.all([
    realpath(allowedInputRoot),
    realpath(requestedPath),
  ]);
  assertPathWithin(realAllowedRoot, inputPath, "Resolved Meshy style image");
  if (!inputPath.toLowerCase().endsWith(".png")) {
    throw new Error("Meshy retexture image style must be a pinned PNG.");
  }

  const inputStat = await stat(inputPath);
  if (!inputStat.isFile()) {
    throw new Error("Meshy style image must be a regular file.");
  }
  if (inputStat.size > MAX_STYLE_IMAGE_BYTES) {
    throw new Error(`Meshy style image exceeds ${MAX_STYLE_IMAGE_BYTES} bytes.`);
  }
  const buffer = await readFile(inputPath);
  const image = inspectInputImage(buffer, inputPath);
  if (image.mimeType !== "image/png") {
    throw new Error("Meshy retexture image style must be a PNG.");
  }
  return {
    kind: "image",
    mimeType: image.mimeType,
    buffer,
    record: {
      kind: "image",
      file: relative(repositoryRoot, inputPath),
      bytes: buffer.length,
      sha256: sha256(buffer),
      mimeType: image.mimeType,
      width: image.width,
      height: image.height,
      transport: "local-png-data-uri",
    },
  };
}

async function prepareInputModel(repositoryRoot, modelArgument) {
  const requestedPath = resolve(repositoryRoot, modelArgument);
  assertPathWithin(repositoryRoot, requestedPath, "Meshy input model");
  const [realRepositoryRoot, inputPath] = await Promise.all([
    realpath(repositoryRoot),
    realpath(requestedPath),
  ]);
  assertPathWithin(realRepositoryRoot, inputPath, "Resolved Meshy input model");
  if (!inputPath.toLowerCase().endsWith(".glb")) {
    throw new Error("Meshy input model must be a GLB.");
  }

  const inputStat = await stat(inputPath);
  if (!inputStat.isFile()) {
    throw new Error("Meshy input model must be a regular file.");
  }
  if (inputStat.size > MAX_INPUT_MODEL_BYTES) {
    throw new Error(
      `Meshy input model exceeds the ${MAX_INPUT_MODEL_BYTES}-byte ` +
        "upload ceiling.",
    );
  }
  const buffer = await readFile(inputPath);
  const inspected = inspectInputModel(buffer);
  return {
    kind: "model",
    buffer,
    record: {
      kind: "model",
      file: relative(realRepositoryRoot, inputPath),
      bytes: buffer.length,
      sha256: sha256(buffer),
      mimeType: inspected.mimeType,
      glbVersion: inspected.version,
      declaredLength: inspected.declaredLength,
      transport: "local-glb-data-uri",
    },
  };
}

async function prepareInput(repositoryRoot, options) {
  if (options.inputTaskId) {
    return {
      kind: "task",
      taskId: validateTaskId(options.inputTaskId),
      record: {
        kind: "task",
        taskId: options.inputTaskId,
      },
    };
  }
  return prepareInputModel(repositoryRoot, options.inputModelPath);
}

function prepareTextStyle(prompt) {
  return {
    kind: "text",
    prompt: validateTextStylePrompt(prompt),
    record: {
      kind: "text",
      prompt,
    },
  };
}

async function prepareStyle(repositoryRoot, options) {
  if (options.imageStylePath) {
    return prepareImageStyle(repositoryRoot, options.imageStylePath);
  }
  return prepareTextStyle(options.textStylePrompt);
}

async function resolveOutputRoot(repositoryRoot, outputArgument, execute) {
  const allowedOutputRoot = resolve(
    repositoryRoot,
    "art/class-models/provider-runs",
  );
  const outputRoot = resolve(repositoryRoot, outputArgument);
  assertPathWithin(allowedOutputRoot, outputRoot, "Meshy output root");
  if (!execute) {
    return outputRoot;
  }
  await mkdir(outputRoot, { recursive: true });
  const [realAllowedRoot, realOutputRoot] = await Promise.all([
    realpath(allowedOutputRoot),
    realpath(outputRoot),
  ]);
  assertPathWithin(realAllowedRoot, realOutputRoot, "Resolved Meshy output root");
  return realOutputRoot;
}

function createAttemptId() {
  return (
    "attempt-" +
    new Date().toISOString().replace(/[-:.TZ]/gu, "") +
    `-${randomUUID()}`
  );
}

async function acquirePaidCallLock(outputRoot, intentDirectory) {
  const lockDirectory = join(outputRoot, ".meshy-paid-call.lock");
  try {
    await mkdir(lockDirectory, { mode: 0o700 });
  } catch (error) {
    if (error?.code === "EEXIST") {
      throw new Error(
        `A Meshy paid-call lock already exists at ${lockDirectory}. ` +
          "Reconcile its intent/task before any new submission.",
      );
    }
    throw error;
  }

  const ownerPath = join(lockDirectory, "owner.json");
  await atomicWriteJson(ownerPath, {
    createdAt: new Date().toISOString(),
    pid: process.pid,
    intentDirectory,
  });
  let retain = false;
  return {
    retain() {
      retain = true;
    },
    async release() {
      if (retain) {
        return;
      }
      await unlink(ownerPath).catch((error) => {
        if (error?.code !== "ENOENT") {
          throw error;
        }
      });
      await rmdir(lockDirectory).catch((error) => {
        if (error?.code !== "ENOENT") {
          throw error;
        }
      });
    },
  };
}

function isDefiniteSubmissionRejection(error) {
  return (
    error instanceof MeshyHttpError &&
    DEFINITE_SUBMISSION_REJECTIONS.has(error.status)
  );
}

async function pollTask({
  taskId,
  apiKey,
  onSnapshot,
  fetchImpl,
  pollIntervalMs = DEFAULT_POLL_INTERVAL_MS,
  pollTimeoutMs = DEFAULT_POLL_TIMEOUT_MS,
  sleep = delay,
  random = Math.random,
}) {
  const deadline = Date.now() + pollTimeoutMs;
  const encodedTaskId = encodeURIComponent(validateTaskId(taskId));

  while (Date.now() < deadline) {
    const task = await safeApiJson(
      `/retexture/${encodedTaskId}`,
      apiKey,
      { fetchImpl, sleep, random },
    );
    await onSnapshot(task);
    if (task?.id && task.id !== taskId) {
      throw new Error(
        `Meshy returned task ${JSON.stringify(task.id)} while polling ` +
          `${JSON.stringify(taskId)}.`,
      );
    }
    const statusKind = classifyTaskStatus(task?.status);
    process.stdout.write(
      `task=${taskId} status=${task.status} progress=${task.progress ?? 0}\n`,
    );
    if (statusKind === "terminal") {
      return task;
    }
    await sleep(pollIntervalMs);
  }

  throw new Error(
    `Meshy retexture task ${taskId} exceeded ${pollTimeoutMs}ms; resume this ` +
      "task instead of submitting another.",
  );
}

export function assertCreditCeiling(task, creditCeiling = 10) {
  const consumedCredits = task?.consumed_credits;
  if (!Number.isFinite(consumedCredits)) {
    throw new Error(
      "Meshy retexture task did not report numeric consumed_credits; " +
        `the ${creditCeiling}-credit ceiling cannot be verified.`,
    );
  }
  if (consumedCredits < 0 || consumedCredits > creditCeiling) {
    throw new Error(
      `Meshy retexture task reported ${consumedCredits} consumed credits; ` +
        `expected a value from 0 through the ${creditCeiling}-credit ` +
        "ceiling.",
    );
  }
  return consumedCredits;
}

function upsertArtifact(artifacts, artifact) {
  const nextArtifacts = artifacts.filter((item) => item.file !== artifact.file);
  nextArtifacts.push(artifact);
  return nextArtifacts.sort((left, right) => left.file.localeCompare(right.file));
}

export function artifactDefinitions(task) {
  if (!task?.model_urls?.glb) {
    throw new Error(`Succeeded task ${task?.id ?? "<unknown>"} has no GLB URL.`);
  }
  const definitions = [
    {
      url: task.model_urls.glb,
      file: "raw-retextured-model.glb",
      kind: "glb",
      maxBytes: MAX_GLB_BYTES,
    },
  ];
  if (task.thumbnail_url) {
    definitions.push({
      url: task.thumbnail_url,
      file: "provider-preview.png",
      kind: "png",
      maxBytes: MAX_IMAGE_BYTES,
    });
  }
  if (task.alpha_thumbnail_url) {
    definitions.push({
      url: task.alpha_thumbnail_url,
      file: "provider-preview-alpha.png",
      kind: "png",
      maxBytes: MAX_IMAGE_BYTES,
    });
  }

  const textureSets = task.texture_urls ?? [];
  if (!Array.isArray(textureSets)) {
    throw new Error("Meshy retexture task returned invalid texture_urls.");
  }
  if (textureSets.length > MAX_TEXTURE_SETS) {
    throw new Error(
      `Meshy returned ${textureSets.length} texture sets; refusing more than ` +
        `${MAX_TEXTURE_SETS}.`,
    );
  }
  for (let index = 0; index < textureSets.length; index += 1) {
    const textureSet = textureSets[index];
    if (!textureSet || typeof textureSet !== "object") {
      throw new Error(`Meshy texture set ${index} is invalid.`);
    }
    for (const [field, filenamePart] of TEXTURE_FIELDS) {
      const url = textureSet[field];
      if (!url) {
        continue;
      }
      if (typeof url !== "string") {
        throw new Error(`Meshy texture set ${index} field ${field} is invalid.`);
      }
      definitions.push({
        url,
        file: `texture-${index}-${filenamePart}.png`,
        kind: "png",
        maxBytes: MAX_IMAGE_BYTES,
      });
    }
  }
  return definitions;
}

function resumeArguments(record) {
  return [
    "scripts/class-models/run-meshy-retexture.mjs",
    "--resume",
    record.taskId,
    "--look-id",
    record.lookId,
    "--execute",
  ];
}

async function completeKnownTask({
  taskId,
  lookId,
  purpose,
  runDirectory,
  apiKey,
  keychainService,
  baseRecord,
  fetchImpl,
}) {
  const requestRecordPath = join(runDirectory, "request-record.json");
  const taskRecordPath = join(runDirectory, "task.sanitized.json");
  const requestRecord = {
    provider: "Meshy",
    endpoint: "POST /openapi/v1/retexture",
    taskId,
    lookId,
    purpose,
    credentialStore: {
      platform: "macOS Keychain",
      service: keychainService,
      valueRecorded: false,
    },
    artifacts: [],
    ...baseRecord,
  };
  requestRecord.taskId = taskId;
  requestRecord.lookId = lookId;
  requestRecord.credentialStore = {
    platform: "macOS Keychain",
    service: keychainService,
    valueRecorded: false,
  };
  requestRecord.artifacts = Array.isArray(requestRecord.artifacts)
    ? requestRecord.artifacts
    : [];
  requestRecord.state = "POLLING";
  delete requestRecord.runnerError;
  delete requestRecord.resumeArguments;
  delete requestRecord.resumable;
  await atomicWriteJson(requestRecordPath, requestRecord);

  let terminalTask;
  let runnerError;
  try {
    terminalTask = await pollTask({
      taskId,
      apiKey,
      fetchImpl,
      onSnapshot: async (task) => {
        await atomicWriteJson(taskRecordPath, sanitizeTask(task));
        requestRecord.status = task?.status ?? null;
        requestRecord.progress = task?.progress ?? null;
        requestRecord.lastPolledAt = new Date().toISOString();
        requestRecord.consumedCredits = Number.isFinite(task?.consumed_credits)
          ? task.consumed_credits
          : null;
        await atomicWriteJson(requestRecordPath, requestRecord);
      },
    });

    requestRecord.status = terminalTask.status;
    requestRecord.finishedAt = new Date().toISOString();
    requestRecord.consumedCredits = Number.isFinite(
      terminalTask.consumed_credits,
    )
      ? terminalTask.consumed_credits
      : null;
    requestRecord.refundPolicy =
      terminalTask.status === "FAILED"
        ? "Meshy documents consumed_credits=0 and a refund for FAILED tasks."
        : terminalTask.status === "CANCELED"
          ? "Meshy does not document refund behavior for CANCELED tasks."
          : null;
    const creditCeiling = Number.isFinite(requestRecord.expectedMaxCredits)
      ? requestRecord.expectedMaxCredits
      : 10;
    requestRecord.consumedCredits = assertCreditCeiling(
      terminalTask,
      creditCeiling,
    );
    requestRecord.creditCeiling = creditCeiling;
    requestRecord.creditCeilingVerified = true;
    await atomicWriteJson(requestRecordPath, requestRecord);

    if (terminalTask.status !== "SUCCEEDED") {
      throw new Error(
        `Meshy retexture task ${taskId} finished with status ` +
          `${terminalTask.status}: ` +
          `${sanitizeErrorMessage(terminalTask.task_error?.message)}`,
      );
    }

    for (const definition of artifactDefinitions(terminalTask)) {
      const artifact = await downloadArtifact({
        url: definition.url,
        outputPath: join(runDirectory, definition.file),
        kind: definition.kind,
        maxBytes: definition.maxBytes,
        fetchImpl,
      });
      const previous = requestRecord.artifacts.find(
        (item) => item.file === artifact.file,
      );
      if (!artifact.contentType && previous?.contentType) {
        artifact.contentType = previous.contentType;
      }
      requestRecord.artifacts = upsertArtifact(
        requestRecord.artifacts,
        artifact,
      );
      await atomicWriteJson(requestRecordPath, requestRecord);
    }

  } catch (error) {
    runnerError = error;
  }

  try {
    requestRecord.balanceAfter = await observeBalance(apiKey, fetchImpl);
    delete requestRecord.balanceAfterError;
    requestRecord.observedBalanceDelta = Number.isFinite(
      requestRecord.balanceBefore,
    )
      ? requestRecord.balanceBefore - requestRecord.balanceAfter
      : null;
    if (
      Number.isFinite(requestRecord.observedBalanceDelta) &&
      Number.isFinite(requestRecord.consumedCredits) &&
      requestRecord.observedBalanceDelta !== requestRecord.consumedCredits
    ) {
      requestRecord.creditAccountingWarning =
        "Account balance delta differs from this task's consumed_credits; " +
        "another account action may have overlapped.";
    } else {
      delete requestRecord.creditAccountingWarning;
    }
  } catch (error) {
    requestRecord.balanceAfterError = sanitizeErrorMessage(error?.message);
    runnerError ??= error;
  }

  if (runnerError) {
    requestRecord.runnerError = sanitizeErrorMessage(runnerError?.message);
    const canResume =
      !terminalTask || terminalTask.status === "SUCCEEDED";
    requestRecord.state =
      terminalTask?.status === "FAILED"
        ? "FAILED"
        : terminalTask?.status === "CANCELED"
          ? "CANCELED"
          : "RESUMABLE";
    requestRecord.resumable = canResume;
    if (canResume) {
      requestRecord.resumeArguments = resumeArguments(requestRecord);
    } else {
      delete requestRecord.resumeArguments;
    }
  } else {
    requestRecord.state = "ARCHIVED";
    delete requestRecord.runnerError;
    delete requestRecord.resumable;
    delete requestRecord.resumeArguments;
  }
  requestRecord.assetRetentionNote =
    "Meshy API outputs expire after at most three days; local artifacts and " +
    "hashes are the durable record.";
  await atomicWriteJson(requestRecordPath, requestRecord);

  if (runnerError) {
    throw runnerError;
  }
  process.stdout.write(`balance_after=${requestRecord.balanceAfter}\n`);
  process.stdout.write(
    `task_consumed_credits=${requestRecord.consumedCredits}\n`,
  );
  process.stdout.write(
    `observed_balance_delta=${requestRecord.observedBalanceDelta}\n`,
  );
  return requestRecord;
}

async function createTask({
  options,
  repositoryRoot,
  outputRoot,
  input,
  style,
  fetchImpl,
  readApiKeyImpl,
}) {
  const settings = retextureSettings(options.textureResolution);
  const creditCeiling = retextureCreditCeiling(options.textureResolution);
  const fingerprint = requestFingerprint(
    input,
    options.lookId,
    style,
    settings,
  );
  const sanitizedRequest = buildSanitizedRequestPreview(
    input,
    style,
    settings,
  );
  const lookDirectory = join(outputRoot, options.lookId);
  const attemptsDirectory = join(lookDirectory, ".retexture-attempts");
  await mkdir(attemptsDirectory, { recursive: true });
  const attemptDirectory = join(attemptsDirectory, createAttemptId());
  await mkdir(attemptDirectory, { recursive: false });
  let currentDirectory = attemptDirectory;
  let intentPath = join(currentDirectory, "request-intent.json");
  const intent = {
    schemaVersion: 1,
    state: "PREPARED",
    createdAt: new Date().toISOString(),
    provider: "Meshy",
    endpoint: "POST /openapi/v1/retexture",
    lookId: options.lookId,
    purpose: options.purpose,
    input: input.record,
    ...(input.kind === "task"
      ? { inputTaskId: input.taskId }
      : { inputModel: input.record }),
    expectedMaxCredits: creditCeiling,
    requestFingerprint: fingerprint,
    style: style.record,
    settings,
    sanitizedRequest,
    credentialStore: {
      platform: "macOS Keychain",
      service: options.keychainService,
      valueRecorded: false,
    },
  };
  await atomicWriteJson(intentPath, intent);
  process.stdout.write(
    `intent_directory=${relative(repositoryRoot, attemptDirectory)}\n`,
  );

  const paidCallLock = await acquirePaidCallLock(outputRoot, attemptDirectory);
  let taskId;
  try {
    const apiKey = readApiKeyImpl(options.keychainService);
    intent.balanceBefore = await observeBalance(apiKey, fetchImpl);
    if (intent.balanceBefore < creditCeiling) {
      intent.state = "REJECTED_LOCALLY";
      intent.runnerError =
        `Balance ${intent.balanceBefore} is below the documented ` +
        `${creditCeiling}-credit retexture cost.`;
      await atomicWriteJson(intentPath, intent);
      throw new Error(intent.runnerError);
    }
    intent.state = "SUBMITTING";
    intent.submittedAt = new Date().toISOString();
    await atomicWriteJson(intentPath, intent);

    let created;
    try {
      const payload = buildRequestPayload({
        ...(input.kind === "task"
          ? { inputTaskId: input.taskId }
          : { inputModel: input }),
        style,
        settings,
      });
      created = await apiJson(
        "/retexture",
        apiKey,
        {
          method: "POST",
          body: JSON.stringify(payload),
        },
        fetchImpl,
      );
    } catch (error) {
      intent.state = isDefiniteSubmissionRejection(error)
        ? "REJECTED"
        : "SUBMISSION_UNKNOWN";
      intent.runnerError = sanitizeErrorMessage(error?.message);
      await atomicWriteJson(intentPath, intent);
      if (intent.state === "SUBMISSION_UNKNOWN") {
        paidCallLock.retain();
        throw new SubmissionUnknownError(
          `${intent.runnerError} Submission outcome is unknown; inspect recent ` +
            "Meshy tasks and balance before removing the paid-call lock.",
        );
      }
      throw error;
    }

    if (typeof created?.result !== "string" || !created.result) {
      intent.state = "SUBMISSION_UNKNOWN";
      intent.runnerError =
        "Meshy accepted the retexture request but returned no usable task ID.";
      intent.createResponse = sanitizeTask(created);
      await atomicWriteJson(intentPath, intent);
      paidCallLock.retain();
      throw new SubmissionUnknownError(
        `${intent.runnerError} Inspect recent Meshy tasks and balance before ` +
          "removing the paid-call lock.",
      );
    }

    try {
      taskId = validateTaskId(created.result);
    } catch (error) {
      intent.state = "SUBMISSION_UNKNOWN";
      intent.runnerError = sanitizeErrorMessage(error?.message);
      intent.createResponse = sanitizeTask(created);
      await atomicWriteJson(intentPath, intent);
      paidCallLock.retain();
      throw new SubmissionUnknownError(
        `${intent.runnerError} Inspect recent Meshy tasks and balance before ` +
          "removing the paid-call lock.",
      );
    }
    intent.state = "SUBMITTED";
    intent.taskId = taskId;
    await atomicWriteJson(intentPath, intent);

    const runDirectory = join(lookDirectory, taskDirectorySegment(taskId));
    if (await pathExists(runDirectory)) {
      throw new Error(`Meshy run directory already exists: ${runDirectory}.`);
    }
    await rename(attemptDirectory, runDirectory);
    await syncDirectory(dirname(attemptDirectory));
    currentDirectory = runDirectory;
    intentPath = join(currentDirectory, "request-intent.json");
    process.stdout.write(`task_created=${taskId}\n`);

    const baseRecord = {
      schemaVersion: 1,
      state: "SUBMITTED",
      submittedAt: intent.submittedAt,
      balanceBefore: intent.balanceBefore,
      input: input.record,
      ...(input.kind === "task"
        ? { inputTaskId: input.taskId }
        : { inputModel: input.record }),
      style: style.record,
      settings,
      sanitizedRequest,
      expectedMaxCredits: creditCeiling,
      requestFingerprint: fingerprint,
    };
    const record = await completeKnownTask({
      taskId,
      lookId: options.lookId,
      purpose: options.purpose,
      runDirectory,
      apiKey,
      keychainService: options.keychainService,
      baseRecord,
      fetchImpl,
    });
    intent.state = "ARCHIVED";
    intent.finishedAt = record.finishedAt;
    await atomicWriteJson(intentPath, intent);
    process.stdout.write(
      `run_directory=${relative(repositoryRoot, runDirectory)}\n`,
    );
    return record;
  } catch (error) {
    if (taskId) {
      const currentIntent = (await readJsonIfPresent(intentPath)) ?? intent;
      const recordedRun =
        (await readJsonIfPresent(
          join(currentDirectory, "request-record.json"),
        )) ?? {};
      currentIntent.state =
        recordedRun.state ??
        (currentIntent.state === "ARCHIVED" ? "ARCHIVED" : "RESUMABLE");
      currentIntent.runnerError = sanitizeErrorMessage(error?.message);
      const canResume =
        recordedRun.resumable === true ||
        !["ARCHIVED", "FAILED", "CANCELED"].includes(recordedRun.state);
      if (canResume) {
        currentIntent.resumeArguments = [
          "scripts/class-models/run-meshy-retexture.mjs",
          "--resume",
          taskId,
          "--look-id",
          options.lookId,
          "--execute",
        ];
      } else {
        delete currentIntent.resumeArguments;
      }
      await atomicWriteJson(intentPath, currentIntent);
    } else if (
      !["REJECTED", "REJECTED_LOCALLY", "SUBMISSION_UNKNOWN"].includes(
        intent.state,
      )
    ) {
      intent.state = "REJECTED_LOCALLY";
      intent.runnerError = sanitizeErrorMessage(error?.message);
      await atomicWriteJson(intentPath, intent);
    }
    throw error;
  } finally {
    await paidCallLock.release();
  }
}

async function resumeTask({
  options,
  repositoryRoot,
  outputRoot,
  fetchImpl,
  readApiKeyImpl,
}) {
  const taskId = validateTaskId(options.resumeTaskId);
  const runDirectory = join(
    outputRoot,
    options.lookId,
    taskDirectorySegment(taskId),
  );
  await mkdir(runDirectory, { recursive: true });
  const existingRecord =
    (await readJsonIfPresent(join(runDirectory, "request-record.json"))) ?? {};
  if (existingRecord.taskId && existingRecord.taskId !== taskId) {
    throw new Error("Existing run record belongs to a different Meshy task.");
  }
  const apiKey = readApiKeyImpl(options.keychainService);
  const intentPath = join(runDirectory, "request-intent.json");
  const requestRecordPath = join(runDirectory, "request-record.json");
  let record;
  try {
    record = await completeKnownTask({
      taskId,
      lookId: options.lookId,
      purpose:
        existingRecord.purpose ??
        options.purpose ??
        "Resume existing Meshy retexture task",
      runDirectory,
      apiKey,
      keychainService: options.keychainService,
      baseRecord: existingRecord,
      fetchImpl,
    });
  } catch (error) {
    const intent = await readJsonIfPresent(intentPath);
    if (intent) {
      const recordedRun =
        (await readJsonIfPresent(requestRecordPath)) ?? {};
      intent.state = recordedRun.state ?? "RESUMABLE";
      intent.runnerError = sanitizeErrorMessage(error?.message);
      if (recordedRun.resumable === true) {
        intent.resumeArguments = resumeArguments({
          taskId,
          lookId: options.lookId,
        });
      } else {
        delete intent.resumeArguments;
      }
      await atomicWriteJson(intentPath, intent);
    }
    throw error;
  }

  const intent = await readJsonIfPresent(intentPath);
  if (intent) {
    intent.state = "ARCHIVED";
    intent.finishedAt = record.finishedAt;
    delete intent.runnerError;
    delete intent.resumeArguments;
    await atomicWriteJson(intentPath, intent);
  }
  process.stdout.write(
    `run_directory=${relative(repositoryRoot, runDirectory)}\n`,
  );
  return record;
}

export async function main(
  argv = process.argv.slice(2),
  {
    fetchImpl = globalThis.fetch,
    readApiKeyImpl = readApiKey,
  } = {},
) {
  const options = parseArguments(argv);
  const repositoryRoot = resolve(import.meta.dirname, "../..");
  const outputRoot = await resolveOutputRoot(
    repositoryRoot,
    options.outputRoot,
    options.execute,
  );

  if (options.resumeTaskId) {
    const summary = {
      provider: "Meshy",
      mode: "resume",
      endpoint: `GET /openapi/v1/retexture/${encodeURIComponent(
        options.resumeTaskId,
      )}`,
      taskId: options.resumeTaskId,
      lookId: options.lookId,
      outputRoot: relative(repositoryRoot, outputRoot),
      execute: options.execute,
    };
    if (!options.execute) {
      process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
      process.stdout.write("Dry run only. Add --execute to resume the task.\n");
      return summary;
    }
    return resumeTask({
      options,
      repositoryRoot,
      outputRoot,
      fetchImpl,
      readApiKeyImpl,
    });
  }

  const [input, style] = await Promise.all([
    prepareInput(repositoryRoot, options),
    prepareStyle(repositoryRoot, options),
  ]);
  const settings = retextureSettings(options.textureResolution);
  const creditCeiling = retextureCreditCeiling(options.textureResolution);
  const sanitizedRequest = buildSanitizedRequestPreview(
    input,
    style,
    settings,
  );
  const summary = {
    provider: "Meshy",
    mode: "create",
    endpoint: "POST /openapi/v1/retexture",
    lookId: options.lookId,
    purpose: options.purpose,
    expectedMaxCredits: creditCeiling,
    input: input.record,
    style: style.record,
    settings,
    sanitizedRequest,
    requestFingerprint: requestFingerprint(
      input,
      options.lookId,
      style,
      settings,
    ),
    execute: options.execute,
  };
  if (!options.execute) {
    process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
    process.stdout.write(
      `Dry run only. Add --execute to create a paid ${creditCeiling}-credit task.\n`,
    );
    return summary;
  }

  return createTask({
    options,
    repositoryRoot,
    outputRoot,
    input,
    style,
    fetchImpl,
    readApiKeyImpl,
  });
}

const invokedPath = process.argv[1]
  ? pathToFileURL(resolve(process.argv[1])).href
  : "";
if (import.meta.url === invokedPath) {
  main().catch((error) => {
    process.stderr.write(
      `${sanitizeErrorMessage(error?.stack ?? error?.message)}\n`,
    );
    process.exitCode = 1;
  });
}
