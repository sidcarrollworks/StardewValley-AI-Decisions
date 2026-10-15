import importlib.util
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('studio_services', ROOT/'sim/tools/studio_services.py')
services = importlib.util.module_from_spec(spec)
spec.loader.exec_module(services)


class ServiceTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
        self.kill=Mock();self.s=services.Services(self.root,self.kill)
    def tearDown(self): self.temp.cleanup()
    def test_health_must_match_model_and_contract(self):
        self.assertTrue(services.compatible('laya',dict(status='ok',loaded=['typed-decisions'])))
        self.assertFalse(services.compatible('laya',dict(status='ok',loaded=None)))
        health=dict(status='ready',loaded=True,model='claude-haiku-5-5',provider='anthropic',contract='reflection',thinking='disabled')
        self.assertTrue(services.compatible('haiku',health))
        for key,value in [('model','other'),('contract','wording'),('thinking','adaptive'),('loaded',False)]:
            self.assertFalse(services.compatible('haiku',{**health,key:value}))
    def test_ready_external_service_is_reused_and_cannot_be_stopped(self):
        with patch.object(self.s,'probe',return_value=dict(state='ready',owned=False)),patch.object(services.subprocess,'Popen') as launch:
            self.s.start('haiku');launch.assert_not_called()
        with self.assertRaises(ValueError):self.s.stop('haiku')
        self.kill.assert_not_called()
    def test_occupied_wrong_service_is_left_alone(self):
        with patch.object(self.s,'probe',return_value=dict(state='different service',owned=False)),patch.object(services.socket,'create_connection'),patch.object(services.subprocess,'Popen') as launch:
            with self.assertRaisesRegex(ValueError,'occupied'):self.s.start('haiku')
            launch.assert_not_called()
    def test_only_fixed_names_are_launchable(self):
        for name in ('../../anything','python','shell'):
            with self.assertRaises(ValueError):self.s.start(name)
    def test_managed_launch_has_no_shell_and_stop_only_kills_owned_process(self):
        with patch.object(self.s,'probe',return_value=dict(state='offline',owned=False)),patch.object(services.socket,'create_connection',side_effect=ConnectionRefusedError),patch.object(services,'environment_python',return_value=self.root/'python'),patch.object(services,'command',return_value=(['python','fixed-script'],{})),patch.object(services.subprocess,'Popen') as launch:
            self.s.start('haiku');process=launch.return_value
            self.assertEqual(launch.call_args.args[0],['python','fixed-script']);self.assertFalse(launch.call_args.kwargs.get('shell',False))
            self.s.stop('haiku');self.kill.assert_called_once_with(process)
    def test_qwen_uses_cached_pinned_weights_without_installing(self):
        args,env=services.command(self.root,'qwen',self.root/'python')
        self.assertIn('--warmup',args);self.assertIn('c1899de289a04d12100db370d81485cdf75e47ca',args)
        self.assertEqual(env['HF_HUB_OFFLINE'],'1');self.assertNotIn('--download-only',args)
    def test_key_file_is_a_process_argument_never_a_health_snapshot(self):
        with patch.dict(os.environ,{'UNDER_GLASS_ANTHROPIC_KEY_FILE':'test-key-path'}):
            args,_=services.command(self.root,'haiku',self.root/'python')
        self.assertIn('test-key-path',args);self.assertNotIn('--verify',args)
        self.assertNotIn('test-key-path',str(self.s.rows))
    def test_close_only_stops_managed_children(self):
        a,b=Mock(),Mock();self.s.processes={'laya':a,'haiku':b};self.s.close()
        self.assertEqual(self.kill.call_count,2);self.assertEqual(self.s.processes,{})

if __name__=='__main__':unittest.main()
