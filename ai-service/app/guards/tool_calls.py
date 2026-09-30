"""Role: Guard 1. An answer is only allowed if at least one tool was called."""

from app.guards.types import PASS, GuardResult, ToolRun


def check_tool_calls(runs: list[ToolRun]) -> GuardResult:
    if not runs:
        return GuardResult(False, "tool_calls", "No tool was called.")
    return PASS
