# Role: Tests the owner-only launcher with synthetic settings and mocked external processes, never real Keychain or SQL.
import base64, contextlib, importlib.util, io, json, os, subprocess, tempfile, unittest, warnings
from pathlib import Path
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[2]

class PublicDemoTests(unittest.TestCase):
    def setUp(self):
        path=ROOT/'scripts/run-public-demo.py'
        self.assertTrue(path.exists(), 'Owner-only public demo launcher is missing')
        spec=importlib.util.spec_from_file_location('public_demo',path)
        self.tool=importlib.util.module_from_spec(spec);spec.loader.exec_module(self.tool)
        self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
        self.settings={key:'Server=synthetic;Database=synthetic' for key in ('NBJEWEL_SQL_CONNECTION','NBJEWEL_IDENTITY_CONNECTION','AI_PLATFORM_CONNECTION')}
        self.settings['AI_DATA_PROTECTION_SECRET']=base64.b64encode(bytes(range(32))).decode()
        for key in ('ITEM_ASSET_STORAGE_PATH','BUSINESS_STORAGE_PATH','JEWELRY_STORAGE_PATH'):
            path=Path(self.temp.name)/key;path.mkdir();self.settings[key]=str(path)
    def test_serve_whitelists_environment_and_retains_durable_paths(self):
        inherited={'PATH':'/usr/bin','HOME':'/tmp/home','DOTNET_STARTUP_HOOKS':'evil','NBJEWEL_BOOTSTRAP_PASSWORD':'leak','ASPNETCORE_URLS':'http://0.0.0.0:80','GEMINI_API_KEY':'inherited-secret'}
        env=self.tool.settings(self.settings,inherited)
        self.assertEqual('Production',env['ASPNETCORE_ENVIRONMENT']);self.assertEqual('Production',env['DOTNET_ENVIRONMENT'])
        self.assertEqual('http://127.0.0.1:5157',env['ASPNETCORE_URLS']);self.assertEqual('Sql',env['Repository__Provider'])
        self.assertEqual('false',env['SampleIdentity__Enabled']);self.assertEqual('127.0.0.1',env['TRUSTED_PROXIES'])
        self.assertEqual(self.settings['ITEM_ASSET_STORAGE_PATH'],env['ITEM_ASSET_STORAGE_PATH'])
        self.assertEqual('https://nb-jewel.vercel.app',env['APP_PUBLIC_ORIGIN']);self.assertEqual('nb-jewel.vercel.app;localhost;127.0.0.1',env['AllowedHosts'])
        self.assertNotIn('DOTNET_STARTUP_HOOKS',env);self.assertNotIn('NBJEWEL_BOOTSTRAP_PASSWORD',env);self.assertNotIn('GEMINI_API_KEY',env)
    def test_bad_settings_fail_without_secret_values(self):
        for override in ({'Repository__Provider':'Sample'},{'SampleIdentity__Enabled':'true'},{'AI_DATA_PROTECTION_SECRET':'secret-bad'},{'ITEM_ASSET_STORAGE_PATH':'relative'},{'BUSINESS_STORAGE_PATH':self.temp.name+'/missing'},{'ASPNETCORE_URLS':'http://0.0.0.0:90'}):
            with self.subTest(override=next(iter(override))):
                with self.assertRaises(ValueError) as caught:self.tool.settings(self.settings|override,{})
                self.assertNotIn('secret-bad',str(caught.exception))
    def test_optional_provider_keys_are_not_required_and_verification_bypass_is_explicit(self):
        env=self.tool.settings(self.settings,{})
        self.assertEqual('true',env['AiSecurity__RequireEmailVerification'])
        self.assertNotIn('GEMINI_API_KEY',env)
        env=self.tool.settings(self.settings|{'DEMO_DISABLE_EMAIL_VERIFICATION':'true','GEMINI_API_KEY':'synthetic-key'}, {})
        self.assertEqual('false',env['AiSecurity__RequireEmailVerification']);self.assertEqual('synthetic-key',env['GEMINI_API_KEY'])
    def test_keychain_read_uses_no_secret_arguments_and_errors_are_redacted(self):
        with patch.object(self.tool.subprocess,'run',return_value=subprocess.CompletedProcess([],0,json.dumps(self.settings),'secret-stderr')) as run:
            self.assertEqual(self.settings,self.tool.read_keychain())
            self.assertEqual(['/usr/bin/security','find-generic-password','-s','nbjewel-public-demo','-a','configuration','-w'],run.call_args.args[0])
            self.assertNotIn(self.settings['AI_DATA_PROTECTION_SECRET'],str(run.call_args.args))
        with patch.object(self.tool.subprocess,'run',return_value=subprocess.CompletedProcess([],1,'secret-stdout','secret-stderr')):
            with self.assertRaises(ValueError) as caught:self.tool.read_keychain()
            self.assertNotIn('secret',str(caught.exception))
    def test_check_does_not_launch_backend_or_connect_to_sql(self):
        with patch.object(self.tool,'read_keychain',return_value=self.settings),patch.object(self.tool.os,'execvpe') as execute,contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(0,self.tool.main(['check']))
            execute.assert_not_called();self.assertNotIn('Server=',output.getvalue())
    def test_setup_passes_hidden_password_only_to_development_child(self):
        with patch.object(self.tool,'read_keychain',return_value=self.settings),patch.object(self.tool.sys.stdin,'isatty',return_value=True),patch.object(self.tool.getpass,'getpass',side_effect=['SyntheticPassword12!','SyntheticPassword12!']),patch('builtins.input',return_value='owner@example.test'),patch.object(self.tool.os,'execvpe') as execute,patch.object(self.tool.os,'chdir'),contextlib.redirect_stdout(io.StringIO()) as output:
            self.tool.main(['setup-owner'])
            command=execute.call_args.args[1];env=execute.call_args.args[2]
            self.assertIn('--bootstrap-identity',command);self.assertNotIn('SyntheticPassword12!',str(command))
            self.assertEqual('Development',env['DOTNET_ENVIRONMENT']);self.assertEqual('SyntheticPassword12!',env['NBJEWEL_BOOTSTRAP_PASSWORD'])
            self.assertNotIn('SyntheticPassword12!',output.getvalue());self.assertEqual('false',env['PublicDemo__Enabled'])
    def test_reset_requires_exact_confirmation_and_no_child_on_mismatch(self):
        with patch.object(self.tool,'read_keychain',return_value=self.settings),patch.object(self.tool.sys.stdin,'isatty',return_value=True),patch('builtins.input',side_effect=['owner@example.test','wrong']),patch.object(self.tool.os,'execvpe') as execute,contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1,self.tool.main(['reset-owner']));execute.assert_not_called()
    def test_serve_does_not_receive_bootstrap_or_setup_arguments(self):
        with patch.object(self.tool,'read_keychain',return_value=self.settings),patch.object(self.tool.socket,'socket') as socket,patch.object(self.tool.os,'execvpe') as execute,patch.object(self.tool.os,'chdir'),contextlib.redirect_stdout(io.StringIO()):
            socket.return_value.__enter__.return_value.connect_ex.return_value=1
            self.tool.main(['serve'])
            args=execute.call_args.args[1];env=execute.call_args.args[2]
            self.assertIn('--no-launch-profile',args);self.assertNotIn('--bootstrap-identity',args);self.assertNotIn('--setup-db',args)
            self.assertEqual('Production',env['DOTNET_ENVIRONMENT']);self.assertNotIn('NBJEWEL_BOOTSTRAP_PASSWORD',env)
    def test_reset_uses_only_the_explicit_local_reset_command(self):
        with patch.object(self.tool,'read_keychain',return_value=self.settings),patch.object(self.tool.sys.stdin,'isatty',return_value=True),patch('builtins.input',side_effect=['owner@example.test','RESET owner@example.test']),patch.object(self.tool.getpass,'getpass',side_effect=['SyntheticPassword12!','SyntheticPassword12!']),patch.object(self.tool.os,'execvpe') as execute,patch.object(self.tool.os,'chdir'),contextlib.redirect_stdout(io.StringIO()):
            self.tool.main(['reset-owner'])
            args=execute.call_args.args[1];env=execute.call_args.args[2]
            self.assertIn('--reset-owner-password',args);self.assertNotIn('--bootstrap-identity',args);self.assertNotIn('SyntheticPassword12!',str(args))
            self.assertEqual('Development',env['DOTNET_ENVIRONMENT'])
    def test_optional_provider_environment_names_match_runtime_contract(self):
        env=self.tool.settings(self.settings|{'GEMINI_ERP_API_KEY':'synthetic-vision','METAL_RATES_API_KEY':'synthetic-rates'}, {})
        self.assertEqual('synthetic-vision',env['GEMINI_ERP_API_KEY']);self.assertEqual('synthetic-rates',env['METAL_RATES_API_KEY'])
    def test_password_echo_fallback_is_refused(self):
        def fallback(*args):
            warnings.warn('unsafe echo', self.tool.getpass.GetPassWarning)
            self.fail('Password echo fallback was reached')
        with patch.object(self.tool,'read_keychain',return_value=self.settings),patch.object(self.tool.sys.stdin,'isatty',return_value=True),patch('builtins.input',return_value='owner@example.test'),patch.object(self.tool.getpass,'getpass',side_effect=fallback),patch.object(self.tool.os,'execvpe') as execute,contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1,self.tool.main(['setup-owner']));execute.assert_not_called()
    def test_noninteractive_password_input_is_rejected_without_prompt_or_launch(self):
        with patch.object(self.tool,'read_keychain',return_value=self.settings),patch.object(self.tool.sys.stdin,'isatty',return_value=False),patch.object(self.tool.getpass,'getpass') as prompt,patch.object(self.tool.os,'execvpe') as execute,contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1,self.tool.main(['setup-owner']));prompt.assert_not_called();execute.assert_not_called()
if __name__=='__main__':unittest.main()
