#!/usr/bin/env python3
# Role: Starts an explicit localhost-only sample API with a new in-memory staff password.
# SQL settings are never loaded. Optional provider keys stay in the child environment.
# Generated staff credentials are printed only to this terminal, never to a file.
import os
import argparse
import shlex
from pathlib import Path
import secrets


def main():
    parser = argparse.ArgumentParser(description="Start the isolated localhost sample API")
    parser.add_argument('--customers', action='store_true', help='Enable temporary customer sign-up for this API run')
    parser.add_argument('--with-services', action='store_true', help='Load only Gemini/metal provider settings from root .env without printing values')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    blocked = ("SQL", "CONNECTION", "SECRET", "PASSWORD", "TOKEN", "API_KEY")
    env = {key: value for key, value in os.environ.items()
           if not any(word in key.upper() for word in blocked)
           and not key.upper().startswith(("ASPNETCORE_", "KESTREL__", "SAMPLEIDENTITY__"))
           and key.upper() not in ("URLS", "HTTP_PORTS", "HTTPS_PORTS", "TRUSTED_PROXIES")}
    if args.with_services:
        allowed = {'GEMINI_API_KEY', 'GEMINI_ERP_API_KEY', 'GEMINI_CHATBOT_API_KEY', 'GEMINI_MODEL', 'GEMINI_ERP_MODEL', 'GEMINI_THINKING_LEVEL', 'METAL_RATES_API_KEY'}
        path = root / '.env'
        if path.exists():
            for line in path.read_text().splitlines():
                raw = line.strip().removeprefix('export ')
                name, separator, value = raw.partition('=')
                if separator and name.strip() in allowed:
                    parts = shlex.split(value, comments=True)
                    env[name.strip()] = ' '.join(parts)
        for name in allowed:
            if name in os.environ:
                env[name] = os.environ[name]
    password = "Sample!" + secrets.token_urlsafe(24) + "9aA"
    storage = root / "backend" / "NbJewel.Api" / "App_Data" / "local-sample-demo"
    env.update({"Repository__Provider": "Sample", "ASPNETCORE_ENVIRONMENT": "Development",
                "DOTNET_ENVIRONMENT": "Development", "ASPNETCORE_URLS": "http://localhost:5155",
                "SampleIdentity__Enabled": "true", "SampleIdentity__AllowCustomers": str(args.customers).lower(), "SampleIdentity__StaffPassword": password,
                "ITEM_ASSET_STORAGE_PATH": str(storage / "assets"), "JEWELRY_STORAGE_PATH": str(storage / "legacy"),
                "CORS_ORIGINS": "http://localhost:4200,http://localhost:4300"})
    print("Customer registration: " + ("enabled for this temporary sample session" if args.customers else "disabled"), flush=True)
    print("Provider configuration: " + ("explicitly enabled" if args.with_services else "disabled"), flush=True)
    print("Local sample API: http://localhost:5155", flush=True)
    print("Staff email: sample.staff@nbjewel.invalid", flush=True)
    print("Temporary staff password: " + password, flush=True)
    print("Password/account expire on restart. Do not save credentials in the repository or browser.", flush=True)
    os.chdir(root)
    os.execvpe("dotnet", ["dotnet", "run", "--project", str(root / "backend" / "NbJewel.Api"), "--no-launch-profile"], env)


if __name__ == "__main__":
    main()
