"""Offline contracts for the Laya categorization probe: no server, model or network needed."""

import importlib.util
import io
import json
from pathlib import Path
import sys
import unittest

PATH = Path(__file__).resolve().parents[1] / "sim" / "tools" / "laya_categorize_probe.py"
SPEC = importlib.util.spec_from_file_location("laya_categorize_probe", PATH)
probe = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = probe
SPEC.loader.exec_module(probe)


def favouring(slot_bias, truth=None, boost=0.0):
    """A fake Laya that prefers early slots, plus an optional boost for one key wherever it sits."""
    def ask(url, body):
        answers = {}
        for qid, question in body["questions"].items():
            probs = {}
            for label, text in question["criteria"].items():
                slot = int(label[1:])
                probs[label] = slot_bias[slot % len(slot_bias)] + (boost if truth and text == truth else 0)
            total = sum(probs.values())
            answers[qid] = {"type": "choice", "probabilities": {k: v / total for k, v in probs.items()}}
        return {"answers": answers, "usage": {"truncated": False, "state_tokens_dropped": 0}}, 1.0
    return ask


class RotationTests(unittest.TestCase):
    def test_every_key_sits_in_every_slot_once(self):
        keys = ["a", "b", "c", "d"]
        table = probe.rotations(keys)
        self.assertEqual(len(table), 4)
        for slot in range(4):
            self.assertEqual(sorted(row[slot] for row in table), keys)
        self.assertEqual(table[0], keys)

    def test_request_maps_labels_back_and_counts_the_budget_per_question(self):
        body, mappings, size = probe.build_request("The newcomer says: \"Hi\"", "Q?", ["x", "y"], {"x": "ex", "y": "why"}.__getitem__, "m")
        self.assertEqual(body["questions"]["r1"]["criteria"], {"o0": "why", "o1": "ex"})
        self.assertEqual(mappings["r1"], {"o0": "y", "o1": "x"})
        self.assertEqual(size, len("Q?") + len("o0") + len("ex") + len("o1") + len("why") + len("The newcomer says: \"Hi\""))

    def test_balanced_reading_cancels_a_pure_position_bias(self):
        keys = probe.KINDS
        body, mappings, _ = probe.build_request("s", "q", keys, probe.OPTIONS_V1.__getitem__, "m")
        reply, _ = favouring([5, 1, 1, 1])("u", body)
        balanced, raw = probe.aggregate(reply, mappings)
        self.assertAlmostEqual(max(balanced.values()), min(balanced.values()), places=9)
        self.assertEqual(probe.ranked(raw)[0], keys[0])  # raw keeps the bias the rotation removes

    def test_a_real_preference_survives_the_rotation(self):
        body, mappings, _ = probe.build_request("s", "q", probe.KINDS, probe.OPTIONS_V1.__getitem__, "m")
        reply, _ = favouring([3, 1, 1], truth=probe.OPTIONS_V1["Thanked"], boost=2.0)("u", body)
        balanced, _ = probe.aggregate(reply, mappings)
        self.assertEqual(probe.ranked(balanced)[0], "Thanked")


class GuardTests(unittest.TestCase):
    def test_normalise_matches_the_game_and_refuses_bad_values(self):
        self.assertEqual(probe.normalise({"a": 2.0, "b": 1.0}), {"a": 2 / 3, "b": 1 / 3})
        for bad in ({}, {"a": 0.0}, {"a": -1.0}, {"a": float("nan")}):
            with self.assertRaises(ValueError):
                probe.normalise(bad)

    def test_mismatched_labels_are_refused(self):
        with self.assertRaises(ValueError):
            probe.aggregate({"answers": {"r0": {"probabilities": {"o0": 1.0}}}}, {"r0": {"o0": "a", "o1": "b"}})

    def test_truncation_and_collapsed_options_are_reported(self):
        self.assertEqual(probe.usage_problems({"usage": {}}), [])
        self.assertEqual(probe.usage_problems({"usage": {"truncated": True, "state_tokens_dropped": 3,
                                                          "truncated_questions": ["r0"], "options": {"r0": {}}}}),
                         ["truncated", "state tokens dropped", "questions truncated", "options collapsed"])

    def test_distractors_are_deterministic_and_exclude_the_answer(self):
        pool = [k for k in probe.KINDS if k != "SmallTalk"]
        first = probe.distractors("Thanked", pool, 5, "line-1")
        self.assertEqual(first, probe.distractors("Thanked", pool, 5, "line-1"))
        self.assertNotIn("Thanked", first)
        self.assertEqual(len(set(first)), 5)
        self.assertEqual(probe.fnv1a(""), 0x811C9DC5)

    def test_six_options_always_offer_small_talk_and_the_answer(self):
        for label in ("Thanked", "SmallTalk", "Mocked"):
            keys = probe.six_options(label, "line-7")
            self.assertEqual(len(set(keys)), 6)
            self.assertIn(label, keys)
            self.assertIn("SmallTalk", keys)
            self.assertEqual(keys, probe.six_options(label, "line-7"))

    def test_hashed_orders_vary_by_line_and_still_rotate_every_key_through_every_slot(self):
        first, second = probe.hashed(probe.KINDS, "line-1"), probe.hashed(probe.KINDS, "line-2")
        self.assertEqual(sorted(first), sorted(probe.KINDS))
        self.assertNotEqual(first, second)
        self.assertEqual(first, probe.hashed(probe.KINDS, "line-1"))
        for slot in range(len(first)):
            self.assertEqual(sorted(row[slot] for row in probe.rotations(first)), sorted(first))

    def test_every_option_set_fits_the_budget_with_the_longest_catalog_line(self):
        line = "x" * 140
        for options in (probe.OPTIONS_V1, probe.OPTIONS_V2):
            _, _, size = probe.build_request(f'The newcomer says: "{line}"', probe.ACT_INSTRUCTIONS, probe.KINDS, options.__getitem__, "m")
            self.assertLessEqual(size, probe.LAYA_TEXT_LIMIT)
        self.assertEqual(set(probe.OPTIONS_V1), set(probe.OPTIONS_V2))


class RunTests(unittest.TestCase):
    def test_a_run_scores_items_records_receipts_and_skips_refused_replies(self):
        items = [{"id": "t1", "set": "catalog", "text": "Thanks!", "label": "Thanked", "difficulty": None, "clear": True}]
        receipts = io.StringIO()
        rows = probe.run_acts(items, "u", "m", receipts, ask=favouring([1, 1], truth=probe.OPTIONS_V1["Thanked"], boost=5.0))
        self.assertEqual({r["arm"] for r in rows}, {"act15-v1", "act15-v2", "act6-v1"})
        v1 = next(r for r in rows if r["arm"] == "act15-v1")
        self.assertEqual(v1["predicted"], "Thanked")
        self.assertEqual(len(receipts.getvalue().splitlines()), 3)
        refused = probe.run_acts(items, "u", "m", io.StringIO(), ask=lambda u, b: ({"answers": {}, "usage": {"truncated": True}}, 1.0))
        self.assertTrue(all(r["error"] == "truncated" for r in refused))

    def test_claims_offer_none_of_these_and_score_the_key(self):
        case = {"id": "c1", "set": "claims-a", "type": "direct", "listener": "Willy", "line": "I brought the fish.",
                "known": [{"key": "k1", "text": "the fish Willy asked for"}, {"key": "k2", "text": "the broken rod"}], "answer": "k1"}
        receipts = io.StringIO()
        rows = probe.run_claims([case], "u", "m", receipts, ask=favouring([1, 1, 1], truth="the fish Willy asked for", boost=4.0))
        self.assertEqual(rows[0]["predicted"], "k1")
        self.assertEqual(rows[0]["options"], 3)
        sent = json.loads(receipts.getvalue())["request"]["questions"]["r0"]["criteria"]
        self.assertIn("none of these", sent.values())

    def test_only_act_lines_from_the_catalog_are_probed(self):
        catalog = {"lines": [{"id": "a", "kind": "Thanked", "text": "Thanks, {subject}."},
                             {"id": "b", "kind": "PlayedGame.Accepted", "text": "Go on then, one round."}]}
        items = probe.act_items(catalog, None)
        self.assertEqual([(i["id"], i["text"]) for i in items], [("a", "Thanks, Mara.")])

    def test_a_refused_claim_is_counted_and_the_summary_still_builds(self):
        case = {"id": "c1", "set": "claims-a", "type": "direct", "listener": "Willy", "line": "I brought the fish.",
                "known": [{"key": "k1", "text": "the fish Willy asked for"}], "answer": "k1"}
        rows = probe.run_claims([case], "u", "m", io.StringIO(), ask=lambda u, b: ({"answers": {}, "usage": {"truncated": True}}, 1.0))
        self.assertEqual((rows[0]["error"], rows[0]["label"]), ("truncated", "k1"))
        self.assertGreater(rows[0]["chars"], 0)
        built = probe.summary([], rows, ["m"])
        self.assertEqual(built["errors"], 1)
        self.assertEqual(built["medianMs"]["claim"], None)

    def test_the_gate_reports_what_is_taken_and_what_the_top_three_hold_below_it(self):
        rows = [{"label": "Thanked", "predicted": "Thanked", "p": 0.9, "top3": ["Thanked", "Joked", "Welcomed"]},
                {"label": "Mocked", "predicted": "Joked", "p": 0.4, "top3": ["Joked", "Mocked", "Argued"]},
                {"label": "Squabbled", "predicted": "Argued", "p": 0.3, "top3": ["Argued", "Joked", "Welcomed"]}]
        at = {g["threshold"]: g for g in probe.gate(rows)}[0.7]
        self.assertEqual((at["taken"], at["takenRight"], at["belowTop3"]), (round(1 / 3, 4), 1.0, 0.5))

    def test_summaries_count_tone_and_top3(self):
        rows = [{"label": "Mocked", "predicted": "Argued", "raw": "Argued", "pTrue": 0.2, "top3": ["Argued", "Mocked", "Joked"]},
                {"label": "Thanked", "predicted": "Thanked", "raw": "Joked", "pTrue": 0.9, "top3": ["Thanked", "Joked", "Welcomed"]}]
        summary = probe.summarise(rows, lambda r: "all")["all"]
        self.assertEqual((summary["top1"], summary["top3"], summary["tone"], summary["raw"]), (0.5, 1.0, 1.0, 0.0))
        self.assertEqual(probe.tone("SmallTalk"), "smalltalk")


if __name__ == "__main__":
    unittest.main()
