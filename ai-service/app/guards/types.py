"""Role: Shared types for the answer guards: what a tool run is and what a guard decides."""

from dataclasses import dataclass, field
from typing import Any


@dataclass(frozen=True)
class ToolRun:
    """One tool call that was actually executed during a question."""

    name: str
    args: dict[str, Any]
    result: dict[str, Any]

    @property
    def record_ids(self) -> set[str]:
        return {str(s.get("recordId")) for s in self.result.get("sources", [])}


@dataclass(frozen=True)
class Answer:
    text: str
    sources: list[dict[str, str]] = field(default_factory=list)


@dataclass(frozen=True)
class GuardResult:
    passed: bool
    guard: str = ""
    reason: str = ""


PASS = GuardResult(True)
