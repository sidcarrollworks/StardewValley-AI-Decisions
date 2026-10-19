"""Read-only observer diagnostics for a recording; no simulation or model imports."""
import argparse
from collections import Counter
import json
from pathlib import Path
import sys


class InvalidRecording(ValueError):
    pass


def obj(value, path):
    if not isinstance(value, dict):
        raise InvalidRecording(f'{path} must be an object.')
    return value


def rows(value, path):
    if not isinstance(value, list):
        raise InvalidRecording(f'{path} must be an array.')
    return [obj(row, f'{path}[{i}]') for i, row in enumerate(value)]


def text(value, path, required=False):
    if value is None and not required:
        return None
    if not isinstance(value, str) or (required and not value.strip()):
        raise InvalidRecording(f'{path} must be a nonempty string.' if required else f'{path} must be a string or null.')
    return value


def optional_rows(block, key):
    return None if block.get(key) is None else rows(block[key], key)


def unique(target, key, value, kind):
    if key in target and target[key] != value:
        raise InvalidRecording(f'Conflicting duplicate {kind}: {key}.')
    target[key] = value


def opportunity(decision):
    op = obj(obj(decision, 'decision').get('opportunity'), 'decision.opportunity')
    for key in ('id', 'holder', 'trigger'):
        text(op.get(key), f'opportunity.{key}', required=True)
    return op


def block_of(data, key):
    if data.get(key) is None:
        return None
    block = obj(data[key], key)
    if type(block.get('version', 1)) is not int or block.get('version', 1) != 1:
        raise InvalidRecording(f'{key}: unsupported version {block.get("version")}.')
    return block


def selected(holder, person):
    return person is None or holder == person


def groups(values):
    return [{'value': key, 'count': count} for key, count in
            sorted(Counter(values).items(), key=lambda pair: (pair[0] is not None, pair[0] or ''))]


def fraction(numerator, denominator):
    return {'numerator': numerator, 'denominator': denominator,
            'ratio': numerator / denominator if numerator is not None and denominator else None}


def evening_summary(choices, evaluations):
    """Only matched evaluations enter coverage; orphan records never make it exceed 100%."""
    count = len(choices) if choices is not None else None
    if evaluations is None:
        return {'choices': count, 'evaluations': None, 'evaluation_coverage': fraction(None, count),
                'recorded_http_participation': fraction(None, count)}
    matched = {key: val for key, val in evaluations.items() if choices is not None and key in choices}
    answers = [row['answer'] for row in evaluations.values()]
    packets = [(key, row['answer']['packet']) for key, row in evaluations.items() if row['answer'].get('packet') is not None]
    known_omissions = [(key, packet['omitted']) for key, packet in packets if packet.get('omitted') is not None]
    affected = [{'decision_id': key, 'omitted_ids': sorted(ids)} for key, ids in sorted(known_omissions) if ids]
    lengths = [p['characters'] for _, p in packets if p.get('characters') is not None]
    comparable = [row for row in evaluations.values() if row.get('selectedId') is not None
                  and row['request']['fallback'].get('selectedId') is not None]
    identities = Counter((row['request'].get('model'), row['answer'].get('model'), row['answer'].get('reportedModel'))
                         for row in evaluations.values())
    fallback = [a for a in answers if a.get('backend') in ('authored', 'authored-fallback')]
    return {
        'choices': count, 'evaluations': len(evaluations),
        'choices_without_evaluation': count - len(matched) if count is not None else None,
        'orphan_evaluation_ids': sorted(set(evaluations) - set(choices or {})) if choices is not None else None,
        'evaluation_coverage': fraction(len(matched) if count is not None else None, count),
        'recorded_http_attempts': sum(a.get('called') is True for a in answers),
        'recorded_no_http': sum(a.get('called') is False for a in answers),
        'http_attempt_status_unknown': sum(a.get('called') is None for a in answers),
        'recorded_http_participation': fraction(sum(r['answer'].get('called') is True for r in matched.values()) if count is not None else None, count),
        'successful_laya_answers': sum(a.get('backend') == 'laya' for a in answers),
        'authored_or_fallback_answers': len(fallback),
        'other_or_unknown_backend_answers': sum(a.get('backend') not in ('laya', 'authored', 'authored-fallback') for a in answers),
        'backends': groups(a.get('backend') for a in answers),
        'fallback_notes': groups(a.get('note') for a in fallback),
        'packets_recorded': len(packets), 'packets_missing': len(answers) - len(packets),
        'packets_with_omitted_memory': len(affected),
        'omitted_items': sum(len(ids) for _, ids in known_omissions),
        'omission_status_unknown': len(answers) - len(known_omissions),
        'character_range': {'min': min(lengths), 'max': max(lengths)} if lengths else None,
        'character_length_unknown': len(answers) - len(lengths),
        'omissions': affected,
        'disagreement': fraction(sum(r['selectedId'] != r['request']['fallback']['selectedId'] for r in comparable), len(comparable)),
        'selection_comparison_unknown': len(answers) - len(comparable),
        'model_identities': [{'configured': c, 'answer': a, 'reported': r, 'count': n}
                             for (c, a, r), n in sorted(identities.items(), key=lambda pair: tuple((v is not None, v or '') for v in pair[0]))],
    }


def validate_evaluation(row):
    request = obj(row.get('request'), 'evaluation.request')
    op = opportunity(request.get('fallback'))
    answer = obj(row.get('answer'), 'evaluation.answer')
    for name in ('model', 'backend', 'reportedModel', 'note'):
        text(answer.get(name), 'answer.' + name)
    text(request.get('model'), 'request.model')
    text(row.get('selectedId'), 'evaluation.selectedId')
    text(request['fallback'].get('selectedId'), 'fallback.selectedId')
    if answer.get('called') is not None and type(answer['called']) is not bool:
        raise InvalidRecording('answer.called must be boolean or null.')
    if answer.get('packet') is not None:
        packet = obj(answer['packet'], 'answer.packet')
        if packet.get('characters') is not None and (type(packet['characters']) is not int or packet['characters'] < 0):
            raise InvalidRecording('packet.characters must be a nonnegative integer.')
        if packet.get('omitted') is not None:
            if not isinstance(packet['omitted'], list):
                raise InvalidRecording('packet.omitted must be an array.')
            for item in packet['omitted']:
                text(item, 'omitted memory ID', required=True)
    return op


def evenings(data, person):
    block = block_of(data, 'livesEvenings')
    if block is None:
        return {'status': 'not_recorded', 'summary': evening_summary(None, None), 'by_person_phase': []}
    settings = obj(block['settings'] if block.get('settings') is not None else {}, 'livesEvenings.settings')
    text(settings.get('evaluation'), 'settings.evaluation')
    raw_choices, raw_evaluations = optional_rows(block, 'choices'), optional_rows(block, 'decisions')
    choices = {} if raw_choices is not None else None
    evaluations = {} if raw_evaluations is not None or raw_choices is not None else None
    for row in raw_choices or []:
        op = opportunity(row.get('decision'))
        text(row.get('phase'), 'choice.phase')
        # Compare the underlying choice separately: embedded evaluations may also be in decisions.
        unique(choices, op['id'], {'decision': row['decision'], 'phase': row.get('phase') or op['trigger']}, 'choice')
        if row.get('evaluation') is not None:
            evaluation = obj(row['evaluation'], 'choice.evaluation')
            ev_op = validate_evaluation(evaluation)
            if ev_op['id'] != op['id'] or evaluation['request']['fallback'] != row['decision']:
                raise InvalidRecording(f'Evaluation does not match its choice: {op["id"]}.')
            unique(evaluations, op['id'], evaluation, 'evaluation')
    for row in raw_evaluations or []:
        op = validate_evaluation(row)
        if choices is not None and op['id'] in choices and row['request']['fallback'] != choices[op['id']]['decision']:
            raise InvalidRecording(f'Evaluation does not match its choice: {op["id"]}.')
        unique(evaluations, op['id'], row, 'evaluation')
    if choices is not None:
        choices = {k: v for k, v in choices.items() if selected(opportunity(v['decision'])['holder'], person)}
    if evaluations is not None:
        evaluations = {k: v for k, v in evaluations.items() if selected(opportunity(v['request']['fallback'])['holder'], person)}
    def group_key(key, value, evaluation=False):
        op = opportunity(value['request']['fallback'] if evaluation else value['decision'])
        phase = choices[key]['phase'] if choices is not None and key in choices else op['trigger']
        return op['holder'], phase
    keys = {group_key(k, v) for k, v in (choices or {}).items()} | {group_key(k, v, True) for k, v in (evaluations or {}).items()}
    return {
        'status': 'recorded', 'policy': settings.get('evaluation'),
        'policy_note': 'Absent policy is legacy/selective metadata; it does not prove a model was configured.' if settings.get('evaluation') is None else None,
        'configured_model': text(settings.get('model'), 'settings.model'),
        'summary': evening_summary(choices, evaluations),
        'by_person_phase': [{'person': who, 'phase': phase, **evening_summary(
            {k: v for k, v in choices.items() if group_key(k, v) == (who, phase)} if choices is not None else None,
            {k: v for k, v in evaluations.items() if group_key(k, v, True) == (who, phase)} if evaluations is not None else None)}
            for who, phase in sorted(keys)],
    }


def authored_choices(data, name, key, person):
    block = block_of(data, name)
    if block is None:
        return {'status': 'not_recorded', 'choices': None, 'answers': None, 'by_person_phase': []}
    decisions = optional_rows(block, key)
    unique_choices = {}
    for row in decisions or []:
        op = opportunity(row.get('decision'))
        unique(unique_choices, op['id'], row, 'choice')
    counts = Counter()
    matched_choices, evaluations = {}, {}
    for row in unique_choices.values():
        op = opportunity(row['decision'])
        ev = row.get('evaluation')
        if ev is not None:
            ev_op = validate_evaluation(ev)
            if ev_op['id'] != op['id'] or ev['request']['fallback'] != row['decision']:
                raise InvalidRecording(f'Evaluation does not match its choice: {op["id"]}.')
        if selected(op['holder'], person):
            matched_choices[op['id']] = row
            if ev is not None:
                evaluations[op['id']] = ev
            phase = text(row.get('phase'), 'choice.phase') or op['trigger']
            counts[(op['holder'], phase)] += 1
    answers = optional_rows(block, 'answers') if name == 'livesCompany' else None
    unique_answers = {}
    for row in answers or []:
        holder = text(row.get('holder'), 'answer.holder', required=True)
        episode = text(row.get('episodeId'), 'answer.episodeId', required=True)
        if type(row.get('at')) is not int:
            raise InvalidRecording('company answer.at must be an integer.')
        unique(unique_answers, (episode, holder, row['at']), row, 'company answer')
    answer_counts = Counter(r['holder'] for r in unique_answers.values() if selected(r['holder'], person))
    return {'status': 'recorded', 'backend': text(block.get('backend'), name + '.backend'),
            'evaluation_summary': evening_summary(matched_choices, evaluations) if decisions is not None else None,
            'choices': sum(counts.values()) if decisions is not None else None,
            'answers': sum(answer_counts.values()) if answers is not None else None,
            'by_person_phase': [{'person': who, 'phase': phase, 'choices': n} for (who, phase), n in sorted(counts.items())],
            'answers_by_person': [{'person': who, 'answers': n} for who, n in sorted(answer_counts.items())]}


def reflection_summary(records):
    answers = [r['answer'] for r in records]
    passes = [p for a in answers for p in (a.get('evaluations') or [])]
    return {'choices': len(records), 'recorded_passes': len(passes),
            'pass_list_unknown': sum(a.get('evaluations') is None for a in answers),
            'passes_with_response': sum(p.get('response') is not None for p in passes),
            'passes_with_error': sum(p.get('error') is not None for p in passes),
            'generation_prompts_recorded': sum(a.get('generationPrompt') is not None for a in answers),
            'generation_responses_recorded': sum(a.get('generationResponse') is not None for a in answers),
            'legacy_laya_responses_recorded': sum(a.get('layaResponse') is not None for a in answers),
            'http_attempts': None, 'backends': groups(a.get('backend') for a in answers)}


def reflections(data, person):
    if data.get('reflections') is None:
        return {'status': 'not_recorded', 'summary': None, 'by_person': []}
    records = {}
    for row in rows(data['reflections'], 'reflections'):
        request = obj(row.get('request'), 'reflection.request')
        key = text(request.get('id'), 'reflection.request.id', required=True)
        text(request.get('actor'), 'reflection.request.actor', required=True)
        answer = obj(row.get('answer'), 'reflection.answer')
        for field in ('backend', 'generationPrompt', 'generationResponse', 'layaResponse'):
            text(answer.get(field), 'reflection.answer.' + field)
        passes = {}
        for entry in optional_rows(answer, 'evaluations') or []:
            if type(entry.get('pass')) is not int:
                raise InvalidRecording('reflection evaluation.pass must be an integer.')
            for field in ('response', 'error'):
                text(entry.get(field), 'reflection evaluation.' + field)
            unique(passes, entry['pass'], entry, 'reflection pass')
        # Do not mutate input; deduplicate passes and canonicalize their order.
        normalized = {**row, 'answer': {**answer, 'evaluations': [passes[k] for k in sorted(passes)]
                                      if answer.get('evaluations') is not None else None}}
        unique(records, key, normalized, 'reflection')
    records = [r for r in records.values() if selected(r['request']['actor'], person)]
    people = sorted({r['request']['actor'] for r in records})
    return {'status': 'recorded', 'summary': reflection_summary(records),
            'by_person': [{'person': who, **reflection_summary([r for r in records if r['request']['actor'] == who])} for who in people]}


def report(data, person=None):
    data = obj(data, 'recording')
    if data.get('format') != 'under-glass-run' or type(data.get('version')) is not int or data.get('version') != 1:
        raise InvalidRecording('Expected an under-glass-run version 1 JSON recording (not a tape or HTML).')
    live = obj(data['live'] if data.get('live') is not None else {}, 'live')
    if live.get('partial') is not None and type(live['partial']) is not bool:
        raise InvalidRecording('live.partial must be boolean.')
    return {'format': 'under-glass-decision-coverage', 'version': 1, 'observer_only': True,
            'person': person, 'partial': live.get('partial') is True,
            'notes': ['Recorded attempts may originate from tape replay; they do not prove fresh network activity.',
                      'Missing evidence is unknown. No whole-town coverage, latency or quality score is inferred.',
                      'Model names are recorded strings, not verified checkpoint identities. Choices are not outcomes.'],
            'evenings': evenings(data, person),
            'routing': authored_choices(data, 'livesRouting', 'decisions', person),
            'company': authored_choices(data, 'livesCompany', 'choices', person),
            'reflections': reflections(data, person)}


def number(value):
    return 'unknown' if value is None else str(value)


def percent(value):
    return 'unavailable' if value['ratio'] is None else f"{value['numerator']}/{value['denominator']} ({value['ratio']:.1%})"


def one_line(value):
    return 'unknown' if value is None else ' '.join(str(value).split())


def render(result):
    lines = ['Decision coverage | observer report' + (' | INCOMPLETE live snapshot' if result['partial'] else '')]
    if result['person'] is not None:
        lines.append('Person: ' + one_line(result['person']))
    evening = result['evenings']
    if evening['status'] == 'not_recorded':
        lines.append('Evenings: not recorded.')
    else:
        s = evening['summary']
        lines.append(f"Evenings: {number(s['choices'])} recorded choices; evaluation coverage {percent(s['evaluation_coverage'])}.")
        lines.append('Policy: ' + one_line(evening.get('policy') or 'legacy/selective metadata; evaluator configuration unknown'))
        if s.get('evaluations') is not None:
            lines += [f"Recorded HTTP attempts: {s['recorded_http_attempts']}; status unknown for {s['http_attempt_status_unknown']} evaluations. Participation: {percent(s['recorded_http_participation'])}.",
                      f"Answers: {s['successful_laya_answers']} labelled Laya; {s['authored_or_fallback_answers']} authored/fallback; {s['other_or_unknown_backend_answers']} other/unknown.",
                      f"Recall omitted: {s['packets_with_omitted_memory']} packets / {s['omitted_items']} items; omission status unknown for {s['omission_status_unknown']} evaluations.",
                      f"Selection disagreement with fallback: {percent(s['disagreement'])}; this is not accuracy."]
            if s['orphan_evaluation_ids']:
                lines.append(f"{len(s['orphan_evaluation_ids'])} evaluations have no recorded choice; excluded from coverage.")
            for model in s['model_identities']:
                lines.append(f"Model ({model['count']}): configured={one_line(model['configured'])}; answer={one_line(model['answer'])}; reported={one_line(model['reported'])}.")
            for note in s['fallback_notes'][:3]:
                lines.append(f"Fallback ({note['count']}): {one_line(note['value'])[:180]}")
            for omitted in s['omissions'][:3]:
                lines.append(f"Omitted recall: {one_line(omitted['decision_id'])[:120]} | {len(omitted['omitted_ids'])} items (IDs in JSON).")
        lines.append('Person / phase | choices | evaluations | recorded HTTP | no evaluation')
        for row in evening['by_person_phase']:
            lines.append(f"{one_line(row['person'])} / {one_line(row['phase'])} | {number(row['choices'])} | {number(row['evaluations'])} | {number(row.get('recorded_http_attempts'))} | {number(row.get('choices_without_evaluation'))}")
        if not evening['by_person_phase']:
            lines.append('No recorded evening choices/evaluations for this selection.')
    for key, label in [('routing', 'Routing'), ('company', 'Immediate company')]:
        section = result[key]
        lines.append(f"{label}: not recorded." if section['status'] == 'not_recorded' else
                     f"{label}: {number(section['choices'])} recorded choices" +
                     (f"; {number(section['answers'])} separately recorded answers." if key == 'company' else '.'))
        for row in section['by_person_phase']:
            lines.append(f"  {one_line(row['person'])} / {one_line(row['phase'])}: {row['choices']}")
        for row in section.get('answers_by_person', []):
            lines.append(f"  {one_line(row['person'])} / answer: {row['answers']}")
        summary = section.get('evaluation_summary')
        if summary and summary['evaluations']:
            lines.append(f"  Evaluated: {summary['evaluations']}; Laya: {summary['successful_laya_answers']}; authored/fallback: {summary['authored_or_fallback_answers']}; recorded HTTP: {summary['recorded_http_attempts']}.")
    ref = result['reflections']
    if ref['status'] == 'not_recorded':
        lines.append('Reflections: not recorded.')
    else:
        s = ref['summary']
        lines.append(f"Reflections: {s['choices']} choices, {s['recorded_passes']} evaluator passes ({s['passes_with_response']} responses, {s['passes_with_error']} errors); {s['pass_list_unknown']} unknown pass lists.")
        lines.append(f"Inspiration: {s['generation_prompts_recorded']} recorded prompts, {s['generation_responses_recorded']} responses. Reflection HTTP attempts unknown.")
        for row in ref['by_person']:
            lines.append(f"  {one_line(row['person'])}: {row['choices']} choices / {row['recorded_passes']} recorded passes")
    lines.extend(result['notes'])
    return '\n'.join(lines) + '\n'


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('recording', type=Path)
    parser.add_argument('--json', action='store_true', dest='as_json')
    parser.add_argument('--person')
    args = parser.parse_args(argv)
    try:
        data = json.loads(args.recording.read_text(encoding='utf-8-sig'))
        result = report(data, args.person)
    except (OSError, ValueError, RecursionError) as exc:
        print(f'decision-coverage: {exc}', file=sys.stderr)
        return 2
    print(json.dumps(result, indent=2, ensure_ascii=True, sort_keys=True, allow_nan=False) if args.as_json else render(result), end='\n' if args.as_json else '')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
