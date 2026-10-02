"""The notice-board experiment (docs/spec/notice-board.md, roadmap step 21, first stage): how
would each villager react to a note the player pinned on the town board?

Every villager's card (cards.json, variant A, hearts 4) gets the same note, in exactly the state
and question shape the mod will send (src/NpcBoard/NoteReactions.cs): the card, the note as
quoted data, and how the reader feels about the author; a choice over six reactions with the
mod's generic choice instruction and option labels o0..o5 (LayaDecisionClient). The model never
writes text: it only picks a reaction, which the mod turns into an emote and a templated line.

It prints, per note, each villager's reaction and top two probabilities, how the town split, and
the spread: the most any one reaction's probability varied between villagers. A spread under
0.05 means the villagers barely differ (the character-spread worry, docs/spec/laya.md).

Usage:
    python run_notes.py [base_url] [--model typed-decisions|english] [--note "your note"] ...
Without --note it runs five sample notes. Repeat --note to try several of your own.
Set LAYA_API_KEY in the environment if the server requires a bearer token.
Standard library only.
"""

import json
import os
import sys
import time
import urllib.request

BASE = (sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--")
        else "http://127.0.0.1:8000").rstrip("/")
MODEL = "typed-decisions"
if "--model" in sys.argv:
    MODEL = sys.argv[sys.argv.index("--model") + 1]
NOTES = [sys.argv[i + 1] for i, a in enumerate(sys.argv) if a == "--note" and i + 1 < len(sys.argv)]

SAMPLE_NOTES = [
    "Thank you all for the warm welcome. This town already feels like home.",
    "Lost: one chicken. Answers to Gerald. Last seen judging me.",
    "Whoever keeps going through my trash: I know it's you.",
    "Free parsnips at the farm gate tomorrow morning, take as many as you like!",
    "Some days I wonder why I left the city at all.",
]

# The order and wording of NoteReactions.Order / NoteReactions.Option.
REACTIONS = [
    ("Amused", "finds it funny"),
    ("Touched", "is touched by it"),
    ("Curious", "is curious about it"),
    ("Annoyed", "is annoyed by it"),
    ("Offended", "is offended by it"),
    ("Indifferent", "doesn't care about it"),
]
MAX_NOTE_CHARS = 200
FLAT_SPREAD = 0.05

HERE = os.path.dirname(os.path.abspath(__file__))
CARDS = json.load(open(os.path.join(HERE, "cards.json"), encoding="utf-8"))


def clean(text):
    """NoteReactions.Clean: strip the sanitizer's characters, quotes to single, fold space, cap."""
    for ch in "#$%{[":
        text = text.replace(ch, "")
    text = " ".join(text.replace('"', "'").split())
    if len(text) <= MAX_NOTE_CHARS:
        return text
    room = MAX_NOTE_CHARS - 3
    cut = text.rfind(" ", 0, room + 1)
    return (text[:cut] if cut > room // 2 else text[:room]).rstrip() + "..."


def state_of(npc, note):
    """NoteReactions.State: card, then the quoted note, then the reader's feelings for the author
    (no regard in this experiment), sections separated by a blank line (DecisionState)."""
    card = CARDS["cards"][npc]["A"]["4"]
    return "{0}\n\na note pinned on the town notice board, written by the player:\n\"{1}\"\n\n" \
           "{2} reads it. How {2} feels about the player: no strong feelings".format(card.rstrip(), note, npc)


def post(state):
    question = {"type": "choice", "instructions": "Which option fits best?",
                "criteria": {"o%d" % i: text for i, (_, text) in enumerate(REACTIONS)}}
    body = {"state": state, "model": MODEL, "questions": {"q": question}}
    headers = {"Content-Type": "application/json"}
    key = os.environ.get("LAYA_API_KEY")
    if key:
        headers["Authorization"] = "Bearer " + key
    req = urllib.request.Request(BASE + "/v1/systemone", data=json.dumps(body).encode("utf-8"),
                                 headers=headers, method="POST")
    start = time.monotonic()
    with urllib.request.urlopen(req, timeout=180) as resp:
        result = json.load(resp)
    probs = result["answers"].get("q", {}).get("probabilities", {})
    return [float(probs.get("o%d" % i, float("nan"))) for i in range(len(REACTIONS))], (time.monotonic() - start) * 1000


def run(note):
    note = clean(note)
    print('note: "%s"' % note)
    rows = []
    for npc in sorted(CARDS["cards"]):
        p, ms = post(state_of(npc, note))
        if any(x != x for x in p) or max(p) - min(p) < 1e-9:
            rows.append((npc, "Indifferent (no answer)", p))
            continue
        best = max(range(len(p)), key=lambda k: (p[k], -k))
        top = sorted(range(len(p)), key=lambda k: -p[k])[:2]
        rows.append((npc, REACTIONS[best][0], p))
        print("  %-10s %-11s %s  (%.0f ms)" % (npc, REACTIONS[best][0],
              ", ".join("%s %.2f" % (REACTIONS[k][0], p[k]) for k in top), ms))
    counts = {}
    for _, reaction, _ in rows:
        counts[reaction] = counts.get(reaction, 0) + 1
    print("  town: " + ", ".join("%s %d" % kv for kv in sorted(counts.items(), key=lambda kv: -kv[1])))
    answered = [p for _, r, p in rows if not r.endswith("(no answer)")]
    if len(answered) >= 2:
        spread = max(max(p[k] for p in answered) - min(p[k] for p in answered) for k in range(len(REACTIONS)))
        print("  spread: %.2f%s" % (spread, " (flat: the villagers barely differ)" if spread < FLAT_SPREAD else ""))
    print()


def main():
    print("Laya at %s, model %s, %d villagers (cards.json, variant A, hearts 4)\n" % (BASE, MODEL, len(CARDS["cards"])))
    for note in NOTES or SAMPLE_NOTES:
        run(note)


if __name__ == "__main__":
    main()
