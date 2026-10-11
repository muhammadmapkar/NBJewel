"""Role: GeminiProvider, the LLMProvider implementation for Google Gemini.
It uses the official Google Gen AI Python SDK (package google-genai) with
function calling. The key and model name come from settings, never from code.
"""

import logging
from typing import Any, Sequence

from google import genai
from google.genai import types

from app.config import Settings
from app.llm.provider import LLMError, LLMProvider, LLMResponse, Message, ToolCall

logger = logging.getLogger(__name__)

THINKING_LEVELS = {"minimal": "MINIMAL", "low": "LOW", "medium": "MEDIUM", "high": "HIGH"}


class GeminiProvider(LLMProvider):
    def __init__(self, api_key: str, model: str, client: Any | None = None, thinking_level: str = "low") -> None:
        level = thinking_level.strip().lower()
        if level and level not in THINKING_LEVELS:
            raise LLMError(f"GEMINI_THINKING_LEVEL must be one of {', '.join(THINKING_LEVELS)} or empty.")
        self._thinking_level = level or None
        if not model:
            raise LLMError("GEMINI_MODEL is not set.")
        if client is None:
            if not api_key:
                raise LLMError("GEMINI_API_KEY is not set.")
            client = genai.Client(api_key=api_key)
        self._client = client
        self._model = model

    @classmethod
    def from_settings(cls, settings: Settings) -> "GeminiProvider":
        key = settings.gemini_api_key.get_secret_value() if settings.gemini_api_key else ""
        return cls(api_key=key, model=settings.gemini_model, thinking_level=settings.gemini_thinking_level)

    def generate(
        self,
        messages: Sequence[Message],
        tools: Sequence[dict[str, Any]],
        temperature: float = 0.0,
    ) -> LLMResponse:
        system_text = "\n\n".join(m.content for m in messages if m.role == "system" and m.content)
        contents = self._to_contents(messages)
        try:
            response = self._call(contents, system_text, tools, temperature)
        except Exception as problem:  # noqa: BLE001 - keep SDK details (and anything sensitive) out of callers
            if self._thinking_level and self._rejects_thinking(problem):
                # This model does not accept the thinking setting. Carry on without it.
                logger.warning("The model rejected thinking_level=%s. Continuing without it.", self._thinking_level)
                self._thinking_level = None
                try:
                    response = self._call(contents, system_text, tools, temperature)
                except Exception as second:  # noqa: BLE001
                    raise LLMError(f"The Gemini request failed ({type(second).__name__}).") from second
            else:
                raise LLMError(f"The Gemini request failed ({type(problem).__name__}).") from problem
        return self._to_response(response)

    def _call(self, contents: list[types.Content], system_text: str, tools: Sequence[dict[str, Any]], temperature: float) -> Any:
        config = types.GenerateContentConfig(
            system_instruction=system_text or None,
            temperature=temperature,
            tools=[self._to_tool(tools)] if tools else None,
            # The chat loop runs tools itself, so the SDK must not run anything.
            automatic_function_calling=types.AutomaticFunctionCallingConfig(disable=True),
            # Less thinking means faster answers. The tools supply the facts, so little reasoning is needed.
            thinking_config=types.ThinkingConfig(thinking_level=THINKING_LEVELS[self._thinking_level]) if self._thinking_level else None,
        )
        return self._client.models.generate_content(model=self._model, contents=contents, config=config)

    @staticmethod
    def _rejects_thinking(problem: Exception) -> bool:
        return getattr(problem, "code", None) == 400 and "think" in str(problem).lower()

    # ---------- conversion helpers ----------

    @staticmethod
    def _to_tool(tools: Sequence[dict[str, Any]]) -> types.Tool:
        return types.Tool(function_declarations=[
            types.FunctionDeclaration(
                name=t["name"], description=t["description"], parameters_json_schema=t["parameters"]
            ) for t in tools
        ])

    @staticmethod
    def _to_contents(messages: Sequence[Message]) -> list[types.Content]:
        contents: list[types.Content] = []
        for m in messages:
            if m.role == "system":
                continue
            if m.role == "user":
                contents.append(types.Content(role="user", parts=[types.Part(text=m.content)]))
            elif m.role == "assistant":
                parts: list[types.Part] = []
                if m.content:
                    parts.append(types.Part(text=m.content))
                for call in m.tool_calls:
                    # Reuse the original part when we have it, so thought signatures survive.
                    parts.append(call.extra if isinstance(call.extra, types.Part)
                                 else types.Part.from_function_call(name=call.name, args=call.args))
                contents.append(types.Content(role="model", parts=parts))
            elif m.role == "tool":
                part = types.Part.from_function_response(name=m.tool_name or "", response=m.tool_result or {})
                # Results of one model turn go together in a single user message.
                if contents and contents[-1].role == "user" and all(p.function_response for p in contents[-1].parts or []):
                    contents[-1].parts.append(part)
                else:
                    contents.append(types.Content(role="user", parts=[part]))
        return contents

    @staticmethod
    def _to_response(response: Any) -> LLMResponse:
        candidates = getattr(response, "candidates", None) or []
        parts = (candidates[0].content.parts or []) if candidates and candidates[0].content else []
        texts = [p.text for p in parts if p.text and not p.thought]
        calls = tuple(
            ToolCall(name=p.function_call.name or "", args=dict(p.function_call.args or {}), id=p.function_call.id, extra=p)
            for p in parts if p.function_call
        )
        return LLMResponse(text="".join(texts) or None, tool_calls=calls)
