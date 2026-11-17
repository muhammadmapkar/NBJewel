#!/usr/bin/env python3
# Role: Owner-invoked Keychain launcher for the SQL public demo and local-only fixed owner credentials; never loads .env.
import argparse
import base64
import getpass
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import warnings

ROOT = Path(__file__).resolve().parent.parent
CONNECTIONS = ('NBJEWEL_SQL_CONNECTION', 'NBJEWEL_IDENTITY_CONNECTION', 'AI_PLATFORM_CONNECTION')
STORAGE = ('ITEM_ASSET_STORAGE_PATH', 'BUSINESS_STORAGE_PATH', 'JEWELRY_STORAGE_PATH')
OPTIONAL = ('CUTOUT_PYTHON_PATH', 'GEMINI_API_KEY', 'GEMINI_MODEL', 'GEMINI_THINKING_LEVEL',
            'GEMINI_ERP_API_KEY', 'GEMINI_ERP_MODEL', 'METAL_RATES_API_KEY',
            'SMTP_HOST', 'SMTP_FROM', 'SMTP_PORT', 'SMTP_USERNAME', 'SMTP_PASSWORD')
FIXED = {'Repository__Provider': 'Sql', 'SampleIdentity__Enabled': 'false',
         'PublicDemo__Enabled': 'true', 'ASPNETCORE_ENVIRONMENT': 'Production',
         'DOTNET_ENVIRONMENT': 'Production', 'ASPNETCORE_URLS': 'http://127.0.0.1:5157',
         'TRUSTED_PROXIES': '127.0.0.1', 'APP_PUBLIC_ORIGIN': 'https://nb-jewel.vercel.app',
         'CORS_ORIGINS': 'https://nb-jewel.vercel.app', 'AllowedHosts': 'nb-jewel.vercel.app;localhost;127.0.0.1'}

def settings(private, inherited):
    if not isinstance(private, dict) or any(not isinstance(k, str) or not isinstance(v, str) for k,v in private.items()):
        raise ValueError('Keychain configuration must be a JSON object of string settings.')
    allowed = set(CONNECTIONS + STORAGE + OPTIONAL) | {'AI_DATA_PROTECTION_SECRET', 'DEMO_DISABLE_EMAIL_VERIFICATION'} | set(FIXED)
    if set(private) - allowed:
        raise ValueError('Keychain configuration contains unsupported setting names.')
    if any(key in private and private[key] != value for key,value in FIXED.items()):
        raise ValueError('Public demo runtime settings cannot be overridden.')
    if any(not private.get(key, '').strip() for key in CONNECTIONS):
        raise ValueError('All three backend SQL connections are required.')
    try:
        if len(base64.b64decode(private.get('AI_DATA_PROTECTION_SECRET', ''), validate=True)) != 32:
            raise ValueError()
    except (ValueError, TypeError):
        raise ValueError('Stable AI_DATA_PROTECTION_SECRET must encode exactly 32 bytes.') from None
    for key in STORAGE:
        value = private.get(key, '')
        if not value or not Path(value).is_absolute() or not Path(value).is_dir():
            raise ValueError('Every durable storage path must be an existing absolute directory.')
    bypass = private.get('DEMO_DISABLE_EMAIL_VERIFICATION', 'false')
    if bypass not in ('true', 'false'):
        raise ValueError('DEMO_DISABLE_EMAIL_VERIFICATION must be true or false.')
    # Do not propagate ambient connection strings, startup hooks, bootstrap passwords or hosting overrides.
    env = {k:v for k,v in inherited.items() if k in ('PATH', 'HOME', 'TMPDIR', 'LANG', 'LC_ALL')}
    env.update({k:v for k,v in private.items() if k in CONNECTIONS + STORAGE + OPTIONAL + ('AI_DATA_PROTECTION_SECRET',)})
    env.update(FIXED)
    env['AiSecurity__RequireEmailVerification'] = 'false' if bypass == 'true' else 'true'
    env['Logging__LogLevel__Default'] = 'Warning'
    env['Logging__LogLevel__Microsoft.EntityFrameworkCore'] = 'None'
    return env

def read_keychain():
    # Owner creates this generic-password entry in Keychain Access UI. No password appears in argv.
    try:
        result = subprocess.run(['/usr/bin/security', 'find-generic-password', '-s', 'nbjewel-public-demo',
                                 '-a', 'configuration', '-w'], capture_output=True, text=True, check=False)
        if result.returncode:
            raise ValueError('Cannot read the configured Keychain entry. Unlock or create it in Keychain Access.')
        return json.loads(result.stdout)
    except (OSError, json.JSONDecodeError):
        raise ValueError('Keychain configuration is unavailable or invalid JSON.') from None

def main(argv=None):
    parser = argparse.ArgumentParser(description='Owner-only public demo CLI. No migrations, seeds, tunnel or deployment.')
    parser.add_argument('command', choices=('check', 'serve', 'setup-owner', 'reset-owner'))
    args = parser.parse_args(argv)
    try:
        env = settings(read_keychain(), os.environ)
        if args.command == 'check':
            print('Configuration and existing storage paths validated. SQL connectivity and schema are not checked.')
            return 0
        command = ['dotnet', 'run', '--project', str(ROOT/'backend/NbJewel.Api'), '--no-launch-profile']
        if args.command in ('setup-owner', 'reset-owner'):
            if not sys.stdin.isatty():
                raise ValueError('Owner credential commands require an interactive terminal.')
            email = input('Owner email: ').strip()
            if not email or '@' not in email:
                raise ValueError('Enter an owner email.')
            if args.command == 'reset-owner' and input('Type RESET ' + email + ' to replace this existing owner password: ') != 'RESET ' + email:
                raise ValueError('Password reset confirmation did not match. No account changed.')
            with warnings.catch_warnings():
                warnings.simplefilter('error', getpass.GetPassWarning)
                password = getpass.getpass('New owner password (at least 12 characters): ')
                confirmation = getpass.getpass('Repeat password: ')
            if password != confirmation or len(password) < 12:
                raise ValueError('Passwords must match and contain at least 12 characters.')
            env.update(ASPNETCORE_ENVIRONMENT='Development', DOTNET_ENVIRONMENT='Development',
                       PublicDemo__Enabled='false', NBJEWEL_BOOTSTRAP_EMAIL=email, NBJEWEL_BOOTSTRAP_PASSWORD=password)
            command += ['--', '--reset-owner-password' if args.command == 'reset-owner' else '--bootstrap-identity']
        else:
            with socket.socket() as probe:
                if probe.connect_ex(('127.0.0.1', 5157)) == 0:
                    raise ValueError('Port 5157 is already in use. No existing service was stopped.')
        os.chdir(ROOT)
        print('Starting requested owner command. SQL schema and existing data are preserved.', flush=True)
        os.execvpe('dotnet', command, env)
        return 0
    except (ValueError, OSError, getpass.GetPassWarning):
        # Fixed output also redacts filesystem/process errors that could include private values.
        print('Command refused. Check Keychain settings, existing storage paths, terminal input and port 5157. No automatic repair was attempted.', file=sys.stderr)
        return 1

if __name__ == '__main__':
    sys.exit(main())
