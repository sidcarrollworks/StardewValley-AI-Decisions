"""Can Laya tell what a typed line is doing, and which known thing a claim is about?

A probe for the player-speech spec (docs/under-glass/specs/player-speech-spec.md): the Newcomer's
typed line goes to Laya's /v1/systemone as a typed choice, never as text to write. Two questions:

- act: which act kind the line is (the speech catalog's 14 kinds plus small talk);
- claim: which of the listener's known things a claim refers to, or none of them.

Packets follow the game adapter (sim/UnderGlass.Minds/ResilientReflectionMind.cs): the line goes in
`state`, short instructions, slot labels o0..oN, and each question's semantic text (instructions,
labels, descriptions, state) must fit Sid's 1,250-character budget. Position is balanced as the game's
Balanced mode balances it: every option sits in every slot once (a cyclic rotation), and the passes
are normalised (w / max, then to sum 1), averaged and renormalised. Unlike the game, which sends one
request per pass, the probe packs all rotations of one line into one request as separate questions
(Laya scores each question as its own sequence), so the budget applies per question. The base order
of the options is hashed per line, so the raw (first-rotation) reading puts each kind in a varying
slot and its slot histogram measures position, not content. A reply that reports truncation, dropped
state tokens or collapsed options is refused.

Standard library only. Every request and reply is written to a receipts file so the run can be audited.

    python sim/tools/laya_categorize_probe.py --cases out/laya-probe-2026-10-13/cases.json \
        --out out/laya-probe-2026-10-13 --models typed-decisions english
"""

from __future__ import annotations

import argparse
import json
import math
import sys
import time
import urllib.error
import urllib.request
from collections import Counter, defaultdict
from pathlib import Path
from typing import Callable, Iterable

LAYA_TEXT_LIMIT = 1250
SUBJECT = "Mara"

# Plain-English descriptions of each kind, shown to Laya (and to the blind judges) as the options.
# v1 is short; v2 says what tells a kind apart from its neighbours. Neither quotes a catalog line.
OPTIONS_V1 = {
    "Apologised": "apologising",
    "Argued": "arguing about who is right",
    "Comforted": "comforting someone who is upset",
    "Complimented": "paying a compliment",
    "GaveGift": "giving a gift",
    "HelpedSomeone": "offering or giving help",
    "Joked": "joking around",
    "Mocked": "mocking someone",
    "PlayedGame": "inviting someone to play a game",
    "Squabbled": "bickering over something petty",
    "StoodUpFor": "standing up for someone",
    "Thanked": "thanking someone",
    "TreatedToDrink": "buying someone a drink",
    "Welcomed": "welcoming someone new to town",
    "SmallTalk": "small talk, nothing in particular",
}
OPTIONS_V2 = {
    "Apologised": "saying sorry for something they did",
    "Argued": "disagreeing hard and insisting they are right",
    "Comforted": "reassuring someone who is sad or hurt",
    "Complimented": "praising how someone looks or what they did",
    "GaveGift": "handing someone a present",
    "HelpedSomeone": "pitching in to help with a task",
    "Joked": "teasing playfully or joking, without malice",
    "Mocked": "making fun of someone to hurt them",
    "PlayedGame": "suggesting or starting a game together",
    "Squabbled": "a petty spat over something small, like a seat or a turn",
    "StoodUpFor": "defending a third person against someone else",
    "Thanked": "thanking someone for something they did",
    "TreatedToDrink": "offering to pay for someone's drink",
    "Welcomed": "greeting someone who has just arrived in town",
    "SmallTalk": "chatting about nothing in particular, like the weather",
}
KINDS = list(OPTIONS_V1)
HOSTILE = {"Argued", "Mocked", "Squabbled"}
ACT_INSTRUCTIONS = "What is the newcomer doing with this line?"
CLAIM_INSTRUCTIONS = "Which of these does the line refer to?"
NONE_KEY = "none"


def tone(kind: str) -> str:
    """The coarse reading a misread must at least get right: hostile, small talk or friendly."""
    return "hostile" if kind in HOSTILE else "smalltalk" if kind == "SmallTalk" else "friendly"


def fill_subject(text: str, name: str = SUBJECT) -> str:
    return text.replace("{subject}", name)


def fnv1a(text: str) -> int:
    """The repo's deterministic hash (never Python's salted hash())."""
    h = 0x811C9DC5
    for byte in text.encode("utf-8"):
        h ^= byte
        h = (h * 0x01000193) & 0xFFFFFFFF
    return h


def rotations(keys: list[str]) -> list[list[str]]:
    """Slot s of rotation r holds keys[(s + r) % n]: each key sits in each slot exactly once."""
    n = len(keys)
    return [[keys[(s + r) % n] for s in range(n)] for r in range(n)]


def distractors(true_key: str, pool: Iterable[str], count: int, salt: str) -> list[str]:
    """`count` other keys, chosen deterministically by hashing the case id with each key."""
    others = sorted(k for k in pool if k != true_key)
    return sorted(others, key=lambda k: (fnv1a(f"{salt}|{k}"), k))[:count]


def semantic_chars(state: str, instructions: str, criteria: dict[str, str]) -> int:
    """What the game adapter counts against the budget: instructions, labels, descriptions, state."""
    return len(instructions) + sum(len(k) + len(v) for k, v in criteria.items()) + len(state)


def build_request(state: str, instructions: str, keys: list[str], describe: Callable[[str], str],
                  model: str) -> tuple[dict, dict[str, dict[str, str]], int]:
    """One request holding every rotation as its own question. Returns the body, each question's
    slot-label -> key map, and the largest per-question semantic size (one question is one packet)."""
    questions: dict[str, dict] = {}
    mappings: dict[str, dict[str, str]] = {}
    largest = 0
    for r, order in enumerate(rotations(keys)):
        criteria = {f"o{s}": describe(key) for s, key in enumerate(order)}
        questions[f"r{r}"] = {"type": "choice", "instructions": instructions, "criteria": criteria}
        mappings[f"r{r}"] = {f"o{s}": key for s, key in enumerate(order)}
        largest = max(largest, semantic_chars(state, instructions, criteria))
    return {"state": state, "model": model, "questions": questions}, mappings, largest


def usage_problems(reply: dict) -> list[str]:
    usage = reply.get("usage") or {}
    problems = []
    if usage.get("truncated"):
        problems.append("truncated")
    if (usage.get("state_tokens_dropped") or 0) > 0:
        problems.append("state tokens dropped")
    if usage.get("truncated_questions"):
        problems.append("questions truncated")
    if usage.get("options"):
        problems.append("options collapsed")
    return problems


def normalise(weights: dict[str, float]) -> dict[str, float]:
    """The game's ParseWeights: refuse bad values, then (w / max) / sum(w / max)."""
    if not weights or any(not math.isfinite(w) or w < 0 for w in weights.values()):
        raise ValueError("weights must be finite and non-negative")
    top = max(weights.values())
    if top <= 0:
        raise ValueError("all weights are zero")
    scaled = {k: w / top for k, w in weights.items()}
    total = sum(scaled.values())
    return {k: v / total for k, v in scaled.items()}


def aggregate(reply: dict, mappings: dict[str, dict[str, str]]) -> tuple[dict[str, float], dict[str, float]]:
    """Balanced reading (all rotations averaged) and raw reading (rotation r0 alone), keyed by kind."""
    answers = reply.get("answers") or {}
    passes = []
    for qid, mapping in mappings.items():
        probs = (answers.get(qid) or {}).get("probabilities") or {}
        if set(probs) != set(mapping):
            raise ValueError(f"{qid}: labels {sorted(probs)} do not match {sorted(mapping)}")
        passes.append(normalise({mapping[label]: float(p) for label, p in probs.items()}))
    keys = list(passes[0])
    balanced = normalise({k: sum(p[k] for p in passes) / len(passes) for k in keys})
    return balanced, passes[0]


def ranked(weights: dict[str, float]) -> list[str]:
    return sorted(weights, key=lambda k: (-weights[k], k))


def post(url: str, body: dict, timeout: float = 10.0) -> tuple[dict, float]:
    data = json.dumps(body).encode("utf-8")
    request = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"}, method="POST")
    started = time.perf_counter()
    with urllib.request.urlopen(request, timeout=timeout) as response:
        reply = json.loads(response.read(1 << 22).decode("utf-8"))
    return reply, (time.perf_counter() - started) * 1000


def hashed(keys: Iterable[str], salt: str) -> list[str]:
    """The keys in an order fixed by the salt (a line's id), so no kind always sits in one slot."""
    return sorted(keys, key=lambda k: (fnv1a(f"{salt}#{k}"), k))


def six_options(label: str, salt: str) -> list[str]:
    """The true kind, small talk (the game always offers it), and others to make six."""
    pool = [k for k in KINDS if k not in (label, "SmallTalk")]
    keys = [label] + ([] if label == "SmallTalk" else ["SmallTalk"])
    return hashed(keys + distractors(label, pool, 6 - len(keys), salt), salt)


def act_items(catalog: dict, cases: dict | None) -> list[dict]:
    """Catalog act lines (their kind is the label) and, when given, authored player lines and small talk
    that the blind judges agreed on."""
    items = [{"id": line["id"], "set": "catalog", "text": fill_subject(line["text"]), "label": line["kind"],
              "difficulty": None, "clear": None} for line in catalog["lines"]
             if line["kind"] in OPTIONS_V1]  # act lines only: reply lines (an invitation's yes or no) are not acts
    for case in (cases or {}).get("acts", []):
        items.append({"id": case["id"], "set": case["set"], "text": case["text"], "label": case["label"],
                      "difficulty": case.get("difficulty"), "clear": case.get("clear")})
    for item in items:
        if item["set"] == "catalog":
            item["clear"] = ((cases or {}).get("catalogClear") or {}).get(item["id"])
    return items


def run_acts(items: list[dict], url: str, model: str, receipts, ask=post) -> list[dict]:
    results = []
    arms = [("act15-v1", OPTIONS_V1), ("act15-v2", OPTIONS_V2), ("act6-v1", OPTIONS_V1)]
    for item in items:
        state = f'The newcomer says: "{item["text"]}"'
        for arm, options in arms:
            keys = six_options(item["label"], item["id"]) if arm == "act6-v1" else hashed(KINDS, item["id"])
            body, mappings, size = build_request(state, ACT_INSTRUCTIONS, keys, options.__getitem__, model)
            if size > LAYA_TEXT_LIMIT:
                raise ValueError(f'{item["id"]} {arm}: {size} characters is over the {LAYA_TEXT_LIMIT} budget')
            reply, ms = ask(url, body)
            problems = usage_problems(reply)
            receipts.write(json.dumps({"id": item["id"], "arm": arm, "model": model, "request": body, "reply": reply, "ms": ms}) + "\n")
            if problems:
                results.append({**item, "arm": arm, "model": model, "error": ", ".join(problems), "chars": size})
                continue
            balanced, raw = aggregate(reply, mappings)
            order = ranked(balanced)
            results.append({**item, "arm": arm, "model": model, "chars": size, "ms": round(ms, 1),
                            "predicted": order[0], "top3": order[:3], "p": round(balanced[order[0]], 4),
                            "pTrue": round(balanced[item["label"]], 4), "raw": ranked(raw)[0],
                            "r0Slot": next(int(label[1:]) for label, key in mappings["r0"].items() if key == ranked(raw)[0])})
    return results


def run_claims(cases: list[dict], url: str, model: str, receipts, ask=post) -> list[dict]:
    results = []
    for case in cases:
        known = {k["key"]: k["text"] for k in case["known"]}
        known[NONE_KEY] = "none of these"
        keys = hashed(known, case["id"])
        state = f'The newcomer says to {case["listener"]}: "{case["line"]}"'
        body, mappings, size = build_request(state, CLAIM_INSTRUCTIONS, keys, known.__getitem__, model)
        if size > LAYA_TEXT_LIMIT:
            raise ValueError(f'{case["id"]}: {size} characters is over the {LAYA_TEXT_LIMIT} budget')
        reply, ms = ask(url, body)
        problems = usage_problems(reply)
        receipts.write(json.dumps({"id": case["id"], "arm": "claim", "model": model, "request": body, "reply": reply, "ms": ms}) + "\n")
        if problems:
            results.append({"id": case["id"], "set": case["set"], "type": case["type"], "label": case["answer"],
                            "model": model, "chars": size, "error": ", ".join(problems)})
            continue
        balanced, raw = aggregate(reply, mappings)
        order = ranked(balanced)
        results.append({"id": case["id"], "set": case["set"], "type": case["type"], "label": case["answer"],
                        "clear": case.get("clear"), "model": model, "chars": size, "ms": round(ms, 1),
                        "options": len(keys), "predicted": order[0], "p": round(balanced[order[0]], 4),
                        "pTrue": round(balanced[case["answer"]], 4), "raw": ranked(raw)[0]})
    return results


def summarise(rows: list[dict], key: Callable[[dict], str]) -> dict[str, dict]:
    groups: dict[str, list[dict]] = defaultdict(list)
    for row in rows:
        if "error" not in row:
            groups[key(row)].append(row)
    out = {}
    for name, members in sorted(groups.items()):
        n = len(members)
        out[name] = {"n": n, "top1": round(sum(r["predicted"] == r["label"] for r in members) / n, 4),
                     "raw": round(sum(r["raw"] == r["label"] for r in members) / n, 4),
                     "meanPTrue": round(sum(r["pTrue"] for r in members) / n, 4)}
        if "top3" in members[0]:
            out[name]["top3"] = round(sum(r["label"] in r["top3"] for r in members) / n, 4)
            out[name]["tone"] = round(sum(tone(r["predicted"]) == tone(r["label"]) for r in members) / n, 4)
    return out


def median(values: list[float]) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    mid = len(ordered) // 2
    return ordered[mid] if len(ordered) % 2 else (ordered[mid - 1] + ordered[mid]) / 2


def confusion(rows: list[dict]) -> dict[str, dict[str, int]]:
    table: dict[str, Counter] = defaultdict(Counter)
    for row in rows:
        if "error" not in row:
            table[row["label"]][row["predicted"]] += 1
    return {label: dict(sorted(counts.items(), key=lambda kv: -kv[1])) for label, counts in sorted(table.items())}


def calibration(rows: list[dict]) -> list[dict]:
    bins = [(0, .2), (.2, .4), (.4, .6), (.6, .8), (.8, 1.01)]
    out = []
    for lo, hi in bins:
        members = [r for r in rows if "error" not in r and lo <= r["p"] < hi]
        if members:
            out.append({"p": f"{lo:.1f}-{min(hi, 1):.1f}", "n": len(members),
                        "accuracy": round(sum(r["predicted"] == r["label"] for r in members) / len(members), 4)})
    return out


def gate(rows: list[dict], thresholds=(0.5, 0.6, 0.7, 0.8)) -> list[dict]:
    """The confidence rule's trade: at each threshold, the share of lines whose reading is taken and
    how often it is right, and for the rest (the player would pick from the top three) how often the
    top three hold the right act."""
    scored = [r for r in rows if "error" not in r]
    out = []
    for t in thresholds:
        above = [r for r in scored if r["p"] >= t]
        below = [r for r in scored if r["p"] < t]
        out.append({"threshold": t, "taken": round(len(above) / len(scored), 4) if scored else None,
                    "takenRight": round(sum(r["predicted"] == r["label"] for r in above) / len(above), 4) if above else None,
                    "belowTop3": round(sum(r["label"] in r["top3"] for r in below) / len(below), 4) if below else None})
    return out


def summary(acts: list[dict], claims: list[dict], models: list[str]) -> dict:
    """Every table the write-up quotes, from the scored rows (refused replies are counted, not scored)."""
    return {
        "acts": summarise(acts, lambda r: f'{r["model"]} {r["arm"]} {r["set"]}'),
        "actsClear": summarise([r for r in acts if r.get("clear")], lambda r: f'{r["model"]} {r["arm"]} {r["set"]}'),
        "actsByDifficulty": summarise([r for r in acts if r.get("difficulty")], lambda r: f'{r["model"]} {r["arm"]} {r["set"]} {r["difficulty"]}'),
        "actsByKind": summarise(acts, lambda r: f'{r["model"]} {r["arm"]} {r["label"]}'),
        "claims": summarise(claims, lambda r: f'{r["model"]} {r["type"]}'),
        "claimsAll": summarise(claims, lambda r: r["model"]),
        "confusion": {f"{m} {a}": confusion([r for r in acts if r["model"] == m and r["arm"] == a])
                      for m in models for a in ("act15-v1", "act15-v2", "act6-v1")},
        "calibration": {f"{m} {a}": calibration([r for r in acts if r["model"] == m and r["arm"] == a])
                        for m in models for a in ("act15-v1", "act15-v2", "act6-v1")},
        "r0Slots": {f"{m} {a}": dict(sorted(Counter(r["r0Slot"] for r in acts if r["model"] == m and r["arm"] == a and "error" not in r).items()))
                    for m in models for a in ("act15-v1", "act15-v2", "act6-v1")},
        "gate": {f"{m} {a} {group}": gate([r for r in acts if r["model"] == m and r["arm"] == a and r["set"] in sets])
                 for m in models for a in ("act15-v1", "act6-v1")
                 for group, sets in (("player", {"player"}), ("player+smalltalk", {"player", "smalltalk"}))},
        "errors": sum("error" in r for r in acts + claims),
        "maxChars": max([r.get("chars", 0) for r in acts + claims] or [0]),
        "medianMs": {arm: median([r["ms"] for r in acts + claims if "ms" in r and r.get("arm", "claim") == arm])
                     for arm in ("act15-v1", "act15-v2", "act6-v1", "claim")},
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--catalog", default="sim/UnderGlass.Sim/speech-catalog.json")
    parser.add_argument("--cases", help="authored and judged cases (acts, catalogClear, claims)")
    parser.add_argument("--laya-url", default="http://127.0.0.1:8000")
    parser.add_argument("--models", nargs="+", default=["typed-decisions"])
    parser.add_argument("--out", required=True)
    parser.add_argument("--limit", type=int, help="first N act items only (a smoke run)")
    args = parser.parse_args(argv)
    if not args.laya_url.startswith(("http://127.0.0.1", "http://localhost", "http://[::1]")):
        parser.error("Laya must be on loopback")
    url = args.laya_url.rstrip("/") + "/v1/systemone"
    catalog = json.loads(Path(args.catalog).read_text(encoding="utf-8"))
    cases = json.loads(Path(args.cases).read_text(encoding="utf-8")) if args.cases else None
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    items = act_items(catalog, cases)[: args.limit] if args.limit else act_items(catalog, cases)
    health = json.loads(urllib.request.urlopen(args.laya_url.rstrip("/") + "/health", timeout=5).read())
    results = {"health": health, "models": args.models, "budget": LAYA_TEXT_LIMIT, "acts": [], "claims": []}
    with (out / "receipts.jsonl").open("w", encoding="utf-8") as receipts:
        for model in args.models:
            results["acts"] += run_acts(items, url, model, receipts)
            results["claims"] += run_claims((cases or {}).get("claims", []), url, model, receipts)
    results["summary"] = summary(results["acts"], results["claims"], args.models)
    (out / "results.json").write_text(json.dumps(results, indent=1), encoding="utf-8")
    print(json.dumps(results["summary"]["acts"], indent=1))
    print(json.dumps(results["summary"]["claimsAll"], indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
