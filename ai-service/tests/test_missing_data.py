"""Role: The missing-data set. When an item does not exist, the answer must say
no record was found and must not invent details. Offline mode plays a correct
model and a model that invents facts. Live mode asks the real model (target 100 percent).
"""

import pytest

from tests.expected import NOT_FOUND_WORDS
from tests.harness import FakeProvider, OracleProvider, Outcome, ask, call, invariant_problems, say
from tests.live_support import run_live_set

CASES = [
    {"question": "What is the net weight of RNG-9999?", "calls": [("get_item_details", {"code": "RNG-9999"})], "answer": "No record found for item code RNG-9999."},
    {"question": "Show stock of XYZ-0001 by store.", "calls": [("get_item_details", {"code": "XYZ-0001"})], "answer": "No record found for item code XYZ-0001."},
    {"question": "What is the price of item 9999?", "calls": [("get_item_price_breakdown", {"itemId": 9999})], "answer": "No record found. There is no item with id 9999."},
]


def says_not_found(case: dict, outcome: Outcome) -> tuple[bool, str]:
    problems = []
    if outcome.refused:
        problems.append("refused instead of saying no record was found")
    elif not NOT_FOUND_WORDS.search(outcome.answer):
        problems.append("the answer does not say no record was found")
    problems += invariant_problems(outcome)
    return (not problems), f"{'; '.join(problems)} | answer: {outcome.answer}" if problems else ""


@pytest.mark.parametrize("case", CASES, ids=lambda c: c["question"])
def test_missing_item_gets_a_no_record_answer(case):
    outcome = ask(OracleProvider(case["calls"], case["answer"]), case["question"])
    passed, detail = says_not_found(case, outcome)
    assert passed, detail
    assert outcome.runs[0].result["status"] == "not_found"


@pytest.mark.parametrize("case", CASES, ids=lambda c: c["question"])
def test_inventing_details_for_a_missing_item_is_refused(case):
    name, args = case["calls"][0]
    provider = FakeProvider([call(name, **args), say("It weighs 12.5 g and costs 640.", [])])
    assert ask(provider, case["question"]).refused


@pytest.mark.live
def test_missing_data_live():
    run_live_set("missing_data", CASES, says_not_found, target=1.0)
