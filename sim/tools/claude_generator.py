"""Loopback generator service for Under Glass private inspiration, backed by Claude.

It speaks the same chat-completions contract as reflection_generator.py, so the .NET adapter
and the trial suites can use it without a code change: POST /v1/chat/completions with a
system and a user message, and read choices[0].message.content plus the receipt fields
(model, seed, revision, device, usage, finish_reason). The difference is where the text comes
from: this service calls the Anthropic API through the official SDK.

What leaves the machine: the exact request messages (scene, memory, voice cues, and in the
wording suite's reference arm, the selected dialogue excerpts). Nothing else. The reply, its
usage and the served model are returned unchanged to the caller, which records them.

Credentials come from ANTHROPIC_API_KEY, an explicit --api-key-file, or whatever the SDK
resolves on its own. The key is never logged, echoed or written. No request can choose a
model, a credential or an endpoint: those are fixed when the service is launched.

Claude Haiku 5.5 rejects non-default sampling parameters and has no seed. The service
accepts temperature, top_p and seed for contract compatibility, does not send them, echoes
the seed so the caller's receipt check passes, and says so in the reply's settings block.
A seed therefore does not make two runs identical; use the recorded answers (tape) for that.
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
import os
import time
from typing import Any

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from starlette.middleware.trustedhost import TrustedHostMiddleware


DEFAULT_MODEL = "claude-haiku-5-5"
DEVICE = "anthropic-api"
CONTRACTS = ("wording", "reflection")
THINKING_MODES = ("disabled", "adaptive")
EFFORTS = ("low", "medium", "high", "xhigh", "max")

# The two reply shapes the callers accept. The runtime adapter wants a thought and a
# suggested act; the wording suite wants exactly one field. Structured outputs make the
# model's JSON match the launched contract; the callers still apply their own checks.
SCHEMAS = {
    "wording": {
        "type": "object",
        "properties": {"thought": {"type": "string"}},
        "required": ["thought"],
        "additionalProperties": False,
    },
    "reflection": {
        "type": "object",
        "properties": {"thought": {"type": "string"}, "suggestedChoice": {"type": "string"}},
        "required": ["thought", "suggestedChoice"],
        "additionalProperties": False,
    },
}


@dataclass(frozen=True)
class GeneratorConfig:
    model: str = DEFAULT_MODEL
    contract: str = "reflection"
    thinking: str = "disabled"
    effort: str = "low"
    api_key_file: str | None = None
    max_body_bytes: int = 16_384
    max_context_chars: int = 6_000
    max_request_tokens: int = 180
    max_output_tokens: int = 1_024
    timeout_seconds: float = 20.0
    body_timeout_seconds: float = 2.0
    max_retries: int = 0
    max_concurrency: int = 4
    verify: bool = False

    def __post_init__(self) -> None:
        if not isinstance(self.model, str) or not self.model.startswith("claude-") or len(self.model) > 100:
            raise ValueError("model must be a Claude model ID such as claude-haiku-5-5")
        if self.contract not in CONTRACTS:
            raise ValueError(f"contract must be one of {', '.join(CONTRACTS)}")
        if self.thinking not in THINKING_MODES:
            raise ValueError(f"thinking must be one of {', '.join(THINKING_MODES)}")
        if self.effort not in EFFORTS:
            raise ValueError(f"effort must be one of {', '.join(EFFORTS)}")
        if self.thinking == "disabled" and self.effort in ("xhigh", "max"):
            raise ValueError("thinking can be disabled only at effort low, medium or high")
        if self.api_key_file is not None and (not isinstance(self.api_key_file, str) or not self.api_key_file):
            raise ValueError("api_key_file must be a non-empty path")
        for name, ceiling in (("max_body_bytes", 65_536), ("max_context_chars", 12_000),
                              ("max_request_tokens", 4_096), ("max_output_tokens", 8_192),
                              ("max_retries", 5), ("max_concurrency", 16)):
            value = getattr(self, name)
            floor = 0 if name == "max_retries" else 1
            if type(value) is not int or not floor <= value <= ceiling:
                raise ValueError(f"{name} must be an integer in {floor}..{ceiling}")
        for name in ("timeout_seconds", "body_timeout_seconds"):
            value = getattr(self, name)
            if not math.isfinite(value) or not 0 < value <= 120:
                raise ValueError(f"{name} must be finite and in (0,120]")


@dataclass(frozen=True)
class GenerationInput:
    messages: tuple[dict[str, str], ...]
    max_tokens: int
    temperature: float
    top_p: float
    seed: int
    json_object: bool


@dataclass(frozen=True)
class GenerationOutput:
    text: str
    prompt_tokens: int
    completion_tokens: int
    finish_reason: str
    served_model: str
    request_id: str | None = None
    stop_reason: str | None = None
    stop_details: dict[str, Any] | None = None
    cache_read_tokens: int = 0
    cache_creation_tokens: int = 0


class RequestError(ValueError):
    def __init__(self, message: str, status: int = 400):
        super().__init__(message)
        self.status = status


class EngineError(RuntimeError):
    """An upstream failure, already reduced to an HTTP status and a kind the caller can record."""

    def __init__(self, status: int, kind: str, message: str):
        super().__init__(message)
        self.status = status
        self.kind = kind


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
    json_object = False
    if "response_format" in value:
        if value["response_format"] != {"type": "json_object"}:
            raise RequestError("Only response_format json_object is supported; the launched contract selects the schema.")
        json_object = True
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
    turns = [m for m in copied if m["role"] != "system"]
    if turns[0]["role"] != "user":
        raise RequestError("The first non-system message must be a user message.")
    if sum(len(m["content"]) for m in copied) > config.max_context_chars:
        raise RequestError("Message text exceeds the context character limit; no truncation was applied.", 413)
    tokens = value.get("max_tokens", config.max_request_tokens)
    if type(tokens) is not int or not 1 <= tokens <= config.max_request_tokens:
        raise RequestError(f"max_tokens must be an integer in 1..{config.max_request_tokens}.")
    temperature = value.get("temperature", 1.0)
    top_p = value.get("top_p", 0.99)
    for name, number, maximum in (("temperature", temperature, 2), ("top_p", top_p, 1)):
        if type(number) not in (int, float) or not math.isfinite(number) or not 0 < number <= maximum:
            raise RequestError(f"{name} must be finite and in (0,{maximum}].")
    seed = value.get("seed")
    if "seed" in value and (type(seed) is not int or not 0 <= seed < 2**63):
        raise RequestError("seed must be an integer in 0..2^63-1.")
    if seed is None:
        # A stable receipt value for callers that send none; it is not honoured by the API.
        material = json.dumps([config.model, copied, tokens], ensure_ascii=False, separators=(",", ":"), sort_keys=True)
        seed = int.from_bytes(hashlib.sha256(material.encode("utf-8")).digest()[:8], "big") & (2**63 - 1)
    return GenerationInput(tuple(copied), tokens, float(temperature), float(top_p), seed, json_object)


def resolve_credentials(config: GeneratorConfig) -> tuple[str | None, str]:
    """The key, if this service supplies one explicitly, and where credentials come from.
    The key is handed only to the SDK client; nothing here or elsewhere prints or stores it."""
    if config.api_key_file is not None:
        with open(config.api_key_file, encoding="utf-8") as handle:
            key = handle.read().strip()
        if not key:
            raise RuntimeError("The API key file is empty.")
        return key, "file"
    if os.environ.get("ANTHROPIC_API_KEY"):
        return None, "env"
    if os.environ.get("ANTHROPIC_AUTH_TOKEN"):
        return None, "env-token"
    return None, "sdk-default"


class ClaudeEngine:
    """Calls the Anthropic Messages API. A client can be injected for offline contract tests."""

    def __init__(self, config: GeneratorConfig, client: Any = None):
        self.config = config
        self.client = client
        self.credentials = "injected" if client is not None else "unresolved"
        self.verified_model: str | None = None

    def load(self) -> None:
        if self.client is None:
            try:
                import anthropic
            except ImportError as exc:
                raise RuntimeError("The anthropic package is not installed in this Python environment; "
                                   "install sim/tools/claude-generator-requirements.txt explicitly.") from exc
            key, self.credentials = resolve_credentials(self.config)
            options = {"timeout": self.config.timeout_seconds, "max_retries": self.config.max_retries}
            self.client = anthropic.Anthropic(api_key=key, **options) if key else anthropic.Anthropic(**options)
        if self.config.verify:
            try:
                self.verified_model = self.client.models.retrieve(self.config.model).id
            except Exception as exc:  # noqa: BLE001 - reported through /health, never silently ignored
                raise self._translate(exc) from exc

    def build_request(self, request: GenerationInput) -> dict[str, Any]:
        system_parts = [m["content"] for m in request.messages if m["role"] == "system"]
        turns = [{"role": m["role"], "content": m["content"]} for m in request.messages if m["role"] != "system"]
        kwargs: dict[str, Any] = {
            "model": self.config.model,
            # Thinking tokens count toward max_tokens on Claude, so the caller's small cap is
            # recorded, not forwarded; the caller still enforces its own thought length.
            "max_tokens": self.config.max_output_tokens,
            "messages": turns,
            "thinking": {"type": self.config.thinking},
            "output_config": {"effort": self.config.effort},
        }
        if system_parts:
            kwargs["system"] = "\n\n".join(system_parts)
        if request.json_object:
            kwargs["output_config"]["format"] = {"type": "json_schema", "schema": SCHEMAS[self.config.contract]}
        return kwargs

    def generate(self, request: GenerationInput) -> GenerationOutput:
        if self.client is None:
            raise RuntimeError("Client is not loaded.")
        try:
            response = self.client.messages.create(**self.build_request(request))
        except Exception as exc:  # noqa: BLE001 - every upstream failure becomes an explicit EngineError
            raise self._translate(exc) from exc
        return self.read_response(response)

    @staticmethod
    def read_response(response: Any) -> GenerationOutput:
        text = "".join(block.text for block in response.content if getattr(block, "type", None) == "text")
        stop = getattr(response, "stop_reason", None)
        finish = {"end_turn": "stop", "stop_sequence": "stop", "max_tokens": "length"}.get(stop, stop or "unknown")
        details = getattr(response, "stop_details", None)
        if details is not None and hasattr(details, "to_dict"):
            details = details.to_dict()
        usage = response.usage
        return GenerationOutput(
            text, int(usage.input_tokens), int(usage.output_tokens), finish, str(response.model),
            getattr(response, "_request_id", None), stop, details if isinstance(details, dict) else None,
            int(getattr(usage, "cache_read_input_tokens", 0) or 0),
            int(getattr(usage, "cache_creation_input_tokens", 0) or 0))

    @staticmethod
    def _translate(exc: Exception) -> EngineError:
        if isinstance(exc, EngineError):
            return exc
        name = type(exc).__name__
        status = getattr(exc, "status_code", None)
        message = f"{name}: {str(exc)[:300]}"
        if name in ("AuthenticationError", "PermissionDeniedError"):
            return EngineError(503, "unavailable", message)
        if name == "RateLimitError" or status == 429:
            return EngineError(429, "rate_limited", message)
        if name == "APITimeoutError":
            return EngineError(504, "timeout", message)
        if name == "APIConnectionError":
            return EngineError(502, "upstream_unreachable", message)
        if name in ("BadRequestError", "NotFoundError", "UnprocessableEntityError") or (status is not None and 400 <= status < 500):
            return EngineError(502, "upstream_rejected", message)
        return EngineError(502, "upstream_error", message)


def error(message: str, status: int, kind: str = "invalid_request_error") -> JSONResponse:
    return JSONResponse({"error": {"message": message, "type": kind}}, status_code=status)


def create_app(config: GeneratorConfig, engine: Any = None) -> FastAPI:
    """Engine injection lets contract tests run without the SDK, credentials or the network."""
    engine = engine if engine is not None else ClaudeEngine(config)
    pool = ThreadPoolExecutor(max_workers=config.max_concurrency, thread_name_prefix="claude-generator")
    state: dict[str, Any] = {"loaded": False, "error": None, "in_flight": 0}

    @asynccontextmanager
    async def lifespan(app):
        try:
            await asyncio.get_running_loop().run_in_executor(pool, engine.load)
            state["loaded"] = True
        except Exception as exc:  # noqa: BLE001 - startup problems are reported by /health
            state["error"] = f"{type(exc).__name__}: {exc}"
        try:
            yield
        finally:
            pool.shutdown(wait=False, cancel_futures=True)

    app = FastAPI(title="Under Glass Claude generator", docs_url=None, redoc_url=None,
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

    def settings() -> dict[str, Any]:
        return {"contract": config.contract, "thinking": config.thinking, "effort": config.effort,
                "max_output_tokens": config.max_output_tokens, "timeout_seconds": config.timeout_seconds,
                "max_retries": config.max_retries, "sampling_forwarded": False, "seed_honoured": False}

    @app.get("/health")
    async def health():
        ready = state["loaded"] and state["error"] is None
        return JSONResponse({"status": "ready" if ready else "unavailable", "loaded": state["loaded"],
                             "error": state["error"], "busy": state["in_flight"] > 0, "model": config.model,
                             "provider": "anthropic", "device": DEVICE, "credentials": getattr(engine, "credentials", None),
                             "verified_model": getattr(engine, "verified_model", None), **settings()},
                            status_code=200 if ready else 503)

    @app.get("/v1/models")
    async def models():
        return {"object": "list", "data": [{"id": config.model, "object": "model", "owned_by": "anthropic"}]}

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
        try:
            parsed = parse_request(await read_body(request), config)
        except RequestError as exc:
            return error(str(exc), exc.status)
        if not state["loaded"] or state["error"] is not None:
            return error("Generator unavailable; inspect /health. Credentials are never read from a request.", 503, "unavailable")
        started = time.monotonic()
        state["in_flight"] += 1
        try:
            result = await asyncio.wait_for(
                asyncio.get_running_loop().run_in_executor(pool, engine.generate, parsed),
                timeout=config.timeout_seconds + 2)
        except TimeoutError:
            return error("Generation timed out.", 504, "timeout")
        except EngineError as exc:
            return error(str(exc), exc.status, exc.kind)
        except RequestError as exc:
            return error(str(exc), exc.status)
        except Exception as exc:  # noqa: BLE001 - never leak a traceback; the kind is recorded by the caller
            return error(f"Generation failed: {type(exc).__name__}.", 500, "generation_error")
        finally:
            state["in_flight"] -= 1
        return {"id": f"claude-{parsed.seed}", "object": "chat.completion", "model": config.model,
                "choices": [{"index": 0, "message": {"role": "assistant", "content": result.text},
                             "finish_reason": result.finish_reason}],
                "usage": {"prompt_tokens": result.prompt_tokens, "completion_tokens": result.completion_tokens,
                          "total_tokens": result.prompt_tokens + result.completion_tokens,
                          "input_tokens": result.prompt_tokens, "output_tokens": result.completion_tokens,
                          "cache_read_input_tokens": result.cache_read_tokens,
                          "cache_creation_input_tokens": result.cache_creation_tokens,
                          "elapsed_ms": (time.monotonic() - started) * 1000},
                # revision names the model the API actually served; device names the provider.
                "seed": parsed.seed, "revision": result.served_model, "device": DEVICE,
                "provider": "anthropic", "request_id": result.request_id,
                "stop_reason": result.stop_reason, "stop_details": result.stop_details,
                "settings": {**settings(), "temperature_requested": parsed.temperature,
                             "top_p_requested": parsed.top_p, "max_tokens_requested": parsed.max_tokens,
                             "credentials": getattr(engine, "credentials", None)}}

    return app


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--model", default=DEFAULT_MODEL)
    parser.add_argument("--contract", choices=CONTRACTS, required=True,
                        help="wording: replies carry only a thought; reflection: a thought and a suggestedChoice")
    parser.add_argument("--thinking", choices=THINKING_MODES, default="disabled")
    parser.add_argument("--effort", choices=EFFORTS, default="low")
    parser.add_argument("--port", type=int, default=8081)
    parser.add_argument("--timeout-seconds", type=float, default=20.0)
    parser.add_argument("--max-retries", type=int, default=0)
    parser.add_argument("--max-output-tokens", type=int, default=1_024)
    parser.add_argument("--api-key-file", default=None, help="Read the API key from this file instead of the environment.")
    parser.add_argument("--verify", action="store_true", help="Confirm the model and credentials with one models.retrieve call at startup.")
    args = parser.parse_args()
    if not 1 <= args.port <= 65535:
        parser.error("port must be in 1..65535")
    try:
        config = GeneratorConfig(model=args.model, contract=args.contract, thinking=args.thinking, effort=args.effort,
                                 api_key_file=args.api_key_file, timeout_seconds=args.timeout_seconds,
                                 max_retries=args.max_retries, max_output_tokens=args.max_output_tokens, verify=args.verify)
    except ValueError as exc:
        parser.error(str(exc))
    import uvicorn

    # Host and worker count are deliberately not configurable. Proxy forwarding
    # is disabled so client addresses cannot be replaced by request headers.
    uvicorn.run(create_app(config), host="127.0.0.1", port=args.port, workers=1, proxy_headers=False)


if __name__ == "__main__":
    main()
