# nbjewel-assistant

An internship demo for Cetas (jewelry retail ERP, NB Jewel). It has an Angular catalog, a .NET API, and a chat assistant that answers only from ERP data through fixed read-only tools. The assistant runs inside the .NET API.

This repository uses fake data only. It contains no real client data and does not claim to reproduce NB Jewel internals. Anything unconfirmed is marked "assumed" in the docs.

## Structure

A beginner map. Each source file starts with a short comment that says what it is for.

- `frontend/`: Angular app (themed shell, catalog, chat panel, virtual try-on). Code is in `frontend/src/app/`. Try-on code is in `frontend/src/app/features/tryon/`.
- `backend/NbJewel.Api/`: .NET Web API.
  - `Controllers/`: the HTTP endpoints (items, stock, categories, chat and so on).
  - `Chat/`: the chatbot. The 8 read-only tools (`ChatTools.cs`), the 4 answer guards (`Guards.cs`), the system prompt, the Gemini REST client and the loop that ties them together (`ChatOrchestrator.cs`).
  - `Models/`: the data shapes and the shared item validation rules.
  - `Jewelry/`: the virtual try-on jewelry store (upload checks, owner PIN, file based repository). Separate from the chatbot.
  - `Repositories/`: where data comes from. `SampleItemRepository` reads JSON, `SqlItemRepository` reads SQL Server. `Program.cs` picks one.
- `backend/NbJewel.Api.Tests/`: .NET tests (API and chatbot). They run on the Sample repository, and on SQL Server too when a test connection is set.
- `sample-data/`: synthetic JSON used by the Sample repository and to seed SQL Server. `sample-data/jewelry/` holds the 6 generated try-on sample images.
- `docs/`: documentation (see below) and the SQL schema `item_catalog_schema.sql`.
- `scripts/setup-db.sh`: one command to prepare the local SQL Server.
- `docker-compose.yml`: the local SQL Server container (Sql mode only).
- `.env.example`: every environment variable name. Names only, no values.

## Documentation

- [Architecture](docs/architecture.md): diagram, chat request flow, swap plan, assumed details
- [Tool registry](docs/tool-registry.md): the 8 read-only tools
- [Grounding design](docs/grounding-design.md): how answers are kept to the data, and the known limits
- [Decision record: where the chatbot lives](docs/decision-ai-service.md): why it moved into the .NET API
- [Demo script](docs/demo-script.md): a 10 minute walkthrough
- [Data privacy checklist](docs/data-privacy-checklist.md): checks before any real data, and API key rules
- [Questions for Sakthivel](docs/questions-for-sakthivel.md): open questions
- [Run guide](docs/run-guide.md): start the services, run the tests, secrets check
- [Virtual try-on design](docs/tryon-design.md): how the camera try-on works, landmarks, assumptions, known limits
- [Virtual try-on deploy](docs/tryon-deploy.md): https hosting, environment variables, CORS, testing on a phone
- Older placeholders from the first phase, not updated since: [API contracts](docs/api-contracts.md), [Assumptions](docs/assumptions.md), [Data dictionary](docs/data-dictionary.md)

## Quick start

Requirements: .NET 10 SDK, Node.js with npm. Two services run together, each in its own Terminal tab, from the repository root. More detail is in the [run guide](docs/run-guide.md).

### Sample mode (default, no database)

The API reads `sample-data/` and keeps changes in memory. A restart resets them.

```bash
# Tab 1: .NET API on 5155 (first time: cp .env.example .env, fill in GEMINI_API_KEY and GEMINI_MODEL)
set -a; source .env; set +a
cd backend/NbJewel.Api && dotnet run --launch-profile http

# Tab 2: Angular app on 4200 (first time: npm install)
cd frontend && npm start
```

Without a Gemini key and model the catalog still works, and chat answers "The assistant is busy. Please try again."

Virtual try-on: set `OWNER_PIN` in `.env` before starting the API. Then open `http://localhost:4200/owner/jewelry` (owner, asks for the PIN) and `http://localhost:4200/tryon` (customer). Uploads are saved under `backend/NbJewel.Api/App_Data/jewelry` unless `JEWELRY_STORAGE_PATH` is set. Frontend unit tests: `cd frontend && npm test`. To try it on a phone, see [tryon-deploy.md](docs/tryon-deploy.md).

### Sql mode (SQL Server in Docker)

Changes are saved in the database. This needs Docker. On Apple Silicon the container runs under amd64 emulation.

```bash
cp .env.example .env     # then set MSSQL_SA_PASSWORD and NBJEWEL_SQL_CONNECTION in .env
./scripts/setup-db.sh    # starts SQL Server, creates the database, loads the sample data
set -a; source .env; set +a
cd backend/NbJewel.Api && Repository__Provider=Sql dotnet run --launch-profile http
```

Start the Angular app as in Sample mode. Use `./scripts/setup-db.sh --reset` to drop the database and load fresh sample data. To run the tests on both providers, set `NBJEWEL_SQL_CONNECTION` to a server and run `dotnet test backend/NbJewel.Api.Tests`. Tests use a separate database called `NBJewelTest`, never the dev database.

Keep the real `GEMINI_API_KEY` only in the .NET API environment, never in the frontend or in git.

## Status

| Area | Status |
|---|---|
| Sample data (30 items, 10 designs, variants, stock, stores, categories, rates) | Done |
| .NET API: items, designs, variants, stock (read and add), price, status workflow, categories, stores, metal rates, Swagger, CORS | Done |
| Chatbot inside the .NET API: `POST /api/chat`, 8 read-only tools, 4 answer guards, Gemini REST client, 30 s per model call and 45 s per question, 503 "The assistant is busy. Please try again." on failure | Done |
| Angular themed shell and static pages (dashboard, scheme master, chit master, financial year, password) | Done, mock data only |
| Angular catalog: list (design code column, search, sort), filters, detail, price breakdown, stock entry, create and edit with design dropdown, status workflow badges | Done |
| Angular printable item tag with a QR code of the item code (`/catalog/:id/tag`, uses the `qrcode` npm package) | Done, print preview not checked |
| Angular images tab | Done with fake in-memory data, no backend endpoint |
| Angular variants tab | Done, served by the API |
| Angular chat panel with source chips, refusal style and the server's error message | Done |
| .NET tests (API and chatbot): 149 passed, 2 skipped (SQL parity tests that need a SQL Server) | Done |
| Live test against Gemini (5 questions: 3 on topic, 1 off topic, 1 no match) | Two clean runs, 5 of 5 each (latest 2026-10-14, 1.4 to 8.1 s per answer). An earlier run was partial because of Gemini 503s and slow calls |
| Frontend unit tests | Not built |
| Login, roles and permissions | Not built |
| SQL Server repository (`SqlItemRepository`, switch with `Repository:Provider`), docker-compose, setup script, tests that run on both providers | Built. Sample tests pass. SQL tests are written but not yet run against a real server (no Docker on the dev machine yet) |
| Real NB Jewel API repository | Not built |
| Backend endpoints for images and stock history | Not built |
| Image upload and storage | Not built |
| Theme pages connected to the API | Not built (mock data) |
| Deployment and CI | Not built |
| Real client data | Not allowed until the [privacy checklist](docs/data-privacy-checklist.md) is complete |

## Known gaps

- No login, roles or permissions. Anyone who can reach the API can read and change items. This is acceptable only for a local demo with fake data.
- The SQL Server path has not been run against a real server yet. Treat it as unverified until `./scripts/setup-db.sh` and the SQL tests have passed once.
- The Angular item form still lets a user type a net weight and sends it. The API ignores it and always computes gross minus stone.
- Images and stock history have no backend endpoint. The images tab uses fake in-memory data.
- The live Gemini result is two clean runs of 5 questions. Gemini sometimes answers 503 or slowly. The API then fails fast and the user sees the busy message.
- The guards check numbers and sources, not whether the sentence around a number is true. See the [grounding design](docs/grounding-design.md).
- Sample mode loses all changes on restart.

## Safety boundaries

- The assistant answers only from ERP data returned by fixed read-only tools.
- There is no free-form SQL and no answering from model memory.
- The data layer is swappable: sample data now, SQL Server or a verified ERP API later.
- All identifiers, names, amounts, rates and records are fake.

## Assumed SQL fields

These columns were added to `docs/item_catalog_schema.sql` so the SQL provider returns the same JSON as the Sample provider. They are assumed, not confirmed by NB Jewel:

- `ItemVariant.Color` and `ItemVariant.PriceDelta` (assumed).
- `StockByStore.Reserved` (assumed). Low stock means on hand minus reserved is below the threshold.
- `StockByStore` now keeps stock per item, optional variant and store (assumed redesign), because the sample data has items with no variants.
- `MetalRate.Currency`, default AED (assumed).
- `Store.Code`, made by the seed as ST01, ST02 and so on (assumed).
- Purity `Fineness` values for 24K, 22K, 18K and 925 (assumed).
- Net weight is always gross minus stone. The API ignores any `netWt` a client sends and returns 400 when stone is above gross.
