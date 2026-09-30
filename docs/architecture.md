# Architecture placeholder

## Phase 1

This phase contains repository layout, documentation placeholders, and fake JSON
fixtures only. No Angular, .NET, or FastAPI service is scaffolded yet.

## Future direction

**Assumed:** A future .NET Web API will expose typed, read-only endpoints over a
swappable sample-data repository. A future FastAPI service will orchestrate
fixed assistant tools, and a future Angular application will consume the API.

The assistant must not execute free-form SQL or answer from model memory.
