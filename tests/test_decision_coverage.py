"""Offline coverage contracts. Synthetic evidence never calls a model or runs the town."""
from copy import deepcopy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / 'sim/tools/decision_coverage.py'
SPEC = importlib.util.spec_from_file_location('decision_coverage', SCRIPT)
coverage = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(coverage)


def recording(**blocks):
    return {'format': 'under-glass-run', 'version': 1, **blocks}


def decision(key='d1', holder='Penny', phase='offer', selected='wait'):
    return {'opportunity': {'id': key, 'holder': holder, 'trigger': phase, 'at': 100},
            'selectedId': selected, 'torn': False}


def evaluation(d=None, backend='laya', called=True, selected='offer', **answer):
    return {'request': {'fallback': deepcopy(d or decision()), 'model': 'typed-decisions'},
            'answer': {'backend': backend, 'called': called, 'model': 'typed-decisions',
                       'reportedModel': 'laya-rl-agent', 'packet': {'characters': 1000, 'omitted': []}, **answer},
            'selectedId': selected}


def choice(d=None, e=None):
    d = deepcopy(d or decision())
    return {'decision': d, 'phase': d['opportunity']['trigger'], 'evaluation': deepcopy(e)}


def evening(choices=None, evaluations=None, **extra):
    return recording(livesEvenings={'version': 1, 'choices': choices or [], 'decisions': evaluations or [], **extra})


def reflection(key='r1', actor='Penny', passes=None):
    return {'request': {'id': key, 'actor': actor}, 'answer': {'backend': 'hybrid',
            'generationPrompt': 'PRIVATE PROMPT', 'generationResponse': 'PRIVATE RESPONSE',
            'evaluations': passes}, 'choice': 'wait'}


class DecisionCoverageTests(unittest.TestCase):
    def test_route_evaluations_are_counted_separately_and_must_match(self):
        d = decision(phase='free-time')
        run = recording(livesRouting={'decisions': [choice(d, evaluation(d))]})
        result = coverage.report(run)
        self.assertEqual(1, result['routing']['evaluation_summary']['successful_laya_answers'])
        self.assertIsNone(result['evenings']['summary']['choices'])
        self.assertEqual(0, coverage.report(run, 'Gus')['routing']['evaluation_summary']['evaluations'])
        run['livesRouting']['decisions'][0]['evaluation']['request']['fallback']['selectedId'] = 'different'
        with self.assertRaises(coverage.InvalidRecording):
            coverage.report(run)

    def test_older_recording_and_missing_blocks_are_unknown(self):
        r = coverage.report(recording())
        self.assertEqual('not_recorded', r['evenings']['status'])
        self.assertIsNone(r['evenings']['summary']['choices'])
        self.assertIsNone(r['evenings']['summary']['evaluation_coverage']['ratio'])
        self.assertIsNone(r['reflections']['summary'])
        self.assertTrue(r['observer_only'])

    def test_empty_is_not_missing_and_zero_denominator_is_unavailable(self):
        s = coverage.report(evening())['evenings']['summary']
        self.assertEqual(0, s['choices'])
        self.assertEqual(0, s['evaluations'])
        self.assertIsNone(s['evaluation_coverage']['ratio'])
        self.assertIsNone(s['disagreement']['ratio'])

    def test_mixed_evidence_counts_attempts_and_fallbacks_separately(self):
        ds = [decision('d' + str(i)) for i in range(4)]
        es = [evaluation(ds[0]), evaluation(ds[1], backend='authored-fallback', called=False,
                                           packet=None, note='Overflow'),
              evaluation(ds[2], backend='authored-fallback', called=True, note='Timeout')]
        r = coverage.report(evening([choice(d) for d in ds], es))
        s = r['evenings']['summary']
        self.assertEqual(4, s['choices'])
        self.assertEqual(3, s['evaluations'])
        self.assertEqual(1, s['choices_without_evaluation'])
        self.assertEqual(.75, s['evaluation_coverage']['ratio'])
        self.assertEqual(.5, s['recorded_http_participation']['ratio'])
        self.assertEqual(2, s['recorded_http_attempts'])
        self.assertEqual(1, s['successful_laya_answers'])
        self.assertEqual(2, s['authored_or_fallback_answers'])
        self.assertEqual(['Overflow', 'Timeout'], [v['value'] for v in s['fallback_notes']])
        self.assertIsNone(r['evenings']['policy'])

    def test_all_policy_untorn_and_third_choice_are_not_discarded(self):
        ds = [decision('d' + str(i)) for i in range(4)]
        es = [evaluation(d) for d in ds]
        r = coverage.report(evening([choice(d, e) for d, e in zip(ds, es)], es, settings={'evaluation': 'All'}))
        self.assertEqual('All', r['evenings']['policy'])
        self.assertEqual(4, r['evenings']['summary']['evaluations'])
        self.assertEqual(1, r['evenings']['summary']['evaluation_coverage']['ratio'])

    def test_identical_duplicates_count_once_and_conflicts_reject(self):
        e = evaluation()
        data = evening([choice(e=e), choice(e=e)], [e, deepcopy(e)])
        self.assertEqual(1, coverage.report(data)['evenings']['summary']['evaluations'])
        data['livesEvenings']['decisions'][1]['selectedId'] = 'other'
        with self.assertRaisesRegex(coverage.InvalidRecording, 'Conflicting duplicate evaluation'):
            coverage.report(data)

    def test_conflicting_choice_and_wrong_embedded_id_reject(self):
        with self.assertRaisesRegex(coverage.InvalidRecording, 'Conflicting duplicate choice'):
            coverage.report(evening([choice(), choice(decision(selected='different'))]))
        with self.assertRaisesRegex(coverage.InvalidRecording, 'does not match'):
            coverage.report(evening([choice(e=evaluation(decision('other')))]))

    def test_external_evaluation_cannot_change_its_recorded_fallback(self):
        e = evaluation(decision(selected='changed'))
        with self.assertRaisesRegex(coverage.InvalidRecording, 'does not match'):
            coverage.report(evening([choice()], [e]))

    def test_orphans_are_visible_but_cannot_inflate_coverage(self):
        data = evening([choice()], [evaluation(), evaluation(decision('orphan'))])
        s = coverage.report(data)['evenings']['summary']
        self.assertEqual(['orphan'], s['orphan_evaluation_ids'])
        self.assertEqual(2, s['evaluations'])
        self.assertEqual(1, s['evaluation_coverage']['ratio'])
        self.assertEqual(1, s['recorded_http_participation']['numerator'])

    def test_missing_choice_list_is_not_a_zero_denominator(self):
        data = recording(livesEvenings={'decisions': [evaluation()]})
        s = coverage.report(data)['evenings']['summary']
        self.assertEqual(1, s['evaluations'])
        self.assertIsNone(s['choices'])
        self.assertIsNone(s['evaluation_coverage']['numerator'])
        self.assertIsNone(s['orphan_evaluation_ids'])

    def test_optional_evidence_unknown_and_new_backends_visible(self):
        e = evaluation(backend='new-model', packet=None)
        del e['answer']['called']
        del e['answer']['reportedModel']
        del e['selectedId']
        s = coverage.report(evening([choice(e=e)]))['evenings']['summary']
        self.assertEqual(1, s['http_attempt_status_unknown'])
        self.assertEqual(1, s['other_or_unknown_backend_answers'])
        self.assertEqual(1, s['omission_status_unknown'])
        self.assertEqual(1, s['character_length_unknown'])
        self.assertEqual(1, s['selection_comparison_unknown'])
        self.assertIsNone(s['model_identities'][0]['reported'])

    def test_omissions_lengths_and_identity_are_reported_without_resolving_aliases(self):
        es = [evaluation(packet={'characters': 1250, 'omitted': ['m2', 'm1']}),
              evaluation(decision('d2'), packet={'characters': 500})]
        s = coverage.report(evening(evaluations=es))['evenings']['summary']
        self.assertEqual({'min': 500, 'max': 1250}, s['character_range'])
        self.assertEqual(1, s['packets_with_omitted_memory'])
        self.assertEqual(2, s['omitted_items'])
        self.assertEqual(1, s['omission_status_unknown'])
        self.assertEqual(['m1', 'm2'], s['omissions'][0]['omitted_ids'])
        self.assertEqual({'configured': 'typed-decisions', 'answer': 'typed-decisions',
                          'reported': 'laya-rl-agent', 'count': 2}, s['model_identities'][0])

    def test_disagreement_uses_evaluated_selection_not_fallback_choice(self):
        es = [evaluation(), evaluation(decision('d2'), selected='wait')]
        s = coverage.report(evening([choice(e=e) for e in es[:1]], es))['evenings']['summary']
        self.assertEqual({'numerator': 1, 'denominator': 2, 'ratio': .5}, s['disagreement'])

    def test_person_filter_changes_all_denominators(self):
        ds = [decision(holder='Penny'), decision('d2', 'Pam', 'answer')]
        data = evening([choice(d) for d in ds], [evaluation(d) for d in ds])
        data['livesRouting'] = {'decisions': [{'decision': d} for d in ds]}
        data['livesCompany'] = {'choices': [choice(d) for d in ds],
                               'answers': [{'episodeId': 'game', 'holder': who, 'at': 101} for who in ['Penny', 'Pam']]}
        data['reflections'] = [reflection(actor='Penny'), reflection('r2', 'Pam')]
        r = coverage.report(data, 'Pam')
        self.assertEqual(1, r['evenings']['summary']['choices'])
        self.assertEqual(1, r['evenings']['summary']['recorded_http_attempts'])
        self.assertEqual(1, r['routing']['choices'])
        self.assertEqual(1, r['company']['answers'])
        self.assertEqual(1, r['reflections']['summary']['choices'])
        missing = coverage.report(data, 'Nobody')
        self.assertEqual(0, missing['evenings']['summary']['choices'])
        self.assertIn('No recorded evening choices', coverage.render(missing))

    def test_company_answers_are_separate_and_deduplicated(self):
        a = {'episodeId': 'e1', 'holder': 'Pam', 'at': 10}
        data = recording(livesCompany={'choices': [choice(), choice()], 'answers': [a, a]})
        r = coverage.report(data)['company']
        self.assertEqual(1, r['choices'])
        self.assertEqual(1, r['answers'])
        self.assertEqual([{'person': 'Pam', 'answers': 1}], r['answers_by_person'])

    def test_reflection_passes_are_not_choices_or_inferred_http(self):
        passes = [{'pass': 1, 'response': 'response'}, {'pass': 2, 'error': 'failed'}]
        data = recording(reflections=[reflection(passes=passes), reflection('r2')])
        r = coverage.report(data)
        s = r['reflections']['summary']
        self.assertEqual(2, s['choices'])
        self.assertEqual(2, s['recorded_passes'])
        self.assertEqual(1, s['passes_with_response'])
        self.assertEqual(1, s['passes_with_error'])
        self.assertEqual(1, s['pass_list_unknown'])
        self.assertIsNone(s['http_attempts'])
        self.assertNotIn('PRIVATE', coverage.render(r))
        self.assertNotIn('PRIVATE', json.dumps(r))

    def test_conflicting_reflection_passes_are_rejected(self):
        data = recording(reflections=[reflection(passes=[{'pass': 1, 'error': 'a'}, {'pass': 1, 'error': 'b'}])])
        with self.assertRaisesRegex(coverage.InvalidRecording, 'Conflicting duplicate reflection pass'):
            coverage.report(data)

    def test_partial_and_tape_provenance_remain_explicit(self):
        data = evening([choice(e=evaluation())]); data['live'] = {'partial': True}
        r = coverage.report(data)
        self.assertTrue(r['partial'])
        self.assertIn('INCOMPLETE', coverage.render(r))
        self.assertIn('tape replay', coverage.render(r))

    def test_order_independence_and_input_not_mutated(self):
        ds = [decision('d2', 'Pam'), decision('d1')]
        es = [evaluation(d) for d in ds]
        data = evening([choice(d, e) for d, e in zip(ds, es)], es)
        data['reflections'] = [reflection(passes=[{'pass': 2}, {'pass': 1}])]
        before = deepcopy(data)
        a = coverage.report(data)
        self.assertEqual(before, data)
        data['livesEvenings']['choices'].reverse(); data['livesEvenings']['decisions'].reverse()
        data['reflections'][0]['answer']['evaluations'].reverse()
        b = coverage.report(data)
        self.assertEqual(a, b)
        self.assertEqual(coverage.render(a), coverage.render(b))

    def test_malformed_records_reject_without_fabricating_zeroes(self):
        for data in [[], {}, recording(live=[]), recording(livesEvenings={'settings': []}),
                     recording(version=True), recording(livesEvenings=[]), recording(livesEvenings={'choices': 'bad'}),
                     recording(livesRouting={'version': 2}), evening([{'decision': {}}]),
                     evening(evaluations=[evaluation(called='yes')]),
                     evening(evaluations=[evaluation(packet={'characters': -1})])]:
            with self.subTest(data=data), self.assertRaises(coverage.InvalidRecording):
                coverage.report(data)

    def test_cli_does_not_modify_input_and_returns_machine_readable_json(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'run.json'
            content = json.dumps(evening([choice(e=evaluation())])).encode()
            path.write_bytes(content)
            result = subprocess.run([sys.executable, str(SCRIPT), str(path), '--json', '--person', 'Penny'], capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual('under-glass-decision-coverage', json.loads(result.stdout)['format'])
            self.assertEqual(content, path.read_bytes())
            path.write_text('<html>not a recording</html>')
            result = subprocess.run([sys.executable, str(SCRIPT), str(path)], capture_output=True, text=True)
            self.assertEqual(2, result.returncode)
            self.assertNotIn('Traceback', result.stderr)
            self.assertEqual('', result.stdout)


if __name__ == '__main__':
    unittest.main()
