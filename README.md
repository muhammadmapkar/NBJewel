# nbjewel-assistant

Phase 1 foundation for a Cetas internship demo for a jewelry retail ERP.

This repository uses fake data only. It does not contain real client data and
does not claim to reproduce NB Jewel internals. Unverified project details are
labeled as assumed in the documentation.

## Current structure

- `frontend/`: reserved for Angular standalone components in a later phase
- `backend/`: reserved for the .NET Web API in a later phase
- `ai-service/`: reserved for the Python FastAPI service in a later phase
- `sample-data/`: small synthetic JSON fixtures for local contract tests
- `docs/`: architecture, assumptions, API, and data dictionary placeholders
- `legacy-react-prototype/`: preserved throwaway React/Vite prototype

Phase 1 does not scaffold Angular, .NET, Python, SQL Server, authentication, or
production integrations.

## Setup

1. Copy `.env.example` to `.env` for local backend configuration.
2. Keep the real `GEMINI_API_KEY` only in the backend environment.
3. Review the JSON files in `sample-data/` as the local fixture source.
4. Use the documentation in `docs/` as the starting point for later service
   contracts.

The preserved prototype has its own package files and is not the target
application for this phase.

## Safety boundaries

- The assistant will answer only from ERP data returned by fixed read-only
  tools.
- Free-form SQL and model-memory answers are out of scope.
- The data layer must remain swappable, starting with sample data and allowing
  a later SQL Server or verified ERP API adapter.
- All identifiers, names, amounts, rates, and operational records are fake.
