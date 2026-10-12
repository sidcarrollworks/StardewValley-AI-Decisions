# Claude Haiku 5.5 as the inspiration generator — 2026-10-12

Sid asked for another model behind the private-inspiration slot after Qwen3-0.6B left the
[fixed-intention wording probe](voice-wording-2026-10-12.md) with one clear pass in twenty-four.
This note records the first run of `claude-haiku-5-5` through the same probe, a second run with
adaptive thinking on, a blind multi-judge review of all three sets beside the withheld authored
controls, and one seven-day hybrid recording with the same model in the real reflection loop.

Headline: on the six curated gratitude cases, Haiku with thinking off kept the fixed plan,
private form and life-stage register in 18 of 24 replies (Qwen: 0 of 24), copied no source
dialogue, and its three voices were told apart blind 22 of 24 times. It also showed its own
faults: each character acquires a pet phrase, thinking mode drifts into speech addressed to the
other person, and the model adds small continuations and self-traits the scene never supplied.
Nothing here is promoted into runtime defaults; the Qwen service remains the local default and the
Claude service is an explicit opt-in launched per experiment.

## What changed and what did not

The .NET adapter, the trial harness, both prompt frames, the case pack, the profile pack, the
corpus and the runtime generation prompt are unchanged. What is new is a second loopback service,
`sim/tools/claude_generator.py`, that speaks the same chat-completions contract as the Qwen
service and calls the Anthropic API through the official SDK. The callers cannot tell the two
apart except through the receipt, which is the point: `--llm-url` still has to be loopback, the
seed is still echoed, `revision` names the model the API actually served and `device` is
`anthropic-api`.

What leaves the machine is exactly the request the harness builds: in the wording suite the
scene frame, the composed voice line and, in the reference arm only, the four selected game
dialogue lines per character; in the runtime loop the context and memory strings, the known-source
facts, the allowed act kinds, the composed voice and one selected anchor. Candidate lines, the
evaluator cue, pack hashes and request IDs never go to the generator in either path. The API key
came from a file outside the checkout, passed as `--api-key-file`; nothing logs, echoes or stores it.

Haiku 5.5 rejects non-default `temperature`/`top_p` and has no seed. The service validates them
for contract compatibility, does not forward them, echoes the seed so the receipt check passes,
and writes `sampling_forwarded: false` and `seed_honoured: false` into every reply. A seed
therefore does not make two runs identical; exact replay is tape mode, as it already was for the
GPU service across runtimes. Thinking tokens count toward `max_tokens` on Claude, so the caller's
120/180-token cap is recorded but the service sends its own 1,024-token ceiling; the harness
still enforces the 180/220-character thought limits.

| Setting | Condition A (`haiku-nothink`) | Condition B (`haiku-think`) |
| --- | --- | --- |
| Model requested and served | `claude-haiku-5-5` (receipt `revision`: `claude-haiku-5-5`) | same |
| Thinking / effort | `disabled` / `low` | `adaptive` / `medium` |
| Output shape | structured output, schema `{thought}` for the wording contract | same |
| Sampling | temperature 0.7 requested by the harness; not sent (API default) | same |
| Seed | per-case SHA-256 seeds, echoed, not honoured | same |
| Deadlines | service 20 s; CLI `--timeout-ms 30000` | service 30 s; CLI `--timeout-ms 45000` |
| Replies / format failures / copy flags | 24 / 0 / 0 | 24 / 0 / 0 |
| Client latency, median (range), ms | 1,548 (1,052–2,208) | 1,621 (900–2,060) |
| Input tokens per request, range | 653–761 | 654–762 |
| Output tokens per reply, range | 34–55 | 35–58 |
| Finish reason | `stop` on all 24 | `stop` on all 24 |

Input tokens are roughly double Qwen's 303–382 for the same bytes: a different tokenizer, not a
different prompt. Output tokens in condition B are the JSON only; at medium effort the model
mostly skipped thinking on these short requests.

## Wording suite: mechanical reading

The checks are the harness's own (format, copy flags) plus a small script over the result files
(`out/voice-wording-haiku-2026-10-12/review/wording_diagnostics.py`): repeated 3-grams within a
character's eight replies, bigram overlap between a case's two replicates, replies identical
across arms, and surface form flags. None of these is a quality score.

| Measure | Qwen final frame | Haiku, thinking off | Haiku, thinking on |
| --- | --- | --- | --- |
| Format failures / source-copy replies | 0 / 6 | 0 / 0 | 0 / 0 |
| Identical replicate pairs (of 12) / arm-identical replies | 2 / 2 | 0 / 0 | 0 / 0 |
| Replicate bigram overlap, median | 0.09 | 0.24 | 0.33 |
| Mean thought length, characters (Vincent / Maru / George) | 58 / 63 / 95 | 91 / 101 / 113 | 99 / 109 / 104 |
| Most repeated phrase per character (count of 8) | George: copied `George:Mon`/`Fri` (4); Maru/Vincent: "newcomer for the" (3) | George "plainly, without making a fuss" (4); Maru "thank them properly" (5); Vincent "say thank you" (7) | George "make a fuss about it" (6); Maru "Newcomer's help earlier" (4); Vincent "say thank you" (6) |
| Replies with a thank word | 13 | 24 | 24 |
| Replies that address the other person ("you", "Thanks, Newcomer") | 5 | 10 | 13 |

Two things stand out. Qwen's low replicate overlap is not diversity: its replies are short, often
imperative to-do lines, and two case/arm pairs are identical across seeds. Haiku's higher overlap
is the opposite problem: each character locks onto one phrase that paraphrases their voice
descriptor. "Dislikes fuss" becomes "without making a fuss" in six of George's eight thoughts;
Maru's "properly" and Vincent's "before I forget" recur the same way. That is the catchphrase
failure the roadmap warns against, in a softer form: the phrase is new wording, not a quoted line,
but it is still a tic the observer would notice within a week.

## Blind review

Codex's review of the Qwen run was a single reader scoring a combined criterion. To compare
three sets without that bottleneck, the 72 generated replies and the 12 withheld authored
controls were shuffled into one unlabeled pool and read by Claude subagents (the session's
model, Fable 5.1) in batches of twelve. Each batch was scored independently by three
meaning judges and three voice judges; the majority decides, and a split vote is "unclear".

- Meaning judges saw the character, life stage, scene, relationship and fixed intention, and
  scored plan (future / done / present-performed / imperative / absent), form (private /
  addressed-speech / narrator / mixed), grounding (ok / adds-facts / shifts-relationship /
  echoes-prompt) and register (fits / adult-generic / exaggerated), quoting the deciding words.
  "Pass" requires future, private, ok and fits; "fail" is a plan that is done, performed now or
  absent, speech addressed to the other person, narration, invented facts or exaggeration.
- Voice judges saw only the thought with the character's name masked, plus the three voice
  descriptors and each character's four actual dialogue lines, and guessed the author.

The authored controls are the calibration row: all twelve passed unanimously, so the judges are
not stricter than the criteria. Their voice accuracy is 8 of 12, with every miss on Maru, whose
plain controls read as anyone's. This is model-assisted review by the same vendor as the model
under test, applied to six curated cases; it ranks these sets against each other and the
controls, and claims nothing about general quality. Every verdict, its evidence, the pack, the
key and the judges' raw transcripts are kept under `out/voice-wording-haiku-2026-10-12/review/`.

| Set (24 replies unless noted) | Pass | Unclear | Fail | Plan "future" | Form "private" | Grounding "ok" | Register "fits" | Author guessed right (majority) | "Could be anyone" votes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Authored controls (12) | 12 | 0 | 0 | 12 | 12 | 12 | 12 | 8 / 12 | 50% |
| Qwen final frame | 0 | 6 | 18 | 2 | 16 | 12 | 9 | 13 / 24 | 69% |
| Haiku, thinking off | 18 | 1 | 5 | 23 | 23 | 19 | 24 | 22 / 24 | 25% |
| Haiku, thinking on | 8 | 1 | 15 | 17 | 16 | 14 | 24 | 22 / 24 | 26% |

By arm, Haiku with thinking off passed 9 of 12 in each arm; the references neither helped nor
caused copying. With thinking on, the profile-only arm fell to 3 of 12 because six replies opened
with a vocative ("Newcomer, that gift earlier was good…") and performed the thanks instead of
planning it; the reference arm kept the private form (11 of 12) but added unsupported detail in 7
of 12 ("the next time I see them", "I didn't expect to need it", "really saved me time").

Qwen's 13 of 24 author guesses are inflated by copying: four George replies and two Vincent
replies are the characters' own game lines, which are trivially attributable. Its generated
wording was called generic in 92% of profile-arm votes. Haiku's voices were separable without
copying: Vincent and George 8 of 8 in both conditions, Maru 6 of 8, matching the pattern in the
authored controls where Maru is also the hardest to place.

Haiku's failures, with the judges' evidence:

| Set | Reply | Verdict |
| --- | --- | --- |
| Thinking off, George, profile | "That gift earlier was good, and I should say so. Thanks, Newcomer. I mean it." | Performs the thanks inside the thought; form mixed. |
| Thinking off, George, references | "Newcomer's gift was a good one, and I liked it more than I'd let on. I should thank them for it, plainly, without making a fuss." | "more than I'd let on" invents an earlier understated reaction. |
| Thinking off, Maru, references | "I really did like that gift, so I should tell Newcomer thanks when we meet again." | "when we meet again" contradicts the scene, which says they meet now. |
| Thinking on, Vincent, profile | "Newcomer, I really like the gift you gave me. I want to say thank you, and I mean it." | Spoken to the other person; thanks performed now. |
| Thinking on, George, profile | "I owe you one for earlier. That help was useful, and I didn't expect it, so thanks." | Addressed speech; "owe you one" shifts the stance from thanks to a debt. |

Against the held-out controls, Haiku's Vincent ("Newcomer gave me that thing today and I really
liked it. I want to say thank you before I forget.") is plainer than "Ooh, I like my gift! I
wanna thank them." but is a child's sentence; the reference arm with thinking on produced "I
wanna tell them thank you, right now, before I forget." without copying any reference. George's
"That gift earlier was better than I let on." is closer to the authored "Hm. A good gift. I ought
to thank them." in attitude than in length: every Haiku thought is one to two sentences of
around a hundred characters, where the controls are forty to sixty.

## Runtime hybrid week

The same service, launched with `--contract reflection` (schema `{thought, suggestedChoice}`),
replaced the generator in a seven-day seed-7 recording with the catalog slices, feelings, quiet
reflection 0.35, dreams 0.15, balanced Laya and the character pack enabled. Laya and the
simulation were unchanged.

| Measure | Haiku, thinking off, profiles on | Qwen, profiles on (same flags) | Qwen, profiles off (first week of the [October 12 pack](observer-test-2026-10-09.md)) |
| --- | --- | --- | --- |
| Reflections / real generator answers / fallbacks | 64 / 64 / 0 | 63 / 62 / 1 (one overlong thought) | 60 / 60 / 0 |
| Generator latency, median (max), ms | 1,644 (2,763) | 1,086 (1,568) | 942 |
| Thought names the suggested act / a different act / unclear | 48 / 0 / 16 | 19 / 31 / 12 | 16 / 24 / 20 |
| Suggested the first-listed act | 16 of 64 | 36 of 62 | 27 of 60 |
| Thoughts reciting mood/regard/opportunity words | 2 | 15 | 29 |
| Thoughts naming an item or example the scene lacks | 6 | 0 | 2 |
| Thoughts hedged with "maybe" | 47 | 8 | 6 |
| Accepted / reshaped / deferred / rejected | 23 / 25 / 8 / 8 | 17 / 31 / 8 / 6 | 11 / 29 / 10 / 10 |

Agreement is a keyword reading of the thought against the suggested act kind, the same heuristic
used in the review of PR #82; it is a rough count, not a semantic verdict. With Haiku the
accepted/reshaped label becomes meaningful again, because the suggestion and the thought agree
three times out of four and never plainly contradict each other. The adapter still shows the
generator the acts in alphabetical kind order; Haiku did not inherit Qwen's first-position habit
(25% first-listed, where Argued sits first), but the fixed order remains a confound to remove.

Every call stayed under the adapter's five-second deadline (maximum 2.8 s), so the run had no
fallback; a slower API day would turn into labelled `authored-fallback` answers, not silent
changes. The recording replays from its own tape with the same flags and pack.

Three things the week shows that the wording probe cannot:

- Voices carry into the loop. Sebastian: "Newcomer welcomed me, and I'd rather not owe anyone
  for that, but a quiet thank-you costs little and keeps the peace." George: "Evelyn helped me
  when I didn't ask for it, and I hate owing anyone, but I suppose a plain thank-you would cost
  me nothing." Pierre: "I told Newcomer they looked sharp, and I meant it partly to be seen as
  the one who notices good things." Abigail: "I'm half-tempted to test them with an argument just
  to see how they handle pushback." These are the archetype tensions, not trait recitals.
- The memory sentence is the generator's only account of the event, and the catalog kinds without
  a verb in `MemoryVerb` reach it as "you interacted with Lewis (StoodUpFor)". Haiku read that
  as "Lewis stood me up and I still did my part". Welcomed (21 of 64), PlayedGame (5) and
  StoodUpFor (1) all arrive this way; Qwen's week had the same gaps. This is not changed here
  because the memory text is part of the exact-match tape contract and the October 12 pack
  replays against it; add the verbs at the next request-format change.
- "Maybe" opens or hedges 47 of 64 thoughts: the instruction to imagine "an uncertain future
  possibility" has become a verbal tic. Six thoughts invent a concrete item ("a bit of bread from
  my kitchen", "the shiny stone", "a small tool I've fixed up"). Imagined specifics are not
  evidence and the act stays a generic gift, but the observer will read the bread and see no
  bread; the prompt forbids invented people, events and acts, not items, and that gap is a
  decision for Sid rather than a prompt tweak.

## What this establishes, and what it does not

On these cases Haiku 5.5 without thinking is the better inspiration generator by every criterion
the probe can measure: plan, form, grounding, register, zero copying, separable voices, and
agreement between prose and suggested act in the loop. Turning adaptive thinking on made the
wording more vivid and more wrong, drifting into address and added detail; keep it off unless a
later probe shows otherwise. It is a cloud model: roughly 1.5 s per thought, a few cents per
28-day recording, no seed, and the scene text leaves the machine. It replaces nothing by default.

It does not establish distinct voice in the roadmap's sense. The per-character pet phrases are the
same descriptor turned into prose, Maru remains hard to place for judges and controls alike,
and twelve cases on a fixed gratitude plan say nothing about hostility, deferral, dreams or the
long run of a town. The review was model-assisted; Sid's own reading under the
[observer protocol](observer-test-2026-10-09.md) still decides. Twenty-four replies per condition
are too few for rates; replicates 3 and beyond are deterministic seeds if a larger run is wanted.

Sid's reading of this (2026-10-12): Haiku with thinking off is the generator for hybrid recordings
from here, at a cost he accepts; the local service stays as the offline control. Imagined items may
stay in private thoughts; the concern he keeps for later is an imagined detail that does not fit
the context or what a player would see.

Open follow-ups, in order: give the generator a seeded order of acts and stop labelling a decision
"reshaped" when the thought already names the chosen act (both carried from the PR #82 review);
add the missing memory verbs when the tape contract next changes; keep imagined details within
what the scene and the observer could see; and repeat this probe with a second cloud tier only if
a specific failure above needs it, since Haiku already clears the mechanical bar.

## Addendum, the same day: the scene reaches both models

The first step of Sid's agreed order (roadmap, "The agreed order") gives each reflection request
a `ReflectionScene`: where the remembered encounter happened and how long ago, where the actor
is now and the time of day, how they know the subject, the pair's kind and unkind acts this week
and the actor's own appraisal reasons, all from the actor's records. The generator gets it as
fields; Laya gets one line when the budget allows. The same seven-day seed-7 week was recorded
twice more with Haiku, thinking off and profiles on, once with raw place keys and once with
homes worded as "home", "Jas's home" or "the Saloon home". The weeks diverge after their first
decisions, so these are three towns, not one town measured three ways.

| Measure | No scene (above) | Scene, raw place keys | Scene, worded places (final) |
| --- | --- | --- | --- |
| Reflections / real generator answers / fallbacks | 64 / 64 / 0 | 63 / 62 / 1 (one 5-second timeout) | 59 / 59 / 0 |
| Thoughts naming a place, a time or the week | 0 | 56 of 62 | 52 of 59 |
| Thoughts reciting mood/regard/opportunity words | 2 | 0 | 0 |
| Scene line present in the Laya packet | n/a | 60 of 62 | 59 of 59 |
| Thought names the suggested act / a different act / unclear (keyword reading) | 48 / 0 / 16 | 43 / 0 / 19 | 35 / 2 / 22 |
| Thoughts naming an item the scene lacks | 6 | 8 | 6 |
| Thoughts hedged with "maybe" | 47 | 41 | 40 |
| Accepted / reshaped / deferred / rejected | 23 / 25 / 8 / 8 | 21 / 26 / 9 / 6 | 18 / 24 / 7 / 10 |

The scene is used: "Newcomer said hello in the Square earlier, and I didn't really answer";
"Emily left that gift for me at the saloon yesterday, so I should find a way to give something
back to her before the week gets busy"; "Newcomer's been kind to me twice this week, so maybe I
owe them something back, even if I'm a bit prickly today"; George at home, "Evelyn helped me when
I didn't ask, and I hate how much that stung." The number-recital tic is gone. Invented items did
not go up and now fit the place: Marnie's "a bit of bread or a jar of jam, the way I'd welcome
anyone new", Vincent's "like my marble", Abigail's "something small from the stall" in the Square.
"StoodUpFor" no longer reaches the generator as a bare kind name.

The keyword agreement count fell with the scene (75% → 69% → 59%) while plain contradictions
stayed at two or fewer, and both of those are hedged compound sentences ("maybe I could return
one, or at least say thanks"). The thoughts got longer and more conditional, which the keyword
heuristic reads as "unclear"; whether the suggested act still matches is a question for the blind
judging method above, not for a regex, and it is left open here. "Maybe" still opens most
thoughts.

The two omitted Laya lines in the raw-key week were packets with a long thought and five lines
where the required context and memory would not have fit beside it; the recorded packets show
the omission. Older tapes, including the verified October 12 pack, no longer replay against this
request shape; the final week replays from its own tape. Files:
`out/scene-context-2026-10-12/hybrid-haiku-scene2-seed7-7d.json` (log hash `f98991c612d1eab8`,
SHA256 `6d3b6e24b3a23d4b7b476e3c4487f066300e40ad9aa3503bc35202b494aa8921`) and the first pass
`hybrid-haiku-scene-seed7-7d.json` (`ddaf3058fa13cb09`,
`c98bd2ba94056d6a0293e04d4567fadf5810885b4468ff587d4b45de0e2c1c13`).

## Reproduce

The SDK goes into the existing sidecar environment explicitly; no launcher installs it:

```powershell
.\sidecar\.venv\Scripts\python.exe -m pip install -r sim/tools/claude-generator-requirements.txt
# The key file holds the key as its only line and lives outside the checkout.
.\sim\run-claude-generator.ps1 -Contract wording -Port 8081 -ApiKeyFile $env:USERPROFILE\.anthropic\api-key.txt -Verify
dotnet run -c Release --project sim/UnderGlass.ReflectionTrial -- --suite wording --llm-url http://127.0.0.1:8081 --llm-model claude-haiku-5-5 --character-inspiration sim/character-inspiration.json --voice-cases sim/voice-wording-cases.json --dialogue-corpus out/stardew-dialogue-2026-10-12/corpus.json --dialogue-manifest out/stardew-dialogue-2026-10-12/raw-assets.json --timeout-ms 30000 --out out/voice-wording-haiku-2026-10-12/rerun.json --report out/voice-wording-haiku-2026-10-12/rerun.md --html out/voice-wording-haiku-2026-10-12/rerun.html
# Condition B: -Thinking adaptive -Effort medium -Port 8082, then the same trial against 8082.
# The runtime loop needs the reflection contract and a running Laya.
.\sim\run-claude-generator.ps1 -Contract reflection -Port 8083 -ApiKeyFile $env:USERPROFILE\.anthropic\api-key.txt -Verify
dotnet run -c Release --project sim/UnderGlass.Replay -- --seed 7 --days 7 --catalog returns,company,welcome,repair,sides,late --feel on --desire on --reflection-chance 0.35 --dream-chance 0.15 --reflection hybrid --laya-url http://127.0.0.1:8000 --llm-url http://127.0.0.1:8083 --llm-model claude-haiku-5-5 --character-inspiration sim/character-inspiration.json --out out/voice-wording-haiku-2026-10-12/hybrid-haiku-seed7-7d.json --html out/voice-wording-haiku-2026-10-12/hybrid-haiku-seed7-7d.html
```

A live rerun will not reproduce these replies; the API has no seed. Compare receipts, prompts and
hashes before interpreting one. The blind review is rerun from the preserved pack with the kept
workflow script; its verdicts are also not deterministic.

## Input and result identity

Both wording runs report case `packHash` `37e58b2ec12da0cf5c3d6f7a808ad960505d1535ebe1edc70f8f7b437f9b9c12`,
profile `profilePackHash` `67aff67c40e47fb339d8ed304328b3e7647671a7cc6755496dd73ed4e4cc5c4e` and
corpus `corpusHash` `8db33c9a15d1d31949e4111c9b94a63f14bf37f5869c5eda6c768cf219258bda`, the same
values as the Qwen runs. Game 1.6.15.24356. Service source: commit `ee3976e`.

| File | File SHA256 |
| --- | --- |
| `out/voice-wording-haiku-2026-10-12/haiku-nothink.json` | `345cecf4c0810b5cb5d1690a35182a6ef21aec11c3ef14d5e40d36ca802d5be2` |
| `out/voice-wording-haiku-2026-10-12/haiku-think.json` | `7590240f7eb9a3d482de2b40a5dd5990cd2b3a295befa515fbbe78afcee3113a` |
| `out/voice-wording-haiku-2026-10-12/hybrid-haiku-seed7-7d.json` (log hash `db182e956b8dfe36`) | `9c49e135d30a09620686f7db9e73758a14b41a9800e45dfd90a1dd4eed92f261` |
| `out/voice-wording-haiku-2026-10-12/hybrid-qwen-seed7-7d.json` | `672f964a17d991fae94089ec783ff52b40e0e9d6e2a2d04f6866eb883dc9d439` (log hash `51678d6afb253a5e`) |
| `out/voice-wording-haiku-2026-10-12/review/judging-rows.json` | `bafe5d865f20fa117211850175a0e5e8f05cc7708c0eb484c4acbf1a9ba323c9` |

These are local ignored artifacts; the reference-arm files contain the dialogue excerpts and stay
out of Git.
