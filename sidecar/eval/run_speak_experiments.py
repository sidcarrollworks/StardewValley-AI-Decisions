"""Speak-question rewording experiments.

The eval set found the weak spot: the planner's speak proposition ("does X have something worth
telling the player today?") clusters at 0.15-0.4 on both checkpoints, below the 0.5 speak
threshold. This script holds the STATES fixed (the real card + news bullets, as the planner
sends them) and sweeps candidate wordings, so the best one can be picked by data instead of
guesswork.

Usage: python run_speak_experiments.py [base_url] [model]
Writes results to speak_experiments.md next to this file.
"""

import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = (sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--")
        else "http://127.0.0.1:8000").rstrip("/")
MODEL = sys.argv[2] if len(sys.argv) > 2 else "typed-decisions"
MODELS = [MODEL] if len(sys.argv) > 2 else ["typed-decisions", "english"]

CARD_HALEY = (
    "npc: Haley\n"
    "temperament: manners polite, outgoing, optimistic\n"
    "voice: sunny, a little vain, warms up slowly\n"
    "hearts with the player: 4 of 10\n"
    "today: spring 13 (Wednesday), sunny, morning\n"
)
CARD_WILLY = (
    "npc: Willy\n"
    "temperament: manners neutral, neutral, neutral\n"
    "voice: gruff, warm, old sailor\n"
    "hearts with the player: 3 of 10\n"
    "today: spring 13 (Wednesday), sunny, morning\n"
)

# (id, card, news bullet as the planner now sends it (NewsPhrasing plain sentences), expected:
# does this deserve a line?) States under MinNews 2.0 never reach the model in-game; they stay as
# negative controls so the wording still has to separate newsy from dull when asked.
STATES = [
    ("gift-loved", CARD_HALEY,
     "- yesterday the player gave Haley a Sunflower (a loved gift)", True),
    ("quest-helped", CARD_WILLY,
     "- yesterday the player completed the request for Willy", True),
    ("festival-talked", CARD_HALEY,
     "- yesterday Haley went to the festival with the player", True),
    ("saw-player", CARD_HALEY,
     "- yesterday Haley saw the player at Pierre's General Store", True),
    ("birthday-forgotten", CARD_HALEY,
     "- yesterday was Haley's birthday and the player forgot", True),
    ("dull-housemate", CARD_HALEY,
     "- yesterday Haley saw Caroline at home", False),
    ("dull-no-news", CARD_HALEY,
     "- yesterday Haley saw Emily at home", False),
]

WORDINGS = [
    ("baseline", "does Haley have something worth telling the player today?"),
    ("wants-to-tell", "is there something Haley wants to tell the player today?"),
    ("bring-up", "would Haley bring this up with the player tomorrow?"),
    ("worth-telling-about", "is this worth telling the player about?"),
    ("news-for-player", "does Haley have news for the player?"),
    ("mention", "should Haley mention this to the player tomorrow?"),
    ("share", "does Haley feel like sharing this with the player?"),
]


def ask(state, wording, npc, model):
    q = {"type": "noul", "instructions": wording.replace("Haley", npc)}
    body = {"state": state, "model": model, "questions": {"q": q}}
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
    ms = (time.monotonic() - start) * 1000
    return result["answers"]["q"]["noul"], ms


def run_model(model):
    rows = []  # per state: dict wording -> p
    latencies = []
    for state_id, card, bullet, _ in STATES:
        npc = "Willy" if "Willy" in card else "Haley"
        state = card + "\nnews:\n" + bullet + "\n"
        probs = {}
        for wording_id, wording in WORDINGS:
            p, ms = ask(state, wording, npc, model)
            probs[wording_id] = p
            latencies.append(ms)
            print("%-10s %-18s %-18s p=%.4f (%.0f ms)" % (model, state_id, wording_id, p, ms))
        rows.append((state_id, probs))
        print("-" * 78)
    return rows, latencies


def main():
    lines = ["# Speak-question rewording experiment (%s)" % time.strftime("%Y-%m-%d"), ""]
    for model in MODELS:
        rows, latencies = run_model(model)

        # Summary: separation between the newsy states and the dull ones, per wording.
        newsy_ids = [s[0] for s in STATES if s[3]]
        dull_ids = [s[0] for s in STATES if not s[3]]
        summary = []
        for wording_id, _ in WORDINGS:
            newsy = [probs[wording_id] for sid, probs in rows if sid in newsy_ids]
            dull = [probs[wording_id] for sid, probs in rows if sid in dull_ids]
            mean_newsy = sum(newsy) / len(newsy)
            mean_dull = sum(dull) / len(dull)
            above = sum(1 for p in newsy if p >= 0.5)
            summary.append((wording_id, mean_newsy, mean_dull, mean_newsy - mean_dull, above))
        summary.sort(key=lambda r: (r[4], r[3]), reverse=True)
        latencies.sort()
        p50 = latencies[len(latencies) // 2]

        lines.append("## Model: %s (median %.0f ms)" % (model, p50))
        lines.append("")
        lines.append("### Per state, per wording (P that the NPC has a line)")
        lines.append("")
        lines.append("| state | " + " | ".join(w for w, _ in WORDINGS) + " |")
        lines.append("|" + "---|" * (len(WORDINGS) + 1))
        for sid, probs in rows:
            lines.append("| %s | %s |" % (sid, " | ".join("%.3f" % probs.get(w, float("nan")) for w, _ in WORDINGS)))
        lines.append("")
        lines.append("### Separation (newsy vs dull), sorted by best")
        lines.append("")
        lines.append("| wording | mean newsy | mean dull | gap | newsy >= 0.5 |")
        lines.append("|---|---|---|---|---|")
        for wording_id, mean_newsy, mean_dull, gap, above in summary:
            lines.append("| %s | %.3f | %.3f | %.3f | %d/%d |" % (wording_id, mean_newsy, mean_dull, gap, above, len(newsy_ids)))
        lines.append("")

    lines.append("Note: the news bullets are the plain sentences the planner now sends (NewsPhrasing),")
    lines.append("and the dull states are below MinNews 2.0, so in-game they never reach the model.")
    lines.append("The speak threshold is now 0.25, a veto floor: the compressed answer band must not")
    lines.append("randomly veto real news (a quest state measured 0.46-0.49 under the chosen wording).")

    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "speak_experiments.md")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("\nwrote %s" % path)


if __name__ == "__main__":
    try:
        main()
    except urllib.error.HTTPError as e:
        print("HTTP %d: %s" % (e.code, e.read().decode("utf-8", "replace")), file=sys.stderr)
        sys.exit(1)
    except urllib.error.URLError as e:
        print("Could not reach %s: %s (is laya-serve running?)" % (BASE, e.reason), file=sys.stderr)
        sys.exit(1)
