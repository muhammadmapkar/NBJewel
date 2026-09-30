"""Role: The language model interface. The chat loop (Phase 6) depends only on
this file, so Gemini can be swapped for another model without changing the loop.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Any, Literal, Sequence


class LLMError(Exception):
    """Raised when the model cannot be used. The message never contains the API key."""


@dataclass(frozen=True)
class ToolCall:
    name: str
    args: dict[str, Any]
    id: str | None = None
    # Opaque data a provider needs to see again when the call is sent back in history.
    extra: Any = field(default=None, compare=False, repr=False)


@dataclass(frozen=True)
class Message:
    role: Literal["system", "user", "assistant", "tool"]
    content: str = ""
    # Set on assistant messages that ask for tools.
    tool_calls: tuple[ToolCall, ...] = ()
    # Set on tool messages: which tool answered and what it returned.
    tool_name: str | None = None
    tool_result: dict[str, Any] | None = None
    tool_call_id: str | None = None


@dataclass(frozen=True)
class LLMResponse:
    """Either text, or tool calls, or both empty if the model returned nothing."""

    text: str | None = None
    tool_calls: tuple[ToolCall, ...] = ()

    @property
    def has_tool_calls(self) -> bool:
        return len(self.tool_calls) > 0


class LLMProvider(ABC):
    @abstractmethod
    def generate(
        self,
        messages: Sequence[Message],
        tools: Sequence[dict[str, Any]],
        temperature: float = 0.0,
    ) -> LLMResponse:
        """Sends the conversation and tool declarations, and returns text or tool calls.

        Each tool declaration is {"name", "description", "parameters"} with a JSON schema.
        """
