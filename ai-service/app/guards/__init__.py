"""Role: Runs the three answer guards in order and reports the first failure."""

from app.guards.numbers import check_numbers
from app.guards.sources import check_sources
from app.guards.tool_calls import check_tool_calls
from app.guards.types import PASS, Answer, GuardResult, ToolRun

REFUSAL_TEXT = "I can only answer from NB Jewel data. I have no tool for that."


def run_guards(answer: Answer, runs: list[ToolRun]) -> GuardResult:
    for result in (check_tool_calls(runs), check_sources(answer, runs), check_numbers(answer, runs)):
        if not result.passed:
            return result
    return PASS


__all__ = ["Answer", "GuardResult", "ToolRun", "REFUSAL_TEXT", "run_guards"]
