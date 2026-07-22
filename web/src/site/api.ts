export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
    credentials: 'same-origin',
  });
  if (!response.ok) {
    let message = response.statusText;
    try {
      const problem = await response.json();
      message = problem.detail ?? problem.title ?? message;
    } catch {
      /* not json */
    }
    throw new ApiError(response.status, message);
  }
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T>(url: string, body?: unknown) => request<T>('POST', url, body),
};

export interface Me {
  id: string;
  displayName: string;
  email: string;
}

export interface BotSummary {
  id: string;
  name: string;
  slug: string;
  accent: string;
  owner: string;
  activeVersion: { id: string; versionNumber: number; artifactHash: string } | null;
  versionCount: number;
}

export interface MyBot {
  id: string;
  name: string;
  slug: string;
  accent: string;
  latestVersion: { versionNumber: number; status: string; isActive: boolean } | null;
}

export interface BotDetail {
  id: string;
  name: string;
  slug: string;
  accent: string;
  owner: string;
  isOwner: boolean;
  versions: {
    id: string;
    versionNumber: number;
    status: string;
    artifactHash: string | null;
    isActive: boolean;
    createdAt: string;
    buildLog: string | null;
    entryType: string | null;
    sources: { relativePath: string; content: string }[] | null;
  }[];
}

export interface MatchSummary {
  id: string;
  mapId: string;
  status: string;
  winnerSlot: number | null;
  endReason: string | null;
  endTick: number | null;
  createdAt: string;
  participants: {
    slot: number;
    nameSnapshot: string;
    accentSnapshot: string;
    outcome: string | null;
    finalHealth: number | null;
  }[];
}

export interface MatchDetail extends MatchSummary {
  seed: number;
  replayHash: string | null;
  error: string | null;
}

export interface Meta {
  engineVersion: string;
  gameRulesVersion: string;
  maps: { id: string; width: number; height: number }[];
}
