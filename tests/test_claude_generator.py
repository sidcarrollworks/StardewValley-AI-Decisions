"""Offline contracts for the Claude generator service: no SDK, credentials or network needed."""

import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from fastapi.testclient import TestClient


PATH = Path(__file__).resolve().parents[1] / "sim" / "tools" / "claude_generator.py"
SPEC = importlib.util.spec_from_file_location("claude_generator", PATH)
generator = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = generator
SPEC.loader.exec_module(generator)


def body(**changes):
    value = {"model": generator.DEFAULT_MODEL,
             "messages": [{"role": "system", "content": "Return thought JSON."},
                          {"role": "user", "content": "A remembered kindness."}],
             "temperature": 0.7, "max_tokens": 120, "seed": 17, "response_format": {"type": "json_object"}}
    value.update(changes)
    return value


class FakeEngine:
    def __init__(self):
        self.calls = []
        self.load_error = None
        self.failure = None
        self.credentials = "fake"
        self.verified_model = None
        self.output = generator.GenerationOutput('{"thought":"Could I help?"}', 23, 12, "stop",
                                                 "claude-haiku-5-5", "req_1", "end_turn", None, 5, 0)

    def load(self):
        if self.load_error:
            raise self.load_error

    def generate(self, request):
        self.calls.append(request)
        if self.failure:
            raise self.failure
        return self.output


def client(engine=None, config=None, **kwargs):
    return TestClient(generator.create_app(config or generator.GeneratorConfig(), engine or FakeEngine()),
                      base_url="http://127.0.0.1", client=("127.0.0.1", 54321), **kwargs)


class FakeMessages:
    def __init__(self, response):
        self.response = response
        self.kwargs = None

    def create(self, **kwargs):
        self.kwargs = kwargs
        if isinstance(self.response, Exception):
            raise self.response
        return self.response


def fake_response(blocks, stop_reason="end_turn", stop_details=None, model="claude-haiku-5-5"):
    return SimpleNamespace(content=blocks, stop_reason=stop_reason, stop_details=stop_details, model=model,
                           usage=SimpleNamespace(input_tokens=40, output_tokens=9, cache_read_input_tokens=None,
                                                 cache_creation_input_tokens=3), _request_id="req_abc")


class ContractTests(unittest.TestCase):
    def test_health_models_and_completion_receipt(self):
        engine = FakeEngine()
        with client(engine) as api:
            health = api.get("/health")
            self.assertEqual(200, health.status_code)
            self.assertEqual("ready", health.json()["status"])
            self.assertEqual("anthropic", health.json()["provider"])
            self.assertFalse(health.json()["sampling_forwarded"])
            self.assertFalse(health.json()["seed_honoured"])
            self.assertEqual(generator.DEFAULT_MODEL, api.get("/v1/models").json()["data"][0]["id"])
            response = api.post("/v1/chat/completions", json=body())
        self.assertEqual(200, response.status_code)
        value = response.json()
        self.assertEqual('{"thought":"Could I help?"}', value["choices"][0]["message"]["content"])
        self.assertEqual("stop", value["choices"][0]["finish_reason"])
        self.assertEqual(generator.DEFAULT_MODEL, value["model"])
        self.assertEqual(17, value["seed"])
        self.assertEqual("claude-haiku-5-5", value["revision"])
        self.assertEqual(generator.DEVICE, value["device"])
        self.assertEqual("req_1", value["request_id"])
        self.assertEqual(35, value["usage"]["total_tokens"])
        self.assertEqual(5, value["usage"]["cache_read_input_tokens"])
        self.assertGreaterEqual(value["usage"]["elapsed_ms"], 0)
        self.assertEqual(0.7, value["settings"]["temperature_requested"])
        self.assertEqual(120, value["settings"]["max_tokens_requested"])
        self.assertEqual("reflection", value["settings"]["contract"])
        self.assertEqual((0.7, 120, 17, True), (engine.calls[0].temperature, engine.calls[0].max_tokens,
                                                engine.calls[0].seed, engine.calls[0].json_object))

    def test_startup_failure_is_unavailable_and_no_generation(self):
        engine = FakeEngine()
        engine.load_error = RuntimeError("no credentials")
        with client(engine) as api:
            self.assertEqual(503, api.get("/health").status_code)
            self.assertIn("no credentials", api.get("/health").json()["error"])
            reply = api.post("/v1/chat/completions", json=body())
            self.assertEqual(503, reply.status_code)
            self.assertEqual("unavailable", reply.json()["error"]["type"])
        self.assertEqual([], engine.calls)

    def test_request_validation_rejects_other_models_and_shapes(self):
        invalid = [body(model="claude-opus-5-5"), body(model="Qwen/Qwen3-0.6B"), body(max_tokens=181), body(max_tokens=True),
                   body(stream=True), body(seed=-1), body(seed=2**63), body(temperature=0), body(temperature="0.7"),
                   body(top_p=1.1), body(tools=[]), body(response_format={"type": "json_schema"}), body(messages=[]),
                   body(messages=[{"role": "user", "content": [{"type": "image_url"}]}]),
                   body(messages=[{"role": "tool", "content": "hello"}]),
                   body(messages=[{"role": "assistant", "content": "first"}, {"role": "user", "content": "then"}])]
        engine = FakeEngine()
        with client(engine) as api:
            for request in invalid:
                with self.subTest(request=request):
                    self.assertEqual(400, api.post("/v1/chat/completions", json=request).status_code)
        self.assertEqual([], engine.calls)

    def test_body_limits_content_type_and_oversized_context(self):
        engine = FakeEngine()
        with client(engine, generator.GeneratorConfig(max_body_bytes=256)) as api:
            self.assertEqual(413, api.post("/v1/chat/completions", content=b"x" * 257,
                                         headers={"content-type": "application/json"}).status_code)
            self.assertEqual(413, api.post("/v1/chat/completions", content=iter([b"x" * 128, b"x" * 129]),
                                         headers={"content-type": "application/json"}).status_code)
            self.assertEqual(415, api.post("/v1/chat/completions", content="{}").status_code)
            self.assertEqual(400, api.post("/v1/chat/completions", content="not json",
                                         headers={"content-type": "application/json"}).status_code)
        with client(engine, generator.GeneratorConfig(max_context_chars=10)) as api:
            self.assertEqual(413, api.post("/v1/chat/completions", json=body()).status_code)
        self.assertEqual([], engine.calls)

    def test_seed_is_echoed_or_derived_and_never_claimed_honoured(self):
        config = generator.GeneratorConfig()
        self.assertEqual(17, generator.parse_request(body(), config).seed)
        request = body()
        del request["seed"]
        a = generator.parse_request(request, config)
        b = generator.parse_request(dict(reversed(list(request.items()))), config)
        changed = generator.parse_request(body(messages=[{"role": "user", "content": "A different memory."}]), config)
        self.assertEqual(a.seed, b.seed)
        self.assertNotEqual(a.seed, changed.seed)
        with client() as api:
            self.assertFalse(api.post("/v1/chat/completions", json=request).json()["settings"]["seed_honoured"])

    def test_upstream_failures_keep_their_kind_and_status(self):
        engine = FakeEngine()
        cases = [(generator.EngineError(429, "rate_limited", "slow down"), 429, "rate_limited"),
                 (generator.EngineError(504, "timeout", "late"), 504, "timeout"),
                 (generator.EngineError(503, "unavailable", "bad key"), 503, "unavailable"),
                 (generator.EngineError(502, "upstream_rejected", "BadRequestError: nope"), 502, "upstream_rejected"),
                 (RuntimeError("boom"), 500, "generation_error")]
        with client(engine) as api:
            for failure, status, kind in cases:
                with self.subTest(kind=kind):
                    engine.failure = failure
                    reply = api.post("/v1/chat/completions", json=body())
                    self.assertEqual(status, reply.status_code)
                    self.assertEqual(kind, reply.json()["error"]["type"])
                    self.assertNotIn("Traceback", reply.text)
            engine.failure = None
            self.assertEqual(200, api.post("/v1/chat/completions", json=body()).status_code)

    def test_non_stop_finish_reasons_are_reported_not_hidden(self):
        engine = FakeEngine()
        engine.output = generator.GenerationOutput("", 40, 9, "refusal", "claude-haiku-5-5", "req_2", "refusal",
                                                   {"type": "refusal", "category": "general_harms"})
        with client(engine) as api:
            value = api.post("/v1/chat/completions", json=body()).json()
        self.assertEqual("refusal", value["choices"][0]["finish_reason"])
        self.assertEqual("general_harms", value["stop_details"]["category"])
        self.assertEqual("", value["choices"][0]["message"]["content"])

    def test_loopback_clients_and_hosts_only(self):
        with TestClient(generator.create_app(generator.GeneratorConfig(), FakeEngine()),
                        base_url="http://127.0.0.1", client=("192.0.2.1", 1234)) as api:
            self.assertEqual(403, api.get("/health").status_code)
        with client() as api:
            self.assertEqual(400, api.get("/health", headers={"host": "example.com"}).status_code)


class EngineTests(unittest.TestCase):
    def engine(self, response, **config):
        messages = FakeMessages(response)
        engine = generator.ClaudeEngine(generator.GeneratorConfig(**config), client=SimpleNamespace(messages=messages))
        engine.load()
        return engine, messages

    def test_request_mapping_drops_sampling_and_seed_and_selects_the_contract_schema(self):
        engine, messages = self.engine(fake_response([SimpleNamespace(type="text", text='{"thought":"Hm."}')]),
                                       contract="wording", thinking="disabled", effort="low", max_output_tokens=512)
        parsed = generator.parse_request(body(), engine.config)
        output = engine.generate(parsed)
        kwargs = messages.kwargs
        self.assertEqual("Return thought JSON.", kwargs["system"])
        self.assertEqual([{"role": "user", "content": "A remembered kindness."}], kwargs["messages"])
        self.assertEqual({"type": "disabled"}, kwargs["thinking"])
        self.assertEqual("low", kwargs["output_config"]["effort"])
        self.assertEqual(generator.SCHEMAS["wording"], kwargs["output_config"]["format"]["schema"])
        self.assertEqual("json_schema", kwargs["output_config"]["format"]["type"])
        self.assertEqual(512, kwargs["max_tokens"])
        self.assertEqual(generator.DEFAULT_MODEL, kwargs["model"])
        for forbidden in ("temperature", "top_p", "top_k", "seed", "stream", "response_format"):
            self.assertNotIn(forbidden, kwargs)
        self.assertEqual(('{"thought":"Hm."}', 40, 9, "stop", "claude-haiku-5-5", "req_abc", "end_turn"),
                         (output.text, output.prompt_tokens, output.completion_tokens, output.finish_reason,
                          output.served_model, output.request_id, output.stop_reason))
        self.assertEqual((0, 3), (output.cache_read_tokens, output.cache_creation_tokens))

    def test_reflection_contract_schema_requires_a_suggested_choice(self):
        engine, messages = self.engine(fake_response([SimpleNamespace(type="text", text="{}")]), contract="reflection",
                                       thinking="adaptive", effort="medium")
        engine.generate(generator.parse_request(body(), engine.config))
        schema = messages.kwargs["output_config"]["format"]["schema"]
        self.assertEqual(["thought", "suggestedChoice"], schema["required"])
        self.assertFalse(schema["additionalProperties"])
        self.assertEqual({"type": "adaptive"}, messages.kwargs["thinking"])

    def test_no_response_format_means_no_output_schema(self):
        engine, messages = self.engine(fake_response([SimpleNamespace(type="text", text="plain")]))
        request = body()
        del request["response_format"]
        engine.generate(generator.parse_request(request, engine.config))
        self.assertNotIn("format", messages.kwargs["output_config"])

    def test_thinking_blocks_are_skipped_and_stop_reasons_mapped(self):
        blocks = [SimpleNamespace(type="thinking", thinking=""), SimpleNamespace(type="text", text="a"),
                  SimpleNamespace(type="text", text="b")]
        engine, _ = self.engine(fake_response(blocks, stop_reason="max_tokens"))
        output = engine.generate(generator.parse_request(body(), engine.config))
        self.assertEqual(("ab", "length", "max_tokens"), (output.text, output.finish_reason, output.stop_reason))
        details = SimpleNamespace(to_dict=lambda: {"type": "refusal", "category": "bio"})
        engine, _ = self.engine(fake_response([], stop_reason="refusal", stop_details=details))
        output = engine.generate(generator.parse_request(body(), engine.config))
        self.assertEqual(("", "refusal", {"type": "refusal", "category": "bio"}), (output.text, output.finish_reason, output.stop_details))

    def test_upstream_exceptions_become_engine_errors_by_name_and_status(self):
        def make(name, status=None):
            cls = type(name, (Exception,), {})
            exc = cls("detail")
            if status is not None:
                exc.status_code = status
            return exc
        expectations = [(make("AuthenticationError", 401), 503, "unavailable"), (make("RateLimitError", 429), 429, "rate_limited"),
                        (make("APITimeoutError"), 504, "timeout"), (make("APIConnectionError"), 502, "upstream_unreachable"),
                        (make("BadRequestError", 400), 502, "upstream_rejected"), (make("InternalServerError", 500), 502, "upstream_error")]
        for exc, status, kind in expectations:
            with self.subTest(kind=kind):
                engine, _ = self.engine(exc)
                with self.assertRaises(generator.EngineError) as caught:
                    engine.generate(generator.parse_request(body(), engine.config))
                self.assertEqual((status, kind), (caught.exception.status, caught.exception.kind))
                self.assertNotIn("sk-ant", str(caught.exception))

    def test_config_rejects_unsupported_models_contracts_and_thinking_combinations(self):
        for changes in ({"model": "gpt-4"}, {"model": "Qwen/Qwen3-0.6B"}, {"contract": "chat"}, {"thinking": "enabled"},
                        {"effort": "ultra"}, {"thinking": "disabled", "effort": "max"}, {"max_output_tokens": 0},
                        {"timeout_seconds": float("inf")}, {"max_retries": 6}, {"api_key_file": ""}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                generator.GeneratorConfig(**changes)
        generator.GeneratorConfig(thinking="adaptive", effort="max")

    def test_credentials_resolution_prefers_explicit_file_and_never_exposes_the_key(self):
        with tempfile.TemporaryDirectory() as folder:
            path = os.path.join(folder, "key.txt")
            with open(path, "w", encoding="utf-8") as handle:
                handle.write("sk-ant-test-value\n")
            key, source = generator.resolve_credentials(generator.GeneratorConfig(api_key_file=path))
            self.assertEqual(("sk-ant-test-value", "file"), (key, source))
            with open(path, "w", encoding="utf-8") as handle:
                handle.write("   \n")
            with self.assertRaises(RuntimeError):
                generator.resolve_credentials(generator.GeneratorConfig(api_key_file=path))
        with patch.dict(os.environ, {"ANTHROPIC_API_KEY": "sk-ant-env"}, clear=False):
            self.assertEqual((None, "env"), generator.resolve_credentials(generator.GeneratorConfig()))
        with patch.dict(os.environ, {}, clear=True):
            self.assertEqual((None, "sdk-default"), generator.resolve_credentials(generator.GeneratorConfig()))
        engine = FakeEngine()
        with client(engine) as api:
            text = api.get("/health").text + api.post("/v1/chat/completions", json=body()).text
        self.assertNotIn("sk-ant", text)

    def test_load_without_the_sdk_reports_an_explicit_error(self):
        engine = generator.ClaudeEngine(generator.GeneratorConfig())
        with patch.dict(sys.modules, {"anthropic": None}):
            with self.assertRaisesRegex(RuntimeError, "anthropic package is not installed"):
                engine.load()


if __name__ == "__main__":
    unittest.main()
