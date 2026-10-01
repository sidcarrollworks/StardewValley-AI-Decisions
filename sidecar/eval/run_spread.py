"""Run the character-spread eval against a local Laya server (docs/spec/laya.md,
"Character spread"): every villager's card is asked the same fixed questions, once per
question type, to measure whether the model's answers spread across characters and follow
the trait each question should depend on.

Cards come from cards.json (built by tools/CardExporter: variant A is the card the mod
sends today, variant B adds the viewer's leanings summary line). Trait values come from
the seed temperament table. Two mid-game runs (hearts 4 and 6) ask all eight question
types; the newcomer run (hearts 0) asks the newcomer-week welcome question.

Per question, card variant and run it reports the spread (90th minus 10th percentile),
median, min, max, Spearman's rank correlation with the trait, and the median latency.
A question is marked flat when its spread is under FLAT_SPREAD (0.05) and
"doesn't follow personality" when the correlation is under FOLLOWS_MIN (0.3) in the
expected direction. The hearts-4 run's medians and spreads are written to
data/laya-calibration.json.

Usage: python run_spread.py [base_url] [--model typed-decisions|english]
Set LAYA_API_KEY in the environment if the server requires a bearer token.
Standard library only.
"""

import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = (sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--")
        else "http://127.0.0.1:8000").rstrip("/")
MODEL = "typed-decisions"
if "--model" in sys.argv:
    MODEL = sys.argv[sys.argv.index("--model") + 1]

FLAT_SPREAD = 0.05
FOLLOWS_MIN = 0.3

HERE = os.path.dirname(os.path.abspath(__file__))
CARDS = json.load(open(os.path.join(HERE, "cards.json"), encoding="utf-8"))
TRAITS = json.load(open(os.path.join(HERE, "..", "..", "fixtures", "game", "temperament", "temperament.json"), encoding="utf-8"))

# id -> (question text with {npc}, trait, expected direction (+1 / -1), context after the card)
QUESTIONS = {
    "attention_emote": ("Should {npc} try to get the player's attention with an emote now?",
                        "boldness", +1, ""),
    "attention_bubble": ("Should {npc} try to get the player's attention with a speech bubble now?",
                         "boldness", +1, ""),
    "attention_approach": ("Should {npc} drop what they are doing and go looking for the player now?",
                 "boldness", +1, ""),
    "speak": ("Does {npc} have news for the player?",
              "chattiness", +1, ""),
    "hold_against": ("Would {npc} hold this against the player?",
                     "forgiveness", -1,
                     "\ngrudge:\n"
                     "- two days ago the player gave them a hated gift\n"
                     "- yesterday they tried to get the player's attention and were ignored\n"),
    "close_friendly": ("Would {npc} walk over to greet the player now?",
                       "boldness", +1,
                       "\nmotive:\n"
                       "- reason: they miss the player (they have not talked in two days)\n"
                       "- act: walk over and greet\n"
                       "- effective boldness: 0.6 of 1, cost: 0.3 of 1\n"),
    "close_hostile": ("Would {npc} confront the player now?",
                      "boldness", +1,
                      "\nmotive:\n"
                      "- reason: the player stood them up two days ago and never apologized\n"
                      "- act: confront them about it\n"
                      "- effective boldness: 0.6 of 1, cost: 0.5 of 1\n"),
}

CHOOSE = {
    "write": "write a note instead",
    "silent": "say nothing",
    "walk": "walk over and say it in person",
}

WELCOME = "Would {npc} go out of their way to welcome a newcomer in person?"


def post(state, model, questions):
    """One batched request: a state and several questions. Returns (answers dict, ms)."""
    body = {"state": state, "model": model, "questions": questions}
    headers = {"Content-Type": "application/json"}
    key = os.environ.get("LAYA_API_KEY")
    if key:
        headers["Authorization"] = "Bearer " + key
    req = urllib.request.Request(BASE + "/v1/systemone",
                                 data=json.dumps(body).encode("utf-8"),
                                 headers=headers, method="POST")
    start = time.monotonic()
    with urllib.request.urlopen(req, timeout=180) as resp:
        result = json.load(resp)
    return result["answers"], (time.monotonic() - start) * 1000


def warm_up(model):
    try:
        post("npc: Warmup\n", model, {"q": {"type": "noul", "instructions": "Is this a warm-up call?"}})
    except Exception:
        pass  # the first real call will still work; it is just slower


def median(values):
    values = sorted(values)
    n = len(values)
    if n == 0:
        return float("nan")
    mid = n // 2
    return values[mid] if n % 2 else (values[mid - 1] + values[mid]) / 2


def percentile(values, q):
    values = sorted(values)
    if not values:
        return float("nan")
    pos = (len(values) - 1) * q
    low = int(pos)
    high = min(low + 1, len(values) - 1)
    return values[low] + (values[high] - values[low]) * (pos - low)


def rank_series(values):
    """Average ranks of a series (ties share the mean rank)."""
    order = sorted(range(len(values)), key=lambda i: values[i])
    ranks = [0.0] * len(values)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and values[order[j + 1]] == values[order[i]]:
            j += 1
        avg = (i + j) / 2.0 + 1  # 1-based
        for k in range(i, j + 1):
            ranks[order[k]] = avg
        i = j + 1
    return ranks


def spearman(a, b):
    """Spearman's rank correlation (Pearson on ranks). NaN-safe: drops NaN pairs."""
    pairs = [(x, y) for x, y in zip(a, b) if x == x and y == y]
    if len(pairs) < 2:
        return float("nan")
    ra = rank_series([p[0] for p in pairs])
    rb = rank_series([p[1] for p in pairs])
    n = len(pairs)
    ma, mb = sum(ra) / n, sum(rb) / n
    cov = sum((ra[i] - ma) * (rb[i] - mb) for i in range(n))
    va = sum((ra[i] - ma) ** 2 for i in range(n))
    vb = sum((rb[i] - mb) ** 2 for i in range(n))
    if va == 0 or vb == 0:
        return float("nan")
    return cov / (va * vb) ** 0.5


def ask_run(npc_names, variant, hearts, question_ids):
    """Ask the given questions of every villager's card (variant, hearts). One request per
    question (each has its own reference situation appended to the card). Returns
    {question_id: (values, latencies)}."""
    by_question = {qid: ([], []) for qid in question_ids}
    done = 0
    for npc in npc_names:
        card = CARDS["cards"][npc][variant][str(hearts)]
        for qid in question_ids:
            if qid == "choose":
                question = {"type": "choice",
                            "instructions": "What would {0} do?".format(npc),
                            "criteria": dict(CHOOSE)}
                state = card + ("\nmotive:\n"
                                "- reason: they miss the player (they have not talked in two days)\n"
                                "- act: walk over and say it in person\n"
                                "- effective boldness: 0.6 of 1, cost: 0.3 of 1\n")
            elif qid == "welcome":
                question = {"type": "noul", "instructions": WELCOME.format(npc=npc)}
                state = card
            else:
                text, _trait, _dir, context = QUESTIONS[qid]
                question = {"type": "noul", "instructions": text.format(npc=npc)}
                state = card + context
            answers, ms = post(state, MODEL, {"q": question})
            answer = answers.get("q", {})
            if qid == "choose":
                value = answer.get("probabilities", {}).get("walk", float("nan"))
            else:
                value = answer.get("noul", float("nan"))
            by_question[qid][0].append(value)
            by_question[qid][1].append(ms)
            done += 1
            if done % 34 == 0:
                print("  %s %s hearts=%s: %d/%d done (last %.0f ms)" % (
                    variant, hearts, qid, done, len(npc_names) * len(question_ids), ms), flush=True)
    return by_question


def summarize(question_ids, npc_names, variant, hearts, label):
    """One summary row per question for a (variant, hearts) run."""
    rows = {}
    for qid in question_ids:
        by_question = ask_run(npc_names, variant, hearts, [qid])
        values, latencies = by_question[qid]
        rows[qid] = {
            "spread": percentile(values, 0.9) - percentile(values, 0.1),
            "median": median(values),
            "min": min(values),
            "max": max(values),
            "latency_ms": median(latencies),
            "n": len(values),
        }
        if qid == "choose":
            trait_name = "boldness"
            direction = +1
        elif qid == "welcome":
            trait_name = "boldness"
            direction = +1
        else:
            _text, trait_name, direction, _ctx = QUESTIONS[qid]
        traits = [TRAITS["characters"][n]["boldness" if trait_name == "boldness" else trait_name] for n in npc_names]
        r = spearman(values, traits)
        rows[qid]["spearman"] = r
        rows[qid]["flat"] = rows[qid]["spread"] < FLAT_SPREAD
        rows[qid]["follows"] = (r >= FOLLOWS_MIN) if direction > 0 else (r <= -FOLLOWS_MIN)
        rows[qid]["label"] = label
    return rows


def main():
    npc_names = sorted(CARDS["cards"].keys())
    print("character-spread eval: %s on %s (%d villagers)" % (MODEL, BASE, len(npc_names)))
    warm_up(MODEL)

    mid_ids = list(QUESTIONS.keys()) + ["choose"]
    results = {}
    for variant in ("A", "B"):
        for hearts in (4, 6):
            label = "hearts %d" % hearts
            results[(variant, label)] = summarize(mid_ids, npc_names, variant, hearts, label)
    for variant in ("A", "B"):
        results[(variant, "newcomer 0")] = summarize(["welcome"], npc_names, variant, 0, "newcomer 0")

    # Report table
    print()
    for (variant, label), rows in results.items():
        print("== variant %s, %s ==" % (variant, label))
        print("%-28s %7s %7s %7s %7s %7s %7s %s" % ("question", "spread", "median", "min", "max", "spearman", "lat.ms", "marks"))
        for qid, row in rows.items():
            marks = []
            if row["flat"]:
                marks.append("FLAT")
            if not row["follows"]:
                marks.append("no-follow")
            print("%-28s %7.3f %7.3f %7.3f %7.3f %7.3f %7.1f %s" % (
                qid, row["spread"], row["median"], row["min"], row["max"], row["spearman"],
                row["latency_ms"], " ".join(marks)))
        print()

    # Calibration file: hearts-4 run (the mid-game reference) plus the newcomer welcome.
    calibration = {
        "note": ("Per-question medians and spreads of the model's answers over all villagers' "
                 "cards, from the character-spread eval (sidecar/eval/run_spread.py, the hearts-4 "
                 "reference run; the welcome question from the hearts-0 newcomer run). The "
                 "corrections (RelativeScale and w) are deliberately absent: they stay off until "
                 "a spread run says a question needs them (docs/spec/laya.md, 'Character spread')."),
        "checkpoint": MODEL,
        "reference_hearts": 4,
        "flat_spread": FLAT_SPREAD,
        "follows_min": FOLLOWS_MIN,
        "questions": {},
    }
    for qid in mid_ids:
        row = results[("A", "hearts 4")][qid]
        calibration["questions"][qid] = {
            "A": {"median": row["median"], "spread": row["spread"]},
            "B": {"median": results[("B", "hearts 4")][qid]["median"],
                  "spread": results[("B", "hearts 4")][qid]["spread"]},
        }
    welcome_row_a = results[("A", "newcomer 0")]["welcome"]
    welcome_row_b = results[("B", "newcomer 0")]["welcome"]
    calibration["questions"]["welcome_newcomer"] = {
        "A": {"median": welcome_row_a["median"], "spread": welcome_row_a["spread"]},
        "B": {"median": welcome_row_b["median"], "spread": welcome_row_b["spread"]},
    }
    # The planner's real choice question ("which of these would X most want to bring up?") is not
    # measured by this eval: mark it explicitly not calibrated so the mod's test can see that.
    calibration["questions"]["choose_bring_up"] = {
        "A": {"median": None, "spread": None},
        "B": {"median": None, "spread": None},
    }
    out = os.path.join(HERE, "..", "..", "data", "laya-calibration.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(calibration, f, indent=2)
        f.write("\n")
    print("wrote %s" % os.path.normpath(out))


if __name__ == "__main__":
    main()
