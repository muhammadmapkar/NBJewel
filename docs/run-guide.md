# Run guide (macOS)

Two services run together. Start them in this order, each in its own Terminal tab, from the repository root. The chatbot runs inside the .NET API, so there is no separate AI service to start.

| Service | Port | Folder |
|---|---|---|
| .NET API (includes the chatbot) | 5155 | `backend/NbJewel.Api` |
| Angular app | 4200 | `frontend` |

Requirements: .NET 10 SDK, Node.js with npm.

## 1. .NET API (port 5155)

First time only, create your local settings file, then open `.env` in an editor and fill in `GEMINI_API_KEY` and `GEMINI_MODEL` (choose a model code from the Gemini models page). You can also set `GEMINI_THINKING_LEVEL` (minimal, low, medium or high, default low): lower is faster. `.env` is ignored by git.

```bash
cp .env.example .env
```

The API reads settings from its environment, not from the `.env` file directly. Load the file into the Terminal tab, then start the API:

```bash
set -a; source .env; set +a
cd backend/NbJewel.Api && dotnet run --launch-profile http
```

Check it: open `http://localhost:5155/swagger`, or run:

```bash
curl -s "http://localhost:5155/api/items/1"
```

Check the chatbot (needs the key and model):

```bash
curl -s -X POST http://localhost:5155/api/chat -H "Content-Type: application/json" -d '{"question":"Show stock of RNG-0142 by store."}'
```

Without a key or model the API still starts and the catalog works, but chat returns 503 with "The assistant is busy. Please try again."

### Chat limits

- A question is 1 to 500 characters. History is at most 50 messages of 2,000 characters each. Only the last 6 are sent to the model.
- One Gemini request times out after 30 seconds. The whole question has a 45 second limit, because one question can use several model calls.
- When Gemini is down, slow or not configured, the API returns 503 and the chat panel shows the message. The log line for each Gemini call holds only the status, the error type and the time in milliseconds. It never holds the key or the question.

### Storage: sample (default) or SQL Server

By default the API uses the sample JSON files and keeps changes in memory. Item statuses, added stock and edited items all reset to the values in `sample-data/` when the API restarts. For example, if the screen shows BNG-0011 as Complete after someone changed it, a restart brings it back to Draft. The API does not log or save these changes.

To save to SQL Server instead, use the Docker setup in the README (`./scripts/setup-db.sh`), or point the API at your own server. The connection string is read from an environment variable and is never stored in the repository. Use your own server and login (the value below is a placeholder):

```bash
export NBJEWEL_SQL_CONNECTION="Server=YOUR_SERVER;Database=NBJewelSample;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True"
cd backend/NbJewel.Api && Repository__Provider=Sql dotnet run --launch-profile http
```

If the provider is `Sql` and the variable is missing, the API refuses to start and says why. The SQL version has not been run against a real SQL Server in this repository.

The chatbot never writes. Its 8 tools only read, so it works the same with either storage.

## 2. Angular app (port 4200)

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

.NET tests (API and chatbot, no network, no key needed). Last result: 149 passed, 2 skipped. The 2 skipped are the SQL parity tests, which need a SQL Server:

```bash
cd backend && dotnet test NbJewel.Api.Tests -v q
```

To run the tests on SQL Server as well, set `NBJEWEL_SQL_CONNECTION` first. They use a separate database called `NBJewelTest`.

Live check against Gemini. It uses the real model and costs money, so it is a manual run, not a test. Start the API with the key and model loaded, then ask these five through the chat panel or with the `curl` command above:

| Question | Expected |
|---|---|
| "Show stock of RNG-0142 by store." | 7 in total (Dubai 4, Chennai 2, Bengaluru 1), with sources |
| "What is today's 22K rate?" | 66 AED per gram, with sources |
| "Which items are Draft?" | BNG-0011 and EAR-0022, with sources |
| "What is the gold price in the world market today?" | The refusal text |
| "Find the item with code ZZZ-9999." | "No data found." |

Last run, 2026-10-14, Sample data, with the 30 s and 45 s limits: 5 of 5 passed, all status 200, using exactly the five questions above. Times: 5466, 3100 and 2468 ms for the three on-topic questions, 1449 ms for the refusal, 8133 ms for "No data found." The check covered the status, the refused flag, the source count and the "No data found." text. The wording of the answers was not compared. An earlier run also passed 5 of 5 (its questions were not recorded). Gemini can still be slow or answer 503. The API then fails fast and the user sees the busy message.

Frontend: there are no unit tests. Check it compiles:

```bash
cd frontend && npm run build
```

## Secrets check

Repeated on 2026-10-14 over the files tracked now, after the chatbot moved into the .NET API. The history search was done on 2026-09-30 over 12 commits and has not been repeated since. Result only, no secret values are printed here.

| Check | Result |
|---|---|
| `.env` is not tracked by git | Pass. No `.env` file is tracked. `.env.example` has empty values. |
| `.env` is in `.gitignore` | Pass on 2026-09-30. `.env` and `.env.*` are ignored, with `.env.example` allowed. Not rechecked. |
| Key-like strings in tracked files (patterns: `AIza`, `sk-`, private key headers) | Pass. No matches. |
| Key-like strings anywhere in git history | Pass on 2026-09-30 only. Not rerun for the commits made since. |
| Frontend has no key | Pass. No key or LLM host name appears in `frontend/src`. |
| Frontend makes no direct LLM call | Pass on 2026-09-30. The chat code sends `POST /api/chat` to the .NET API only. Not rechecked. |
| Where the key is read | Only in `backend/NbJewel.Api/Chat/GeminiLlmClient.cs` (through `GeminiOptions`, in `Program.cs`), from the `GEMINI_API_KEY` environment variable. `appsettings.json` holds no key. |
| Key never logged | Not rechecked after the move. The Gemini call log line is written to hold only the status, the error type and the time. |

Not checked: files outside this repository, your shell history, and the terminal output you have shared elsewhere. The old Python service folder, with its own ignored `.env` file, was deleted from disk on 2026-10-14. Use the root `.env` only.
