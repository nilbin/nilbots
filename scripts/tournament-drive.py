#!/usr/bin/env python3
"""agent-arena round-robin driver (the skill's §3): fires ranked sets for every
agent pair PLUS every agent vs every reigning champion, waits for completion,
prints set scores and the leaderboard.

Prereqs: the app is up (eval knobs recommended, see the skill) and each agent
has written sandbox/agents/<name>/state.json with {botId, jar, botName}.
Champions are discovered from the server by slug = champions/<dir> name.
Rerun after each improvement iteration; elo accumulates.
"""
import json, pathlib, sys, time, urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
BASE = "http://localhost:8080"


def cookies(jar_path):
    parts = []
    for line in pathlib.Path(jar_path).read_text().splitlines():
        # curl marks HttpOnly cookies with a #HttpOnly_ prefix — those are real cookies.
        if line.startswith("#HttpOnly_"):
            line = line[len("#HttpOnly_"):]
        elif line.startswith("#") or not line.strip():
            continue
        f = line.split("\t")
        if len(f) >= 7:
            parts.append(f"{f[5]}={f[6]}")
    return "; ".join(parts)


def call(method, path, jar=None, body=None):
    req = urllib.request.Request(BASE + path, method=method,
        data=json.dumps(body).encode() if body is not None else None)
    if body is not None:
        req.add_header("Content-Type", "application/json")
    if jar:
        req.add_header("Cookie", cookies(jar))
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read())


def main():
    agents = []
    agents_dir = ROOT / "sandbox" / "agents"
    if agents_dir.exists():
        for d in sorted(agents_dir.iterdir()):
            state = d / "state.json"
            if state.exists():
                agents.append(json.loads(state.read_text()))
    if not agents:
        sys.exit("No sandbox/agents/*/state.json found — run the agent phase first.")

    champion_slugs = {d.name for d in (ROOT / "champions").iterdir() if (d / "bot.wasm").exists()}
    server_bots = call("GET", "/api/bots")
    champions = [(b["name"], b["id"]) for b in server_bots if b.get("slug") in champion_slugs]
    # Accept either key — agents have written "botName" or "name" across generations.
    names = {a["botId"]: (a.get("botName") or a.get("name") or a["botId"][:8]) for a in agents}
    names.update({bot_id: name for name, bot_id in champions})

    sets = []
    for i, agent in enumerate(agents):
        for other in agents[i + 1:]:
            sets.append((agent, other["botId"]))
        for _, champion_id in champions:
            sets.append((agent, champion_id))

    launched = []
    for initiator, opponent_id in sets:
        r = call("POST", "/api/matches/ranked", jar=initiator["jar"],
                 body={"botId": initiator["botId"], "opponentBotId": opponent_id})
        launched.append((r["id"], initiator["botId"], opponent_id))
        print(f"set {r['id'][:8]}  {names[initiator['botId']]} vs {names[opponent_id]}", flush=True)

    pending = {s[0] for s in launched}
    results = {}
    deadline = time.time() + 1800
    while pending and time.time() < deadline:
        time.sleep(3)
        for set_id in list(pending):
            d = call("GET", f"/api/matchsets/{set_id}")
            if d["status"] in ("Completed", "Failed") and d.get("revealed", True):
                results[set_id] = d
                pending.discard(set_id)
    if pending:
        print(f"TIMED OUT waiting for: {pending}", flush=True)

    print("\n=== SET RESULTS ===")
    for set_id, a_id, b_id in launched:
        d = results.get(set_id)
        if not d:
            continue
        if d["status"] == "Failed":
            print(f"{names[a_id]} vs {names[b_id]}: FAILED")
            continue
        games = "".join(
            "D" if g["draw"] else ("W" if g.get("winnerBotId") == a_id else "L")
            for g in d["games"])
        print(f"{names[a_id]:14} {d['scoreA']:>4}-{d['scoreB']:<4} {names[b_id]:14} "
              f"games(A-persp): {games}  elo: {d['ratingChangeA']:+.1f}/{d['ratingChangeB']:+.1f}")

    board = call("GET", "/api/leaderboard")
    print(f"\n=== LEADERBOARD (rules {board['rulesVersion']} ladder) ===")
    for row in board["entries"]:
        print(f"  {row['name']:16} elo {row['rating']:7.1f}  sets {row.get('rankedSets', '?')}")
    if len(board["ladders"]) > 1:
        print(f"  other ladders: {', '.join(v for v in board['ladders'] if v != board['rulesVersion'])}")


if __name__ == "__main__":
    main()
