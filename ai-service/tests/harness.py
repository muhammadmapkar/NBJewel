"""Role: Shared helpers for the Phase 8 tests: a scripted FakeProvider, an
OracleProvider that plays the ideal answer for a golden case, a registry that
records every tool run, and the invariant checks used on every answer.
"""

import json
from dataclasses import dataclass
from typing import Any, Sequence

import httpx

from app.chat import ChatResult, ChatService
from app.guards import Answer, ToolRun
from app.guards.numbers import check_numbers
from app.guards.sources import check_sources
from app.guards.tool_calls import check_tool_calls
from app.llm.provider import LLMProvider, LLMResponse, Message, ToolCall
from app.tools.api_client import ReadOnlyApiClient
from app.tools.catalog_tools import Tool
from app.tools.registry import ToolRegistry
from tests.fixture_api import DataSet, FixtureApi, patched_data

_DATA: DataSet | None = None


def data() -> DataSet:
    """The patched sample data, built once per test run."""
    global _DATA
    if _DATA is None:
        _DATA = patched_data()
    return _DATA


class RecordingRegistry(ToolRegistry):
    """A registry on top of the fixture API that keeps every tool run."""

    def __init__(self, tools: list[Tool] | None = None, dataset: DataSet | None = None) -> None:
        self.api = FixtureApi(dataset or data())
        super().__init__(ReadOnlyApiClient("http://api.test", transport=self.api.transport()), tools)
        self.runs: list[ToolRun] = []

    def run_tool(self, name: str, args: dict[str, Any] | None = None) -> dict[str, Any]:
        result = super().run_tool(name, args)
        self.runs.append(ToolRun(name, args or {}, result))
        return result


class FakeProvider(LLMProvider):
    """Plays back a script of LLMResponse objects and records what it was sent."""

    def __init__(self, script: list[LLMResponse]) -> None:
        self.script = list(script)
        self.calls: list[dict[str, Any]] = []

    def generate(self, messages: Sequence[Message], tools: Sequence[dict[str, Any]], temperature: float = 0.0) -> LLMResponse:
        self.calls.append({"messages": list(messages), "tools": list(tools), "temperature": temperature})
        return self.script.pop(0)


class OracleProvider(LLMProvider):
    """Plays the ideal conversation: first the planned tool calls, then the planned answer.
    Sources are taken from the tool results it was sent, as a careful model would."""

    def __init__(self, calls: list[tuple[str, dict[str, Any]]], answer: str) -> None:
        self.calls_plan, self.answer = calls, answer
        self.requests = 0

    def generate(self, messages: Sequence[Message], tools: Sequence[dict[str, Any]], temperature: float = 0.0) -> LLMResponse:
        self.requests += 1
        results = [m for m in messages if m.role == "tool"]
        if not results:
            return LLMResponse(tool_calls=tuple(ToolCall(n, a) for n, a in self.calls_plan))
        sources: list[dict[str, str]] = []
        for m in results:
            for s in (m.tool_result or {}).get("sources", [])[:1]:
                sources.append({"tool": s["tool"], "recordId": str(s["recordId"])})
        return LLMResponse(text=json.dumps({"answer": self.answer, "sources": sources}))


def call(tool_name: str, /, **args: Any) -> LLMResponse:
    return LLMResponse(tool_calls=(ToolCall(tool_name, args),))


def say(answer: str, sources: list[dict[str, Any]] | None = None) -> LLMResponse:
    return LLMResponse(text=json.dumps({"answer": answer, "sources": sources or []}))


@dataclass
class Outcome:
    question: str
    result: ChatResult
    runs: list[ToolRun]
    provider: Any

    @property
    def refused(self) -> bool:
        return self.result.refused

    @property
    def answer(self) -> str:
        return self.result.answer

    @property
    def ok_runs(self) -> list[ToolRun]:
        return [r for r in self.runs if r.result.get("status") == "ok"]


def ask(provider: LLMProvider, question: str, registry: RecordingRegistry | None = None,
        history: list[tuple[str, str]] | None = None) -> Outcome:
    registry = registry or RecordingRegistry()
    result = ChatService(provider, registry).ask(question, history)
    return Outcome(question, result, registry.runs, provider)


def invariant_problems(outcome: Outcome) -> list[str]:
    """Checks that every non-refusal answer is grounded. Returns a list of problems (empty is good)."""
    if outcome.refused:
        return []
    problems = []
    answer = Answer(outcome.answer, outcome.result.sources)
    if not check_tool_calls(outcome.runs).passed:
        problems.append("no tool call was made")
    if outcome.ok_runs and not outcome.result.sources:
        problems.append("sources are empty")
    if not check_sources(answer, outcome.runs).passed:
        problems.append("a source does not match a tool result")
    if not check_numbers(answer, outcome.runs).passed:
        problems.append("a number is not in the tool results")
    return problems


def unreachable_client() -> ReadOnlyApiClient:
    def refuse(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("refused", request=request)
    return ReadOnlyApiClient("http://api.test", transport=httpx.MockTransport(refuse))
