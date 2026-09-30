# Architecture

Status of each part of the system. Items marked "assumed" are guesses about
NB Jewel, not confirmed facts.

## Current state

| Part | Status | Notes |
|---|---|---|
| Sample data (`sample-data/`) | Done | Synthetic JSON only. No real client data. |
| .NET Web API (`backend/NbJewel.Api`) | Done | Typed endpoints for items, stock, price, categories, stores and metal rates. Swagger and CORS for localhost:4200. Data comes from `SampleItemRepository` behind `IItemRepository`. |
| Angular frontend (`frontend/`) | Partial | Static themed shell and pages (dashboard, scheme master, chit master, change financial year, change password) using mock data. It does not call the API yet. The chat button is a placeholder. |
| AI service (`ai-service/`) | Not built yet | Empty folder. Planned as a FastAPI service with fixed read-only tools. |

## Planned data flow

```
Angular  ->  .NET API  ->  IItemRepository  ->  sample JSON (now)
                                             ->  SQL Server or real ERP API (later)
Angular chat  ->  .NET /api/chat  ->  FastAPI AI service  ->  .NET read-only endpoints
```

## Assumed details

- **Assumed:** NB Jewel prices items from a daily metal rate per purity plus a making charge.
- **Assumed:** NB Jewel runs several stores, each with its own stock levels.
- **Assumed:** The real ERP data will later be reached through SQL Server or a verified API.

## Rules

- The assistant answers only from ERP data through fixed read-only tools.
- The assistant must not run free-form SQL or answer from model memory.
- API keys stay in backend environment variables and never reach the frontend or the repo.
- The data layer is swappable so the sample repository can be replaced later.
