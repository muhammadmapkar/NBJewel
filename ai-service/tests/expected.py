"""Role: Computes the expected answers for the golden questions from the
(patched) sample data, by code. Nothing is hardcoded: every fact is derived
from the data the fixture API serves.

Each golden entry in golden.json has a kind, parameters and a question text.
build_case() turns it into a Case with facts to look for, plus an ideal plan
(tool calls and answer) that the offline OracleProvider plays back.
"""

import json
import re
from dataclasses import dataclass, field
from decimal import Decimal
from pathlib import Path
from typing import Any

from app.guards.numbers import numbers_in_text
from tests.fixture_api import DataSet, price_for

TESTS_DIR = Path(__file__).parent
NOT_FOUND_WORDS = re.compile(r"no record|not found|no item|could not find|couldn't find|does not exist|doesn't exist|no match", re.I)


@dataclass
class Case:
    id: str
    kind: str
    question: str
    facts: list[tuple[str, Any]] = field(default_factory=list)  # ("num", Decimal) or ("text", str)
    calls: list[tuple[str, dict[str, Any]]] = field(default_factory=list)
    answer: str = ""
    expects_sources: bool = True


def num(value: float | int | Decimal) -> tuple[str, Decimal]:
    return "num", Decimal(str(value)).normalize()


def fact_in_answer(fact: tuple[str, Any], answer: str) -> bool:
    kind, value = fact
    if kind == "num":
        return value in numbers_in_text(answer)
    return str(value).lower() in answer.lower()


def load_golden() -> list[dict[str, Any]]:
    return json.loads((TESTS_DIR / "golden.json").read_text(encoding="utf-8"))


def load_out_of_scope() -> list[dict[str, Any]]:
    return json.loads((TESTS_DIR / "out_of_scope.json").read_text(encoding="utf-8"))


def _count(data: DataSet, **filters: str) -> int:
    rows = data.items
    if "category" in filters:
        rows = [i for i in rows if i["categoryId"] == data.category_id(filters["category"])]
    for key, field_name in (("metal", "metalType"), ("purity", "purity"), ("status", "status")):
        if key in filters:
            rows = [i for i in rows if i[field_name].lower() == filters[key].lower()]
    return len(rows)


def low_stock(data: DataSet, threshold: int, store: str | None = None) -> list[dict[str, Any]]:
    store_id = next(s["id"] for s in data.stores if s["name"] == store) if store else None
    found = []
    for item in data.items:
        available = sum(s["onHand"] - s.get("reserved", 0) for s in data.stock_for(item["id"]) if store_id is None or s["storeId"] == store_id)
        if available < threshold:
            found.append({"itemId": item["id"], "code": item["code"], "name": item["name"], "available": available})
    return sorted(found, key=lambda r: (r["available"], r["itemId"]))


def build_case(entry: dict[str, Any], data: DataSet) -> Case:
    kind, p = entry["kind"], entry.get("params", {})
    item = data.items[p["itemIndex"]] if "itemIndex" in p else None
    values = {**p, **({"code": item["code"], "name": item["name"]} if item else {})}
    case = Case(entry["id"], kind, entry["question"].format(**values))

    if kind in ("count_by_category", "count_by_metal", "count_by_purity", "count_by_combo"):
        filters = {k: p[k] for k in ("category", "metal", "purity") if k in p}
        n = _count(data, **filters)
        label = " ".join(str(v) for v in filters.values())
        case.calls = [("search_items", {**filters, "pageSize": 1})]
        case.answer, case.facts = f"There are {n} items matching {label}.", [num(n)]
    elif kind == "count_by_status":
        n = _count(data, status=p["status"])
        case.calls = [("list_items_by_status", {"status": p["status"]})]
        case.answer, case.facts = f"There are {n} items with status {p['status']}.", [num(n)]
        if n == 0:  # the tool answers not_found when no item has the status, so there are no sources
            case.answer, case.facts, case.expects_sources = f"No items have status {p['status']}.", [], False
    elif kind in ("net_weight", "gross_weight"):
        field_name = "netWt" if kind == "net_weight" else "grossWt"
        case.calls = [("get_item_details", {"code": item["code"]})]
        case.answer = f"The {kind.split('_')[0]} weight of {item['code']} ({item['name']}) is {item[field_name]} g."
        case.facts = [num(item[field_name])]
    elif kind == "stock_by_store":
        rows = data.stock_for(item["id"])
        total = sum(r["onHand"] for r in rows)
        case.calls = [("get_stock_by_store", {"itemId": item["id"]})]
        parts = ", ".join(f"{data.store_name(r['storeId'])} {r['onHand']}" for r in rows)
        case.answer = f"{item['code']} has {total} units in total: {parts}."
        case.facts = [num(total)] + [num(r["onHand"]) for r in rows]
    elif kind == "low_stock":
        found = low_stock(data, p["threshold"], p.get("store"))
        assert found, f"golden case {entry['id']} needs at least one low-stock item"
        args: dict[str, Any] = {"threshold": p["threshold"], **({"store": p["store"]} if p.get("store") else {})}
        shown = found[:3]
        case.calls = [("list_low_stock", args)]
        case.answer = f"{len(found)} items have fewer than {p['threshold']} units available, for example " + ", ".join(r["code"] for r in shown) + "."
        case.facts = [num(len(found)), ("text", shown[0]["code"])]
    elif kind == "price_breakdown":
        price = price_for(item, data)
        case.calls = [("get_item_price_breakdown", {"itemId": item["id"]})]
        case.answer = f"The total price of {item['code']} is {price['totalPrice']} {price['currency']}, with a making charge of {price['makingCharge']}."
        case.facts = [num(price["totalPrice"]), num(price["makingCharge"])]
    elif kind == "latest_rate":
        rate = data.latest_rate(p["metal"], p["purity"])
        case.calls = [("get_latest_metal_rate", {"metal": p["metal"], "purity": p["purity"]})]
        case.answer = f"The latest {p['metal']} {p['purity']} rate is {rate['ratePerGram']} AED per gram."
        case.facts = [num(rate["ratePerGram"])]
    elif kind == "list_draft":
        drafts = [i for i in data.items if i["status"] == "Draft"]
        case.calls = [("list_items_by_status", {"status": "Draft"})]
        case.answer = f"{len(drafts)} items are Draft: " + ", ".join(i["code"] for i in drafts) + "."
        case.facts = [num(len(drafts))] + [("text", i["code"]) for i in drafts]
    elif kind == "not_found":
        assert data.item_by_code(p["code"]) is None, f"{p['code']} must not exist"
        case.calls = [("get_item_details", {"code": p["code"]})]
        case.answer = f"No record found for item code {p['code']}."
        case.expects_sources = False
    else:
        raise ValueError(f"Unknown golden kind {kind}")
    return case
