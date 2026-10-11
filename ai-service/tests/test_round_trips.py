"""Role: Tests that list tools return stock with each item, so one tool call
answers stock questions for any filter combination (no hardcoded questions).
Counts are computed from the sample data by code, and the Oracle provider plays
the ideal answer so the guards are tested as well.
"""

import pytest

from app.tools.registry import ToolRegistry
from tests.expected import fact_in_answer, low_stock, num
from tests.harness import OracleProvider, RecordingRegistry, ask, data, invariant_problems


def on_hand(item_id: int, store_id: int | None = None) -> int:
    return sum(s["onHand"] for s in data().stock_for(item_id) if store_id is None or s["storeId"] == store_id)


def matching(category: str | None = None, metal: str | None = None, purity: str | None = None, status: str | None = None):
    rows = data().items
    if category:
        rows = [i for i in rows if i["categoryId"] == data().category_id(category)]
    if metal:
        rows = [i for i in rows if i["metalType"] == metal]
    if purity:
        rows = [i for i in rows if i["purity"] == purity]
    if status:
        rows = [i for i in rows if i["status"] == status]
    return rows


def run(args: dict) -> dict:
    return RecordingRegistry().run_tool("search_items", args)


def test_every_item_in_a_search_includes_total_on_hand():
    result = run({"category": "Rings", "pageSize": 50})
    assert result["status"] == "ok"
    for item in result["data"]["items"]:
        assert item["onHand"] == on_hand(item["id"])


def test_list_items_by_status_includes_on_hand():
    result = RecordingRegistry().run_tool("list_items_by_status", {"status": "Draft"})
    assert [i["onHand"] for i in result["data"]["items"]] == [on_hand(i["id"]) for i in matching(status="Draft")]


@pytest.mark.parametrize("filters", [
    {"category": "Rings", "metal": "Gold", "purity": "22K"},
    {"category": "Necklaces", "metal": "Gold", "purity": "18K"},
    {"metal": "Silver"},
    {"purity": "24K"},
    {"category": "Bangles"},
    {"status": "Complete"},
])
def test_in_stock_only_counts_match_the_data(filters):
    expected = [i for i in matching(**filters) if on_hand(i["id"]) > 0]
    result = run({**filters, "inStockOnly": True, "pageSize": 50})
    assert expected, "the data should have at least one in-stock match for this filter"
    assert result["status"] == "ok"
    assert result["data"]["totalCount"] == len(expected)
    assert [i["id"] for i in result["data"]["items"]] == [i["id"] for i in expected]
    assert all(i["onHand"] > 0 for i in result["data"]["items"])


@pytest.mark.parametrize("filters", [
    {"category": "Rings", "metal": "Gold", "purity": "22K"},
    {"category": "Chains"},
    {"metal": "Silver"},
])
def test_how_many_in_stock_takes_one_tool_call_for_any_filter(filters):
    expected = len([i for i in matching(**filters) if on_hand(i["id"]) > 0])
    label = " ".join(filters.values())
    oracle = OracleProvider([("search_items", {**filters, "inStockOnly": True, "pageSize": 1})], f"There are {expected} {label} items in stock.")
    outcome = ask(oracle, f"How many {label} items are in stock?")
    assert not outcome.refused and invariant_problems(outcome) == []
    assert fact_in_answer(num(expected), outcome.answer)
    assert len(outcome.runs) == 1
    assert oracle.requests == 2, "one model call to pick the tool and one to answer"


def test_total_units_across_items_is_an_allowed_number():
    rings = [i for i in matching(category="Rings", metal="Gold", purity="22K")]
    total = sum(on_hand(i["id"]) for i in rings)
    oracle = OracleProvider([("search_items", {"category": "Rings", "metal": "Gold", "purity": "22K", "pageSize": 50})],
                            f"Together these rings have {total} units on hand.")
    outcome = ask(oracle, "How many units of 22K gold rings do we have?")
    assert not outcome.refused and invariant_problems(outcome) == []


def test_in_stock_paging_uses_the_filtered_list():
    everything = run({"inStockOnly": True, "pageSize": 50})["data"]["items"]
    page2 = run({"inStockOnly": True, "pageSize": 5, "page": 2})
    assert [i["id"] for i in page2["data"]["items"]] == [i["id"] for i in everything[5:10]]
    assert page2["data"]["totalCount"] == len(everything)


def test_empty_results_are_not_found():
    assert run({"category": "Zorbs", "inStockOnly": True})["status"] == "not_found"
    assert run({"category": "Rings", "page": 99})["status"] == "not_found"
    assert run({"category": "Rings", "inStockOnly": True, "page": 99})["status"] == "not_found"


def test_stock_for_many_items_is_fetched_with_get_only():
    registry = RecordingRegistry()
    registry.run_tool("search_items", {"inStockOnly": True, "pageSize": 50})
    assert set(registry.api.methods) == {"GET"}
    assert registry.api.methods.count("GET") > 30  # one stock request per item, run side by side


def test_low_stock_still_matches_the_data_after_the_change():
    for threshold, store in [(5, None), (7, None), (3, "Dubai")]:
        args = {"threshold": threshold, **({"store": store} if store else {})}
        result = RecordingRegistry().run_tool("list_low_stock", args)
        expected = low_stock(data(), threshold, store)
        assert result["data"]["count"] == len(expected)
        assert [i["itemId"] for i in result["data"]["items"]] == [r["itemId"] for r in expected][:50]


def test_api_down_during_parallel_stock_lookup_is_an_error_result():
    from tests.harness import unreachable_client
    result = ToolRegistry(unreachable_client()).run_tool("search_items", {"inStockOnly": True})
    assert result["status"] == "error" and "not reachable" in result["data"]["message"]


def test_in_stock_argument_must_be_a_boolean():
    assert run({"inStockOnly": "maybe"})["status"] == "error"
