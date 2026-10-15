import http.client
import importlib.util
import json
from pathlib import Path
import re
import tempfile
import threading
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('preview_server', ROOT / 'sim/tools/preview_server.py')
server = importlib.util.module_from_spec(spec)
spec.loader.exec_module(server)


def schema():
    return dict(options=[dict(flag=f,type=t,repeat=r,choices=c) for f,t,r,c in [
        ('seed','integer',False,None), ('days','integer',False,None), ('speech','boolean',False,None),
        ('reflection','choice',False,['authored','hybrid','tape']), ('llm-url','url',False,None),
        ('llm-model','text',False,None), ('reflection-tape','path',False,None),
        ('speech-catalog','path',False,None), ('fo','override',True,None),
        ('town','town',False,['pelican']),('out','output',False,None)]],
        knobs={'fo':[dict(name='Steer',type='boolean',value=True),dict(name='SentimentMin',type='number',value=.02)]}, traits=['Kindness'])


class ValidationTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name).resolve()
        (self.root/'catalog.json').write_text('{}')
    def tearDown(self): self.temp.cleanup()
    def args(self, opts): return server.replay_args(self.root,schema(),opts)
    def test_all_cli_flags_have_a_schema_entry(self):
        source=(ROOT/'sim/UnderGlass.Replay/Program.cs').read_text(encoding='utf-8-sig')
        described=(ROOT/'sim/UnderGlass.Replay/ReplayCliSchema.cs').read_text(encoding='utf-8-sig')
        self.assertEqual(set(re.findall(r'case "--([\w-]+)"',source)),set(re.findall(r'new Option\("([\w-]+)"',described)))
    def test_unknown_flags_and_output_injection_refused(self):
        for bad in ({'project':'x'}, {'out':'elsewhere'}, {'days':'1 --inject'}, {'speech':'false'}, {'seed':str(2**63)}):
            with self.assertRaises(server.Invalid): self.args(bad)
    def test_input_paths_confined(self):
        self.assertEqual(self.args({'speech-catalog':'catalog.json'}),['--speech-catalog','catalog.json'])
        for path in ('../secret.json','missing.json',str(self.root.parent/'secret.json')):
            with self.assertRaises(server.Invalid): self.args({'speech-catalog':path})
    def test_local_model_services_only(self):
        for url in ('https://evil.example','http://localhost@evil.example','file:///secret','http://localhost:8080/?secret=x'):
            with self.assertRaises(server.Invalid): self.args({'reflection':'hybrid','llm-url':url,'llm-model':'x'})
        self.assertIn('http://localhost:8080',self.args({'reflection':'hybrid','llm-url':'http://localhost:8080','llm-model':'model with spaces'}))
    def test_overrides_are_typed_and_order_preserved(self):
        self.assertEqual(self.args({'fo':['Steer=false','SentimentMin=0.3']}),['--fo','Steer=false','--fo','SentimentMin=0.3'])
        for row in ('Steer=maybe','Unknown=1','SentimentMin=NaN'):
            with self.assertRaises(server.Invalid): self.args({'fo':[row]})
    def test_tape_requires_file_and_days_are_positive(self):
        for bad in ({'reflection':'tape'}, {'days':'0'}, {'days':'1.2'}):
            with self.assertRaises(server.Invalid): self.args(bad)
    def test_job_cannot_overwrite_a_run_or_launch_two_at_once(self):
        studio=server.Studio(self.root,prepare=False);studio.schema=schema()
        with patch.object(server.threading.Thread,'start'):
            first=studio.start({'name':'../same name','options':{'days':'1'},'formats':['json','html']})
            self.assertTrue(first['outputs']['json'].startswith('/out/run-studio/'))
            with self.assertRaises(server.Invalid): studio.start({'name':'second','options':{}})
            studio.active=None
            second=studio.start({'name':'../same name','options':{'days':'1'}})
            self.assertNotEqual(first['id'],second['id'])
        restored=server.Studio(self.root,prepare=False)
        self.assertEqual(restored.jobs[first['id']]['status'],'interrupted')
    def test_subprocess_receives_an_argument_list_without_a_shell(self):
        studio=server.Studio(self.root,prepare=False);studio.schema=schema()
        with patch.object(server.threading.Thread,'start'):
            job=studio.start({'name':'Test','options':{'days':'1'}})
        with patch.object(server.subprocess,'Popen') as launch:
            launch.return_value.wait.return_value=7
            studio.run(studio.jobs[job['id']])
            self.assertIsInstance(launch.call_args.args[0],list)
            self.assertFalse(launch.call_args.kwargs.get('shell',False))
            self.assertEqual(studio.jobs[job['id']]['status'],'failed')
            self.assertIsNone(studio.active)


class HttpTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        self.studio=server.Studio(self.root,prepare=False);self.studio.schema=schema()
        self.http=server.ThreadingHTTPServer(('127.0.0.1',0),server.Handler);self.http.studio=self.studio
        self.worker=threading.Thread(target=self.http.serve_forever,daemon=True);self.worker.start()
    def tearDown(self):
        self.http.shutdown();self.http.server_close();self.worker.join();self.temp.cleanup()
    def request(self,method,path,headers=None,body=None):
        c=http.client.HTTPConnection('127.0.0.1',self.http.server_port)
        c.request(method,path,body=body,headers=headers or {});r=c.getresponse();result=(r.status,r.read());c.close();return result
    def test_host_origin_and_token_boundaries(self):
        self.assertEqual(self.request('GET','/api/run-studio',{'Host':'evil.example'})[0],403)
        self.assertEqual(self.request('GET','/api/run-studio',{'Origin':'https://evil.example'})[0],403)
        self.assertEqual(self.request('POST','/api/run-studio/jobs',{'Content-Type':'application/json'},'{}')[0],403)
        status,body=self.request('GET','/api/run-studio');self.assertEqual(status,200)
        self.assertEqual(json.loads(body)['token'],self.studio.token)
    def test_upload_is_new_json_copy_and_invalid_json_refused(self):
        headers={'Content-Type':'application/json','X-Run-Studio-Token':self.studio.token}
        status,body=self.request('POST','/api/run-studio/uploads',headers,json.dumps({'name':'../../test','text':'{"schema":1}'}))
        self.assertEqual(status,201);path=self.root/json.loads(body)['path'];self.assertTrue(path.is_file())
        self.assertEqual(self.request('POST','/api/run-studio/uploads',headers,json.dumps({'name':'x','text':'not json'}))[0],400)


if __name__=='__main__': unittest.main()
