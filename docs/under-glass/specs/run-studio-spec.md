# Run studio

## Status

Built, 2026-10-14. `sim/viewer/runs.html` launches recordings from the local preview,
using the shared observer UI. It exposes the Replay executable's current flags, repeatable
feeling/action overrides and character traits. The Lives routing/company/evening flags are now integrated. This does not implement player
speech input or an automated archetype comparison suite.

## Workflow

Start `sim/tools/start-preview.ps1` and choose **New run** in the shared navigation. The
server builds Replay once at startup; restart it after simulation changes. Choose a preset or
CLI defaults, edit the grouped settings and generate. Haiku + Laya is the page's default preset;
local Qwen + Laya, Laya only and authored control are alternatives. Authored control needs neither
Laya nor a generator. Haiku generation uses the configured Anthropic account.

Double-click `Start Under Glass.cmd` to open New run. The launcher tries the usual port and
nearby ports, reusing only a preview whose API identifies this checkout. **Start required servers**
starts installed services and reuses compatible healthy servers. Service status and logs stay
visible; Stop is available only for a process this preview started, and is disabled during a run.
Stopping the preview stops its model children too. No package installation or weight download is
performed. Qwen uses the pinned cached checkpoint on CUDA; custom/CPU services can still be
started with their own launcher and selected through the model fields. Health is not an inference
test, and unavailable model calls can still produce the existing authored fallback.

Model Python comes from this checkout's `sidecar/.venv`, then the main checkout of the worktree,
or `UNDER_GLASS_PYTHON`. Haiku uses its existing environment credentials, an explicit
`UNDER_GLASS_ANTHROPIC_KEY_FILE`, or the existing `~/.anthropic/api-key.txt`; keys never enter
browser settings or recordings. Startup makes no paid inference request.

Only one run executes at a time. Each has a unique folder under `out/run-studio/`, a `job.json`
receipt containing arguments/settings, a log, and JSON and/or standalone HTML. Successful runs
link into Run Viewer; settings can be reused or exported/imported. Failures retain their output
log. Cancellation stops that run's process tree. After an interrupted server session, formerly
active receipts are labelled interrupted, not successful. Output folders are never overwritten.

The page obtains all flags from `UnderGlass.Replay --describe-options`; public numeric and
boolean FeelingOptions/ActOptions properties populate the override pickers. Override values
describe shipped defaults before town/flag changes, not a preview of the final effective state.
Overrides execute last, in listed order. Output flags are represented by format checkboxes;
paths are assigned by the service. The UI bounds duration to 1–36,500 days. Generated towns
currently support 31, 46 and 60 people; uploaded town files remain available.

## Speech testing

The speech editor always includes **Newcomer**, even with no tagged lines. Add and download a
catalog there, upload it under Speech, and enable **Use Newcomer test voice**. `--speech-catalog`
loads the validated catalog; `--newcomer-voice` assigns only the character named Newcomer the
`newcomer` speech tag. Both imply `--speech`. Generic lines remain eligible. They change neither
decisions nor world log hashes. Recordings carry catalog metadata and a SHA-256 fingerprint,
not the whole catalog. No default Newcomer lines or player-input mechanics are added.

## Service boundary

`sim/tools/preview_server.py` serves this checkout on loopback and accepts JSON jobs under
`/api/run-studio`. It validates known flags/types, same-origin requests and a per-session token,
uses argument arrays without a shell, confines input JSON paths to the checkout, and assigns
output paths itself. Uploads are unique local copies under `out/run-studio/inputs`. Model URLs
must be local HTTP service addresses. This is a local development tool, not a public job host.
Static viewing still works without the service; generating does not.

## Checks

- `tests/test_preview_server.py`: CLI/schema parity, typed options, path and request boundaries,
  unique output, job failure/restart behaviour and uploads, without models or .NET execution.
- `sim/tools/test-preview.ps1`: launcher ownership, reuse, stop and unrelated listener checks.
- `tests/test_studio_services.py`: compatible service reuse, owned-child stop, fixed commands,
  cached weights and credential-path separation, without launching models.
- `sim/tools/test-speech-page.cjs`: Newcomer appears in an empty catalog and uses normal weights.
- `SpeechSceneTests`: custom catalogs and the test tag preserve the world and speech determinism.
- Manual browser checks: edit/export/upload a Newcomer line, generate/open a run, cancel a long
  run, edit/reload/validate character context, and start/stop managed Laya, Haiku and cached Qwen.
  No paid generation calls were made; Qwen startup includes its local warmup.

## History workbench and live observation (2026-10-15)

`histories.html` edits an executable version-1 history pack across the existing cast. Start from
the reviewed pilot, import a pack, or add accounts for another person. A browser draft and JSON
export preserve work. The form edits acquisition, cues, current feelings and expectation keys.
Cue pickers use current cast/place names. Editing a sourced account converts that account and its
readings in the draft into explicitly experimental authoring; the shipped source stays untouched.
`ExperimentalStated` is an alternate starting attitude, not an admitted interpretation of canon.
Finished pasts remain recall-only in this authoring mode. See the memory spec for admission.

**Use draft in a new run** validates through Replay, then carries a saved path into New run.
**Test its influence** additionally requests held-choice fallback comparisons with this account
and its readings removed. These do not call Haiku or Laya or simulate a counterfactual whole life.
The server freezes all file inputs in each job folder, including histories and character/speech
packs. History is also recorded in `livesRouting` with its existing hash. Later draft edits never
change an existing run. A history does not add a villager to the town; unknown holders are rejected
by simulation setup. Archetypes still contain no personal histories. Haiku's inspiration prompt
still uses the character-profile background, separately from the Lives memories Laya evaluates.

New-run presets enable routing, company and evenings for the whole cast (no three-person filter).
Only the reviewed pilot has sourced histories by default. Other characters learn during the run;
users can author explicit experimental starting conditions for them. Existing action eligibility
still applies: the editor does not create new activities from prose.

**Watch while generating** supplies observer snapshots to Run Viewer and Life chains. Generation
pace is a wall-clock ceiling (10 minutes/sec through unlimited), independent of recorded-playback
speed. Pause waits until the current model answer finishes, then stops advancement. Model requests
remain sequential at their simulated minute; no asynchronous answer can change the decision order.
The UI distinguishes reflection, evening-choice waits, pause, failure and completion. Turning off
Follow latest, scrubbing, or selecting a chain stops the viewport following without stopping the
simulation. Pause run stops simulation advancement. Resume follows the latest snapshots again.

Partial snapshots have `live.at` and `live.partial`. A partial run is never offered as successful.
The final recording is written separately; stopped/failed jobs retain their last partial snapshot.
The recorder constructs and serializes snapshots synchronously on the simulation worker, and
atomically replaces files. Output callbacks and pacing consume no simulation draws. A regression
test compares the full final JSON with and without intermediate serialization. Startup validation,
file snapshotting and controls share the existing same-origin/token boundary.

This first transport publishes full snapshots about once a second and at model-call boundaries;
it is not a bounded incremental stream. Longer runs cost progressively more serialization and
browser work. Start with short experiments; incremental transport and editing histories for an
uploaded custom town deserve a later slice. Existing all-flags/custom-town support remains.

Verification: browser-created Robin account, engine validation, frozen run input, live pause/resume,
viewer/chain navigation; a one-day seed-7 Haiku + Laya run completed in about 6 seconds unlimited,
with one generated/reflection evaluation and three Laya evening decisions. This is a smoke
measurement, not a long-run throughput guarantee. The larger activity vocabulary remains next.
