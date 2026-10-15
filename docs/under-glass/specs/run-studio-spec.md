# Run studio

## Status

Built, 2026-10-14. `sim/viewer/runs.html` launches recordings from the local preview,
using the shared observer UI. It exposes the Replay executable's current flags, repeatable
feeling/action overrides and character traits. This does not implement the planned Lives pilot,
player speech input or an automated archetype comparison suite.

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
