<!-- Role: Owner-only next steps and local browser-security check results for NB Jewel. -->
# Next steps

## Local security check (2026-11-13, sample mode, curl only)

API run with `dotnet run --launch-profile http` on port 5155, no database, no keys.

| Check | Result |
|---|---|
| CORS preflight from `http://localhost:4200` | Allowed, credentials allowed, `x-csrf-token` header allowed |
| CORS preflight from `http://evil.example` | No `Access-Control-Allow-Origin` header returned (blocked) |
| `POST /api/auth/login` without CSRF token | 400 |
| `GET /api/auth/csrf` | Sets `nbjewel.csrf` cookie, `HttpOnly`, `SameSite=Strict`, `no-store` |
| `GET /api/auth/me` without login | 401 |
| `POST /api/auth/login` with valid CSRF token and cookie | 503 (see below) |

Not verified: login cookie flags, session cookie after login, conversation isolation between users, real browser behaviour. Login returned 503 in sample mode. The likely cause is that identity needs SQL Server. This is a guess and was not confirmed. The API log also showed "Assistant maintenance failed" at startup in sample mode.

## Only you can do these

1. Add the Gemini API key to the backend environment variables (never in the repo or frontend).
2. Add SMTP settings to the backend environment variables for email delivery.
3. Ask Mr. Sakthivel for the real ERP role mapping and production provisioning. See `docs/questions-for-sakthivel.md`.

## Needs your approval first

- Restart `nbjewel-sql-test` (it exited with code 139) to rerun the SQL suite or test login against real SQL.
- Any push, merge or deployment.

## Can be given to other agents

See `docs/agent-tasks.md`.
