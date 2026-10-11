# Run guide (macOS)

Three services run together. Start them in this order, each in its own Terminal tab, from the repository root.

| Service | Port | Folder |
|---|---|---|
| .NET API | 5155 | `backend/NbJewel.Api` |
| AI service | 8000 | `ai-service` |
| Angular app | 4200 | `frontend` |

Requirements: .NET 10 SDK, Python 3.11 or newer, Node.js with npm.

## 1. .NET API (port 5155)

```bash
cd backend/NbJewel.Api && dotnet run --launch-profile http
```

Check it: open `http://localhost:5155/swagger`, or run:

```bash
curl -s "http://localhost:5155/api/items/1"
```

### Storage: sample (default) or SQL Server

By default the API uses the sample JSON files and keeps changes in memory. Item statuses, added stock and edited items all reset to the values in `sample-data/` when the API restarts. For example, if the screen shows BNG-0011 as Complete after someone changed it, a restart brings it back to Draft. The API does not log or save these changes.

To save to SQL Server instead, create the database with `docs/item_catalog_schema.sql`, then set the provider and the connection string in the Terminal tab that starts the API. The connection string is read from an environment variable and is never stored in the repository. Use your own server and login (the value below is a placeholder):

```bash
export NBJEWEL_SQL_CONNECTION="Server=YOUR_SERVER;Database=NBJewelSample;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True"
cd backend/NbJewel.Api && Repository__Provider=Sql dotnet run --launch-profile http
```

If the provider is `Sql` and the variable is missing, the API refuses to start and says why. The SQL version is build-checked only. It has not been run against a real SQL Server in this repository.

The chatbot (AI service) never writes. It sends GET requests only, so it works the same with either storage.

## 2. AI service (port 8000)

First time only, create the virtual environment and install the packages:

```bash
cd ai-service && python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
```

First time only, create your local settings file, then open `ai-service/.env` in an editor and fill in `GEMINI_API_KEY` and `GEMINI_MODEL` (choose a model code from the Gemini models page). `NBJEWEL_API_URL` already points to port 5155. You can also add `GEMINI_THINKING_LEVEL` (minimal, low, medium or high, default low): lower is faster. `.env` is ignored by git.

```bash
cd ai-service && cp .env.example .env
```

Start it from the `ai-service` folder, so the `.env` file is found:

```bash
cd ai-service && .venv/bin/uvicorn app.main:app --port 8000
```

Check it:

```bash
curl -s http://localhost:8000/health
```

Without a key or model the service still starts and `/health` works, but chat returns 503 "Assistant unavailable. Retry."

## 3. Angular app (port 4200)

First time only:

```bash
cd frontend && npm install
```

Start it:

```bash
cd frontend && npm start
```

Open `http://localhost:4200/catalog`.

Stop any service with Ctrl+C in its Terminal tab.

## Tests

.NET tests (49 tests, no network):

```bash
cd backend && dotnet test NbJewel.Api.Tests
```

AI service offline tests (no key, no network; live tests show as skipped):

```bash
cd ai-service && .venv/bin/python -m pytest -q
```

AI service live tests use the real Gemini model and cost money. They run only when `RUN_LIVE=1` and both variables below are set. Put your own values in your shell for this session only. `REPEATS` is the number of runs per question (default 3). A report is written to `ai-service/tests/report.md`, which git ignores.

```bash
export GEMINI_API_KEY="paste-your-key-here"
export GEMINI_MODEL="model-code-from-the-gemini-docs"
cd ai-service && RUN_LIVE=1 REPEATS=3 .venv/bin/python -m pytest -q -m live
```

Live targets and the test sets are described in `ai-service/tests/README.md`. No live run has been recorded in this repository.

Frontend: there are no unit tests. Check it compiles (the status rule and stock validation tests are in the .NET project):

```bash
cd frontend && npm run build
```

## Secrets check

Done on 2026-09-30 over the 12 commits in this repository. Result only, no secret values are printed here.

| Check | Result |
|---|---|
| `.env` is not tracked by git | Pass. Only `.env.example` and `ai-service/.env.example` are tracked, and both have empty key values. No `.env` file exists on disk. |
| `.env` is in `.gitignore` | Pass. `.env` and `.env.*` are ignored, with `.env.example` allowed. |
| Key-like strings in tracked source, docs and tests (patterns: `AIza`, `sk-`, `Bearer` tokens, private key headers, plus GitHub, Slack and AWS key prefixes) | Pass. No matches. |
| Key-like strings anywhere in git history (all commits) | Pass. No matches. The same search finds the word `GEMINI_API_KEY` in history, so the search works. |
| Frontend has no key | Pass. No key or LLM host name appears in `frontend/src`. |
| Frontend makes no direct LLM call | Pass. The chat code sends `POST /api/chat` to the .NET API only. The source chips look up an item with the catalog's normal `GET /api/items/:id` call. |
| .NET project has no key | Pass. No key reference in `backend/`. `appsettings.json` holds only the AI service URL and the storage choice (`Repository:Provider`). The SQL connection string is read from the `NBJEWEL_SQL_CONNECTION` environment variable and is not in any file. |
| `legacy-react-prototype/` has no keys | Pass. No key strings and no network calls. A loose text search matched `ask-data-transcript` and `js-tokens`, which are harmless names. |
| Where the key is read | Only in `ai-service/app/config.py` and `ai-service/app/llm/gemini.py`, from the environment, as a secret value that is never logged. |

Not checked: files outside this repository, your shell history, and the terminal output you have shared elsewhere.
