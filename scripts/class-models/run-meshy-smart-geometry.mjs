#!/usr/bin/env node

import { createHash, randomUUID } from "node:crypto";
import { execFileSync } from "node:child_process";
import { createReadStream } from "node:fs";
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
  basename,
  dirname,
  extname,
  isAbsolute,
  join,
  relative,
  resolve,
  sep,
} from "node:path";
import { pathToFileURL } from "node:url";

const API_ROOT = "https://api.meshy.ai/openapi/v1";
const ACTIVE_STATUSES = new Set(["PENDING", "IN_PROGRESS"]);
const TERMINAL_STATUSES = new Set(["SUCCEEDED", "FAILED", "CANCELED"]);
const ALLOWED_ASSET_HOSTS = new Set(["assets.meshy.ai"]);
const REDIRECT_STATUSES = new Set([301, 302, 303, 307, 308]);
const EXPECTED_SMART_CREDITS = 5;
const DEFAULT_POLL_INTERVAL_MS = 5_000;
const DEFAULT_POLL_TIMEOUT_MS = 30 * 60_000;
const API_REQUEST_TIMEOUT_MS = 30_000;
const DOWNLOAD_REQUEST_TIMEOUT_MS = 120_000;
const MAX_JSON_BYTES = 2 * 1024 * 1024;
const MAX_INPUT_BYTES = 20 * 1024 * 1024;
const MAX_GLB_BYTES = 512 * 1024 * 1024;
const MAX_IMAGE_BYTES = 128 * 1024 * 1024;
const MIN_INPUT_DIMENSION = 1_040;
const MAX_INPUT_DIMENSION = 8_192;
const MAX_REDIRECTS = 5;
const MAX_SAFE_ATTEMPTS = 5;
const PNG_SIGNATURE = Buffer.from([
  0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a,
]);

export class MeshyHttpError extends Error {
  constructor(message, status) {
    super(message);
    this.name = "MeshyHttpError";
    this.status = status;
  }
}

export class ArtifactValidationError extends Error {
  constructor(message) {
    super(message);
    this.name = "ArtifactValidationError";
  }
}

class SubmissionUnknownError extends Error {
  constructor(message) {
    super(message);
    this.name = "SubmissionUnknownError";
  }
}

function usage() {
  return (
    "Create: run-meshy-smart-geometry.mjs --input <png> --look-id <id> " +
    "--purpose <text> [--target-polycount 4000] [--output-root <path>] " +
    "[--keychain-service nilbots.meshy.api] --execute\n" +
    "Resume: run-meshy-smart-geometry.mjs --resume <task-id> " +
    "--look-id <id> [--purpose <text>] [--output-root <path>] " +
    "[--keychain-service nilbots.meshy.api] --execute"
  );
}

export function parseArguments(argv) {
  const options = {
    targetPolycount: 4_000,
    targetPolycountProvided: false,
    keychainService: "nilbots.meshy.api",
    outputRoot: "art/class-models/provider-runs/meshy",
    execute: false,
  };

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    const takeValue = () => {
      index += 1;
      if (index >= argv.length) {
        throw new Error(`Missing value for ${argument}.\n${usage()}`);
      }
      return argv[index];
    };

    switch (argument) {
      case "--input":
        options.input = takeValue();
        break;
      case "--look-id":
        options.lookId = takeValue();
        break;
      case "--purpose":
        options.purpose = takeValue();
        break;
      case "--target-polycount":
        options.targetPolycount = Number(takeValue());
        options.targetPolycountProvided = true;
        break;
      case "--output-root":
        options.outputRoot = takeValue();
        break;
      case "--keychain-service":
        options.keychainService = takeValue();
        break;
      case "--resume":
        options.resumeTaskId = takeValue();
        break;
      case "--execute":
        options.execute = true;
        break;
      default:
        throw new Error(`Unknown argument: ${argument}\n${usage()}`);
    }
  }

  if (!options.lookId) {
    throw new Error(`--look-id is required.\n${usage()}`);
  }
  if (
    options.lookId.length > 100 ||
    !/^[a-z0-9][a-z0-9-]*$/u.test(options.lookId)
  ) {
    throw new Error("--look-id must contain lowercase letters, digits, or hyphens.");
  }
  if (!options.keychainService || /[\0\r\n]/u.test(options.keychainService)) {
    throw new Error("--keychain-service must be a non-empty single-line value.");
  }

  if (options.resumeTaskId) {
    validateTaskId(options.resumeTaskId);
    if (options.input || options.targetPolycountProvided) {
      throw new Error("--resume cannot be combined with --input or --target-polycount.");
    }
    return options;
  }

  if (!options.input || !options.purpose) {
    throw new Error(`--input and --purpose are required for a new task.\n${usage()}`);
  }
  if (
    !Number.isInteger(options.targetPolycount) ||
    options.targetPolycount < 100 ||
    options.targetPolycount > 15_000
  ) {
    throw new Error("--target-polycount must be an integer from 100 through 15000.");
  }

  return options;
}

export function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

export function isPathWithin(rootPath, candidatePath) {
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

export function validateTaskId(taskId) {
  if (
    typeof taskId !== "string" ||
    taskId.length === 0 ||
    taskId.length > 512 ||
    /[\u0000-\u001f\u007f]/u.test(taskId)
  ) {
    throw new Error("Meshy task ID must be a non-empty opaque string without controls.");
  }
  return taskId;
}

export function taskDirectorySegment(taskId) {
  validateTaskId(taskId);
  if (
    taskId !== "." &&
    taskId !== ".." &&
    /^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$/u.test(taskId)
  ) {
    return taskId;
  }
  return `task-${sha256(Buffer.from(taskId, "utf8")).slice(0, 32)}`;
}

function requestSettings(targetPolycount) {
  return {
    model_type: "smart-topology",
    ai_model: "meshy-t2",
    target_polycount: targetPolycount,
    should_texture: false,
    target_formats: ["glb"],
    alpha_thumbnail: true,
  };
}

function requestFingerprint(lookId, input, settings) {
  return sha256(
    Buffer.from(
      JSON.stringify({
        endpoint: "POST /openapi/v1/image-to-3d",
        lookId,
        inputSha256: input.sha256,
        settings,
      }),
      "utf8",
    ),
  );
}

function inspectPng(buffer) {
  if (
    buffer.length < 24 ||
    !buffer.subarray(0, PNG_SIGNATURE.length).equals(PNG_SIGNATURE) ||
    buffer.subarray(12, 16).toString("ascii") !== "IHDR"
  ) {
    throw new Error("Input has a .png extension but is not a valid PNG header.");
  }
  return {
    mimeType: "image/png",
    width: buffer.readUInt32BE(16),
    height: buffer.readUInt32BE(20),
  };
}

function inspectJpeg(buffer) {
  if (
    buffer.length < 4 ||
    buffer[0] !== 0xff ||
    buffer[1] !== 0xd8 ||
    buffer[2] !== 0xff
  ) {
    throw new Error("Input has a JPEG extension but is not a valid JPEG header.");
  }

  const startOfFrameMarkers = new Set([
    0xc0, 0xc1, 0xc2, 0xc3, 0xc5, 0xc6, 0xc7,
    0xc9, 0xca, 0xcb, 0xcd, 0xce, 0xcf,
  ]);
  let offset = 2;
  while (offset + 3 < buffer.length) {
    if (buffer[offset] !== 0xff) {
      offset += 1;
      continue;
    }
    while (offset < buffer.length && buffer[offset] === 0xff) {
      offset += 1;
    }
    const marker = buffer[offset];
    offset += 1;
    if (marker === 0xd8 || marker === 0xd9 || marker === 0x01) {
      continue;
    }
    if (offset + 2 > buffer.length) {
      break;
    }
    const segmentLength = buffer.readUInt16BE(offset);
    if (segmentLength < 2 || offset + segmentLength > buffer.length) {
      break;
    }
    if (startOfFrameMarkers.has(marker)) {
      if (segmentLength < 7) {
        break;
      }
      return {
        mimeType: "image/jpeg",
        width: buffer.readUInt16BE(offset + 5),
        height: buffer.readUInt16BE(offset + 3),
      };
    }
    offset += segmentLength;
  }
  throw new Error("JPEG input does not contain a readable frame header.");
}

export function inspectInputImage(buffer, filePath) {
  const extension = extname(filePath).toLowerCase();
  let result;
  if (extension === ".png") {
    result = inspectPng(buffer);
  } else if (extension === ".jpg" || extension === ".jpeg") {
    result = inspectJpeg(buffer);
  } else {
    throw new Error("Meshy Smart input must be a PNG or JPEG.");
  }

  if (
    result.width < MIN_INPUT_DIMENSION ||
    result.height < MIN_INPUT_DIMENSION ||
    result.width > MAX_INPUT_DIMENSION ||
    result.height > MAX_INPUT_DIMENSION
  ) {
    throw new Error(
      `Meshy input dimensions must each be ${MIN_INPUT_DIMENSION}–` +
        `${MAX_INPUT_DIMENSION}px; received ${result.width}×${result.height}.`,
    );
  }
  return result;
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
    throw new Error(`Unable to read Meshy credential from Keychain service ${service}.`);
  }

  if (!value || /[\r\n]/u.test(value)) {
    throw new Error(`Keychain service ${service} returned an invalid credential.`);
  }
  return value;
}

function sanitizeErrorMessage(value) {
  return String(value ?? "unknown error")
    .replace(
      /data:[^,;\s]+(?:;[^,\s]+)*;base64,[A-Za-z0-9+/=_-]+/giu,
      "<redacted-data-uri>",
    )
    .slice(0, 2_000);
}

function sanitizeUrl(value) {
  let url;
  try {
    url = new URL(value);
  } catch {
    return "<invalid-url>";
  }
  url.username = "";
  url.password = "";
  url.search = "";
  url.hash = "";

  let pathname = url.pathname;
  if (url.hostname === "assets.meshy.ai") {
    const segments = pathname.split("/");
    const tasksIndex = segments.indexOf("tasks");
    if (tasksIndex > 1) {
      segments.splice(1, tasksIndex - 1, "<account>");
      pathname = segments.join("/");
    }
  }
  return `${url.protocol}//${url.host}${pathname}`;
}

export function sanitizeTask(value) {
  if (Array.isArray(value)) {
    return value.map(sanitizeTask);
  }
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, child]) => {
        if (/^(authorization|api[_-]?key|token|secret)$/iu.test(key)) {
          return [key, "<redacted>"];
        }
        return [key, sanitizeTask(child)];
      }),
    );
  }
  if (typeof value === "string" && /^data:/iu.test(value)) {
    return "<redacted-data-uri>";
  }
  if (typeof value === "string" && /^https?:\/\//iu.test(value)) {
    return sanitizeUrl(value);
  }
  return value;
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

async function fetchWithTimeout(
  fetchImpl,
  url,
  init,
  timeoutMs,
) {
  const controller = new AbortController();
  const timeout = setTimeout(() => {
    const error = new Error(`Request exceeded ${timeoutMs}ms.`);
    error.name = "TimeoutError";
    controller.abort(error);
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
      redirect: "error",
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

export function isSafeRetryable(error) {
  if (error instanceof MeshyHttpError) {
    return error.status === 429 || error.status >= 500;
  }
  return (
    error?.name === "AbortError" ||
    error?.name === "TimeoutError" ||
    error?.code === "ECONNRESET" ||
    error?.code === "ECONNREFUSED" ||
    error?.code === "ENETUNREACH" ||
    error?.code === "ETIMEDOUT" ||
    error instanceof TypeError
  );
}

export function classifyTaskStatus(status) {
  if (ACTIVE_STATUSES.has(status)) {
    return "active";
  }
  if (TERMINAL_STATUSES.has(status)) {
    return "terminal";
  }
  throw new Error(
    `Meshy returned undocumented status ${JSON.stringify(status)}.`,
  );
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

async function atomicWriteText(filePath, contents) {
  await mkdir(dirname(filePath), { recursive: true });
  const temporaryPath = join(
    dirname(filePath),
    `.${basename(filePath)}.${process.pid}.${randomUUID()}.tmp`,
  );
  let handle;
  try {
    handle = await open(temporaryPath, "wx", 0o644);
    await handle.writeFile(contents, "utf8");
    await handle.sync();
    await handle.close();
    handle = undefined;
    await rename(temporaryPath, filePath);
    await syncDirectory(dirname(filePath));
  } catch (error) {
    await handle?.close().catch(() => {});
    await unlink(temporaryPath).catch(() => {});
    throw error;
  }
}

export async function atomicWriteJson(filePath, value) {
  await atomicWriteText(filePath, `${JSON.stringify(value, null, 2)}\n`);
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

export function validateArtifactUrl(value) {
  let url;
  try {
    url = new URL(value);
  } catch {
    throw new ArtifactValidationError("Meshy artifact URL is invalid.");
  }
  if (url.protocol !== "https:") {
    throw new ArtifactValidationError("Meshy artifact URL must use HTTPS.");
  }
  if (url.username || url.password) {
    throw new ArtifactValidationError(
      "Meshy artifact URL must not contain user information.",
    );
  }
  if (!ALLOWED_ASSET_HOSTS.has(url.hostname)) {
    throw new ArtifactValidationError(
      `Unapproved Meshy artifact host: ${url.hostname}.`,
    );
  }
  return url;
}

export function validateArtifactBuffer(kind, buffer) {
  if (kind === "glb") {
    if (buffer.length < 12 || buffer.subarray(0, 4).toString("ascii") !== "glTF") {
      throw new ArtifactValidationError("Downloaded model is not a GLB.");
    }
    const version = buffer.readUInt32LE(4);
    const declaredLength = buffer.readUInt32LE(8);
    if (version !== 2) {
      throw new ArtifactValidationError(`Expected GLB version 2, received ${version}.`);
    }
    if (declaredLength !== buffer.length) {
      throw new ArtifactValidationError(
        `GLB declared ${declaredLength} bytes but downloaded ${buffer.length}.`,
      );
    }
    return;
  }
  if (kind === "png") {
    if (
      buffer.length < PNG_SIGNATURE.length ||
      !buffer.subarray(0, PNG_SIGNATURE.length).equals(PNG_SIGNATURE)
    ) {
      throw new ArtifactValidationError("Downloaded preview is not a PNG.");
    }
    return;
  }
  throw new ArtifactValidationError(`Unsupported artifact kind: ${kind}.`);
}

function validateArtifactHeader(kind, header, totalBytes) {
  if (kind === "glb") {
    if (header.length < 12 || header.subarray(0, 4).toString("ascii") !== "glTF") {
      throw new ArtifactValidationError("Downloaded model is not a GLB.");
    }
    const version = header.readUInt32LE(4);
    const declaredLength = header.readUInt32LE(8);
    if (version !== 2) {
      throw new ArtifactValidationError(`Expected GLB version 2, received ${version}.`);
    }
    if (declaredLength !== totalBytes) {
      throw new ArtifactValidationError(
        `GLB declared ${declaredLength} bytes but downloaded ${totalBytes}.`,
      );
    }
    return;
  }
  if (
    kind !== "png" ||
    header.length < PNG_SIGNATURE.length ||
    !header.subarray(0, PNG_SIGNATURE.length).equals(PNG_SIGNATURE)
  ) {
    throw new ArtifactValidationError("Downloaded preview is not a PNG.");
  }
}

async function fetchArtifactResponse(urlValue, fetchImpl) {
  let currentUrl = validateArtifactUrl(urlValue);
  for (let redirect = 0; redirect <= MAX_REDIRECTS; redirect += 1) {
    const response = await fetchWithTimeout(
      fetchImpl,
      currentUrl,
      { redirect: "manual" },
      DOWNLOAD_REQUEST_TIMEOUT_MS,
    );
    if (REDIRECT_STATUSES.has(response.status)) {
      if (redirect === MAX_REDIRECTS) {
        throw new ArtifactValidationError("Meshy artifact exceeded redirect limit.");
      }
      const location = response.headers.get("location");
      if (!location) {
        throw new ArtifactValidationError(
          "Meshy artifact redirect omitted a location.",
        );
      }
      await response.body?.cancel();
      currentUrl = validateArtifactUrl(new URL(location, currentUrl).toString());
      continue;
    }
    if (!response.ok) {
      throw new MeshyHttpError(
        `Meshy artifact download failed: ${response.status} ${response.statusText}`,
        response.status,
      );
    }
    if (response.url) {
      validateArtifactUrl(response.url);
    }
    return response;
  }
  throw new ArtifactValidationError("Meshy artifact redirect handling failed.");
}

async function writeAll(handle, buffer) {
  let offset = 0;
  while (offset < buffer.length) {
    const { bytesWritten } = await handle.write(
      buffer,
      offset,
      buffer.length - offset,
      null,
    );
    if (bytesWritten <= 0) {
      throw new Error("Artifact write made no progress.");
    }
    offset += bytesWritten;
  }
}

async function hashExistingArtifact(filePath, kind, maxBytes) {
  const fileStat = await stat(filePath);
  if (!fileStat.isFile()) {
    throw new ArtifactValidationError(`${filePath} is not a regular file.`);
  }
  if (fileStat.size > maxBytes) {
    throw new ArtifactValidationError(
      `${basename(filePath)} exceeds ${maxBytes} bytes.`,
    );
  }
  const handle = await open(filePath, "r");
  const header = Buffer.alloc(Math.min(32, fileStat.size));
  try {
    await handle.read(header, 0, header.length, 0);
  } finally {
    await handle.close();
  }
  validateArtifactHeader(kind, header, fileStat.size);

  const hash = createHash("sha256");
  for await (const chunk of createReadStream(filePath)) {
    hash.update(chunk);
  }
  return {
    file: basename(filePath),
    bytes: fileStat.size,
    sha256: hash.digest("hex"),
    contentType: "",
  };
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

async function downloadArtifactOnce({
  url,
  outputPath,
  kind,
  maxBytes,
  fetchImpl,
}) {
  if (await pathExists(outputPath)) {
    return hashExistingArtifact(outputPath, kind, maxBytes);
  }

  const response = await fetchArtifactResponse(url, fetchImpl);
  const contentLength = Number.parseInt(
    response.headers.get("content-length") ?? "",
    10,
  );
  if (Number.isFinite(contentLength) && contentLength > maxBytes) {
    throw new ArtifactValidationError(
      `${basename(outputPath)} exceeds ${maxBytes} bytes.`,
    );
  }
  if (!response.body) {
    throw new ArtifactValidationError(
      `${basename(outputPath)} returned an empty body.`,
    );
  }

  const temporaryPath = join(
    dirname(outputPath),
    `.${basename(outputPath)}.${process.pid}.${randomUUID()}.part`,
  );
  const handle = await open(temporaryPath, "wx", 0o600);
  const hash = createHash("sha256");
  let totalBytes = 0;
  let header = Buffer.alloc(0);
  let closed = false;

  try {
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
          throw new ArtifactValidationError(
            `${basename(outputPath)} exceeds ${maxBytes} bytes.`,
          );
        }
        if (header.length < 32) {
          header = Buffer.concat([header, chunk]).subarray(0, 32);
        }
        hash.update(chunk);
        await writeAll(handle, chunk);
      }
    } finally {
      reader.releaseLock();
    }

    if (Number.isFinite(contentLength) && contentLength !== totalBytes) {
      throw new ArtifactValidationError(
        `${basename(outputPath)} declared ${contentLength} bytes but sent ` +
          `${totalBytes}.`,
      );
    }
    validateArtifactHeader(kind, header, totalBytes);
    await handle.sync();
    await handle.close();
    closed = true;
    await rename(temporaryPath, outputPath);
    await syncDirectory(dirname(outputPath));
    return {
      file: basename(outputPath),
      bytes: totalBytes,
      sha256: hash.digest("hex"),
      contentType: response.headers.get("content-type") ?? "",
    };
  } catch (error) {
    if (!closed) {
      await handle.close().catch(() => {});
    }
    await unlink(temporaryPath).catch(() => {});
    throw error;
  }
}

export async function downloadArtifact(options) {
  return withSafeRetry(
    () => downloadArtifactOnce(options),
    options.retryOptions,
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
      `/image-to-3d/${encodedTaskId}`,
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
    const status = task?.status;
    const statusKind = classifyTaskStatus(status);
    process.stdout.write(
      `task=${taskId} status=${status} progress=${task.progress ?? 0}\n`,
    );
    if (statusKind === "terminal") {
      return task;
    }
    await sleep(pollIntervalMs);
  }

  throw new Error(
    `Meshy task ${taskId} exceeded ${pollTimeoutMs}ms; resume this task ` +
      "instead of submitting another.",
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

function upsertArtifact(artifacts, artifact) {
  const nextArtifacts = artifacts.filter((item) => item.file !== artifact.file);
  nextArtifacts.push(artifact);
  return nextArtifacts.sort((left, right) => left.file.localeCompare(right.file));
}

function resumeArguments(record) {
  return [
    "scripts/class-models/run-meshy-smart-geometry.mjs",
    "--resume",
    record.taskId,
    "--look-id",
    record.lookId,
    "--execute",
  ];
}

async function observeBalance(apiKey, fetchImpl) {
  return numericBalance(
    await safeApiJson("/balance", apiKey, { fetchImpl }),
  );
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
  let requestRecord = {
    provider: "Meshy",
    endpoint: "POST /openapi/v1/image-to-3d",
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
    await atomicWriteJson(requestRecordPath, requestRecord);

    if (terminalTask.status === "SUCCEEDED") {
      if (!terminalTask.model_urls?.glb) {
        throw new Error(`Succeeded task ${taskId} has no GLB URL.`);
      }
      const artifactDefinitions = [
        {
          url: terminalTask.model_urls.glb,
          file: "raw-model.glb",
          kind: "glb",
          maxBytes: MAX_GLB_BYTES,
        },
        ...(terminalTask.thumbnail_url
          ? [
              {
                url: terminalTask.thumbnail_url,
                file: "provider-preview.png",
                kind: "png",
                maxBytes: MAX_IMAGE_BYTES,
              },
            ]
          : []),
        ...(terminalTask.alpha_thumbnail_url
          ? [
              {
                url: terminalTask.alpha_thumbnail_url,
                file: "provider-preview-alpha.png",
                kind: "png",
                maxBytes: MAX_IMAGE_BYTES,
              },
            ]
          : []),
      ];

      for (const definition of artifactDefinitions) {
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
    } else {
      throw new Error(
        `Meshy task ${taskId} finished with status ${terminalTask.status}: ` +
          `${sanitizeErrorMessage(terminalTask.task_error?.message)}`,
      );
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
    const creditWarnings = [];
    if (
      Number.isFinite(requestRecord.observedBalanceDelta) &&
      Number.isFinite(requestRecord.consumedCredits) &&
      requestRecord.observedBalanceDelta !== requestRecord.consumedCredits
    ) {
      creditWarnings.push(
        "Account balance delta differs from this task's consumed_credits; " +
          "another account action may have overlapped.",
      );
    }
    if (
      Number.isFinite(requestRecord.expectedMaxCredits) &&
      Number.isFinite(requestRecord.consumedCredits) &&
      requestRecord.consumedCredits > requestRecord.expectedMaxCredits
    ) {
      creditWarnings.push(
        `Task consumed ${requestRecord.consumedCredits} credits, above the ` +
          `recorded ${requestRecord.expectedMaxCredits}-credit expectation.`,
      );
    }
    if (
      terminalTask?.status === "FAILED" &&
      Number.isFinite(requestRecord.consumedCredits) &&
      requestRecord.consumedCredits !== 0
    ) {
      creditWarnings.push(
        "FAILED task reported non-zero consumed_credits despite Meshy's " +
          "documented refund behavior.",
      );
    }
    if (creditWarnings.length > 0) {
      requestRecord.creditAccountingWarnings = creditWarnings;
    } else {
      delete requestRecord.creditAccountingWarnings;
    }
    delete requestRecord.creditAccountingWarning;
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

async function prepareInput(repositoryRoot, inputArgument) {
  const allowedInputRoot = resolve(
    repositoryRoot,
    "art/class-models/concept-targets",
  );
  const requestedPath = resolve(repositoryRoot, inputArgument);
  assertPathWithin(allowedInputRoot, requestedPath, "Meshy input");
  const [realAllowedRoot, inputPath] = await Promise.all([
    realpath(allowedInputRoot),
    realpath(requestedPath),
  ]);
  assertPathWithin(realAllowedRoot, inputPath, "Resolved Meshy input");

  const inputStat = await stat(inputPath);
  if (!inputStat.isFile()) {
    throw new Error("Meshy input must be a regular file.");
  }
  if (inputStat.size > MAX_INPUT_BYTES) {
    throw new Error(`Meshy input exceeds ${MAX_INPUT_BYTES} bytes.`);
  }
  const buffer = await readFile(inputPath);
  const image = inspectInputImage(buffer, inputPath);
  return {
    path: inputPath,
    buffer,
    record: {
      file: relative(repositoryRoot, inputPath),
      bytes: buffer.length,
      sha256: sha256(buffer),
      mimeType: image.mimeType,
      width: image.width,
      height: image.height,
    },
  };
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

function isDefiniteSubmissionRejection(error) {
  return (
    error instanceof MeshyHttpError &&
    [400, 401, 402, 403, 404, 429].includes(error.status)
  );
}

async function createTask({
  options,
  repositoryRoot,
  outputRoot,
  input,
  fetchImpl,
  credentialReader,
}) {
  const settings = requestSettings(options.targetPolycount);
  const fingerprint = requestFingerprint(
    options.lookId,
    input.record,
    settings,
  );
  const lookDirectory = join(outputRoot, options.lookId);
  const attemptsDirectory = join(lookDirectory, ".attempts");
  await mkdir(attemptsDirectory, { recursive: true });
  const attemptDirectory = join(
    attemptsDirectory,
    createAttemptId(),
  );
  await mkdir(attemptDirectory, { recursive: false });
  let currentDirectory = attemptDirectory;
  let intentPath = join(currentDirectory, "request-intent.json");
  const intent = {
    schemaVersion: 1,
    state: "PREPARED",
    createdAt: new Date().toISOString(),
    provider: "Meshy",
    endpoint: "POST /openapi/v1/image-to-3d",
    lookId: options.lookId,
    purpose: options.purpose,
    expectedMaxCredits: EXPECTED_SMART_CREDITS,
    requestFingerprint: fingerprint,
    input: input.record,
    settings,
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
    const apiKey = credentialReader(options.keychainService);
    intent.balanceBefore = await observeBalance(apiKey, fetchImpl);
    if (intent.balanceBefore < EXPECTED_SMART_CREDITS) {
      intent.state = "REJECTED_LOCALLY";
      intent.runnerError =
        `Balance ${intent.balanceBefore} is below the documented ` +
        `${EXPECTED_SMART_CREDITS}-credit Smart geometry cost.`;
      await atomicWriteJson(intentPath, intent);
      throw new Error(intent.runnerError);
    }
    intent.state = "SUBMITTING";
    intent.submittedAt = new Date().toISOString();
    await atomicWriteJson(intentPath, intent);

    const imageUrl =
      `data:${input.record.mimeType};base64,` +
      input.buffer.toString("base64");
    let created;
    try {
      created = await apiJson(
        "/image-to-3d",
        apiKey,
        {
          method: "POST",
          body: JSON.stringify({
            image_url: imageUrl,
            ...settings,
          }),
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
        "Meshy accepted the create request but returned no usable task ID.";
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

    const runDirectory = join(
      lookDirectory,
      taskDirectorySegment(taskId),
    );
    if (await pathExists(runDirectory)) {
      throw new Error(`Meshy run directory already exists: ${runDirectory}.`);
    }
    await rename(attemptDirectory, runDirectory);
    await syncDirectory(dirname(attemptDirectory));
    await syncDirectory(lookDirectory);
    currentDirectory = runDirectory;
    intentPath = join(currentDirectory, "request-intent.json");
    process.stdout.write(`task_created=${taskId}\n`);

    const baseRecord = {
      schemaVersion: 1,
      state: "SUBMITTED",
      submittedAt: intent.submittedAt,
      balanceBefore: intent.balanceBefore,
      input: input.record,
      settings,
      expectedMaxCredits: EXPECTED_SMART_CREDITS,
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
          "scripts/class-models/run-meshy-smart-geometry.mjs",
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
  credentialReader,
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
  const apiKey = credentialReader(options.keychainService);
  const record = await completeKnownTask({
    taskId,
    lookId: options.lookId,
    purpose:
      existingRecord.purpose ??
      options.purpose ??
      "Resume existing Meshy Smart geometry task",
    runDirectory,
    apiKey,
    keychainService: options.keychainService,
    baseRecord: existingRecord,
    fetchImpl,
  });

  const intentPath = join(runDirectory, "request-intent.json");
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
    credentialReader = readApiKey,
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
      endpoint: `GET /openapi/v1/image-to-3d/${encodeURIComponent(
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
      credentialReader,
    });
  }

  const input = await prepareInput(repositoryRoot, options.input);
  const settings = requestSettings(options.targetPolycount);
  const summary = {
    provider: "Meshy",
    mode: "create",
    endpoint: "POST /openapi/v1/image-to-3d",
    lookId: options.lookId,
    purpose: options.purpose,
    expectedMaxCredits: EXPECTED_SMART_CREDITS,
    input: input.record,
    settings,
    requestFingerprint: requestFingerprint(
      options.lookId,
      input.record,
      settings,
    ),
    execute: options.execute,
  };
  if (!options.execute) {
    process.stdout.write(`${JSON.stringify(summary, null, 2)}\n`);
    process.stdout.write("Dry run only. Add --execute to create a paid task.\n");
    return summary;
  }

  return createTask({
    options,
    repositoryRoot,
    outputRoot,
    input,
    fetchImpl,
    credentialReader,
  });
}

const invokedPath = process.argv[1] ? pathToFileURL(resolve(process.argv[1])).href : "";
if (import.meta.url === invokedPath) {
  main().catch((error) => {
    process.stderr.write(`${sanitizeErrorMessage(error?.stack ?? error?.message)}\n`);
    process.exitCode = 1;
  });
}
