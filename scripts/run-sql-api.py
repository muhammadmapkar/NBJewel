#!/usr/bin/env python3
# Role: Starts a loopback SQL-backed demo using backend environment variables, without loading .env or changing SQL.
import argparse,os,socket,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent

def settings(source):
    env=dict(source)
    required=('NBJEWEL_SQL_CONNECTION','NBJEWEL_IDENTITY_CONNECTION','AI_PLATFORM_CONNECTION')
    missing=[key for key in required if not env.get(key,'').strip()]
    if missing: raise ValueError('Missing backend environment keys: '+', '.join(missing))
    for key in list(env):
        if key.upper().startswith(('SAMPLEIDENTITY__','ASPNETCORE_','KESTREL__')) or key.upper() in ('URLS','HTTP_PORTS','HTTPS_PORTS','TRUSTED_PROXIES'):
            env.pop(key)
    env.update(Repository__Provider='Sql',SampleIdentity__Enabled='false',ASPNETCORE_ENVIRONMENT='Development',DOTNET_ENVIRONMENT='Development',ASPNETCORE_URLS='http://localhost:5155',CORS_ORIGINS='http://localhost:4200,http://localhost:4300')
    env.setdefault('ITEM_ASSET_STORAGE_PATH',str(ROOT/'backend/NbJewel.Api/App_Data/sql-demo/assets'))
    return env

def main():
    parser=argparse.ArgumentParser(description='SQL demo launcher. Never migrates, seeds, or restarts another API.')
    parser.add_argument('--check',action='store_true',help='Validate required environment names only, without connecting or starting')
    args=parser.parse_args()
    try: env=settings(os.environ)
    except ValueError as error: sys.exit(str(error))
    if args.check: print('Required SQL configuration is present; connectivity/schema not checked.');return
    with socket.socket() as probe:
        if probe.connect_ex(('127.0.0.1',5155))==0: sys.exit('Port 5155 is in use. Export the sample QA item before stopping the existing API yourself.')
    os.chdir(ROOT)
    print('Starting SQL-backed local API. No database setup is performed.',flush=True)
    os.execvpe('dotnet',['dotnet','run','--project',str(ROOT/'backend/NbJewel.Api'),'--no-launch-profile'],env)
if __name__=='__main__': main()
