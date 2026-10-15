"""Fixed local model launchers for the preview. Never installs packages or downloads weights."""
import json
import os
from pathlib import Path
import socket
import subprocess
import threading
from urllib.error import HTTPError, URLError
from urllib.request import urlopen

SERVICES = {
    'laya': dict(label='Laya', port=8000, model='typed-decisions'),
    'haiku': dict(label='Haiku', port=8083, model='claude-haiku-5-5'),
    'qwen': dict(label='Local Qwen', port=8080, model='Qwen/Qwen3-0.6B'),
}


def compatible(name, health):
    if not isinstance(health, dict):
        return False
    model = SERVICES[name]['model']
    if name == 'laya':
        loaded = health.get('loaded', [])
        return health.get('status') == 'ok' and isinstance(loaded, (list, dict)) and model in loaded
    return (health.get('status') == 'ready' and health.get('loaded') is True
            and health.get('model') == model
            and (name != 'haiku' or (health.get('provider') == 'anthropic'
                                    and health.get('contract') == 'reflection'
                                    and health.get('thinking') == 'disabled')))


def environment_python(root):
    explicit = os.environ.get('UNDER_GLASS_PYTHON')
    if explicit:
        path = Path(explicit).resolve()
        if not path.is_file():
            raise ValueError('UNDER_GLASS_PYTHON must name an installed Python executable.')
        return path
    roots = [root]
    result = subprocess.run(['git', 'rev-parse', '--path-format=absolute', '--git-common-dir'],
                            cwd=root, capture_output=True, text=True)
    if result.returncode == 0:
        roots.append(Path(result.stdout.strip()).parent)
    relative = 'sidecar/.venv/Scripts/python.exe' if os.name == 'nt' else 'sidecar/.venv/bin/python'
    for base in roots:
        path = base / relative
        if path.is_file():
            return path
    raise ValueError('Create sidecar/.venv first, or set UNDER_GLASS_PYTHON before starting the preview.')


def command(root, name, python):
    env = os.environ.copy()
    env.update(PYTHONUNBUFFERED='1', PYTHONIOENCODING='utf-8', HF_HUB_OFFLINE='1', TRANSFORMERS_OFFLINE='1')
    service = SERVICES[name]
    if name == 'laya':
        exe = python.with_name('laya-serve.exe' if os.name == 'nt' else 'laya-serve')
        if not exe.is_file():
            raise ValueError('Laya is not installed in the selected Python environment.')
        env.update(LAYA_HOST='127.0.0.1', LAYA_PORT=str(service['port']),
                   LAYA_MODELS=service['model'], LAYA_PRELOAD='1')
        return [str(exe)], env
    script = 'claude_generator.py' if name == 'haiku' else 'reflection_generator.py'
    args = [str(python), '-u', str(root / 'sim/tools' / script),
            '--port', str(service['port']), '--model', service['model']]
    if name == 'qwen':
        args += ['--revision', 'c1899de289a04d12100db370d81485cdf75e47ca', '--device', 'cuda', '--warmup']
    else:
        args += ['--contract', 'reflection', '--thinking', 'disabled']
        keyfile = os.environ.get('UNDER_GLASS_ANTHROPIC_KEY_FILE')
        if keyfile:
            args += ['--api-key-file', keyfile]
        elif not (env.get('ANTHROPIC_API_KEY') or env.get('ANTHROPIC_AUTH_TOKEN')):
            standard = Path.home() / '.anthropic/api-key.txt'
            if standard.is_file():
                args += ['--api-key-file', str(standard)]
    return args, env


class Services:
    def __init__(self, root, kill):
        self.root, self.kill = Path(root), kill
        self.folder = self.root / 'out/run-studio/services'
        self.folder.mkdir(parents=True, exist_ok=True)
        self.processes, self.errors = {}, {}
        self.lock = threading.RLock()
        self.checking = False
        self.rows = [dict(id=name, **spec, state='checking', owned=False) for name, spec in SERVICES.items()]

    def probe(self, name):
        spec = SERVICES[name]
        with self.lock:
            process = self.processes.get(name)
            owned = process is not None and process.poll() is None
            error = self.errors.get(name)
        row = dict(id=name, **spec, state='offline', owned=owned,
                   url=f"http://127.0.0.1:{spec['port']}")
        try:
            with urlopen(row['url'] + '/health', timeout=1) as response:
                health = json.loads(response.read(64000))
            row['state'] = 'ready' if compatible(name, health) else 'different service'
        except HTTPError:
            row['state'] = 'unavailable'
            row['message'] = 'Health check failed. Check the server log or its own launcher.'
        except (URLError, OSError, ValueError):
            if owned:
                row['state'] = 'starting'
            elif process is not None and process.poll() is not None:
                row['state'] = 'failed'
                row['message'] = 'Process exited. Check its local log.'
        if error:
            row['message'] = error
        row['log'] = '/out/run-studio/services/' + name + '.log' if (self.folder / (name + '.log')).exists() else None
        return row

    def snapshot(self):
        with self.lock:
            if not self.checking:
                self.checking = True
                threading.Thread(target=self.refresh, daemon=True).start()
            return [dict(r) for r in self.rows]

    def refresh(self):
        try:
            rows = [self.probe(name) for name in SERVICES]
            with self.lock:
                self.rows = rows
        finally:
            with self.lock:
                self.checking = False

    def start(self, name):
        if name not in SERVICES:
            raise ValueError('Unknown model service.')
        with self.lock:
            current = self.probe(name)
            if current['state'] == 'ready' or current['owned']:
                return
            port = SERVICES[name]['port']
            try:
                with socket.create_connection(('127.0.0.1', port), timeout=.3):
                    raise ValueError(f'Port {port} is occupied by an unavailable or different service. It was left running.')
            except (ConnectionRefusedError, TimeoutError, OSError):
                pass
            args, env = command(self.root, name, environment_python(self.root))
            with (self.folder / (name + '.log')).open('ab') as log:
                process = subprocess.Popen(args, cwd=self.root, env=env, stdout=log, stderr=subprocess.STDOUT,
                    creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0, start_new_session=os.name != 'nt')
            self.processes[name] = process
            self.errors.pop(name, None)

    def stop(self, name):
        with self.lock:
            process = self.processes.get(name)
            if process is None:
                raise ValueError('This preview did not start that server; leave it running or stop it in its own launcher.')
            self.kill(process)
            self.processes.pop(name, None)
            self.errors.pop(name, None)

    def close(self):
        with self.lock:
            for process in self.processes.values():
                self.kill(process)
            self.processes.clear()
