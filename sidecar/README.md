# Laya sidecar

The mod's `LayaDecisionClient` (src/NpcDecision) talks to a local **Laya** server over HTTP.
Laya is a separate Python process (Convai Innovations, github.com/NandhaKishorM/laya, checked
against v0.3.22). Nothing here is needed for the fake/shadow client; only run it when the mod
is configured to use Laya.

## Requirements

- Windows 10/11 (the scripts also work on Linux/macOS)
- Python 3.10 or newer (`py --version` or `python --version`)
- A few GB of disk for PyTorch plus the model weights
- A CUDA GPU is optional. On CPU a decision takes a few hundred ms; on GPU tens of ms.

## Install (once)

From this `sidecar/` folder, in PowerShell:

```powershell
py -3 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install "laya[serve]"
```

(Git Bash: `python -m venv .venv && source .venv/Scripts/activate`; Linux/macOS:
`python3 -m venv .venv && source .venv/bin/activate`; then the same `pip install`.)

`pip` installs the CPU build of PyTorch by default on Windows. **Use the GPU if you have an NVIDIA
card**: it is about seven times faster (measured below). With the venv active, replace torch with
the CUDA build of the same version:

```powershell
python -m pip install "torch==2.14.0+cu130" --index-url https://download.pytorch.org/whl/cu130
python -c "import torch; print(torch.cuda.is_available(), torch.cuda.get_device_name(0))"
```

The second line must print `True` and the card's name. The version must match the torch that
`laya[serve]` installed (`python -m pip show torch`); `cu130` needs an NVIDIA driver recent enough
for CUDA 13 (Sid's 616.92 works). The download is a few GB. `laya-serve` picks the GPU by itself
(`LAYA_DEVICE` defaults to auto), and `/health` then reports `"device":"cuda"`.

**Measured on Sid's PC** (RTX 4070 Ti, `typed-decisions`, 2026-09-30, `laya-serve` 0.3.22, a
~400-character state):

| Request | CPU median | GPU median |
|---|---|---|
| one yes/no | 246 ms | 37 ms |
| one choice, 5 options | 280 ms | 31 ms |
| yes/no + choice in one request | 488 ms | 36 ms |
| a night's plan, ~30 NPCs (estimate) | ~15 s | ~1 s batched, ~2 s unbatched |

## Run

```powershell
.\run-laya.ps1          # PowerShell
```

```bash
./run-laya.sh           # Git Bash / Linux / macOS
```

Both scripts use `.venv` in this folder when it exists (otherwise whatever `laya-serve` is on PATH,
e.g. an already-activated venv) and set:

| variable | value | why |
|---|---|---|
| `LAYA_HOST` | `127.0.0.1` | loopback only; see the security note below |
| `LAYA_PORT` | `8000` | matches the `LayaOptions.BaseUrl` default |
| `LAYA_MODELS` | `typed-decisions` | preload only the checkpoint the mod asks for |
| `LAYA_PRELOAD` | `1` | load weights at start-up, not on the first request |

Any of these already set in your environment wins over the script's default. Other knobs:
`LAYA_DEVICE` (`cuda` / `cpu`, default auto), `LAYA_THREADS` (cap CPU threads; keep it at or
below your physical core count), `LAYA_API_KEY` (require `Authorization: Bearer <key>`; put the
same key in `LayaOptions.ApiKey`), `LAYA_MAX_CONCURRENT` (default 16; excess requests get a 503).

**First run is slow.** The weights (a ModernBERT-large checkpoint, ~421M parameters) download from
Hugging Face on first use and are cached in your Hugging Face cache
(`%USERPROFILE%\.cache\huggingface` by default). The first requests after start-up are also slower
than steady state. Wait for the health check below before starting the game; the mod's client
falls back to neutral answers if Laya is slow or down.

## Check it

```powershell
curl.exe http://127.0.0.1:8000/health
```

should print something like `{"status":"ok","loaded":["typed-decisions"],...}`.
(In Windows PowerShell 5 use `curl.exe`, not `curl`, which is an alias for `Invoke-WebRequest`.)

Then ask it one real question:

```powershell
python smoke_test.py            # or: python smoke_test.py http://127.0.0.1:8000
```

It posts one yes/no (`noul`) question to `/v1/systemone` and prints the probability.
Set `LAYA_API_KEY` in the environment first if the server requires one.

## Security note

Laya's own default is `LAYA_HOST=0.0.0.0`, which listens on every network interface and exposes
the server to anyone on your LAN (with no auth unless `LAYA_API_KEY` is set). The scripts here bind
to `127.0.0.1` so only this PC can reach it. Keep it that way unless you have a reason not to; if
Windows Firewall asks whether to allow Python on networks, you can decline.

## Point the mod at it

After the mod has run once, SMAPI creates `config.json` next to the mod. Set:

```json
{ "DecisionBackend": "Laya", "LayaUrl": "http://127.0.0.1:8000", "LayaModel": "typed-decisions" }
```

The SMAPI log says whether Laya answered its health check. If it is down or slow, every decision
falls back to the deterministic default (per-call timeout `DecisionTimeoutMs`, overnight budget
`PlanningBudgetMs`), and model calls never run on the game thread.

## When questions fail

- **The server's log:** both run scripts append everything the server prints to
  `sidecar/laya.log` (not committed), with a line marking each start. Errors such as
  `CUDA error: unknown error` show up there. Set `LAYA_LOG` to another path to move it; to turn it
  off, set it empty (`LAYA_LOG= ./run-laya.sh`) or, in PowerShell, to `off`.
- **The mod's side:** each fallback in the viewer's call log shows why it fell back, and so does
  the `error` field of the playtest `model` records; `python tools/playtest_summary.py <folder>`
  counts fallbacks by reason.
- **`/health` can say ok while every question fails** (it doesn't touch the GPU). If the GPU
  context is lost, restart the server; it doesn't recover in-process.
