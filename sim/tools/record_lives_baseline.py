"""Capture the pre-Lives baseline. Standard library only; never calls a model.

python sim/tools/record_lives_baseline.py --out out/lives-baseline-2026-10-15
Use --plan-only to write the exact commands without building or running Replay.
Existing output directories are refused, including incomplete packs.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import statistics
import subprocess
import time


ROOT = Path(__file__).resolve().parents[2]
CATALOG = "company,invitations,errands"


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def save(path, value):
    Path(path).write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def make_plan(days, seeds):
    if not 1 <= days <= 112 or not seeds or len(set(seeds)) != len(seeds):
        raise ValueError("Use 1–112 days and distinct seeds.")
    if any(not -(2**63) <= seed < 2**63 for seed in seeds):
        raise ValueError("Seeds must fit Replay's signed 64-bit integer.")
    plans = []
    for seed in seeds:
        shared = ["--seed", str(seed), "--days", str(days), "--town", "pelican"]
        for mode in ("routine", "prerequisites"):
            name = f"{mode}-seed{seed}"
            args = shared.copy()
            if mode == "prerequisites":
                args += ["--catalog", CATALOG, "--reflection", "authored"]
            plans.append(dict(name=name, mode=mode, seed=seed, arguments=args))
    # Same seed and settings: these are verification runs, not extra experiment arms.
    first = seeds[0]
    for mode in ("routine", "prerequisites"):
        source = next(p for p in plans if p["seed"] == first and p["mode"] == mode)
        plans.append(dict(name=f"{source['name']}-no-nights", mode="observer-check", seed=first,
                          reference=source["name"], arguments=source["arguments"] + ["--no-nights"]))
    source = next(p for p in plans if p["seed"] == first and p["mode"] == "prerequisites")
    args = source["arguments"].copy()
    args[args.index("--reflection") + 1] = "tape"
    args += ["--reflection-tape", f"{source['name']}.json"]
    plans.append(dict(name=f"{source['name']}-tape", mode="tape-check", seed=first,
                      reference=source["name"], arguments=args))
    return plans


def difference(left, right, ignored=()):
    """Report differing top-level fields, including missing keys (not just the log hash)."""
    return sorted(key for key in left.keys() | right.keys()
                  if key not in ignored and (key not in left or key not in right or left[key] != right[key]))


def verify(reference, candidate, mode):
    if mode == "observer-check":
        if "nights" not in reference or "nights" in candidate:
            raise ValueError("Observer check must compare nights on against nights absent.")
        ignored = ("label", "nights")
    elif mode == "tape-check":
        if not reference.get("reflections"):
            raise ValueError("Tape check is vacuous: no reflections were recorded.")
        ignored = ("label",)
    else:
        raise ValueError(f"Unknown check: {mode}")
    changed = difference(reference, candidate, ignored)
    if changed:
        raise ValueError("Verification differs in: " + ", ".join(changed))
    return dict(status="passed", ignoredFields=list(ignored),
                comparedFields=sorted(set(reference) - set(ignored)))


def summarize(run):
    if run.get("format") != "under-glass-run" or run.get("version") != 1:
        raise ValueError("Expected a version 1 Under Glass recording.")
    if "lives" in run or run.get("illustrative"):
        raise ValueError("This baseline must be a real pre-Lives recording.")
    backends = Counter(r["answer"]["backend"] for r in run.get("reflections", []))
    if set(backends) - {"authored", "authored-fallback"}:
        raise ValueError("The baseline must contain authored answers only.")
    kinds = [k["name"] for k in run["kinds"]]
    acts = Counter(kinds[a[3]] for a in run["acts"])
    people = {}
    for person in ("Penny", "Pam", "Gus"):
        rows = [r for r in run.get("nights", {}).get("rows", [])
                if r["person"] == person and r["sleptHow"] != "start"]
        wakes = [r for r in rows if r["wokeAt"] is not None]
        # Exclude the initial partial sleep. Final incomplete nights remain separately visible.
        wake_minutes = [r["wokeAt"] % 1440 for r in wakes]
        next_choices = [r["next"] for r in wakes if r["next"] is not None]
        people[person] = dict(completedWakings=len(wakes), incompleteSleeps=len(rows) - len(wakes),
                              medianWakeMinute=statistics.median(wake_minutes) if wakes else None,
                              wakingBeforeSix=sum(m < 360 for m in wake_minutes),
                              nextChoiceCounts=dict(Counter(c["why"] for c in next_choices)),
                              firstChoicesWithOnlyHome=sum(
                                  c.get("options") is not None
                                  and [o["option"] for o in c["options"]] == ["home"]
                                  for c in next_choices))
    return dict(logHash=run["hash"], days=run["days"], population=len(run["names"]),
                actCounts=dict(sorted(acts.items())), reflections=sum(backends.values()),
                backendCounts=dict(backends), invitations=len(run.get("invitations", [])),
                favours=len(run.get("favours", [])), nights=len(run.get("nights", {}).get("rows", [])),
                people=people)


def source_identity(root):
    def git(*args):
        return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()
    files = git("ls-files", "--cached", "--others", "--exclude-standard", "--", "sim").splitlines()
    return dict(commit=git("rev-parse", "HEAD"), status=git("status", "--porcelain").splitlines(),
                simFileSha256={p: digest(root / p) for p in sorted(set(files)) if (root / p).is_file()})


def report(pack):
    lines = ["# Lives pilot baseline", "", "Real runs of the existing engine; no Lives mechanics or model calls.", "",
             "Routine = current town. Prerequisites = that town plus Company, Invitations, errands and authored reflection.",
             "These arms isolate prerequisites as a group; they do not measure the effect of history.", "",
             "| Run | Process seconds | JSON MiB | Reflections | Log hash |",
             "|---|---:|---:|---:|---|"]
    for item in pack["runs"]:
        if item["status"] != "completed":
            continue
        s = item["summary"]
        lines.append(f"| {item['name']} | {item['processSeconds']:.2f} | {item['bytes']/1048576:.2f} | {s['reflections']} | `{s['logHash']}` |")
    lines += ["", "Wall time includes process startup, simulation, serialization and output; excludes the build.",
              "CPU time, allocation and peak memory are not measured by this tool. Do not infer them from wall time.", "",
              "| Check | Result |", "|---|---|"]
    for item in pack["runs"]:
        if "verification" in item:
            lines.append(f"| {item['name']} | {item['verification']['status']} |")
    lines += ["", "The manifest retains settings, source hashes, commands, output checksums and night summaries.",
              "Only the command label is ignored for tape comparison. Observer comparison also omits the nights block.",
              "Original JSON files can be loaded in Run Viewer; they have no Life chains data.", ""]
    return "\n".join(lines)


def capture(root, out, days, seeds, plan_only=False):
    plans = make_plan(days, seeds)
    root, out = Path(root).resolve(), Path(out).resolve()
    if out == root or root not in out.parents:
        raise ValueError("Output must be a new directory inside this checkout.")
    # Refuse any existing directory, before a build or a write. A failed run is evidence too.
    if out.exists():
        raise ValueError("Output already exists; choose another directory to preserve the pack.")
    source = source_identity(root)
    dll = root / "sim/UnderGlass.Replay/bin/Release/net8.0/UnderGlass.Replay.dll"
    pack = dict(format="under-glass-lives-baseline", version=1,
                createdUtc=datetime.now(timezone.utc).isoformat(), source=source, days=days,
                status="planned", liveModelCalls=False, cpuSeconds=None, peakMemoryBytes=None, runs=[])
    for plan in plans:
        args = plan["arguments"].copy()
        if "--reflection-tape" in args:
            i = args.index("--reflection-tape") + 1
            args[i] = str(out / args[i])
        command = ["dotnet", str(dll), *args, "--out", str(out / f"{plan['name']}.json")]
        pack["runs"].append({**plan, "command": command, "status": "planned"})
    out.mkdir(parents=True)
    save(out / "manifest.json", pack)
    if plan_only:
        return pack
    try:
        with (out / "build.log").open("w", encoding="utf-8") as log:
            subprocess.run(["dotnet", "build", "sim/UnderGlass.Replay", "-c", "Release", "--nologo"],
                           cwd=root, stdout=log, stderr=subprocess.STDOUT, check=True)
        pack["status"] = "running"
        for item in pack["runs"]:
            item["status"] = "running"
            save(out / "manifest.json", pack)
            started = time.perf_counter()
            with (out / f"{item['name']}.log").open("w", encoding="utf-8") as log:
                subprocess.run(item["command"], cwd=root, stdout=log, stderr=subprocess.STDOUT, check=True)
            item["processSeconds"] = round(time.perf_counter() - started, 4)
            path = out / f"{item['name']}.json"
            run = json.loads(path.read_text(encoding="utf-8-sig"))
            item.update(bytes=path.stat().st_size, sha256=digest(path), settings=run["settings"], summary=summarize(run))
            if "reference" in item:
                reference = json.loads((out / f"{item['reference']}.json").read_text(encoding="utf-8-sig"))
                item["verification"] = verify(reference, run, item["mode"])
            item["status"] = "completed"
            save(out / "manifest.json", pack)
            print(f"{item['name']}: {item['processSeconds']:.2f}s, hash {run['hash']}", flush=True)
        pack["status"] = "completed"
    except (Exception, KeyboardInterrupt) as exc:
        pack["status"] = "failed"
        pack["error"] = str(exc) or type(exc).__name__
        for item in pack["runs"]:
            if item["status"] == "running":
                item["status"] = "failed"
        raise
    finally:
        save(out / "manifest.json", pack)
        (out / "summary.md").write_text(report(pack), encoding="utf-8")
    return pack


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True)
    parser.add_argument("--days", type=int, default=35)
    parser.add_argument("--seeds", type=int, nargs="+", default=[1, 2, 3])
    parser.add_argument("--plan-only", action="store_true")
    args = parser.parse_args()
    try:
        pack = capture(ROOT, ROOT / args.out, args.days, args.seeds, args.plan_only)
    except (ValueError, OSError, subprocess.CalledProcessError) as exc:
        parser.exit(1, f"{exc}\n")
    print(f"{pack['status']}: {args.out}/manifest.json")


if __name__ == "__main__":
    main()
