#!/usr/bin/env python3
"""Per-day tables from one save's playtest log (src/NpcMinds/Playtest).

The mod writes one JSON-lines file per in-game day under
<moddir>/playtest/<save>/<year>-<season>-<day>.jsonl: one record object per line with
"tick", "type" and the type's fields (docs/spec/debug-tools.md, "Playtest log").

This tool turns such a folder into the per-day tables a PR description wants:

  1. Ladder       step x kind counts (attempts, blocks, ignored, completed, ...)
  2. Gossip       distinct stories (subject + originalKind) with max hops, AskAround answers
  3. Model calls  grouped by caller + template: calls, fellBack count, median ms
  4. Memory       per-NPC diary census against the 500-entry cap (loud within 10% of it)
  5. Perf         the five slowest ticks (section "tick") and the median ms per section
  6. Decisions    the motives runner (step 14): outcomes, acts by motive, close calls
  7. Stresses     diary kinds that stirred a feeling: count, median magnitude, marks left
  8. Regard       changes by cause, and the strongest grudges in the day's snapshot

Usage:
    python tools/playtest_summary.py <save-folder> [--day <year>-<season>-<day>]

Without --day every day file in the folder is summarized, oldest first. Garbled lines are
skipped and counted; a folder with no data prints a clear message instead of crashing.

Standard library only (Python 3.9+).
"""

import argparse
import json
import os
import re
import statistics
import sys
from collections import Counter

CAP = 500                # MemoryStore's per-NPC diary cap (src/NpcMemory)
CAP_WATCH_RATIO = 0.90   # "within 10% of the cap" => call it out loudly
DAY_FILE_RE = re.compile(r"^(\d+)-([A-Za-z]+)-(\d+)\.jsonl$")
SEASON_ORDER = {"spring": 0, "summer": 1, "fall": 2, "winter": 3}
KIND_ORDER = ("attempt", "blocked", "ignored", "completed")  # preferred column order
KNOWN_TYPES = ("presence", "ladder", "gossip", "game", "plan", "memory", "model", "perf",
               "decision", "stress", "regard")
DECISION_KINDS = ("act", "pass", "blocked", "responded", "ignored", "expired", "grudge")
GRUDGE_SHOWN = -0.3      # regard at or below this shows in the Regard section's snapshot
TEMPLATE_WIDTH = 64


def text(record, key, default="?"):
    """A record's string field; numbers are stringified, absent/null becomes default."""
    value = record.get(key)
    if value is None:
        return default
    return value if isinstance(value, str) else str(value)


def number(record, key):
    """A record's numeric field, or None when absent or not a number."""
    value = record.get(key)
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return value
    return None


def median_text(values):
    values = [v for v in values if v is not None]
    if not values:
        return "-"
    return "{:.1f}".format(statistics.median(values))


def clip(value, width=TEMPLATE_WIDTH):
    if len(value) <= width:
        return value
    return value[:width - 3] + "..."


def table(headers, rows, indent="  ", right=()):
    """Aligned plain-text columns; columns in `right` are right-aligned."""
    widths = [len(str(h)) for h in headers]
    for row in rows:
        for i, cell in enumerate(row):
            widths[i] = max(widths[i], len(str(cell)))
    lines = [indent + "  ".join(
        str(h).rjust(widths[i]) if i in right else str(h).ljust(widths[i])
        for i, h in enumerate(headers)).rstrip()]
    for row in rows:
        lines.append(indent + "  ".join(
            str(cell).rjust(widths[i]) if i in right else str(cell).ljust(widths[i])
            for i, cell in enumerate(row)).rstrip())
    return lines


def day_sort_key(name):
    """Chronological order: year, season, day; anything else sorts last, by name."""
    match = DAY_FILE_RE.match(name)
    if not match:
        return (1, 0, 0, 0, name)
    year, season, day = int(match.group(1)), match.group(2).lower(), int(match.group(3))
    return (0, year, SEASON_ORDER.get(season, 99), day, name)


def load_day(path):
    """(records, unparsable) for one day file. Garbled lines are skipped, not fatal."""
    records = []
    unparsable = 0
    with open(path, "r", encoding="utf-8-sig", errors="replace") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            try:
                record = json.loads(line)
            except ValueError:
                unparsable += 1
                continue
            if not isinstance(record, dict) or not isinstance(record.get("type"), str):
                unparsable += 1
                continue
            records.append(record)
    return records, unparsable


def ladder_section(records):
    """Attempts and outcomes by step: step x kind counts."""
    counts = {}
    display = {}
    for record in records:
        step = text(record, "step")
        kind = text(record, "kind")
        key = (step, kind.lower())
        counts[key] = counts.get(key, 0) + 1
        display.setdefault(kind.lower(), kind)
    if not counts:
        return ["  (no ladder records)"]
    totals = {}
    for (step, _), count in counts.items():
        totals[step] = totals.get(step, 0) + count
    steps = sorted(totals, key=lambda s: (-totals[s], s.lower()))
    kinds = sorted(display, key=lambda k: (
        KIND_ORDER.index(k) if k in KIND_ORDER else len(KIND_ORDER), k))
    rows = [[step] + [counts.get((step, kind), 0) for kind in kinds] for step in steps]
    return ["  step x kind:"] + table(
        ["step"] + [display[kind] for kind in kinds], rows,
        indent="    ", right=tuple(range(1, len(kinds) + 1)))


def gossip_section(records):
    """Distinct stories (subject + originalKind) with max hops, plus AskAround answers."""
    stories = {}
    asked = answered_yes = answered_no = unanswered = 0
    for record in records:
        subject = text(record, "subject")
        original = text(record, "originalKind")
        entry = stories.setdefault((subject, original), {"records": 0, "hops": []})
        entry["records"] += 1
        hops = number(record, "hops")
        if hops is not None:
            entry["hops"].append(hops)
        if text(record, "kind", "").lower() == "ask":
            asked += 1
            if record.get("answered") is True:
                answered_yes += 1
            elif record.get("answered") is False:
                answered_no += 1
            else:
                unanswered += 1
    lines = []
    if stories:
        ordered = sorted(stories.items(), key=lambda kv: (
            -(max(kv[1]["hops"]) if kv[1]["hops"] else 0),
            kv[0][0].lower(), kv[0][1].lower()))
        rows = [[subject, original, entry["records"],
                 max(entry["hops"]) if entry["hops"] else "-"]
                for (subject, original), entry in ordered]
        lines.append("  stories (subject + originalKind) with max hops:")
        lines += table(["subject", "originalKind", "records", "max hops"], rows,
                       indent="    ", right=(2, 3))
    else:
        lines.append("  (no gossip stories)")
    lines.append("  AskAround answers: asked {}, yes {}, no {}, unanswered {}".format(
        asked, answered_yes, answered_no, unanswered))
    return lines


def model_section(records):
    """Model calls grouped by caller + template: calls, fellBack count, median ms."""
    groups = {}
    for record in records:
        key = (text(record, "caller"), text(record, "template"))
        entry = groups.setdefault(key, {"calls": 0, "fell_back": 0, "ms": []})
        entry["calls"] += 1
        if record.get("fellBack") is True:
            entry["fell_back"] += 1
        ms = number(record, "ms")
        if ms is not None:
            entry["ms"].append(ms)
    if not groups:
        return ["  (no model records)"]
    ordered = sorted(groups.items(), key=lambda kv: (
        -kv[1]["calls"], kv[0][0].lower(), kv[0][1].lower()))
    rows = [[caller, clip(template), entry["calls"], entry["fell_back"], median_text(entry["ms"])]
            for (caller, template), entry in ordered]
    return ["  calls by caller + template:"] + table(
        ["caller", "template", "calls", "fellBack", "median ms"], rows,
        indent="    ", right=(2, 3, 4))


def memory_section(records):
    """Per-NPC diary census; anyone within 10% of the 500-entry cap is flagged loudly."""
    per_npc = {}
    for record in records:
        npc = text(record, "npc")
        entry = per_npc.setdefault(
            npc, {"diary": [], "added": 0, "trimmed": 0, "ledger": [], "beliefs": []})
        diary = number(record, "diaryEntries")
        if diary is not None:
            entry["diary"].append(int(diary))
        added = number(record, "addedToday")
        if added is not None:
            entry["added"] += int(added)
        trimmed = number(record, "trimmedToday")
        if trimmed is not None:
            entry["trimmed"] += int(trimmed)
        ledger = number(record, "ledgerEntries")
        if ledger is not None:
            entry["ledger"].append(int(ledger))
        beliefs = number(record, "beliefs")
        if beliefs is not None:
            entry["beliefs"].append(int(beliefs))
    if not per_npc:
        return ["  (no memory records)"]
    rows = []
    warnings = []
    for npc in sorted(per_npc, key=str.lower):
        entry = per_npc[npc]
        diary_max = max(entry["diary"]) if entry["diary"] else None
        rows.append([npc,
                     diary_max if diary_max is not None else "-",
                     entry["added"], entry["trimmed"],
                     max(entry["ledger"]) if entry["ledger"] else "-",
                     max(entry["beliefs"]) if entry["beliefs"] else "-"])
        if diary_max is not None and diary_max >= CAP_WATCH_RATIO * CAP:
            warnings.append(
                "  !!! CAP WATCH: {} diary at {}/{} entries ({:.0f}% of the cap) !!!".format(
                    npc, diary_max, CAP, 100.0 * diary_max / CAP))
    lines = ["  per NPC (diary = max across the day; added/trimmed = sums):"]
    lines += table(["npc", "diaryEntries", "addedToday", "trimmedToday", "ledgerEntries", "beliefs"],
                   rows, indent="    ", right=(1, 2, 3, 4, 5))
    if warnings:
        lines.append("")
        lines += warnings
    return lines


def perf_section(records):
    """The five slowest ticks (section "tick") and the median ms per section."""
    sections = {}
    tick_ms = {}
    for record in records:
        section = text(record, "section", "?")
        key = section.lower()
        entry = sections.setdefault(key, {"name": section, "ms": []})
        ms = number(record, "ms")
        if ms is None:
            continue
        entry["ms"].append(ms)
        if key == "tick":
            tick = number(record, "tick")
            if tick is not None:
                tick_ms[int(tick)] = tick_ms.get(int(tick), 0.0) + ms
    lines = []
    if tick_ms:
        lines.append("  slowest ticks ('tick' section):")
        for tick, ms in sorted(tick_ms.items(), key=lambda kv: -kv[1])[:5]:
            lines.append("    tick {}  {:.1f} ms".format(tick, ms))
    else:
        lines.append("  (no tick perf records)")
    measured = {key: entry for key, entry in sections.items() if entry["ms"]}
    if measured:
        ordered = sorted(measured.items(), key=lambda kv: (-len(kv[1]["ms"]), kv[0]))
        rows = [[entry["name"], len(entry["ms"]), median_text(entry["ms"])]
                for _, entry in ordered]
        lines.append("  median ms per section:")
        lines += table(["section", "samples", "median ms"], rows, indent="    ", right=(1, 2))
    return lines


def decision_section(records):
    """The motives runner: outcomes by kind, acts by motive and act, and the close calls."""
    if not records:
        return ["  (no decision records)"]
    kinds = Counter(text(record, "kind").lower() for record in records)
    ordered = sorted(kinds, key=lambda k: (
        DECISION_KINDS.index(k) if k in DECISION_KINDS else len(DECISION_KINDS), k))
    lines = ["  outcomes: " + ", ".join("{} {}".format(kind, kinds[kind]) for kind in ordered)]
    acts = [record for record in records if text(record, "kind").lower() == "act"]
    if acts:
        pairs = Counter((text(r, "motive"), ("hostile " if r.get("hostile") else "") + text(r, "act"))
                        for r in acts)
        rows = [[motive, act, count] for (motive, act), count
                in sorted(pairs.items(), key=lambda kv: (-kv[1], kv[0]))]
        lines.append("  acts by motive:")
        lines += table(["motive", "act", "count"], rows, indent="    ", right=(2,))
        who = Counter(text(r, "npc") for r in acts)
        lines.append("  most acts: " + ", ".join(
            "{} {}".format(npc, count) for npc, count in sorted(who.items(), key=lambda kv: (-kv[1], kv[0]))[:8]))
    asked = [r for r in records if number(r, "modelP") is not None and text(r, "kind").lower() in ("act", "pass")]
    if asked:
        said_yes = sum(1 for r in asked if text(r, "kind").lower() == "act" and text(r, "call") == "close")
        shifts = [number(r, "tilted") - number(r, "modelP") for r in asked if number(r, "tilted") is not None]
        lines.append("  close calls: {} asked, {} acted on; median Laya p {:.2f}, median mood tilt {}".format(
            len(asked), said_yes, statistics.median([number(r, "modelP") for r in asked]),
            "-" if not shifts else "{:+.3f}".format(statistics.median(shifts))))
    blocked = Counter(text(r, "detail") for r in records if text(r, "kind").lower() == "blocked")
    if blocked:
        lines.append("  blocked by: " + ", ".join(
            "{} x{}".format(reason, count) for reason, count in sorted(blocked.items(), key=lambda kv: (-kv[1], kv[0]))))
    grudges = [r for r in records if text(r, "kind").lower() == "grudge"]
    for r in grudges:
        lines.append("  would lose friendship: {} (grudge {}; {})".format(
            text(r, "npc"), text(r, "grudge"), clip(text(r, "detail"), 80)))
    return lines


def stress_section(records):
    """Diary kinds that stirred a feeling: count, median magnitude, and lasting marks."""
    if not records:
        return ["  (no stress records)"]
    kinds = {}
    for record in records:
        entry = kinds.setdefault(text(record, "kind"), {"n": 0, "mag": [], "marks": 0, "severe": 0, "yield": 0})
        entry["n"] += 1
        entry["mag"].append(number(record, "magnitude"))
        if (number(record, "plastic") or 0) != 0:
            entry["marks"] += 1
        entry["severe"] += 1 if record.get("severe") else 0
        entry["yield"] += 1 if record.get("yieldCrossed") else 0
    def median2(values):
        values = [v for v in values if v is not None]
        return "{:.2f}".format(statistics.median(values)) if values else "-"
    rows = [[kind, e["n"], median2(e["mag"]), e["marks"], e["severe"], e["yield"]]
            for kind, e in sorted(kinds.items(), key=lambda kv: (-kv[1]["n"], kv[0]))]
    return ["  by kind:"] + table(["kind", "count", "median magnitude", "marks", "severe", "yield"],
                                  rows, indent="    ", right=(1, 2, 3, 4, 5))


def regard_section(records):
    """Regard changes by cause (the snapshot aside) and the strongest grudges in the snapshot."""
    if not records:
        return ["  (no regard records)"]
    changes = [r for r in records if text(r, "cause") != "snapshot"]
    lines = []
    if changes:
        causes = Counter(text(r, "cause").split(" (")[0] for r in changes)
        lines.append("  changes: " + ", ".join(
            "{} x{}".format(cause, count) for cause, count in sorted(causes.items(), key=lambda kv: (-kv[1], kv[0]))))
    snapshot = [r for r in records if text(r, "cause") == "snapshot"]
    if snapshot:
        low = sorted((r for r in snapshot if (number(r, "after") or 0) <= GRUDGE_SHOWN),
                     key=lambda r: number(r, "after") or 0)
        lines.append("  snapshot: {} pair(s); {} at or below {}".format(len(snapshot), len(low), GRUDGE_SHOWN))
        for r in low[:8]:
            lines.append("    {} -> {}: {:+.2f}".format(text(r, "observer"), text(r, "subject"), number(r, "after") or 0))
    return lines or ["  (no regard changes)"]


SECTIONS = (("Ladder", "ladder", ladder_section),
            ("Decisions", "decision", decision_section),
            ("Stresses", "stress", stress_section),
            ("Regard", "regard", regard_section),
            ("Gossip", "gossip", gossip_section),
            ("Model calls", "model", model_section),
            ("Memory", "memory", memory_section),
            ("Perf", "perf", perf_section))


def render_day(stem, records, unparsable):
    """All output lines for one day file. Pure: takes the parsed records, prints nothing."""
    note = ", {} unparsable".format(unparsable) if unparsable else ""
    lines = ["=== {} ({}{}) ===".format(
        stem, "{} record{}".format(len(records), "" if len(records) == 1 else "s"), note)]
    if not records:
        lines.append("  (no records)")
        return lines
    counts = Counter(record["type"] for record in records)
    lines.append("records by type: " + ", ".join(
        "{} {}".format(name, count)
        for name, count in sorted(counts.items(), key=lambda kv: (-kv[1], kv[0]))))
    by_type = {}
    for record in records:
        by_type.setdefault(record["type"], []).append(record)
    for title, key, builder in SECTIONS:
        lines.append("")
        lines.append(title)
        lines += builder(by_type.get(key, []))
    return lines


def main(argv=None):
    parser = argparse.ArgumentParser(
        prog="playtest_summary.py",
        description="Per-day tables from one save's playtest log folder.")
    parser.add_argument("folder",
                        help="path to the save's log folder (Mods/StardewNpcMod/playtest/<save>)")
    parser.add_argument("--day", metavar="DAY",
                        help="only this day, e.g. 2-fall-17 (default: every day file, sorted)")
    args = parser.parse_args(argv)

    folder = args.folder
    if not os.path.isdir(folder):
        print("no data: folder not found: {}".format(folder))
        return 1

    if args.day:
        stem = args.day[:-len(".jsonl")] if args.day.endswith(".jsonl") else args.day
        path = os.path.join(folder, stem + ".jsonl")
        if not os.path.isfile(path):
            print("no data: no file {}.jsonl in {}".format(stem, folder))
            return 1
        paths = [path]
    else:
        names = sorted((name for name in os.listdir(folder)
                        if DAY_FILE_RE.match(name)
                        and os.path.isfile(os.path.join(folder, name))),
                       key=day_sort_key)
        if not names:
            others = sum(1 for name in os.listdir(folder) if name.lower().endswith(".jsonl"))
            suffix = " ({} other .jsonl file(s) ignored)".format(others) if others else ""
            print("no data: no <year>-<season>-<day>.jsonl files in {}{}".format(folder, suffix))
            return 0
        paths = [os.path.join(folder, name) for name in names]

    unparsable_total = 0
    unknown = Counter()
    for index, path in enumerate(paths):
        records, unparsable = load_day(path)
        unparsable_total += unparsable
        unknown.update(record["type"] for record in records
                       if record["type"] not in KNOWN_TYPES)
        if index:
            print()
        for line in render_day(os.path.basename(path)[:-len(".jsonl")], records, unparsable):
            print(line)

    print()
    if unknown:
        print("Unknown record types ignored: " + ", ".join(
            "{} x{}".format(name, count) for name, count in sorted(unknown.items())))
    print("Unparsable lines: {}".format(unparsable_total))
    return 0


if __name__ == "__main__":
    sys.exit(main())
