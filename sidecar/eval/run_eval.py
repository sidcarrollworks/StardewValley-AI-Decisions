"""Run the golden eval set against a local Laya server and compare checkpoints.

The eval set (docs/spec/laya.md, "Acceptance tests"): pairs of states whose expected
direction is known (a shy NPC should score lower for a bubble than an outgoing one;
loved-gift news should be picked over seeing a housemate). Each case is answered by both
checkpoints; the script reports per-pair agreement and per-call latency.

Usage: python run_eval.py [base_url] [--models typed-decisions english]
Set LAYA_API_KEY in the environment if the server requires a bearer token.
Standard library only. Writes nothing; prints a summary table.
"""

import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = (sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--")
        else "http://127.0.0.1:8000").rstrip("/")
MODELS = ["typed-decisions", "english"]

CARD_HALEY = (
    "npc: Haley\n"
    "temperament: manners polite, outgoing, optimistic\n"
    "voice: sunny, a little vain, warms up slowly\n"
    "hearts with the player: 4 of 10\n"
    "today: spring 12 (Tuesday), sunny, 7:30 PM\n"
)
CARD_PENNY = (
    "npc: Penny\n"
    "temperament: manners polite, shy, neutral\n"
    "voice: gentle, bookish, a little lonely\n"
    "hearts with the player: 2 of 10\n"
    "today: spring 12 (Tuesday), sunny, 7:30 PM\n"
)
CARD_SAM = (
    "npc: Sam\n"
    "temperament: manners neutral, outgoing, optimistic\n"
    "voice: easygoing, playful, loud\n"
    "hearts with the player: 1 of 10\n"
    "today: spring 12 (Tuesday), sunny, 7:30 PM\n"
)
CARD_WILLY = (
    "npc: Willy\n"
    "temperament: manners neutral, neutral, neutral\n"
    "voice: gruff, warm, old sailor\n"
    "hearts with the player: 3 of 10\n"
    "today: spring 12 (Tuesday), rainy, 7:30 PM\n"
)
CARD_ALEX = (
    "npc: Alex\n"
    "temperament: manners polite, outgoing, optimistic\n"
    "voice: brash, athletic, secretly soft\n"
    "hearts with the player: 0 of 10\n"
    "today: spring 12 (Tuesday), sunny, 7:30 PM\n"
)

GIFT_NEWS = "\nnews:\n- yesterday the player gave Haley a sunflower; she loves it\n"
DULL_NEWS = "\nnews:\n- yesterday Haley saw Caroline at home\n"
QUEST_NEWS = "\nnews:\n- yesterday the player completed Willy's fishing request\n"

# (id, kind, a_state, b_state, question_builder, a_better)
# kind: "noul" (compare a.noul vs b.noul, expect a > b) or "choice" (compare a choice prob
# against b's choice prob on the same options).
CASES = [
    ("speak_gift_vs_dull", "noul",
     CARD_HALEY + GIFT_NEWS, CARD_HALEY + DULL_NEWS,
     "Does Haley have something worth telling the player today?"),
    ("speak_quest_vs_dull", "noul",
     CARD_WILLY + QUEST_NEWS, CARD_WILLY + DULL_NEWS,
     "Does Willy have something worth telling the player today?"),
    ("bubble_shy_vs_outgoing", "noul",
     CARD_PENNY, CARD_SAM,
     "Should {npc} try to get the player's attention with a speech bubble now?"),
    ("emote_shy_vs_outgoing", "noul",
     CARD_PENNY, CARD_SAM,
     "Should {npc} try to get the player's attention with an emote now?"),
    ("approach_missing_vs_seen", "noul",
     CARD_ALEX + "\nlast seen: Alex has not seen the player in three days; someone said they are at the mountain\n",
     CARD_ALEX + "\nlast seen: Alex saw the player an hour ago at Pierre's General Store\n",
     "Should Alex drop what they are doing and go looking for the player now?"),
    ("choose_gift_vs_dull", "choice",
     CARD_HALEY + GIFT_NEWS, CARD_HALEY + DULL_NEWS,
     "Which of these would Haley most want to bring up?",
     ),
]

CHOICE_OPTIONS = [
    ("o0", "talk about the sunflower the player gave Haley yesterday"),
    ("o1", "talk about seeing Caroline at home yesterday"),
]


def ask(state, question, model):
    if isinstance(question, str) and question.startswith("Which"):
        q = {"type": "choice", "instructions": question,
             "criteria": {label: desc for label, desc in CHOICE_OPTIONS}}
        question_text = question
    else:
        q = {"type": "noul", "instructions": question}
        question_text = question

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
    answer = result["answers"]["q"]
    if "noul" in answer:
        return answer["noul"], ms
    probs = answer.get("probabilities", {})
    return probs.get("o0", 0.0), ms  # P(first option)


def main():
    print("eval set: %d cases x %d models against %s" % (len(CASES), len(MODELS), BASE))
    print("=" * 78)
    for model in MODELS:
        agree = 0
        latencies = []
        print("model: %s" % model)
        for case_id, kind, state_a, state_b, question, *_ in CASES:
            q_a = question.replace("{npc}", "Penny") if "{npc}" in question else question
            q_b = question.replace("{npc}", "Sam") if "{npc}" in question else question
            # For the bubble/emote pairs, state A is Penny's card and B is Sam's; the question
            # template is per-NPC. The two non-template noul cases use the same question.
            a, ms_a = ask(state_a, q_a, model)
            b, ms_b = ask(state_b, q_b, model)
            latencies += [ms_a, ms_b]
            better = a > b if case_id != "bubble_shy_vs_outgoing" and case_id != "emote_shy_vs_outgoing" else a < b
            agree += 1 if better else 0
            print("  %-24s a=%-6.4f b=%-6.4f  %s  (%.0f/%.0f ms)"
                  % (case_id, a, b, "PASS" if better else "FAIL", ms_a, ms_b))
        latencies.sort()
        p50 = latencies[len(latencies) // 2]
        p95 = latencies[int(len(latencies) * 0.95 - 1)]
        print("  agreement %d/%d; median %.0f ms, p95 %.0f ms" % (agree, len(CASES), p50, p95))
        print("-" * 78)


if __name__ == "__main__":
    try:
        main()
    except urllib.error.HTTPError as e:
        print("HTTP %d: %s" % (e.code, e.read().decode("utf-8", "replace")), file=sys.stderr)
        sys.exit(1)
    except urllib.error.URLError as e:
        print("Could not reach %s: %s (is laya-serve running?)" % (BASE, e.reason), file=sys.stderr)
        sys.exit(1)
