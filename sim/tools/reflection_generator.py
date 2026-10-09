"""Small, cache-only local generator for Under Glass private imagined proposals.

Run with the existing sidecar Python environment. This service returns model text
unchanged; the simulation adapter owns JSON/choice validation and explicit fallback.
No request can select a model, revision, device, file, or remote URL to load.
"""

from __future__ import annotations

import argparse
import asyncio
from concurrent.futures import ThreadPoolExecutor
from contextlib import asynccontextmanager
from dataclasses import dataclass
import hashlib
import ipaddress
import json
import math
import re
import threading
import time
from typing import Any

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from starlette.middleware.trustedhost import TrustedHostMiddleware


MODEL_ID = "Qwen/Qwen3-0.6B"
MODEL_REVISION = "c1899de289a04d12100db370d81485cdf75e47ca"


@dataclass(frozen=True)
class GeneratorConfig:
    model: str = MODEL_ID
    revision: str = MODEL_REVISION
    device: str = "cpu"
    max_body_bytes: int = 16_384
    max_context_chars: int = 6_000
    max_input_tokens: int = 2_048
    max_new_tokens: int = 180
    timeout_seconds: float = 4.0
    body_timeout_seconds: float = 2.0
    warmup: bool = False

    def __post_init__(self) -> None:
        if self.model != MODEL_ID:
            raise ValueError(f"Only the reviewed {MODEL_ID} checkpoint is supported.")
        if not re.fullmatch(r"[a-f0-9]{40}", self.revision):
            raise ValueError("revision must be an explicit 40-character commit SHA")
        if self.device not in ("cpu", "cuda"):
            raise ValueError("device must be cpu or cuda")
        for name, ceiling in (("max_body_bytes", 65_536), ("max_context_chars", 12_000),
                              ("max_input_tokens", 4_096), ("max_new_tokens", 180)):
            value = getattr(self, name)
            if type(value) is not int or not 1 <= value <= ceiling:
                raise ValueError(f"{name} must be an integer in 1..{ceiling}")
        for name in ("timeout_seconds", "body_timeout_seconds"):
            value = getattr(self, name)
            if not math.isfinite(value) or not 0 < value <= 60:
                raise ValueError(f"{name} must be finite and in (0,60]")


@dataclass(frozen=True)
class GenerationInput:
    messages: tuple[dict[str, str], ...]
    max_tokens: int
    temperature: float
    top_p: float
    seed: int


@dataclass(frozen=True)
class GenerationOutput:
    text: str
    prompt_tokens: int
    completion_tokens: int
    finish_reason: str


class RequestError(ValueError):
    def __init__(self, message: str, status: int = 400):
        super().__init__(message)
        self.status = status


def parse_request(value: Any, config: GeneratorConfig) -> GenerationInput:
    if not isinstance(value, dict):
        raise RequestError("Expected a JSON object.")
    allowed = {"model", "messages", "max_tokens", "temperature", "top_p", "seed",
               "stream", "response_format"}
    if set(value) - allowed:
        raise RequestError("Unsupported request fields: " + ", ".join(sorted(set(value) - allowed)))
    if value.get("model") != config.model:
        raise RequestError("model must match the configured model ID.")
    if value.get("stream", False) is not False:
        raise RequestError("Streaming is not supported.")
    if "response_format" in value and value["response_format"] != {"type": "json_object"}:
        raise RequestError("Only response_format json_object is supported; output is validated by the caller.")
    messages = value.get("messages")
    if not isinstance(messages, list) or not 1 <= len(messages) <= 8:
        raise RequestError("messages must contain 1..8 text messages.")
    copied = []
    for message in messages:
        if not isinstance(message, dict) or set(message) != {"role", "content"}:
            raise RequestError("Each message needs only role and content.")
        if message["role"] not in ("system", "user", "assistant") or not isinstance(message["content"], str):
            raise RequestError("Messages must have a supported role and string content.")
        copied.append(dict(message))
    if not any(m["role"] == "user" and m["content"].strip() for m in copied):
        raise RequestError("At least one nonempty user message is required.")
    if sum(len(m["content"]) for m in copied) > config.max_context_chars:
        raise RequestError("Message text exceeds the context character limit; no truncation was applied.", 413)
    tokens = value.get("max_tokens", config.max_new_tokens)
    if type(tokens) is not int or not 1 <= tokens <= config.max_new_tokens:
        raise RequestError(f"max_tokens must be an integer in 1..{config.max_new_tokens}.")
    temperature = value.get("temperature", 0.7)
    top_p = value.get("top_p", 0.8)
    for name, number, maximum in (("temperature", temperature, 2), ("top_p", top_p, 1)):
        if type(number) not in (int, float) or not math.isfinite(number) or not 0 < number <= maximum:
            raise RequestError(f"{name} must be finite and in (0,{maximum}].")
    seed = value.get("seed")
    if "seed" in value and (type(seed) is not int or not 0 <= seed < 2**63):
        raise RequestError("seed must be an integer in 0..2^63-1.")
    if seed is None:
        # Include settings/revision: reproducible on the same runtime, not a claim
        # that different GPU kernels or dependency versions are bit-identical.
        material = json.dumps([config.model, config.revision, copied, tokens, float(temperature), float(top_p)],
                              ensure_ascii=False, separators=(",", ":"), sort_keys=True)
        seed = int.from_bytes(hashlib.sha256(material.encode("utf-8")).digest()[:8], "big") & (2**63 - 1)
    return GenerationInput(tuple(copied), tokens, float(temperature), float(top_p), seed)


class TransformerEngine:
    def __init__(self, config: GeneratorConfig):
        self.config = config
        self.tokenizer = None
        self.model = None
        self.torch = None

    def load(self) -> None:
        import torch
        from transformers import AutoModelForCausalLM, AutoTokenizer

        if self.config.device == "cuda" and not torch.cuda.is_available():
            raise RuntimeError("CUDA was requested but is unavailable; select --device cpu explicitly.")
        kwargs = {"revision": self.config.revision, "local_files_only": True, "trust_remote_code": False}
        self.tokenizer = AutoTokenizer.from_pretrained(self.config.model, **kwargs)
        dtype = torch.float16 if self.config.device == "cuda" else torch.float32
        self.model = AutoModelForCausalLM.from_pretrained(self.config.model, dtype=dtype, **kwargs)
        self.model.to(self.config.device)
        self.model.eval()
        self.torch = torch

    def generate(self, request: GenerationInput, stop: threading.Event, deadline: float) -> GenerationOutput:
        from transformers import StoppingCriteria, StoppingCriteriaList

        torch = self.torch
        if self.model is None or self.tokenizer is None or torch is None:
            raise RuntimeError("Model is not loaded.")

        def check_deadline() -> None:
            if stop.is_set() or time.monotonic() >= deadline:
                raise TimeoutError("Generation deadline exceeded or request cancelled.")

        class DeadlineCriteria(StoppingCriteria):
            def __call__(self, input_ids, scores, **kwargs):
                return torch.full((input_ids.shape[0],), stop.is_set() or time.monotonic() >= deadline,
                                  device=input_ids.device, dtype=torch.bool)

        check_deadline()
        # Qwen's official nonthinking template, not a text filter over its answer.
        text = self.tokenizer.apply_chat_template(list(request.messages), tokenize=False,
                                                  add_generation_prompt=True, enable_thinking=False)
        encoded = self.tokenizer(text, return_tensors="pt", add_special_tokens=False)
        prompt_tokens = encoded["input_ids"].shape[-1]
        if prompt_tokens > self.config.max_input_tokens:
            raise RequestError("Prompt exceeds input token limit; no truncation was applied.", 413)
        check_deadline()
        encoded = encoded.to(self.config.device)
        devices = [torch.cuda.current_device()] if self.config.device == "cuda" else []
        # Only touch the RNGs whose states fork_rng saves. torch.manual_seed also
        # affects other accelerators, so seed these generators directly instead.
        with torch.random.fork_rng(devices=devices), torch.inference_mode():
            torch.random.default_generator.manual_seed(request.seed)
            for device in devices:
                torch.cuda.default_generators[device].manual_seed(request.seed)
            output = self.model.generate(
                **encoded, max_new_tokens=request.max_tokens, do_sample=True,
                temperature=request.temperature, top_p=request.top_p, top_k=20,
                stopping_criteria=StoppingCriteriaList([DeadlineCriteria()]),
                pad_token_id=self.tokenizer.eos_token_id,
            )
        check_deadline()
        new_tokens = output[0][prompt_tokens:]
        content = self.tokenizer.decode(new_tokens, skip_special_tokens=True)
        count = len(new_tokens)
        eos = self.model.generation_config.eos_token_id
        eos_ids = eos if isinstance(eos, list) else [eos]
        ended = count > 0 and int(new_tokens[-1]) in eos_ids
        return GenerationOutput(content, prompt_tokens, count,
                                "stop" if ended or count < request.max_tokens else "length")


def error(message: str, status: int, kind: str = "invalid_request_error") -> JSONResponse:
    return JSONResponse({"error": {"message": message, "type": kind}}, status_code=status)


def create_app(config: GeneratorConfig, engine: Any = None) -> FastAPI:
    """Engine injection lets contract tests run without torch or cached models."""
    engine = engine if engine is not None else TransformerEngine(config)
    gate = threading.Lock()
    pool = ThreadPoolExecutor(max_workers=1, thread_name_prefix="reflection-generator")
    active_stop: threading.Event | None = None
    state = {"loaded": False, "warm": False, "error": None}

    @asynccontextmanager
    async def lifespan(app):
        try:
            await asyncio.get_running_loop().run_in_executor(pool, engine.load)
            state["loaded"] = True
            if config.warmup:
                warm = parse_request({"model": config.model, "messages": [{"role": "user", "content": "Say hello."}],
                                      "max_tokens": min(8, config.max_new_tokens)}, config)
                await asyncio.get_running_loop().run_in_executor(
                    pool, engine.generate, warm, threading.Event(), time.monotonic() + config.timeout_seconds)
                state["warm"] = True
        except Exception as exc:
            state["error"] = f"{type(exc).__name__}: {exc}"
        try:
            yield
        finally:
            if active_stop is not None:
                active_stop.set()
            pool.shutdown(wait=False, cancel_futures=True)

    app = FastAPI(title="Under Glass reflection generator", docs_url=None, redoc_url=None,
                  openapi_url=None, lifespan=lifespan)
    app.add_middleware(TrustedHostMiddleware, allowed_hosts=["127.0.0.1", "localhost", "[::1]"])

    @app.middleware("http")
    async def local_only(request: Request, call_next):
        try:
            local = request.client is not None and ipaddress.ip_address(request.client.host).is_loopback
        except ValueError:
            local = False
        if not local:
            return error("Only loopback clients are accepted.", 403)
        return await call_next(request)

    @app.get("/health")
    async def health():
        ready = state["loaded"] and state["error"] is None
        return JSONResponse({"status": "ready" if ready else "unavailable", **state,
                             "busy": gate.locked(), "model": config.model, "revision": config.revision,
                             "device": config.device, "cache_only": True, "thinking": False,
                             "max_input_tokens": config.max_input_tokens, "max_new_tokens": config.max_new_tokens,
                             "timeout_seconds": config.timeout_seconds}, status_code=200 if ready else 503)

    @app.get("/v1/models")
    async def models():
        return {"object": "list", "data": [{"id": config.model, "object": "model", "owned_by": "local",
                                            "revision": config.revision}]}

    async def read_body(request: Request) -> Any:
        if request.headers.get("content-type", "").split(";", 1)[0].strip().lower() != "application/json":
            raise RequestError("Content-Type must be application/json.", 415)
        length = request.headers.get("content-length")
        if length is not None:
            try:
                length = int(length)
            except ValueError:
                raise RequestError("Invalid Content-Length.") from None
            if length < 0 or length > config.max_body_bytes:
                raise RequestError("Request body exceeds byte limit.", 413)
        body = bytearray()
        try:
            async with asyncio.timeout(config.body_timeout_seconds):
                async for chunk in request.stream():
                    if len(body) + len(chunk) > config.max_body_bytes:
                        raise RequestError("Request body exceeds byte limit.", 413)
                    body.extend(chunk)
        except TimeoutError:
            raise RequestError("Request body timed out.", 408) from None
        try:
            return json.loads(body)
        except (ValueError, UnicodeError):
            raise RequestError("Request body is not valid JSON.") from None

    @app.post("/v1/chat/completions")
    async def chat(request: Request):
        nonlocal active_stop
        try:
            parsed = parse_request(await read_body(request), config)
        except RequestError as exc:
            return error(str(exc), exc.status)
        if not state["loaded"] or state["error"] is not None:
            return error("Model unavailable; inspect /health. Normal startup never downloads a checkpoint.", 503, "unavailable")
        if not gate.acquire(blocking=False):
            return error("Generator is busy; requests are not queued.", 503, "busy")
        stop = threading.Event()
        active_stop = stop
        started = time.monotonic()
        deadline = started + config.timeout_seconds

        def work():
            try:
                return engine.generate(parsed, stop, deadline)
            finally:
                # A timed-out HTTP caller does not make the GPU available. The
                # worker owns release until generation actually stops.
                gate.release()

        future = asyncio.get_running_loop().run_in_executor(pool, work)

        async def disconnected():
            while not await request.is_disconnected():
                await asyncio.sleep(0.025)

        disconnect = asyncio.create_task(disconnected())
        try:
            completed, _ = await asyncio.wait((future, disconnect), timeout=config.timeout_seconds,
                                              return_when=asyncio.FIRST_COMPLETED)
            if not completed:
                raise TimeoutError()
            if disconnect in completed:
                stop.set()
                future.add_done_callback(lambda done: None if done.cancelled() else done.exception())
                return error("Client disconnected; worker cancellation requested.", 499, "cancelled")
            result = future.result()
        except (TimeoutError, asyncio.CancelledError) as exc:
            stop.set()
            # Consume an eventual worker exception after the HTTP call has ended.
            future.add_done_callback(lambda done: None if done.cancelled() else done.exception())
            if isinstance(exc, asyncio.CancelledError):
                raise
            return error("Generation timed out; worker cancellation requested.", 504, "timeout")
        except RequestError as exc:
            return error(str(exc), exc.status)
        except Exception as exc:
            return error(f"Generation failed: {type(exc).__name__}.", 500, "generation_error")
        finally:
            disconnect.cancel()
        return {"id": f"reflection-{parsed.seed}", "object": "chat.completion", "model": config.model,
                "choices": [{"index": 0, "message": {"role": "assistant", "content": result.text},
                             "finish_reason": result.finish_reason}],
                "usage": {"prompt_tokens": result.prompt_tokens, "completion_tokens": result.completion_tokens,
                          "total_tokens": result.prompt_tokens + result.completion_tokens,
                          "input_tokens": result.prompt_tokens, "output_tokens": result.completion_tokens,
                          "elapsed_ms": (time.monotonic() - started) * 1000},
                "seed": parsed.seed, "revision": config.revision, "device": config.device}

    return app


def download_checkpoint(config: GeneratorConfig) -> str:
    from huggingface_hub import snapshot_download

    return snapshot_download(repo_id=config.model, revision=config.revision,
                             allow_patterns=["config.json", "generation_config.json", "tokenizer.json",
                                             "tokenizer_config.json", "vocab.json", "merges.txt",
                                             "model.safetensors", "LICENSE", "README.md"])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", default=MODEL_ID)
    parser.add_argument("--revision", default=MODEL_REVISION)
    parser.add_argument("--device", choices=("cpu", "cuda"), required=True)
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--timeout-seconds", type=float, default=4.0)
    parser.add_argument("--warmup", action="store_true")
    parser.add_argument("--download-only", action="store_true", help="Explicitly fetch the pinned reviewed checkpoint, then exit.")
    args = parser.parse_args()
    if not 1 <= args.port <= 65535:
        parser.error("port must be in 1..65535")
    try:
        config = GeneratorConfig(model=args.model, revision=args.revision, device=args.device,
                                 timeout_seconds=args.timeout_seconds, warmup=args.warmup)
    except ValueError as exc:
        parser.error(str(exc))
    if args.download_only:
        print(download_checkpoint(config))
        return
    import uvicorn

    # Host and worker count are deliberately not configurable. Proxy forwarding
    # is disabled so client addresses cannot be replaced by request headers.
    uvicorn.run(create_app(config), host="127.0.0.1", port=args.port, workers=1, proxy_headers=False)


if __name__ == "__main__":
    main()
