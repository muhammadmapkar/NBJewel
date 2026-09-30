"""Role: Tests that the FastAPI app starts and answers GET /health."""

from fastapi.testclient import TestClient

from app.main import app


def test_health_returns_ok():
    response = TestClient(app).get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_no_chat_endpoint_yet():
    assert TestClient(app).post("/chat", json={}).status_code == 404
