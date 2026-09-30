"""Role: The FastAPI app for the NB Jewel AI service. Phase 5 has a health check only."""

from fastapi import FastAPI

app = FastAPI(title="NB Jewel AI Service")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}
