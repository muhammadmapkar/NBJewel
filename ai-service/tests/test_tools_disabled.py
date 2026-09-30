"""Role: The tools-disabled set. With zero tools in the registry, every question
must get the fixed refusal, because nothing can be grounded. Offline mode plays
models that answer from memory or try tools that do not exist. Live mode asks
the real model with no tools (target 100 percent).
"""

import pytest

from app.guards import REFUSAL_TEXT
from tests.expected import load_golden, build_case
from tests.harness import FakeProvider, Outcome, RecordingRegistry, ask, call, data, say
from tests.live_support import run_live_set

QUESTIONS = [build_case(e, data()).question for e in load_golden()]


def no_tools() -> RecordingRegistry:
    return RecordingRegistry(tools=[])


def is_refusal(case: dict, outcome: Outcome) -> tuple[bool, str]:
    return (outcome.refused and outcome.answer == REFUSAL_TEXT), f"Not refused: {outcome.answer}"


def test_an_empty_registry_has_no_tools_and_declares_none():
    registry = no_tools()
    assert registry.declarations() == []
    assert registry.run_tool("search_items", {"query": "ring"})["status"] == "error"


@pytest.mark.parametrize("question", QUESTIONS)
def test_every_question_is_refused_when_the_model_answers_from_memory(question):
    provider = FakeProvider([say("There are 7 of them, total 120 grams.", [{"tool": "search_items", "recordId": "1"}])])
    outcome = ask(provider, question, no_tools())
    assert outcome.refused and outcome.answer == REFUSAL_TEXT
    assert provider.calls[0]["tools"] == []


@pytest.mark.parametrize("question", QUESTIONS[:10])
def test_every_question_is_refused_when_the_model_tries_a_tool_that_does_not_exist(question):
    provider = FakeProvider([call("search_items", query="ring"), say("Found 6 rings.", [{"tool": "search_items", "recordId": "1"}])])
    outcome = ask(provider, question, no_tools())
    assert outcome.refused
    assert outcome.runs[0].result["status"] == "error"


@pytest.mark.live
def test_tools_disabled_live():
    run_live_set("tools_disabled", [{"question": q} for q in QUESTIONS], is_refusal, target=1.0, registry_factory=no_tools)
