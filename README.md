# nbjewel-assistant

An internship demo for Cetas (jewelry retail ERP, NB Jewel). It has an Angular catalog, a .NET API, and a chat assistant that answers only from ERP data through fixed read-only tools.

This repository uses fake data only. It contains no real client data and does not claim to reproduce NB Jewel internals. Anything unconfirmed is marked "assumed" in the docs.

## Structure

- `frontend/`: Angular app (themed shell, catalog, chat panel)
- `backend/`: .NET Web API and its tests
- `ai-service/`: Python FastAPI service (tools, guards, Gemini provider) and its tests
- `sample-data/`: synthetic JSON used by the API
- `docs/`: documentation (see below)
- `legacy-react-prototype/`: an older throwaway React prototype, kept for reference

## Documentation

- [Architecture](docs/architecture.md): diagram, chat request flow, swap plan, assumed details
- [Tool registry](docs/tool-registry.md): the 8 read-only tools
- [Grounding design](docs/grounding-design.md): how answers are kept to the data, and the known limits
- [Demo script](docs/demo-script.md): a 10 minute walkthrough
- [Data privacy checklist](docs/data-privacy-checklist.md): checks before any real data, and API key rules
- [Questions for Sakthivel](docs/questions-for-sakthivel.md): open questions
- [Run guide](docs/run-guide.md): start the services, run the tests, secrets check
- [AI service tests](ai-service/tests/README.md): offline and live test modes
- Older placeholders from the first phase, not updated since: [API contracts](docs/api-contracts.md), [Assumptions](docs/assumptions.md), [Data dictionary](docs/data-dictionary.md)

## Quick start

Start the .NET API on 5155, the AI service on 8000 and the Angular app on 4200. The exact commands are in the [run guide](docs/run-guide.md). Keep the real `GEMINI_API_KEY` only in the AI service environment, never in the frontend or in git.

## Status

| Area | Status |
|---|---|
| Sample data (30 items, 10 designs, variants, stock, stores, categories, rates) | Done |
| .NET API: items, designs, variants, stock (read and add), price, status workflow, categories, stores, metal rates, Swagger, CORS | Done |
| .NET `POST /api/chat` forwarder (503 when the assistant is unavailable) | Done |
| Angular themed shell and static pages (dashboard, scheme master, chit master, financial year, password) | Done, mock data only |
| Angular catalog: list (design code column, search, sort), filters, detail, price breakdown, stock entry, create and edit with design dropdown, status workflow badges | Done |
| Angular images tab | Done with fake in-memory data, no backend endpoint |
| Angular variants tab | Done, served by the API |
| AI service: 8 read-only tools, chat loop, 4 answer guards, Gemini provider | Done |
| Angular chat panel with source chips and refusal style | Done |
| Offline tests: AI service (275) and .NET (49) | Done |
| Live tests against Gemini (code written) | Not run, no result recorded |
| Frontend unit tests | Not built |
| Login, roles and permissions | Not built |
| SQL Server repository (`SqlItemRepository`, switch with `Repository:Provider`) | Built, build-checked only, not run against a server |
| Real NB Jewel API repository | Not built |
| Backend endpoints for images and stock history | Not built |
| Image upload and storage | Not built |
| Theme pages connected to the API | Not built (mock data) |
| Deployment, Docker, CI | Not built |
| Real client data | Not allowed until the [privacy checklist](docs/data-privacy-checklist.md) is complete |

## Safety boundaries

- The assistant answers only from ERP data returned by fixed read-only tools.
- There is no free-form SQL and no answering from model memory.
- The data layer is swappable: sample data now, SQL Server or a verified ERP API later.
- All identifiers, names, amounts, rates and records are fake.
