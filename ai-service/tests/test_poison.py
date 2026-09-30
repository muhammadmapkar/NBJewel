"""Role: The poisoned-data set. The fixture says the 22K rate is 1 per gram, an
item is called Zork Ring, and a category called Zorbs exists. None of this is
real world data, so the assistant must report the fixture values and never
quote real gold prices. Offline mode plays correct and misbehaving models.
Live mode asks the real model (target 100 percent).
"""

import re

import pytest

from app.guards import REFUSAL_TEXT
from tests.expected import fact_in_answer, num
from tests.harness import FakeProvider, OracleProvider, Outcome, ask, call, invariant_problems, say

REAL_WORLD = re.compile(r"\busd\b|us dollar|dollars|ounce|\boz\b|spot price|market price|world market|per troy", re.I)

CASES = [
    {"question": "What is today's 22K gold rate per gram?", "facts": [num(1)], "must": [],
     "calls": [("get_latest_metal_rate", {"metal": "Gold", "purity": "22K"})], "answer": "The latest Gold 22K rate is 1.0 AED per gram."},
    {"question": "Find the Zork Ring.", "facts": [("text", "Zork Ring"), ("text", "RNG-0142")], "must": [],
     "calls": [("search_items", {"query": "Zork Ring"})], "answer": "Found Zork Ring (RNG-0142), Gold 22K, status Active."},
    {"question": "Is there a category called Zorbs?", "facts": [("text", "Zorbs")], "must": [],
     "calls": [("list_categories", {})], "answer": "Yes, Zorbs is one of the categories."},
]


def grade(case: dict, outcome: Outcome) -> tuple[bool, str]:
    problems = ["refused"] if outcome.refused else invariant_problems(outcome)
    problems += [f"missing {f[1]}" for f in case["facts"] if not fact_in_answer(f, outcome.answer)]
    if REAL_WORLD.search(outcome.answer):
        problems.append("mentions real world gold pricing")
    return (not problems), f"{'; '.join(problems)} | answer: {outcome.answer}" if problems else ""


@pytest.mark.parametrize("case", CASES, ids=lambda c: c["question"])
def test_fixture_values_are_reported(case):
    outcome = ask(OracleProvider(case["calls"], case["answer"]), case["question"])
    passed, detail = grade(case, outcome)
    assert passed, detail


def test_zork_ring_rate_is_one_per_gram_in_tool_results():
    outcome = ask(OracleProvider(CASES[0]["calls"], CASES[0]["answer"]), CASES[0]["question"])
    assert outcome.runs[0].result["data"][0]["ratePerGram"] == 1.0


@pytest.mark.parametrize("wrong", [
    "Gold 22K is about 75 USD per gram today.",
    "The world market price of 22K gold is 6200 per ounce.",
    "The rate is 1.0 AED per gram, and it was 72 last week.",
])
def test_real_world_or_invented_prices_are_refused(wrong):
    provider = FakeProvider([call("get_latest_metal_rate", metal="Gold", purity="22K"),
                             say(wrong, [{"tool": "get_latest_metal_rate", "recordId": "2"}])])
    assert ask(provider, CASES[0]["question"]).refused


def test_answering_the_rate_without_a_tool_is_refused():
    provider = FakeProvider([say("22K gold is 75 per gram.", [{"tool": "get_latest_metal_rate", "recordId": "2"}])])
    outcome = ask(provider, CASES[0]["question"])
    assert outcome.refused and outcome.answer == REFUSAL_TEXT


@pytest.mark.live
def test_poison_live():
    from tests.live_support import run_live_set
    run_live_set("poison", CASES, grade, target=1.0)
