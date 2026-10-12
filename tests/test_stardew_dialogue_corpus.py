"""Synthetic corpus tests: no installed game, model, network or extracted dialogue needed."""
import importlib.util
from pathlib import Path
import unittest


MODULE = Path(__file__).resolve().parents[1] / "sim/tools/stardew_dialogue_corpus.py"
spec = importlib.util.spec_from_file_location("stardew_dialogue_corpus", MODULE)
corpus = importlib.util.module_from_spec(spec)
spec.loader.exec_module(corpus)


def asset(name, entries, kind="string-dictionary"):
    return {"asset": name, "entries": entries, "format": kind, "entryCount": len(entries), "sha256": "ABC123"}


def manifest(*assets):
    return {"schemaVersion": 1, "language": "en", "gameVersion": "synthetic-1", "gameAssemblySha256": "DLL123",
            "characterNames": ["Ada", "Bea", "Roommate"], "assets": list(assets), "unsupported": []}


class DialogueParsingTests(unittest.TestCase):
    def texts(self, parsed, role="npc"):
        return [node["text"] for node in parsed["nodes"] if node.get("role") == role]

    def test_portraits_pages_placeholders_and_gender_variants_survive(self):
        result = corpus.parse_dialogue("Hello, @.$h^Hello, friend.$s#$b#Come in.")
        self.assertEqual(["Hello, @.", "Hello, friend.", "Come in."], self.texts(result))
        self.assertEqual(["h"], result["nodes"][0]["portraits"])
        self.assertEqual(["s"], result["nodes"][1]["portraits"])
        self.assertEqual(1, result["nodes"][1]["variant"]["gender"])
        self.assertIn("page-break", [node["kind"] for node in result["nodes"]])

    def test_player_answer_is_never_npc_voice(self):
        result = corpus.parse_dialogue("$q 1/2 old#What do you think?#$r 1 50 agree#I agree.#Thanks.$h")
        self.assertEqual(["What do you think?", "Thanks."], self.texts(result))
        self.assertEqual(["I agree."], self.texts(result, "player"))
        answer = next(node for node in result["nodes"] if node.get("role") == "player")
        self.assertEqual({"id": "1", "friendship": 50, "replyKey": "agree"}, answer["answer"])

    def test_inline_question_keeps_player_options_and_conditional_npc_replies(self):
        result = corpus.parse_dialogue("$y 'Ready?_Yes_Let us go._No_Then we can wait.'")
        self.assertEqual(["Ready?", "Let us go.", "Then we can wait."], self.texts(result))
        self.assertEqual(["Yes", "No"], self.texts(result, "player"))
        self.assertEqual([0, 1], [node["replyToInlineAnswer"] for node in result["nodes"] if "replyToInlineAnswer" in node])

    def test_world_state_branches_are_not_collapsed(self):
        result = corpus.parse_dialogue("$d restored#The shop reopened.|The shop is shut.")
        self.assertEqual(["The shop reopened.", "The shop is shut."], self.texts(result))
        text = [node for node in result["nodes"] if node["kind"] == "text"]
        self.assertEqual([0, 1], [node["variant"]["branch"] for node in text])
        self.assertTrue(all(node["conditions"] == ["$d restored"] for node in text))

    def test_random_alternatives_remain_separate(self):
        result = corpus.parse_dialogue("Hello.||Good morning.")
        self.assertEqual(["Hello.", "Good morning."], self.texts(result))
        self.assertEqual([0, 1], [node["variant"]["random"] for node in result["nodes"]])

    def test_unknown_command_is_not_promoted_to_speech(self):
        raw = "$unknown private_engine_argument"
        result = corpus.parse_dialogue(raw)
        self.assertEqual([], self.texts(result))
        self.assertEqual(raw, result["nodes"][0]["raw"])
        self.assertTrue(result["warnings"])

    def test_numbered_flag_is_not_a_portrait_with_accidental_spoken_key(self):
        result = corpus.parse_dialogue("$1 SecretFlag#A real line.")
        self.assertEqual(["A real line."], self.texts(result))
        self.assertEqual("conditional-command", result["nodes"][0]["kind"])

    def test_broken_inline_question_and_dangling_answer_are_explicit(self):
        for raw in ("$y 'Unpaired_answer'", "$r 1 0 reply"):
            self.assertTrue(corpus.parse_dialogue(raw)["warnings"])

    def test_reference_parser_preserves_exact_case_and_normalizes_asset_separator(self):
        self.assertEqual([{"asset": "Strings/Characters", "key": "Exact_Key"}],
                         corpus.references("[LocalizedText Strings\\Characters:Exact_Key]"))
        self.assertEqual([{"asset": "Strings/schedules/Ada", "key": "summer.0"}],
                         corpus.references("Strings\\schedules\\Ada:summer.0"))


class CorpusTests(unittest.TestCase):
    def test_raw_source_conditions_hash_and_original_markup_are_retained(self):
        raw = "$d flag#One.$h|Two.$s"
        result, people, report = corpus.build_corpus(manifest(asset("Characters/Dialogue/Ada", {"summer_Mon8": raw})))
        row = result["records"][0]
        self.assertEqual(raw, row["raw"])
        self.assertEqual({"rawKey": "summer_Mon8"}, row["conditions"])
        self.assertEqual({"asset": "Characters/Dialogue/Ada", "key": "summer_Mon8", "sha256": "ABC123", "gameVersion": "synthetic-1"}, row["source"])
        self.assertEqual("Ada", people["Ada"]["records"][0]["speaker"])
        self.assertEqual(1, report["rawStringEntries"])

    def test_event_speakers_and_quoted_slashes_keep_conditions_and_context(self):
        raw = 'music/0 0/Ada 1 1 2/speak Ada "A/B is a choice.$h"/speak Bea "Wait."'
        result, people, _ = corpus.build_corpus(manifest(asset("Data/Events/Room", {"17/f Ada 1000": raw})))
        speech = [row for row in result["records"] if row["category"] == "event-speech"]
        self.assertEqual(["Ada", "Bea"], [row["speaker"] for row in speech])
        self.assertEqual("A/B is a choice.$h", speech[0]["raw"])
        self.assertEqual(["f Ada 1000"], speech[0]["conditions"]["eventPreconditions"])
        self.assertEqual(raw, people["Ada"]["sourceContexts"][speech[0]["parentId"]]["raw"])

    def test_event_question_and_fork_are_not_assigned_to_an_npc(self):
        raw = 'music/0 0/Ada 1 1 2/question fork "Do it?#Yes#No"/fork other/speak Ada "All right."'
        result, _, _ = corpus.build_corpus(manifest(asset("Data/Events/Room", {"1": raw})))
        controls = [row for row in result["records"] if row["category"] == "event-control"]
        self.assertEqual(2, len(controls))
        self.assertTrue(all(row["speaker"] is None and row["parsed"]["warnings"] for row in controls))

    def test_case_sensitive_event_conditions_stay_distinct(self):
        result, _, _ = corpus.build_corpus(manifest(asset("Data/Events/Room", {
            "1/o Ada": 'speak Ada "First."', "1/O Ada": 'speak Ada "Second."'})))
        source_keys = {row["source"]["key"] for row in result["records"]}
        self.assertEqual({"1/o Ada", "1/O Ada"}, source_keys)
        self.assertEqual(len(result["records"]), len({row["id"] for row in result["records"]}))

    def test_shared_key_hints_do_not_claim_speaker_identity(self):
        _, people, _ = corpus.build_corpus(manifest(asset("Strings/Characters", {"Ada_Bea_Argument": "They argued."})))
        row = people["Ada"]["records"][0]
        self.assertIsNone(row["speaker"])
        self.assertEqual("key-hint", row["attribution"])
        self.assertEqual(["Ada", "Bea"], row["speakerCandidates"])

    def test_generic_marriage_lines_remain_shared_and_include_roommate_fallback(self):
        _, people, _ = corpus.build_corpus(manifest(
            asset("Data/Characters", {"Ada": {"CanBeRomanced": True}, "Bea": {"CanBeRomanced": False}}, "character-metadata"),
            asset("Characters/Dialogue/MarriageDialogue", {"Rainy_0": "Stay a while."}),
            asset("Characters/Dialogue/MarriageDialogueRoommate", {"Indoor_0": "Hello."})))
        row = people["Ada"]["records"][0]
        self.assertIsNone(row["speaker"])
        self.assertEqual("shared-spouse", row["attribution"])
        self.assertEqual(["Ada", "Roommate"], row["speakerCandidates"])
        self.assertEqual([], people["Bea"]["records"])
        self.assertEqual(2, len(people["Roommate"]["records"]))

    def test_gift_ids_do_not_become_dialogue(self):
        result, people, _ = corpus.build_corpus(manifest(asset("Data/NPCGiftTastes", {
            "Ada": "Lovely!/72 74/Thanks./80/No./90/Horrible!/92/Okay./94/extra", "Universal_Love": "72 74"})))
        lines = [row for row in people["Ada"]["records"] if row["category"] == "gift-reaction"]
        self.assertEqual(5, len(lines))
        self.assertEqual({"Lovely!", "Thanks.", "No.", "Horrible!", "Okay."}, {row["raw"] for row in lines})
        self.assertTrue(all(row["parsed"]["nodes"] == [] for row in result["records"] if row["category"] == "gift-taste-source"))

    def test_phone_reference_inherits_speaker_and_preserves_source_and_condition(self):
        result, people, _ = corpus.build_corpus(manifest(
            asset("Data/IncomingPhoneCalls", {"call1": {"FromNpc": "Ada", "Dialogue": "[LocalizedText Strings\\Characters:SharedCall]", "TriggerCondition": "SEASON winter"}}, "phone-call-metadata"),
            asset("Strings/Characters", {"SharedCall": "Are you there?"})))
        resolved = next(row for row in people["Ada"]["records"] if row["category"] == "resolved-reference")
        self.assertEqual("Ada", resolved["speaker"])
        self.assertEqual("Strings/Characters", resolved["source"]["asset"])
        self.assertEqual("Data/IncomingPhoneCalls", resolved["referenceCaller"]["asset"])
        self.assertEqual("SEASON winter", resolved["conditions"]["TriggerCondition"])
        self.assertTrue(any(row["speaker"] is None for row in result["records"] if row["category"] == "shared-string"))

    def test_unresolved_reference_and_unavailable_assets_are_reported(self):
        source = manifest(asset("Characters/Dialogue/Ada", {"Mon": "[LocalizedText Strings\\Missing:Key]"}))
        source["unsupported"] = [{"asset": "Strings/credits", "reason": "typed list"}]
        result, _, report = corpus.build_corpus(source)
        self.assertTrue(result["records"][0]["parsed"]["warnings"])
        self.assertEqual(source["unsupported"], report["unsupportedAssets"])
        self.assertIn("Not a claim to contain every line reachable in the game.", report["limitations"])

    def test_input_order_does_not_change_corpus(self):
        a = asset("Characters/Dialogue/Ada", {"Tue": "Two.", "Mon": "One."})
        b = asset("Characters/Dialogue/Bea", {"Mon": "Hello."})
        self.assertEqual(corpus.build_corpus(manifest(a, b)), corpus.build_corpus(manifest(b, a)))

    def test_wrong_schema_or_language_fails_instead_of_mixing_corpora(self):
        for key, value in (("schemaVersion", 99), ("language", "fr")):
            source = manifest()
            source[key] = value
            with self.assertRaises(ValueError):
                corpus.build_corpus(source)


if __name__ == "__main__":
    unittest.main()
