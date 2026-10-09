"""Offline contracts: no model downloads, imports, or GPU needed for these tests."""

import asyncio
from concurrent.futures import ThreadPoolExecutor
from contextlib import nullcontext
import importlib.util
import json
from pathlib import Path
import sys
import threading
import time
from types import SimpleNamespace
import unittest
from unittest.mock import MagicMock, patch

from fastapi.testclient import TestClient
from starlette.requests import Request


PATH = Path(__file__).resolve().parents[1] / "sim" / "tools" / "reflection_generator.py"
SPEC = importlib.util.spec_from_file_location("reflection_generator", PATH)
generator = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = generator
SPEC.loader.exec_module(generator)


def body(**changes):
    value = {"model": generator.MODEL_ID,
             "messages": [{"role": "system", "content": "Return thought JSON."},
                          {"role": "user", "content": "A remembered kindness."}]}
    value.update(changes)
    return value


class FakeEngine:
    def __init__(self):
        self.calls = []
        self.load_error = None
        self.failure = None
        self.entered = threading.Event()
        self.release = threading.Event()
        self.release.set()
        self.last_stop = None
        self.text = '  {"thought":"Could I help?","suggestedChoice":"o1"}\n'

    def load(self):
        if self.load_error:
            raise self.load_error

    def generate(self, request, stop, deadline):
        self.calls.append(request)
        self.last_stop = stop
        self.entered.set()
        self.release.wait(2)
        if self.failure:
            raise self.failure
        return generator.GenerationOutput(self.text, 23, 12, "stop")


def client(engine=None, config=None, **kwargs):
    return TestClient(generator.create_app(config or generator.GeneratorConfig(), engine or FakeEngine()),
                      base_url="http://127.0.0.1", client=("127.0.0.1", 54321), **kwargs)


class ContractTests(unittest.TestCase):
    def test_health_models_and_raw_completion_receipt(self):
        engine = FakeEngine()
        with client(engine) as api:
            health = api.get("/health")
            self.assertEqual(200, health.status_code)
            self.assertTrue(health.json()["loaded"])
            self.assertFalse(health.json()["warm"])
            self.assertFalse(health.json()["thinking"])
            self.assertTrue(health.json()["cache_only"])
            self.assertEqual(generator.MODEL_ID, api.get("/v1/models").json()["data"][0]["id"])
            response = api.post("/v1/chat/completions", json=body(seed=17, response_format={"type": "json_object"}))
        self.assertEqual(200, response.status_code)
        value = response.json()
        self.assertEqual(engine.text, value["choices"][0]["message"]["content"])
        self.assertEqual(35, value["usage"]["total_tokens"])
        self.assertEqual(23, value["usage"]["input_tokens"])
        self.assertEqual(12, value["usage"]["output_tokens"])
        self.assertGreaterEqual(value["usage"]["elapsed_ms"], 0)
        self.assertEqual(17, value["seed"])
        self.assertEqual("cpu", value["device"])
        self.assertEqual(generator.MODEL_REVISION, value["revision"])
        self.assertEqual((0.7, 0.8, 180), (engine.calls[0].temperature, engine.calls[0].top_p, engine.calls[0].max_tokens))

    def test_load_failure_is_unavailable_and_no_generation(self):
        engine = FakeEngine()
        engine.load_error = OSError("checkpoint is not cached")
        with client(engine) as api:
            self.assertEqual(503, api.get("/health").status_code)
            self.assertIn("not cached", api.get("/health").json()["error"])
            self.assertEqual(503, api.post("/v1/chat/completions", json=body()).status_code)
        self.assertEqual([], engine.calls)

    def test_warmup_is_explicit_and_reported(self):
        engine = FakeEngine()
        with client(engine, generator.GeneratorConfig(warmup=True)) as api:
            self.assertTrue(api.get("/health").json()["warm"])
            self.assertEqual(8, engine.calls[0].max_tokens)

    def test_declared_and_actual_body_limits(self):
        engine = FakeEngine()
        with client(engine, generator.GeneratorConfig(max_body_bytes=256)) as api:
            self.assertEqual(413, api.post("/v1/chat/completions", content=b"x" * 257,
                                         headers={"content-type": "application/json"}).status_code)
            # Exercise actual streamed bytes independently of Content-Length.
            self.assertEqual(413, api.post("/v1/chat/completions", content=iter([b"x" * 128, b"x" * 129]),
                                         headers={"content-type": "application/json"}).status_code)
        self.assertEqual([], engine.calls)

    def test_json_and_content_type_errors(self):
        with client() as api:
            self.assertEqual(415, api.post("/v1/chat/completions", content="{}").status_code)
            self.assertEqual(400, api.post("/v1/chat/completions", content="not json",
                                         headers={"content-type": "application/json"}).status_code)

    def test_oversized_context_is_not_truncated(self):
        engine = FakeEngine()
        with client(engine, generator.GeneratorConfig(max_context_chars=10)) as api:
            self.assertEqual(413, api.post("/v1/chat/completions", json=body()).status_code)
        self.assertEqual([], engine.calls)

    def test_request_cannot_select_external_model_or_generate_unbounded_output(self):
        invalid = [body(model="https://example.com/model"), body(max_tokens=181), body(max_tokens=True),
                   body(stream=True), body(seed=-1), body(seed=True), body(seed=2**63),
                   body(temperature=0), body(temperature="0.7"), body(top_p=1.1), body(tools=[]),
                   body(response_format={"type": "json_schema"}), body(messages=[]),
                   body(messages=[{"role": "user", "content": [{"type": "image_url"}]}]),
                   body(messages=[{"role": "tool", "content": "hello"}])]
        engine = FakeEngine()
        with client(engine) as api:
            for request in invalid:
                with self.subTest(request=request):
                    self.assertEqual(400, api.post("/v1/chat/completions", json=request).status_code)
        self.assertEqual([], engine.calls)

    def test_nonfinite_sampling_parameters_rejected(self):
        for name in ("temperature", "top_p"):
            for value in (float("nan"), float("inf")):
                with self.subTest(name=name, value=value):
                    with self.assertRaises(generator.RequestError):
                        generator.parse_request(body(**{name: value}), generator.GeneratorConfig())

    def test_seed_is_stable_and_explicit_seed_is_preserved(self):
        config = generator.GeneratorConfig()
        request = body()
        a = generator.parse_request(request, config)
        b = generator.parse_request(dict(reversed(list(request.items()))), config)
        changed = generator.parse_request(body(messages=[{"role": "user", "content": "A different memory."}]), config)
        self.assertEqual(a.seed, b.seed)
        self.assertNotEqual(a.seed, changed.seed)
        self.assertEqual(0, generator.parse_request(body(seed=0), config).seed)
        self.assertEqual(2**63 - 1, generator.parse_request(body(seed=2**63 - 1), config).seed)

    def test_busy_request_rejected_without_queue(self):
        engine = FakeEngine()
        engine.release.clear()
        with client(engine) as api, ThreadPoolExecutor(max_workers=1) as pool:
            first = pool.submit(api.post, "/v1/chat/completions", json=body())
            self.assertTrue(engine.entered.wait(1))
            try:
                second = api.post("/v1/chat/completions", json=body())
                self.assertEqual(503, second.status_code)
                self.assertEqual("busy", second.json()["error"]["type"])
                self.assertTrue(api.get("/health").json()["busy"])
                self.assertEqual(1, len(engine.calls))
            finally:
                engine.release.set()
            self.assertEqual(200, first.result().status_code)

    def test_timeout_requests_stop_but_keeps_slot_until_worker_exits(self):
        engine = FakeEngine()
        engine.release.clear()
        with client(engine, generator.GeneratorConfig(timeout_seconds=0.03)) as api:
            try:
                first = api.post("/v1/chat/completions", json=body())
                self.assertEqual(504, first.status_code)
                self.assertTrue(engine.last_stop.is_set())
                self.assertTrue(api.get("/health").json()["busy"])
                self.assertEqual(503, api.post("/v1/chat/completions", json=body()).status_code)
            finally:
                engine.release.set()
            deadline = time.monotonic() + 1
            while api.get("/health").json()["busy"] and time.monotonic() < deadline:
                time.sleep(.005)
            self.assertEqual(200, api.post("/v1/chat/completions", json=body()).status_code)

    def test_engine_failure_releases_slot(self):
        engine = FakeEngine()
        engine.failure = RuntimeError("bad kernel")
        with client(engine) as api:
            self.assertEqual(500, api.post("/v1/chat/completions", json=body()).status_code)
            engine.failure = None
            self.assertEqual(200, api.post("/v1/chat/completions", json=body()).status_code)

    def test_loopback_clients_and_hosts_only(self):
        with TestClient(generator.create_app(generator.GeneratorConfig(), FakeEngine()),
                        base_url="http://127.0.0.1", client=("192.0.2.1", 1234)) as api:
            self.assertEqual(403, api.get("/health").status_code)
        with client() as api:
            self.assertEqual(400, api.get("/health", headers={"host": "example.com"}).status_code)


class LoaderTests(unittest.TestCase):
    def test_cache_only_and_device_precision_are_explicit(self):
        for device, dtype in (("cpu", "fp32"), ("cuda", "fp16")):
            with self.subTest(device=device):
                torch = SimpleNamespace(float32="fp32", float16="fp16", cuda=SimpleNamespace(is_available=lambda: True))
                model_class = MagicMock()
                tokenizer_class = MagicMock()
                transformers = SimpleNamespace(AutoModelForCausalLM=model_class, AutoTokenizer=tokenizer_class)
                with patch.dict(sys.modules, {"torch": torch, "transformers": transformers}):
                    engine = generator.TransformerEngine(generator.GeneratorConfig(device=device))
                    engine.load()
                expected = {"revision": generator.MODEL_REVISION, "local_files_only": True, "trust_remote_code": False}
                tokenizer_class.from_pretrained.assert_called_once_with(generator.MODEL_ID, **expected)
                model_class.from_pretrained.assert_called_once_with(generator.MODEL_ID, dtype=dtype, **expected)
                model_class.from_pretrained.return_value.to.assert_called_once_with(device)
                model_class.from_pretrained.return_value.eval.assert_called_once()

    def test_no_silent_cpu_fallback(self):
        torch = SimpleNamespace(cuda=SimpleNamespace(is_available=lambda: False))
        models = MagicMock()
        with patch.dict(sys.modules, {"torch": torch, "transformers": models}):
            with self.assertRaisesRegex(RuntimeError, "CUDA was requested"):
                generator.TransformerEngine(generator.GeneratorConfig(device="cuda")).load()
        models.AutoModelForCausalLM.from_pretrained.assert_not_called()

    def test_config_rejects_unreviewed_or_unbounded_loading(self):
        for changes in ({"model": "elsewhere"}, {"revision": "main"}, {"device": "auto"},
                        {"max_new_tokens": 181}, {"max_input_tokens": 10_000}, {"timeout_seconds": float("inf")}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                generator.GeneratorConfig(**changes)

    def test_download_is_explicit_pinned_and_safetensors_only(self):
        hub = MagicMock()
        with patch.dict(sys.modules, {"huggingface_hub": hub}):
            generator.download_checkpoint(generator.GeneratorConfig())
        args = hub.snapshot_download.call_args.kwargs
        self.assertEqual(generator.MODEL_ID, args["repo_id"])
        self.assertEqual(generator.MODEL_REVISION, args["revision"])
        self.assertIn("model.safetensors", args["allow_patterns"])
        self.assertFalse(any(".py" in name or ".bin" in name for name in args["allow_patterns"]))

    def test_nonthinking_generation_bounds_tokens_preserves_text_and_forks_rng(self):
        tokenizer = MagicMock()
        tokenizer.apply_chat_template.return_value = "rendered"
        encoded = {"input_ids": SimpleNamespace(shape=(1, 3))}
        encoded_wrapper = MagicMock()
        encoded_wrapper.__getitem__.side_effect = encoded.__getitem__
        encoded_wrapper.to.return_value = encoded
        tokenizer.return_value = encoded_wrapper
        tokenizer.decode.return_value = "  raw output\n"
        tokenizer.eos_token_id = 9
        model = MagicMock()
        model.generate.return_value = [[1, 2, 3, 4, 9]]
        model.generation_config.eos_token_id = 9
        torch = SimpleNamespace(random=SimpleNamespace(fork_rng=MagicMock(side_effect=lambda **kw: nullcontext()),
                                                      default_generator=MagicMock()),
                                inference_mode=lambda: nullcontext())
        engine = generator.TransformerEngine(generator.GeneratorConfig())
        engine.tokenizer, engine.model, engine.torch = tokenizer, model, torch
        transformers = SimpleNamespace(StoppingCriteria=object, StoppingCriteriaList=list)
        parsed = generator.parse_request(body(seed=42, max_tokens=10), generator.GeneratorConfig())
        with patch.dict(sys.modules, {"transformers": transformers}):
            result = engine.generate(parsed, threading.Event(), time.monotonic() + 1)
        self.assertEqual("  raw output\n", result.text)
        self.assertEqual((3, 2, "stop"), (result.prompt_tokens, result.completion_tokens, result.finish_reason))
        self.assertFalse(tokenizer.apply_chat_template.call_args.kwargs["enable_thinking"])
        self.assertEqual(10, model.generate.call_args.kwargs["max_new_tokens"])
        self.assertEqual(20, model.generate.call_args.kwargs["top_k"])
        torch.random.fork_rng.assert_called_once_with(devices=[])
        torch.random.default_generator.manual_seed.assert_called_once_with(42)

    def test_token_limit_fails_before_generation(self):
        engine = generator.TransformerEngine(generator.GeneratorConfig(max_input_tokens=2))
        engine.torch = SimpleNamespace()
        engine.model = MagicMock()
        engine.tokenizer = MagicMock(return_value={"input_ids": SimpleNamespace(shape=(1, 3))})
        with patch.dict(sys.modules, {"transformers": SimpleNamespace(StoppingCriteria=object, StoppingCriteriaList=list)}):
            with self.assertRaisesRegex(generator.RequestError, "no truncation"):
                engine.generate(generator.parse_request(body(), generator.GeneratorConfig()),
                                threading.Event(), time.monotonic() + 1)
        engine.model.generate.assert_not_called()

    def test_cancelled_or_elapsed_request_does_not_enter_model(self):
        engine = generator.TransformerEngine(generator.GeneratorConfig())
        engine.torch, engine.model, engine.tokenizer = SimpleNamespace(), MagicMock(), MagicMock()
        stopped = threading.Event()
        stopped.set()
        with patch.dict(sys.modules, {"transformers": SimpleNamespace(StoppingCriteria=object, StoppingCriteriaList=list)}):
            for stop, deadline in ((stopped, time.monotonic() + 1), (threading.Event(), time.monotonic() - 1)):
                with self.assertRaises(TimeoutError):
                    engine.generate(generator.parse_request(body(), generator.GeneratorConfig()), stop, deadline)
        engine.model.generate.assert_not_called()
        engine.tokenizer.apply_chat_template.assert_not_called()


class CancellationTests(unittest.IsolatedAsyncioTestCase):
    async def run_cancel(self, disconnect):
        engine = FakeEngine()
        engine.release.clear()
        app = generator.create_app(generator.GeneratorConfig(), engine)
        endpoint = next(route.endpoint for route in app.routes if route.path == "/v1/chat/completions")
        queue = asyncio.Queue()
        queue.put_nowait({"type": "http.request", "body": json.dumps(body()).encode(), "more_body": False})
        request = Request({"type": "http", "method": "POST", "path": "/v1/chat/completions",
                           "headers": [(b"content-type", b"application/json")]}, queue.get)
        async with app.router.lifespan_context(app):
            task = asyncio.create_task(endpoint(request))
            self.assertTrue(await asyncio.to_thread(engine.entered.wait, 1))
            try:
                if disconnect:
                    queue.put_nowait({"type": "http.disconnect"})
                    self.assertEqual(499, (await asyncio.wait_for(task, 1)).status_code)
                else:
                    task.cancel()
                    with self.assertRaises(asyncio.CancelledError):
                        await task
                self.assertTrue(engine.last_stop.is_set())
            finally:
                engine.release.set()

    async def test_handler_cancellation_signals_worker(self):
        await self.run_cancel(False)

    async def test_client_disconnect_signals_worker(self):
        await self.run_cancel(True)


if __name__ == "__main__":
    unittest.main()
