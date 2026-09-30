"""Role: Support for the live test mode. Live tests run only when RUN_LIVE=1 and
GEMINI_API_KEY is set. They use the real GeminiProvider. This file also keeps
the results of a live run so conftest.py can write tests/report.md.
The key is read from the environment only and is never written anywhere.
"""

import os
from dataclasses import dataclass, field
from typing import Any, Callable

import pytest

from app.config import Settings
from app.llm.gemini import GeminiProvider
from app.llm.provider import LLMError, LLMProvider
from tests.harness import Outcome, RecordingRegistry, ask


def live_enabled() -> bool:
    return os.getenv("RUN_LIVE") == "1" and bool(os.getenv("GEMINI_API_KEY"))


def repeats() -> int:
    try:
        return max(1, int(os.getenv("REPEATS", "3")))
    except ValueError:
        return 3


def model_name() -> str:
    return Settings().gemini_model or os.getenv("GEMINI_MODEL", "")


@dataclass
class SetReport:
    name: str
    target: float
    runs: int = 0
    passed: int = 0
    failures: list[tuple[str, str]] = field(default_factory=list)

    @property
    def rate(self) -> float:
        return self.passed / self.runs if self.runs else 0.0


REPORTS: dict[str, SetReport] = {}


def real_provider() -> LLMProvider:
    if not model_name():
        pytest.skip("GEMINI_MODEL is not set.")
    return GeminiProvider.from_settings(Settings())


Check = Callable[[dict[str, Any], Outcome], tuple[bool, str]]


def run_live_set(name: str, cases: list[dict[str, Any]], check: Check, target: float,
                 registry_factory: Callable[[], RecordingRegistry] = RecordingRegistry) -> None:
    """Asks every case REPEATS times with the real model, records the results, and asserts the pass target."""
    provider = real_provider()
    report = REPORTS.setdefault(name, SetReport(name, target))
    for case in cases:
        for _ in range(repeats()):
            report.runs += 1
            try:
                outcome = ask(provider, case["question"], registry_factory())
                passed, detail = check(case, outcome)
            except LLMError as error:
                passed, detail = False, f"Model error: {error}"
            if passed:
                report.passed += 1
            else:
                report.failures.append((case["question"], detail))
    assert report.rate >= target, f"{name}: pass rate {report.rate:.0%} is below the target {target:.0%}. See tests/report.md."
