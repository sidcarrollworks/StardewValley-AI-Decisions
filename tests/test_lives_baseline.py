"""Offline checks for the baseline harness; no .NET build or model services."""
import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

MODULE = Path(__file__).resolve().parents[1] / "sim/tools/record_lives_baseline.py"
spec = importlib.util.spec_from_file_location("lives_baseline", MODULE)
baseline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(baseline)


def recording():
    return dict(format="under-glass-run", version=1, hash="abc", days=35,
                label="first", names=["Penny", "Pam", "Gus"], settings={},
                kinds=[dict(name="PlayedGame")], acts=[[0, 10, 0, 0]],
                reflections=[dict(answer=dict(backend="authored"))],
                nights=dict(rows=[]))


class BaselineTests(unittest.TestCase):
    def test_plan_separates_prerequisites_from_routine_and_has_no_live_backend(self):
        plans = baseline.make_plan(35, [1, 2, 3])
        self.assertEqual(len(plans), 9)
        for item in plans:
            args = item["arguments"]
            self.assertNotIn("--inject", args)
            self.assertNotIn("--lives", args)
            self.assertNotIn("--llm-url", args)
            self.assertNotIn("--laya-url", args)
            if item["mode"] == "routine":
                self.assertNotIn("--catalog", args)
                self.assertNotIn("--reflection", args)
            elif item["mode"] == "prerequisites":
                self.assertEqual(args[args.index("--catalog") + 1], "company,invitations,errands")
                self.assertEqual(args[args.index("--reflection") + 1], "authored")

    def test_invalid_plans_rejected(self):
        for days, seeds in [(0, [1]), (113, [1]), (35, []), (35, [1, 1]), (35, [2**63])]:
            with self.subTest(days=days, seeds=seeds), self.assertRaises(ValueError):
                baseline.make_plan(days, seeds)

    def test_observer_check_ignores_only_nights_and_label(self):
        ref = recording()
        run = copy.deepcopy(ref)
        del run["nights"]
        run["label"] = "no nights"
        self.assertEqual(baseline.verify(ref, run, "observer-check")["status"], "passed")
        run["acts"][0][1] += 1
        with self.assertRaisesRegex(ValueError, "acts"):
            baseline.verify(ref, run, "observer-check")

    def test_observer_requires_an_actual_toggle(self):
        with self.assertRaisesRegex(ValueError, "nights on"):
            baseline.verify(recording(), recording(), "observer-check")

    def test_tape_checks_every_field_except_command_label(self):
        ref = recording()
        run = copy.deepcopy(ref)
        run["label"] = "tape"
        baseline.verify(ref, run, "tape-check")
        for key in ("hash", "acts", "nights", "reflections", "settings"):
            with self.subTest(key=key):
                changed = copy.deepcopy(run)
                del changed[key]
                with self.assertRaisesRegex(ValueError, key):
                    baseline.verify(ref, changed, "tape-check")

    def test_empty_tape_is_not_success(self):
        ref = recording()
        ref["reflections"] = []
        with self.assertRaisesRegex(ValueError, "vacuous"):
            baseline.verify(ref, copy.deepcopy(ref), "tape-check")

    def test_nonbaseline_recordings_rejected(self):
        for field, value in [("lives", {}), ("illustrative", True), ("version", 2),
                             ("reflections", [dict(answer=dict(backend="laya"))])]:
            with self.subTest(field=field):
                run = recording()
                run[field] = value
                with self.assertRaises(ValueError):
                    baseline.summarize(run)

    def test_night_summary_excludes_initial_sleep_and_counts_incomplete_separately(self):
        run = recording()
        row = dict(person="Penny", sleptHow="bed", wokeAt=1600,
                   next=dict(why="home", options=[dict(option="home", weight=1)]))
        run["nights"]["rows"] = [row, {**row, "sleptHow": "start"}, {**row, "wokeAt": None, "next": None}]
        summary = baseline.summarize(run)["people"]["Penny"]
        self.assertEqual(summary["completedWakings"], 1)
        self.assertEqual(summary["incompleteSleeps"], 1)
        self.assertEqual(summary["medianWakeMinute"], 160)
        self.assertEqual(summary["firstChoicesWithOnlyHome"], 1)

    def test_existing_output_never_overwritten_or_built(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(baseline, "source_identity") as identity:
            root = Path(directory)
            out = root / "existing"
            out.mkdir()
            (out / "manifest.json").write_text("preserve me")
            with self.assertRaisesRegex(ValueError, "already exists"):
                baseline.capture(root, out, 35, [1])
            self.assertEqual((out / "manifest.json").read_text(), "preserve me")
            identity.assert_not_called()

    def test_outside_checkout_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "checkout"
            root.mkdir()
            for out in (root, root.parent / "elsewhere"):
                with self.assertRaisesRegex(ValueError, "inside this checkout"):
                    baseline.capture(root, out, 35, [1])

    def test_plan_only_calls_no_process_and_resolves_tape_path(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(baseline, "source_identity", return_value={}), \
                patch.object(baseline.subprocess, "run") as run:
            root = Path(directory)
            out = root / "planned"
            pack = baseline.capture(root, out, 35, [1], plan_only=True)
            self.assertEqual(pack["status"], "planned")
            run.assert_not_called()
            command = pack["runs"][-1]["command"]
            self.assertEqual(command[command.index("--reflection-tape") + 1], str(out / "prerequisites-seed1.json"))

    def test_build_failure_is_preserved_and_no_run_is_completed(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(baseline, "source_identity", return_value={}), \
                patch.object(baseline.subprocess, "run", side_effect=subprocess.CalledProcessError(1, ["dotnet"])):
            out = Path(directory) / "failed"
            with self.assertRaises(subprocess.CalledProcessError):
                baseline.capture(directory, out, 35, [1])
            pack = json.loads((out / "manifest.json").read_text())
            self.assertEqual(pack["status"], "failed")
            self.assertTrue(all(item["status"] == "planned" for item in pack["runs"]))
            self.assertTrue((out / "summary.md").exists())


if __name__ == "__main__":
    unittest.main()
