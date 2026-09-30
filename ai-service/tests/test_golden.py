"""Role: The golden set, 40 questions with expected answers computed from the
sample data. Offline mode plays the ideal model (OracleProvider) to prove the
tools, the fixture and the guards accept a correct, grounded answer. Live mode
asks the real model. Pass target: 90 percent or more.
"""

import pytest

from tests.expected import NOT_FOUND_WORDS, Case, build_case, fact_in_answer, load_golden
from tests.harness import OracleProvider, Outcome, ask, data, invariant_problems
from tests.live_support import run_live_set

CASES = [build_case(entry, data()) for entry in load_golden()]


def grade(case: Case, outcome: Outcome) -> tuple[bool, str]:
    """Checks one golden answer. Returns (passed, what went wrong with the answer)."""
    if outcome.refused:
        return False, f"Refused: {outcome.answer}"
    problems = invariant_problems(outcome)
    problems += [f"missing {kind} {value}" for kind, value in case.facts if not fact_in_answer((kind, value), outcome.answer)]
    if case.expects_sources and not outcome.result.sources:
        problems.append("sources are empty")
    if not case.expects_sources and not NOT_FOUND_WORDS.search(outcome.answer):
        problems.append("the answer does not say no record was found")
    return (not problems), f"{'; '.join(problems)} | answer: {outcome.answer}" if problems else ""


@pytest.mark.parametrize("case", CASES, ids=[c.id for c in CASES])
def test_golden_offline(case: Case):
    outcome = ask(OracleProvider(case.calls, case.answer), case.question)
    passed, detail = grade(case, outcome)
    assert passed, f"{case.question} -> {detail}"
    assert outcome.runs, "the ideal plan must call a tool"


@pytest.mark.live
def test_golden_live():
    cases = [{"question": c.question, "case": c} for c in CASES]
    run_live_set("golden", cases, lambda entry, outcome: grade(entry["case"], outcome), target=0.90)
