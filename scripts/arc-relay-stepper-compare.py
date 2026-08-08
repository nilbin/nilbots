#!/usr/bin/env python3
"""Head-to-head runner for the greedy vs coordinated mind steppers.

The two steppers answer the same question — "which tile does this body
step to" — with different amounts of compute. Greedy asks each body in
isolation; coordinated runs a windowed space-time cooperative search
(`CoordinatedArenaStepper`). Whether the extra compute buys anything
visible is an empirical question, and every earlier answer to it was
measured on sources that have since moved.

This script runs the SAME cells under both steppers off ONE published
binary and reports wall-clock cost honestly:

  * matches run SERIALLY, one at a time — parallel cells contend for
    cores and make per-match timing meaningless;
  * each cell is run --repeats times per mode and the MEDIAN is kept, so
    one scheduler hiccup does not become a finding;
  * per-tick planning cost is isolated by LEAST-SQUARES FIT of median
    wall-clock against each cell's end tick. The slope is the marginal
    cost of one more tick — the part a stepper actually controls — and
    the intercept absorbs the fixed cost (process start, sheet compile,
    mind compile, replay write) without having to model it. This matters
    because the two steppers do not produce equal-length matches: total
    wall clock conflates "costs more per tick" with "played longer", and
    only the slope separates them.

    The headline number comes from the SHARED-INTERCEPT fit over all
    observations of both modes at once: fixed cost is identical by
    construction (same binary, same sheets, same compile), so letting
    each mode buy its own intercept spends the six points a mode has on
    a parameter that cannot really differ, and the slope/intercept split
    becomes unstable. Pooling gives one fixed cost and one slope per
    mode. Per-mode fits are still reported, and a large disagreement
    between the two is the signal that the cell tick-ranges were too
    narrow to identify the slope.
  * a `--print-contract` probe is also reported as the process-start
    floor. It resolves the contract and exits BEFORE the mind compiles,
    so it is a floor on fixed cost, not the whole of it — the regression
    intercept is the better estimate.

Both teams run the same mind, so the stepper flag moves BOTH sides. Any
per-team asymmetry in the output is the sheet, not the stepper.

This runner measures COST and produces replays. It makes no balance
claim: six cells are six games. Feed the replay directories to
arc-relay-traffic-read.py / -tempo-read.py / -unit-audit.py for the
behavioral half.

Usage:
  arc-relay-stepper-compare.py --bin DIR/botarena --mind PROJECT \
      --sheet-a A.json --sheet-b B.json --out DIR \
      [--cells w-9001 ...] [--repeats 3] [--loop-profile ambush-warren-11]

Writes DIR/<mode>-<cell>/ per match (replay.json.gz + run.json, exactly
the layout the read scripts expect) and DIR/stepper-compare.json with
the timings.
"""

from __future__ import annotations

import argparse
import gzip
import json
import shutil
import statistics
import subprocess
import time
from pathlib import Path

STANDARD_CELLS = ["w-9001", "w-9002", "e-9005", "e-9009", "w-9012", "e-9004"]
MODES = ["greedy", "coordinated"]


def run_once(cmd: list[str]) -> tuple[float, subprocess.CompletedProcess]:
    """Wall-clock a subprocess. Monotonic, so a clock step cannot make a
    match look instant."""
    start = time.monotonic()
    proc = subprocess.run(cmd, capture_output=True, text=True)
    return time.monotonic() - start, proc


def match_cmd(args, mode: str, cell: str, out: Path) -> list[str]:
    side, seed = cell.split("-")
    s0, s1 = (args.sheet_a, args.sheet_b) if side == "w" else (
        args.sheet_b, args.sheet_a)
    cmd = [
        args.bin, "experiment", "arc-relay",
        "--bot", args.mind, "--opponent", args.mind,
        "--runtime", "in-process",
        "--loop-profile", args.loop_profile,
        "--seed", seed,
        "--sheet0", s0, "--sheet1", s1,
        "--out", str(out),
    ]
    if mode != "greedy":
        cmd += ["--mind-stepper", mode]
    return cmd


def overhead_seconds(args, mode: str, cell: str) -> float:
    """A FLOOR on fixed cost: the contract print exits right after
    resolution, before the mind compiles. Reported for scale, not used
    to derive the per-tick number."""
    cmd = match_cmd(args, mode, cell, Path(args.out) / "_contract-probe")
    cmd.append("--print-contract")
    samples = [run_once(cmd)[0] for _ in range(args.repeats)]
    return statistics.median(samples)


def fit(ticks: list[int], seconds: list[float]) -> tuple[float, float]:
    """Ordinary least squares of seconds on ticks -> (slope, intercept).
    Slope is the marginal second-per-tick; intercept is fixed cost."""
    mx, my = statistics.fmean(ticks), statistics.fmean(seconds)
    sxx = sum((t - mx) ** 2 for t in ticks)
    sxy = sum((t - mx) * (s - my) for t, s in zip(ticks, seconds))
    slope = sxy / sxx if sxx else float("nan")
    return slope, my - slope * mx


def solve(matrix: list[list[float]]) -> list[float]:
    """Gauss-Jordan with partial pivoting on an augmented matrix. Small
    and dependency-free on purpose: numpy is not a dependency of this
    repo's scripts and this system is 3x3."""
    n = len(matrix)
    for i in range(n):
        pivot = max(range(i, n), key=lambda r: abs(matrix[r][i]))
        matrix[i], matrix[pivot] = matrix[pivot], matrix[i]
        for r in range(n):
            if r == i:
                continue
            factor = matrix[r][i] / matrix[i][i]
            for c in range(i, n + 1):
                matrix[r][c] -= factor * matrix[i][c]
    return [matrix[i][n] / matrix[i][i] for i in range(n)]


def pooled_fit(observations: list[tuple[str, int, float]]) -> dict:
    """One shared intercept, one slope per mode.

    Design row is [1, ticks if greedy else 0, ticks if coordinated else 0],
    so the intercept is estimated from all observations jointly while each
    mode keeps its own marginal per-tick cost.
    """
    rows = [
        [1.0,
         float(t) if mode == "greedy" else 0.0,
         float(t) if mode == "coordinated" else 0.0]
        for mode, t, _ in observations
    ]
    y = [s for _, _, s in observations]
    n = 3
    normal = [
        [sum(rows[k][i] * rows[k][j] for k in range(len(rows)))
         for j in range(n)]
        + [sum(rows[k][i] * y[k] for k in range(len(rows)))]
        for i in range(n)
    ]
    fixed, greedy_slope, coord_slope = solve(normal)
    residuals = [
        y[k] - (fixed + greedy_slope * rows[k][1] + coord_slope * rows[k][2])
        for k in range(len(rows))
    ]
    rms = (sum(r * r for r in residuals) / len(residuals)) ** 0.5
    return {
        "fixedSeconds": fixed,
        "greedyMsPerTick": greedy_slope * 1000,
        "coordinatedMsPerTick": coord_slope * 1000,
        "ratio": coord_slope / greedy_slope if greedy_slope else float("nan"),
        "residualRmsMs": rms * 1000,
    }


def end_tick(rundir: Path) -> int | None:
    run = json.loads((rundir / "run.json").read_text())
    return run.get("Result", {}).get("EndTick")


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--bin", required=True, help="published botarena binary")
    p.add_argument("--mind", required=True, help="mind project directory")
    p.add_argument("--sheet-a", required=True)
    p.add_argument("--sheet-b", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--cells", nargs="*", default=STANDARD_CELLS)
    p.add_argument("--repeats", type=int, default=3)
    p.add_argument("--loop-profile", default="ambush-warren-11")
    args = p.parse_args()

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    results: dict = {"cells": args.cells, "repeats": args.repeats, "modes": {}}

    for mode in MODES:
        per_cell = {}
        print(f"== {mode}")
        for cell in args.cells:
            rundir = out / f"{mode}-{cell}"
            samples = []
            for i in range(args.repeats):
                if rundir.exists():
                    shutil.rmtree(rundir)
                elapsed, proc = run_once(match_cmd(args, mode, cell, rundir))
                if proc.returncode != 0:
                    print(proc.stdout[-2000:])
                    print(proc.stderr[-2000:])
                    raise SystemExit(f"{mode} {cell} failed")
                samples.append(elapsed)
            ticks = end_tick(rundir)
            med = statistics.median(samples)
            per_cell[cell] = {
                "samples": samples,
                "median": med,
                "endTick": ticks,
            }
            print(f"  {cell}: median {med:.2f}s over {ticks} ticks "
                  f"(samples {', '.join(f'{s:.2f}' for s in samples)})")
        ov = overhead_seconds(args, mode, args.cells[0])
        medians = [v["median"] for v in per_cell.values()]
        ticks = [v["endTick"] for v in per_cell.values()]
        slope, intercept = fit(ticks, medians)
        results["modes"][mode] = {
            "perCell": per_cell,
            "meanMatchSeconds": statistics.fmean(medians),
            "processStartFloorSeconds": ov,
            "fitMsPerTick": slope * 1000,
            "fitFixedSeconds": intercept,
            "meanEndTick": statistics.fmean(ticks),
            "totalTicks": sum(t for t in ticks if t),
        }
        print(f"  mean match {statistics.fmean(medians):.2f}s | "
              f"mean end tick {statistics.fmean(ticks):.0f} | "
              f"fit {slope * 1000:.2f} ms/tick + {intercept:.2f}s fixed | "
              f"start floor {ov:.2f}s")

    g, c = results["modes"]["greedy"], results["modes"]["coordinated"]
    observations = [
        (mode, v["endTick"], v["median"])
        for mode in MODES
        for v in results["modes"][mode]["perCell"].values()
    ]
    pooled = pooled_fit(observations)
    results["pooledFit"] = pooled
    results["delta"] = {
        "meanMatchSeconds": c["meanMatchSeconds"] - g["meanMatchSeconds"],
        "meanMatchRatio": c["meanMatchSeconds"] / g["meanMatchSeconds"],
        "perModeFitMsPerTick": c["fitMsPerTick"] - g["fitMsPerTick"],
        "perModeMsPerTickRatio": c["fitMsPerTick"] / g["fitMsPerTick"],
        "pooledMsPerTick":
            pooled["coordinatedMsPerTick"] - pooled["greedyMsPerTick"],
        "pooledMsPerTickRatio": pooled["ratio"],
    }
    (out / "stepper-compare.json").write_text(json.dumps(results, indent=1))
    d = results["delta"]
    print(f"\npooled fit: fixed {pooled['fixedSeconds']:.2f}s | "
          f"greedy {pooled['greedyMsPerTick']:.2f} ms/tick | "
          f"coordinated {pooled['coordinatedMsPerTick']:.2f} ms/tick "
          f"(residual RMS {pooled['residualRmsMs']:.0f} ms)")
    print(f"coordinated / greedy: match {d['meanMatchRatio']:.2f}x "
          f"({d['meanMatchSeconds']:+.2f}s), "
          f"planning {pooled['ratio']:.2f}x "
          f"({d['pooledMsPerTick']:+.2f} ms/tick)")
    print(f"wrote {out / 'stepper-compare.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
