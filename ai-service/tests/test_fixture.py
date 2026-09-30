"""Role: Sanity tests for the Phase 8 fixture: the patches are applied to a
copy only, the test sets have the required size and coverage, and the fixture
API behaves like the real read endpoints.
"""

import json
from collections import Counter

from tests.expected import build_case, load_golden, load_out_of_scope
from tests.fixture_api import (INJECTED_CODES, INJECTED_DESCRIPTION, INJECTED_NAME, SAMPLE_DIR, apply_patches, load_sample,
                               patched_data)
from tests.harness import RecordingRegistry, data


def test_patches_do_not_change_the_loaded_or_on_disk_sample_data():
    before = load_sample()
    patched = apply_patches(before)
    assert len(before.items) == 30 and len(patched.items) == 32
    assert not any(c["name"] == "Zorbs" for c in before.categories)
    assert json.loads((SAMPLE_DIR / "items.json").read_text(encoding="utf-8")) == before.items


def test_poison_and_injection_patches_are_present():
    d = patched_data()
    assert d.item_by_code("RNG-0142")["name"] == "Zork Ring"
    assert d.latest_rate("Gold", "22K")["ratePerGram"] == 1.0
    assert any(c["name"] == "Zorbs" for c in d.categories)
    assert d.item_by_code(INJECTED_CODES["description"])["description"] == INJECTED_DESCRIPTION
    assert d.item_by_code(INJECTED_CODES["name"])["name"] == INJECTED_NAME


def test_golden_set_has_40_cases_covering_every_required_topic():
    golden = load_golden()
    assert len(golden) == 40
    kinds = Counter(g["kind"] for g in golden)
    assert {"count_by_category", "count_by_status", "count_by_metal", "count_by_purity", "net_weight", "gross_weight",
            "stock_by_store", "low_stock", "price_breakdown", "latest_rate", "list_draft", "not_found"} <= set(kinds)
    assert len({g["id"] for g in golden}) == 40


def test_every_golden_case_has_expected_facts_computed_from_data():
    for entry in load_golden():
        case = build_case(entry, data())
        assert case.question and case.calls
        assert case.facts or not case.expects_sources, entry["id"]


def test_out_of_scope_set_has_30_distinct_questions_including_arabic_tamil_and_hindi():
    questions = load_out_of_scope()
    assert len(questions) == 30 and len({q["question"] for q in questions}) == 30
    text = "".join(q["question"] for q in questions)
    assert any("؀" <= ch <= "ۿ" for ch in text)   # Arabic
    assert any("஀" <= ch <= "௿" for ch in text)   # Tamil
    assert any("ऀ" <= ch <= "ॿ" for ch in text)   # Hindi
    topics = {q["topic"] for q in questions}
    assert {"world_gold_price", "weather", "cricket", "investment_advice", "poem", "recipe", "code", "model_identity",
            "other_company", "write_data", "raw_sql", "ignore_rules"} <= topics


def test_fixture_api_price_matches_the_zork_ring_rule():
    price = RecordingRegistry().run_tool("get_item_price_breakdown", {"itemId": 1})
    assert price["status"] == "ok" and price["data"]["totalPrice"] == 45.57
