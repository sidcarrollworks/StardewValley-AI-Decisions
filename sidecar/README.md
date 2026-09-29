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

`pip` installs the CPU build of PyTorch by default on Windows. For a GPU, install the CUDA build of
torch first, following the selector at pytorch.org, then `pip install "laya[serve]"`.

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
