"""Role: The FastAPI app for the NB Jewel AI service. It has a health check and
POST /chat, which answers a question using the read-only tools and the guards.
"""

import logging
from functools import lru_cache
from typing import Literal

from fastapi import Depends, FastAPI, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel, ConfigDict, Field, field_validator

from app.chat import ChatResult, ChatService
from app.config import get_settings
from app.llm.gemini import GeminiProvider
from app.llm.provider import LLMError
from app.tools.registry import ToolRegistry

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")

app = FastAPI(title="NB Jewel AI Service")

UNAVAILABLE = {"message": "Assistant unavailable. Retry."}


class HistoryMessage(BaseModel):
    model_config = ConfigDict(extra="forbid")

    role: Literal["user", "assistant"]
    content: str = Field(..., max_length=2000)


class ChatRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    question: str = Field(..., min_length=1, max_length=500)
    history: list[HistoryMessage] | None = Field(None, max_length=50)

    @field_validator("question")
    @classmethod
    def _not_blank(cls, value: str) -> str:
        if not value.strip():
            raise ValueError("Question must not be blank.")
        return value.strip()


class Source(BaseModel):
    tool: str
    recordId: str


class ChatResponse(BaseModel):
    answer: str
    sources: list[Source]
    refused: bool


@lru_cache
def get_chat_service() -> ChatService:
    """Builds the chat service once. Raises LLMError if the Gemini key or model is missing."""
    return ChatService(GeminiProvider.from_settings(get_settings()), ToolRegistry())


def provide_chat_service() -> ChatService:
    return get_chat_service()


@app.exception_handler(LLMError)
def llm_unavailable(request: Request, error: LLMError) -> JSONResponse:
    # The model is missing or failed. The message never includes key or SDK details.
    logging.getLogger("app.chat").warning("LLM unavailable: %s", error)
    return JSONResponse(status_code=503, content=UNAVAILABLE)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/chat", response_model=ChatResponse)
def chat(request: ChatRequest, service: ChatService = Depends(provide_chat_service)) -> ChatResponse:
    history = [(m.role, m.content) for m in (request.history or [])]
    result: ChatResult = service.ask(request.question, history)
    return ChatResponse(answer=result.answer, sources=[Source(**s) for s in result.sources], refused=result.refused)
