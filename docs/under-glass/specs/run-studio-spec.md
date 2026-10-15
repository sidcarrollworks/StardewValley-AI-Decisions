# Run studio

## Status

Built, 2026-10-14. `sim/viewer/runs.html` launches recordings from the local preview,
using the shared observer UI. It exposes the Replay executable's current flags, repeatable
feeling/action overrides and character traits. This does not implement the planned Lives pilot,
player speech input or an automated archetype comparison suite.

## Workflow

Start `sim/tools/start-preview.ps1` and choose **New run** in the shared navigation. The
server builds Replay once at startup; restart it after simulation changes. Choose a preset or
CLI defaults, edit the grouped settings and generate. Models must already be running; a hybrid
service may use a paid provider. Authored control needs neither Laya nor a generator.

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
- `sim/tools/test-speech-page.cjs`: Newcomer appears in an empty catalog and uses normal weights.
- `SpeechSceneTests`: custom catalogs and the test tag preserve the world and speech determinism.
- Manual browser check: edit/export/upload a Newcomer line, generate and open a run, and cancel
  a long run. Model inference is covered by existing adapters, not invoked by these UI checks.
