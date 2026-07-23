#!/usr/bin/env python3
"""A/B rules balance harness (docs/GAME-DESIGN.md methodology).

Runs the same round-robin of 6-game mirrored sets under each candidate ruleset
with FIXED seeds, then prints the comparison table (draw rate, elimination
share, game length). Ship a rule change only if draws drop and games shorten
without collapsing strategy diversity.

Usage:
  python3 scripts/balance-eval.py                     # all champions/, default rulesets
  python3 scripts/balance-eval.py --bots a=path/bot.wasm b=... \
      --rulesets 0.2,energy --seeds 101,202,303

Bots default to every champions/<slug>/bot.wasm (the frozen population — add
current-gen artifacts via --bots for a stronger sample). Rulesets are whatever
the CLI's --rules accepts. First run pays one CLI build (~30 s), then ~1-3 s
per match.
"""
import argparse, pathlib, re, statistics, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
LINE = re.compile(r"^g\d+ \S+\s+s\d+\s+slot\d\s+(WIN|LOSS|DRAW)\S*.*?(Elimination|MaxTicks|Disqualification|Domination)\s+t(\d+)")


def default_bots():
    bots = {}
    for directory in sorted((ROOT / "champions").iterdir()):
        wasm = directory / "bot.wasm"
        if wasm.exists():
            bots[directory.name] = str(wasm)
    return bots


def run_set(bots, a, b, rules, seeds, workdir, maps=None):
    command = ["dotnet", "run", "--project", str(ROOT / "src" / "BotArena.Cli"), "--", "set",
               "--bot", bots[a], "--opponent", bots[b], "--seeds", seeds, "--rules", rules]
    if maps:
        command += ["--maps", maps]
    out = subprocess.run(command, capture_output=True, text=True, cwd=workdir, timeout=1800)
    games = [(m.group(1), m.group(2), int(m.group(3)))
             for line in out.stdout.splitlines()
             if (m := LINE.match(line.strip()))]
    if len(games) != 6:
        print(f"WARN {a} vs {b} [{rules}]: parsed {len(games)}/6 games", file=sys.stderr)
        print(out.stdout[-800:] + out.stderr[-400:], file=sys.stderr)
    return games


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--bots", nargs="*", default=[], help="name=path/to/bot.wasm (default: champions/)")
    parser.add_argument("--rulesets", default="0.1,0.2,energy")
    parser.add_argument("--seeds", default="101,202,303")
    parser.add_argument("--maps", default=None, help="comma list; default = the set command's pool")
    parser.add_argument("--workdir", default="/tmp/balance-eval")
    args = parser.parse_args()

    # Absolute paths: sets run from --workdir, so relative bot paths would not resolve.
    bots = {kv.split("=", 1)[0]: str(pathlib.Path(kv.split("=", 1)[1]).resolve())
            for kv in args.bots} or default_bots()
    if len(bots) < 2:
        sys.exit("Need at least 2 bots (champions/ empty? pass --bots).")
    pathlib.Path(args.workdir).mkdir(parents=True, exist_ok=True)
    names = list(bots)
    pairs = [(names[i], names[j]) for i in range(len(names)) for j in range(i + 1, len(names))]

    stats = {}
    for rules in args.rulesets.split(","):
        # Per-arm subdirectory: replay dirs are named by bots/map/seed only, so two
        # arms sharing a workdir silently clobber each other's replays (found during
        # the 0.4 ship run — aggregates were fine, post-hoc replay analysis wasn't).
        arm_dir = pathlib.Path(args.workdir) / rules.replace("/", "-")
        arm_dir.mkdir(parents=True, exist_ok=True)
        games = []
        for a, b in pairs:
            games += run_set(bots, a, b, rules, args.seeds, str(arm_dir), args.maps)
            print(f"  done {a} vs {b} [{rules}]", flush=True)
        if not games:
            continue
        draws = sum(1 for g in games if g[0] == "DRAW")
        eliminations = sum(1 for g in games if g[1] == "Elimination")
        ticks = [g[2] for g in games]
        stats[rules] = (len(games), draws, eliminations, statistics.median(ticks), sum(ticks) / len(ticks))

    print(f"\n=== BALANCE COMPARISON ({len(names)} bots, round-robin, 6-game sets, seeds {args.seeds}) ===")
    print(f"{'ruleset':14} {'games':>5} {'draws':>5} {'draw%':>6} {'elims':>5} {'medTick':>8} {'avgTick':>8}")
    for rules, (n, draws, elims, med, avg) in stats.items():
        print(f"{rules:14} {n:5} {draws:5} {100 * draws / n:5.0f}% {elims:5} {med:8.0f} {avg:8.0f}")


if __name__ == "__main__":
    main()
