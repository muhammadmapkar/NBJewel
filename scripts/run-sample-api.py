#!/usr/bin/env python3
# Role: Starts an explicit localhost-only sample API with a new in-memory staff password.
# No secrets file or SQL is read. Credentials are printed only to this terminal, never to a file.
import os
from pathlib import Path
import secrets


def main():
    root = Path(__file__).resolve().parent.parent
    blocked = ("SQL", "CONNECTION", "SECRET", "PASSWORD", "TOKEN", "API_KEY")
    env = {key: value for key, value in os.environ.items()
           if not any(word in key.upper() for word in blocked)
           and not key.upper().startswith(("ASPNETCORE_", "KESTREL__", "SAMPLEIDENTITY__"))
           and key.upper() not in ("URLS", "HTTP_PORTS", "HTTPS_PORTS", "TRUSTED_PROXIES")}
    password = "Sample!" + secrets.token_urlsafe(24) + "9aA"
    storage = root / "backend" / "NbJewel.Api" / "App_Data" / "local-sample-demo"
    env.update({"Repository__Provider": "Sample", "ASPNETCORE_ENVIRONMENT": "Development",
                "DOTNET_ENVIRONMENT": "Development", "ASPNETCORE_URLS": "http://localhost:5155",
                "SampleIdentity__Enabled": "true", "SampleIdentity__StaffPassword": password,
                "ITEM_ASSET_STORAGE_PATH": str(storage / "assets"), "JEWELRY_STORAGE_PATH": str(storage / "legacy"),
                "CORS_ORIGINS": "http://localhost:4200,http://localhost:4300"})
    print("Local sample API: http://localhost:5155", flush=True)
    print("Staff email: sample.staff@nbjewel.invalid", flush=True)
    print("Temporary staff password: " + password, flush=True)
    print("Password/account expire on restart. Do not save credentials in the repository or browser.", flush=True)
    os.chdir(root)
    os.execvpe("dotnet", ["dotnet", "run", "--project", str(root / "backend" / "NbJewel.Api"), "--no-launch-profile"], env)


if __name__ == "__main__":
    main()
