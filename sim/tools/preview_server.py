"""Loopback preview and single-job replay service. No shell commands or arbitrary executables."""
import argparse
import json
import math
import os
from pathlib import Path
import re
import secrets
import signal
import subprocess
import threading
import time
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit, unquote
from studio_services import Services


class Invalid(ValueError):
    pass


def local_file(root, value):
    if not isinstance(value, str) or not value or '\x00' in value:
        raise Invalid('Choose a JSON file in this checkout.')
    path = (root / value).resolve()
    if not path.is_relative_to(root) or path.suffix.lower() != '.json' or not path.is_file():
        raise Invalid('Input files must be existing JSON files inside this checkout.')
    return path.relative_to(root).as_posix()


def number(value, integer=False):
    if isinstance(value, bool):
        raise Invalid('Expected a number.')
    try:
        if integer:
            if not re.fullmatch(r'-?\d+', str(value)):
                raise ValueError()
            n = int(value)
        else:
            n = float(value)
            if not math.isfinite(n):
                raise ValueError()
    except (ValueError, TypeError, OverflowError):
        raise Invalid('Expected a finite number.')
    return str(n)


def replay_args(root, schema, supplied):
    """Accept only described Replay arguments, in a stable order; paths stay inside root."""
    if not isinstance(supplied, dict):
        raise Invalid('Options must be an object.')
    spec = {o['flag']: o for o in schema['options'] if o['type'] != 'output'}
    if supplied.keys() - spec.keys():
        raise Invalid('Unknown option: ' + ', '.join(sorted(supplied.keys() - spec.keys())))
    result = []
    for key, o in spec.items():
        value = supplied.get(key)
        if value is None or value == '' or value == []:
            continue
        kind = o['type']
        if kind == 'boolean':
            if not isinstance(value, bool):
                raise Invalid(key + ' must be true or false.')
            if value:
                result.append('--' + key)
            continue
        values = value if o['repeat'] else [value]
        if not isinstance(values, list) or len(values) > 500:
            raise Invalid(key + ' must be a list of overrides.')
        for v in values:
            if not isinstance(v, (str, int, float)) or isinstance(v, bool) or len(str(v)) > 4096:
                raise Invalid('Invalid ' + key)
            v = str(v)
            if kind in ('integer', 'number'):
                v = number(v, kind == 'integer')
                if key == 'seed' and not -(2**63) <= int(v) < 2**63:
                    raise Invalid('Seed must fit a signed 64-bit integer.')
                if key == 'days' and not 1 <= int(v) <= 36500:
                    raise Invalid('Days must be between 1 and 36,500.')
                if key in ('reflection-chance', 'dream-chance') and not 0 <= float(v) <= 1:
                    raise Invalid(key + ' must be between 0 and 1.')
            elif kind == 'choice' and v not in o['choices']:
                raise Invalid('Unknown ' + key)
            elif kind == 'path':
                v = local_file(root, v)
            elif kind == 'town':
                if v.startswith('file:'):
                    v = 'file:' + local_file(root, v[5:])
                elif v not in o['choices'] + ['pelican26'] and not re.fullmatch(r'pelican:\d+@-?\d+', v):
                    raise Invalid('Choose a named town, pelican:60@7, or file:path.json.')
            elif kind == 'url':
                u = urlsplit(v)
                if u.scheme != 'http' or u.hostname not in ('127.0.0.1', 'localhost', '::1') or u.username or u.password or u.query or u.fragment:
                    raise Invalid('Model services must use a local HTTP address without credentials.')
            elif kind == 'catalog':
                names = [x.strip().lower() for x in v.split(',') if x.strip()]
                if set(names) - {x.lower() for x in o['choices']}:
                    raise Invalid('Unknown action catalog slice.')
            elif kind == 'steps' and set(v) - set(o['choices']):
                raise Invalid('Unknown social dynamics step.')
            elif kind == 'override':
                name, sep, val = v.partition('=')
                knob = next((k for k in schema['knobs'][key] if k['name'] == name), None)
                if not sep or knob is None:
                    raise Invalid('Unknown override: ' + v)
                if knob['type'] == 'boolean':
                    if val.lower() not in ('true', 'false'):
                        raise Invalid(name + ' needs true or false.')
                else:
                    val = number(val, knob['type'] == 'integer')
                    if knob['type'] == 'integer' and not -(2**31) <= int(val) < 2**31:
                        raise Invalid(name + ' is outside the integer range.')
                v = name + '=' + val
            elif kind == 'trait':
                m = re.fullmatch(r'([^=]+)=([^:]+):(.+)', v)
                if not m or m[2].lower() not in [t.lower() for t in schema['traits']]:
                    raise Invalid('Use Character=Trait:value.')
                v = m[1] + '=' + m[2] + ':' + number(m[3])
            result.extend(['--' + key, v])
    reflection = supplied.get('reflection')
    if not reflection and any(supplied.get(k) for k in ('character-inspiration', 'laya-url', 'llm-url', 'llm-model', 'reflection-tape', 'dreams', 'dream-chance')):
        raise Invalid('Choose a reflection mode before configuring models, background or dreams.')
    if reflection == 'hybrid' and not all(supplied.get(k) for k in ('llm-url', 'llm-model')):
        raise Invalid('Hybrid needs an inspiration service address and model name.')
    if reflection == 'tape' and not supplied.get('reflection-tape'):
        raise Invalid('Tape mode needs a previous JSON recording.')
    return result


def kill_process(process):
    if process and process.poll() is None:
        if os.name == 'nt':
            subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
        else:
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass


class Studio:
    def __init__(self, root, prepare=True):
        self.root = Path(root).resolve()
        self.store = self.root / 'out/run-studio'
        self.store.mkdir(parents=True, exist_ok=True)
        self.token = secrets.token_urlsafe(32)
        self.lock = threading.RLock()
        self.schema = None
        self.error = None
        self.process = None
        self.active = None
        self.jobs = {}
        self.services = Services(self.root, kill_process) if prepare else None
        self.dll = self.root / 'sim/UnderGlass.Replay/bin/Release/net8.0/UnderGlass.Replay.dll'
        for path in self.store.glob('*/job.json'):
            try:
                job = json.loads(path.read_text(encoding='utf-8'))
                if job['id'] != path.parent.name:
                    continue
                if job['status'] in ('running', 'cancelling'):
                    job['status'] = 'interrupted'
                    job['finished'] = time.time()
                    path.write_text(json.dumps(job, indent=2), encoding='utf-8')
                self.jobs[job['id']] = job
            except (ValueError, KeyError):
                continue
        if prepare:
            threading.Thread(target=self.prepare, daemon=True).start()

    def prepare(self):
        try:
            with (self.store / 'build.log').open('w', encoding='utf-8') as log:
                p = subprocess.run(['dotnet', 'build', 'sim/UnderGlass.Replay', '-c', 'Release', '--nologo'], cwd=self.root, stdout=log, stderr=subprocess.STDOUT)
            if p.returncode:
                raise Invalid('Replay build failed. See out/run-studio/build.log.')
            result = subprocess.run(['dotnet', str(self.dll), '--describe-options'], cwd=self.root, capture_output=True, text=True, encoding='utf-8', check=True)
            self.schema = json.loads(result.stdout)
        except Exception as e:
            self.error = str(e)

    def save(self, job):
        folder = self.store / job['id']
        folder.mkdir(exist_ok=True)
        (folder / 'job.json').write_text(json.dumps(job, indent=2), encoding='utf-8')

    def start(self, payload):
        if not self.schema:
            raise Invalid(self.error or 'Preparing the replay engine. Try again shortly.')
        if not isinstance(payload.get('options', {}), dict):
            raise Invalid('Options must be an object.')
        options = dict(payload.get('options', {}))
        args = replay_args(self.root, self.schema, options)
        live = payload.get('live', False)
        if not isinstance(live, bool):
            raise Invalid('Live must be true or false.')
        rate = self.rate(payload.get('rate', 60))
        formats = payload.get('formats', ['json'])
        if not isinstance(formats, list) or not formats or set(formats) - {'json', 'html'}:
            raise Invalid('Choose JSON and/or standalone HTML.')
        name = payload.get('name', 'Untitled run')
        if not isinstance(name, str) or not name.strip() or len(name) > 120:
            raise Invalid('Name the run in 1–120 characters.')
        with self.lock:
            if self.active:
                raise Invalid('A run is already active. Finish or cancel it first.')
            identity = time.strftime('%Y%m%d-%H%M%S') + '-' + secrets.token_hex(4)
            folder = self.store / identity
            folder.mkdir()
            # Freeze every file input, not just histories, before starting the worker.
            for option in self.schema['options']:
                flag = option['flag']
                value = options.get(flag)
                if not value or not (option['type'] == 'path' or flag == 'town' and str(value).startswith('file:')):
                    continue
                source = local_file(self.root, str(value).removeprefix('file:'))
                target = folder / (flag + '.json')
                target.write_bytes((self.root / source).read_bytes())
                if flag == 'history':
                    self.validate_history(target)
                options[flag] = ('file:' if flag == 'town' else '') + target.relative_to(self.root).as_posix()
            args = replay_args(self.root, self.schema, options)
            live_path = f'out/run-studio/{identity}/live.json' if live else None
            if live:
                args.extend(['--live-output', live_path, '--live-control', str(folder / 'control.json')])
                (folder / 'control.json').write_text(json.dumps(dict(paused=False, rate=rate)), encoding='utf-8')
            outputs = {}
            for fmt in dict.fromkeys(formats):
                path = f'out/run-studio/{identity}/run.{fmt}'
                args.extend(['--out' if fmt == 'json' else '--html', path])
                outputs[fmt] = '/' + path
            job = dict(id=identity, name=name.strip(), status='running', started=time.time(), finished=None,
                       options=options, formats=formats, args=args, outputs=outputs, live=('/' + live_path) if live else None, rate=rate)
            self.jobs[identity] = job
            self.active = identity
            self.save(job)
            threading.Thread(target=self.run, args=(job,), daemon=True).start()
            return dict(job)

    def run(self, job):
        try:
            with (self.store / job['id'] / 'run.log').open('w', encoding='utf-8') as log:
                with self.lock:
                    if job['status'] == 'cancelling':
                        job['status'] = 'cancelled'
                        return
                    self.process = subprocess.Popen(['dotnet', str(self.dll), *job['args']], cwd=self.root, stdout=log, stderr=subprocess.STDOUT,
                        creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0, start_new_session=os.name != 'nt')
                    process = self.process
                code = process.wait()
                with self.lock:
                    job['status'] = 'cancelled' if job['status'] == 'cancelling' else 'succeeded' if code == 0 else 'failed'
                    job['exitCode'] = code
        except Exception as e:
            job['status'], job['error'] = 'failed', str(e)
        finally:
            with self.lock:
                job['finished'] = time.time()
                self.save(job)
                self.active = None
                self.process = None

    def cancel(self, identity):
        with self.lock:
            if identity != self.active:
                raise Invalid('That run is no longer active.')
            self.jobs[identity]['status'] = 'cancelling'
            self.save(self.jobs[identity])
            process = self.process
        kill_process(process)

    @staticmethod
    def rate(value):
        if isinstance(value, bool) or not isinstance(value, (int, float)) or not (value == 0 or 1 <= value <= 1440):
            raise Invalid('Pace must be 0 (unlimited), or 1–1440 simulated minutes per second.')
        return value

    def validate_history(self, path):
        result = subprocess.run(['dotnet', str(self.dll), '--validate-history', str(path)], cwd=self.root,
                                capture_output=True, text=True, encoding='utf-8', timeout=30)
        if result.returncode:
            raise Invalid(result.stderr.strip()[-2000:] or 'Invalid history pack.')

    def control(self, identity, data):
        with self.lock:
            if identity != self.active or not self.jobs[identity].get('live'):
                raise Invalid('This live run is not active.')
            if not isinstance(data.get('paused'), bool):
                raise Invalid('Paused must be true or false.')
            value = dict(paused=data['paused'], rate=self.rate(data.get('rate', 60)))
            target = self.store / identity / 'control.json'
            temp = target.with_suffix('.tmp')
            temp.write_text(json.dumps(value), encoding='utf-8')
            temp.replace(target)

    def live_status(self, identity):
        with self.lock:
            job = dict(self.jobs[identity])
        path = self.store / identity / 'live.json.status.json'
        state = json.loads(path.read_text(encoding='utf-8')) if path.exists() else dict(phase='starting')
        return dict(job=job, state=state)

    def snapshot(self):
        with self.lock:
            return dict(ready=self.schema is not None, error=self.error, token=self.token, schema=self.schema, checkout=str(self.root),
                        services=self.services.snapshot() if self.services else [],
                        jobs=[dict(j) for j in sorted(self.jobs.values(), key=lambda j:j['started'], reverse=True)])


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(args[2].studio.root), **kwargs)

    def trusted(self):
        port = self.server.server_port
        hosts = {f'127.0.0.1:{port}', f'localhost:{port}'}
        host = self.headers.get('Host', '')
        origin = self.headers.get('Origin')
        return host in hosts and (origin is None or origin == 'http://' + host)

    def reply(self, data, status=200):
        encoded = json.dumps(data).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('Content-Length', str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)

    def do_GET(self):
        if not self.trusted():
            return self.reply({'error':'Use this preview directly on localhost.'}, 403)
        path = urlsplit(self.path).path
        if path == '/api/run-studio':
            return self.reply(self.server.studio.snapshot())
        if re.fullmatch(r'/api/run-studio/jobs/[\w-]+/live', path):
            identity = path.split('/')[-2]
            if identity not in self.server.studio.jobs:
                return self.reply({'error':'Unknown run.'}, 404)
            return self.reply(self.server.studio.live_status(identity))
        if re.fullmatch(r'/api/run-studio/jobs/[\w-]+/log', path):
            identity = path.split('/')[-2]
            if identity not in self.server.studio.jobs:
                return self.reply({'error':'Unknown run.'}, 404)
            log = self.server.studio.store / identity / 'run.log'
            data = b''
            if log.exists():
                with log.open('rb') as f:
                    f.seek(max(0, log.stat().st_size - 32000))
                    data = f.read()
            return self.reply({'text':data.decode('utf-8', errors='replace')})
        if path.startswith('/api/'):
            return self.reply({'error':'Unknown endpoint.'}, 404)
        resolved = Path(self.translate_path(path)).resolve()
        if not resolved.is_relative_to(self.server.studio.root) or any(p.startswith('.') for p in Path(unquote(path)).parts if p not in ('/', '')):
            return self.reply({'error':'Path unavailable.'}, 403)
        return super().do_GET()

    def do_POST(self):
        studio = self.server.studio
        if not self.trusted() or not secrets.compare_digest(self.headers.get('X-Run-Studio-Token', ''), studio.token):
            return self.reply({'error':'Reload this local page before submitting.'}, 403)
        if self.headers.get_content_type() != 'application/json':
            return self.reply({'error':'Use JSON.'}, 415)
        try:
            length = int(self.headers.get('Content-Length', '0'))
            if not 0 < length <= 32 * 1024 * 1024:
                raise Invalid('Request must be between 1 byte and 32 MB.')
            data = json.loads(self.rfile.read(length))
            if not isinstance(data, dict):
                raise Invalid('Expected a JSON object.')
            path = urlsplit(self.path).path
            if path == '/api/run-studio/services':
                if not studio.services:
                    raise Invalid('Service management is unavailable.')
                if data.get('action') == 'start':
                    names = data.get('services')
                    if not isinstance(names, list) or not names or any(n not in ('laya','haiku','qwen') for n in names):
                        raise Invalid('Choose Laya, Haiku or Qwen.')
                    for name in dict.fromkeys(names):
                        studio.services.start(name)
                elif data.get('action') == 'stop':
                    with studio.lock:
                        if studio.active:
                            raise Invalid('Finish or cancel the active run before stopping model servers.')
                        studio.services.stop(data.get('service'))
                else:
                    raise Invalid('Choose start or stop.')
                return self.reply({'ok':True})
            if re.fullmatch(r'/api/run-studio/jobs/[\w-]+/control', path):
                studio.control(path.split('/')[-2], data)
                return self.reply({'ok':True})
            if path == '/api/run-studio/history':
                if not studio.schema:
                    raise Invalid('Wait for the engine to finish preparing.')
                text = data.get('text')
                if not isinstance(text, str) or len(text) > 8 * 1024 * 1024:
                    raise Invalid('Expected history JSON, at most 8 MiB.')
                folder = studio.store / 'inputs'
                folder.mkdir(exist_ok=True)
                target = folder / ('history-' + secrets.token_hex(8) + '.json')
                target.write_text(text, encoding='utf-8')
                try:
                    studio.validate_history(target)
                except Exception:
                    target.unlink(missing_ok=True)
                    raise
                return self.reply({'path':target.relative_to(studio.root).as_posix()}, 201)
            if path == '/api/run-studio/jobs':
                return self.reply(studio.start(data), 201)
            if re.fullmatch(r'/api/run-studio/jobs/[\w-]+/cancel', path):
                studio.cancel(path.split('/')[-2])
                return self.reply({'ok':True})
            if path == '/api/run-studio/uploads':
                text = data.get('text')
                if not isinstance(text, str):
                    raise Invalid('Choose a JSON file.')
                json.loads(text)
                folder = studio.store / 'inputs'
                folder.mkdir(exist_ok=True)
                name = re.sub(r'[^a-zA-Z0-9_-]', '-', str(data.get('name', 'input'))[:60])
                target = folder / (name + '-' + secrets.token_hex(4) + '.json')
                target.write_text(text, encoding='utf-8')
                return self.reply({'path':target.relative_to(studio.root).as_posix()}, 201)
            if path == '/api/run-studio/shutdown':
                if studio.active:
                    studio.cancel(studio.active)
                threading.Thread(target=self.server.shutdown, daemon=True).start()
                return self.reply({'ok':True})
            return self.reply({'error':'Unknown endpoint.'}, 404)
        except (ValueError, KeyError, TypeError) as e:
            return self.reply({'error':str(e)}, 400)
        except OSError as e:
            return self.reply({'error':str(e)}, 500)


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--port', type=int, default=8766)
    p.add_argument('--directory', type=Path, default=Path(__file__).resolve().parents[2])
    args = p.parse_args()
    studio = Studio(args.directory)
    server = ThreadingHTTPServer(('127.0.0.1', args.port), Handler)
    server.studio = studio
    try:
        server.serve_forever()
    finally:
        if studio.active:
            studio.cancel(studio.active)
        studio.services.close()
        server.server_close()


if __name__ == '__main__':
    main()
