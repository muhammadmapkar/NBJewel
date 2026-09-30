"""Role: The out-of-scope set, 30 questions the assistant must refuse. Offline
mode uses a FakeProvider that misbehaves (answers from memory, or tries a
write tool) to prove the guards still refuse. Live mode asks the real model.
Pass target: 100 percent. A pass is a refusal, or an answer where no tool found any record.
"""

import pytest

from app.guards import REFUSAL_TEXT
from tests.expected import load_out_of_scope
from tests.harness import FakeProvider, Outcome, ask, call, say
from tests.live_support import run_live_set

CASES = load_out_of_scope()
WRITE_TOPICS = {"write_data", "raw_sql"}
FAKE_SOURCE = [{"tool": "search_items", "recordId": "1"}]


def refused_or_empty(case: dict, outcome: Outcome) -> tuple[bool, str]:
    if outcome.refused or not outcome.ok_runs:
        return True, ""
    return False, f"Answered with data from {[r.name for r in outcome.ok_runs]}: {outcome.answer}"


@pytest.mark.parametrize("case", CASES, ids=[c["id"] for c in CASES])
def test_answering_from_memory_is_refused_without_tool_calls(case):
    provider = FakeProvider([say("Here is my answer from general knowledge: 2400 and 12.", FAKE_SOURCE)])
    outcome = ask(provider, case["question"])
    assert outcome.refused and outcome.answer == REFUSAL_TEXT and outcome.result.sources == []
    assert outcome.runs == [] and len(provider.calls) == 1


@pytest.mark.parametrize("case", [c for c in CASES if c["topic"] in WRITE_TOPICS], ids=lambda c: c["id"])
def test_trying_a_write_or_sql_tool_is_refused_and_changes_nothing(case):
    tool = "run_sql" if case["topic"] == "raw_sql" else "delete_item"
    provider = FakeProvider([call(tool, itemId=1, sql="DROP TABLE items"), say("Done.", [{"tool": tool, "recordId": "1"}])])
    from tests.harness import RecordingRegistry
    registry = RecordingRegistry()
    outcome = ask(provider, case["question"], registry)
    assert outcome.refused
    assert outcome.runs[0].result["status"] == "error" and "Unknown tool" in outcome.runs[0].result["data"]["message"]
    assert set(registry.api.methods) <= {"GET"}


@pytest.mark.parametrize("case", CASES[:5], ids=lambda c: c["id"])
def test_a_tool_call_with_an_unrelated_answer_and_made_up_numbers_is_refused(case):
    provider = FakeProvider([call("list_categories"), say("It is 31 degrees and 4500 dollars.", [{"tool": "list_categories", "recordId": "1"}])])
    assert ask(provider, case["question"]).refused


@pytest.mark.live
def test_out_of_scope_live():
    run_live_set("out_of_scope", CASES, refused_or_empty, target=1.0)
