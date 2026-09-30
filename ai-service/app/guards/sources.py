"""Role: Guard 2. Sources must be present and real: each one must name a tool
that was actually called and a recordId that appears in that tool's results.

One exception: if no tool returned status "ok" (everything was not_found or
error), there are no records to cite, so the sources list must be empty and
the answer can only say that nothing was found.
"""

from app.guards.types import PASS, Answer, GuardResult, ToolRun


def check_sources(answer: Answer, runs: list[ToolRun]) -> GuardResult:
    any_ok = any(r.result.get("status") == "ok" for r in runs)
    if not any_ok:
        if answer.sources:
            return GuardResult(False, "sources", "Sources were given but no tool returned records.")
        return PASS
    if not answer.sources:
        return GuardResult(False, "sources", "Sources are empty.")
    ids_by_tool: dict[str, set[str]] = {}
    for run in runs:
        ids_by_tool.setdefault(run.name, set()).update(run.record_ids)
    for s in answer.sources:
        tool, record_id = s.get("tool"), s.get("recordId")
        if tool not in ids_by_tool:
            return GuardResult(False, "sources", f"Source names a tool that was not called: {tool}.")
        if record_id not in ids_by_tool[tool]:
            return GuardResult(False, "sources", f"Source record {record_id} is not in the results of {tool}.")
    return PASS
