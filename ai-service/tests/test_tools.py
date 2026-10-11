"""Role: Tests for the eight catalog tools and the registry. They use
httpx.MockTransport, so no API key and no network are needed.
"""

import httpx
import pytest

from app.tools.api_client import ReadOnlyApiClient, ReadOnlyViolation
from app.tools.registry import ToolRegistry

ITEMS = [
    {"id": 1, "code": "RNG-0142", "name": "Zork Ring", "categoryId": 2, "designCode": "DSN-ZORK", "metalType": "Gold",
     "purity": "22K", "grossWt": 42.758, "netWt": 42.758, "stoneWt": 0.0, "makingType": "percent", "makingValue": 6.58,
     "huid": "D8PCF3", "status": "Complete", "imageUrl": None},
    {"id": 2, "code": "RNG-0002", "name": "Classic Ring", "categoryId": 2, "designCode": "DSN-102", "metalType": "Gold",
     "purity": "18K", "grossWt": 5.167, "netWt": 5.004, "stoneWt": 0.163, "makingType": "percent", "makingValue": 10.52,
     "huid": None, "status": "Complete", "imageUrl": None},
    {"id": 3, "code": "CHN-0003", "name": "Silver Chain", "categoryId": 5, "designCode": "DSN-300", "metalType": "Silver",
     "purity": "925", "grossWt": 10.0, "netWt": 10.0, "stoneWt": 0.0, "makingType": "flat", "makingValue": 2.0,
     "huid": None, "status": "Draft", "imageUrl": None},
]
CATEGORIES = [{"id": 1, "name": "Jewelry", "parentId": None}, {"id": 2, "name": "Rings", "parentId": 1},
              {"id": 5, "name": "Chains", "parentId": 1}]
STORES = [{"id": 1, "name": "Dubai", "city": "Dubai"}, {"id": 2, "name": "Chennai", "city": "Chennai"}]
STOCK = {1: [{"itemId": 1, "storeId": 1, "onHand": 4, "reserved": 1}, {"itemId": 1, "storeId": 2, "onHand": 2, "reserved": 0}],
         2: [{"itemId": 2, "storeId": 1, "onHand": 1, "reserved": 0}, {"itemId": 2, "storeId": 2, "onHand": 0, "reserved": 0}],
         3: []}
RATES = [{"id": 2, "metalType": "Gold", "purity": "22K", "ratePerGram": 1.0, "currency": "AED", "effectiveDate": "2026-09-30"},
         {"id": 1, "metalType": "Gold", "purity": "24K", "ratePerGram": 72.0, "currency": "AED", "effectiveDate": "2026-09-30"},
         {"id": 4, "metalType": "Silver", "purity": "925", "ratePerGram": 0.9, "currency": "AED", "effectiveDate": "2026-09-30"}]
PRICE = {"itemId": 1, "currency": "AED", "netWeight": 42.758, "ratePerGram": 1.0, "metalValue": 42.76,
         "makingType": "percent", "makingValue": 6.58, "makingCharge": 2.81, "totalPrice": 45.57}


class FakeApi:
    """A tiny stand-in for the .NET API. It records every request method it sees."""

    def __init__(self, items=None, categories=None, stores=None, rates=None) -> None:
        self.items = ITEMS if items is None else items
        self.categories = CATEGORIES if categories is None else categories
        self.stores = STORES if stores is None else stores
        self.rates = RATES if rates is None else rates
        self.methods: list[str] = []

    def __call__(self, request: httpx.Request) -> httpx.Response:
        self.methods.append(request.method)
        path, q = request.url.path, dict(request.url.params)
        if path == "/api/items":
            rows = self.items
            if q.get("search"):
                s = q["search"].lower()
                rows = [i for i in rows if s in i["code"].lower() or s in i["name"].lower()]
            for param, key in (("category", "categoryId"), ("metal", "metalType"), ("purity", "purity"), ("status", "status")):
                if q.get(param):
                    rows = [i for i in rows if str(i[key]).lower() == q[param].lower()]
            size, page = int(q.get("pageSize", 20)), int(q.get("page", 1))
            return httpx.Response(200, json={"items": rows[(page - 1) * size:page * size], "page": page,
                                             "pageSize": size, "totalCount": len(rows)})
        parts = path.strip("/").split("/")
        if len(parts) >= 3 and parts[:2] == ["api", "items"]:
            item_id = int(parts[2])
            item = next((i for i in self.items if i["id"] == item_id), None)
            if item is None:
                return httpx.Response(404)
            if len(parts) == 3:
                return httpx.Response(200, json=item)
            if parts[3] == "stock":
                return httpx.Response(200, json=STOCK.get(item_id, []))
            if parts[3] == "price":
                if item_id == 1:
                    return httpx.Response(200, json=PRICE)
                return httpx.Response(422, text="No latest metal rate exists for this item.")
        if path == "/api/categories":
            return httpx.Response(200, json=self.categories)
        if path == "/api/stores":
            return httpx.Response(200, json=self.stores)
        if path == "/api/metal-rates/latest":
            return httpx.Response(200, json=self.rates)
        return httpx.Response(404)


def make_registry(fake: FakeApi | None = None) -> tuple[ToolRegistry, FakeApi]:
    fake = fake or FakeApi()
    client = ReadOnlyApiClient("http://api.test", transport=httpx.MockTransport(fake))
    return ToolRegistry(client), fake


def down_registry() -> ToolRegistry:
    def refuse(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("connection refused", request=request)
    return ToolRegistry(ReadOnlyApiClient("http://api.test", transport=httpx.MockTransport(refuse)))


def assert_shape(result: dict) -> None:
    assert set(result) == {"status", "data", "sources"}
    assert result["status"] in ("ok", "not_found", "error")
    assert isinstance(result["sources"], list)


# Valid arguments for every tool, used by the API-down test.
VALID_ARGS = {
    "search_items": {"query": "ring"},
    "get_item_details": {"itemId": 1},
    "get_item_price_breakdown": {"itemId": 1},
    "get_stock_by_store": {"itemId": 1},
    "list_low_stock": {"threshold": 2},
    "list_categories": {},
    "get_latest_metal_rate": {"metal": "Gold"},
    "list_items_by_status": {"status": "Complete"},
}


# ---------- Zork Ring ----------

def test_run_tool_search_items_returns_zork_ring():
    registry, _ = make_registry()
    result = registry.run_tool("search_items", {"query": "zork"})
    assert_shape(result)
    assert result["status"] == "ok"
    assert result["data"]["items"][0]["name"] == "Zork Ring"
    assert result["data"]["items"][0]["code"] == "RNG-0142"
    assert result["sources"] == [{"tool": "search_items", "recordId": 1}]


# ---------- ok ----------

def test_search_items_filters_by_category_name_and_status():
    registry, _ = make_registry()
    result = registry.run_tool("search_items", {"category": "rings", "status": "complete"})
    assert result["status"] == "ok"
    assert [i["id"] for i in result["data"]["items"]] == [1, 2]


def test_get_item_details_by_id_and_by_code():
    registry, _ = make_registry()
    by_id = registry.run_tool("get_item_details", {"itemId": 1})
    by_code = registry.run_tool("get_item_details", {"code": "rng-0142"})
    assert by_id["status"] == by_code["status"] == "ok"
    assert by_id["data"]["name"] == by_code["data"]["name"] == "Zork Ring"
    assert by_code["sources"] == [{"tool": "get_item_details", "recordId": 1}]


def test_get_item_price_breakdown_ok():
    registry, _ = make_registry()
    result = registry.run_tool("get_item_price_breakdown", {"itemId": 1})
    assert result["status"] == "ok"
    assert result["data"]["totalPrice"] == 45.57


def test_get_stock_by_store_ok_joins_store_names():
    registry, _ = make_registry()
    result = registry.run_tool("get_stock_by_store", {"itemId": 1})
    assert result["status"] == "ok"
    assert result["data"]["totalOnHand"] == 6
    assert result["data"]["stores"][0] == {"storeId": 1, "storeName": "Dubai", "city": "Dubai",
                                           "onHand": 4, "reserved": 1, "available": 3}


def test_list_low_stock_ok_across_stores_and_for_one_store():
    registry, _ = make_registry()
    everywhere = registry.run_tool("list_low_stock", {"threshold": 1})
    assert everywhere["status"] == "ok"
    assert [r["itemId"] for r in everywhere["data"]["items"]] == [3, 2]  # 0 and 1 on hand
    chennai = registry.run_tool("list_low_stock", {"threshold": 0, "store": "Chennai"})
    assert [r["itemId"] for r in chennai["data"]["items"]] == [2, 3]


def test_list_categories_ok():
    registry, _ = make_registry()
    result = registry.run_tool("list_categories", {})
    assert result["status"] == "ok"
    assert len(result["data"]) == 3
    assert result["sources"][0] == {"tool": "list_categories", "recordId": 1}


def test_get_latest_metal_rate_ok_with_and_without_purity():
    registry, _ = make_registry()
    one = registry.run_tool("get_latest_metal_rate", {"metal": "gold", "purity": "22k"})
    assert one["status"] == "ok" and one["data"][0]["ratePerGram"] == 1.0
    both = registry.run_tool("get_latest_metal_rate", {"metal": "Gold"})
    assert len(both["data"]) == 2


def test_list_items_by_status_ok():
    registry, _ = make_registry()
    result = registry.run_tool("list_items_by_status", {"status": "Draft"})
    assert result["status"] == "ok"
    assert [i["code"] for i in result["data"]["items"]] == ["CHN-0003"]


# ---------- not_found ----------

@pytest.mark.parametrize("name,args", [
    ("search_items", {"query": "nothing-matches"}),
    ("search_items", {"category": "Watches"}),
    ("get_item_details", {"itemId": 99}),
    ("get_item_details", {"code": "NOPE-1"}),
    ("get_item_price_breakdown", {"itemId": 99}),
    ("get_item_price_breakdown", {"itemId": 3}),  # 422: no metal rate
    ("get_stock_by_store", {"itemId": 99}),
    ("get_stock_by_store", {"itemId": 3}),  # no stock records
    ("list_low_stock", {"threshold": 0, "store": "Nowhere"}),
    ("get_latest_metal_rate", {"metal": "Platinum"}),
    ("list_items_by_status", {"status": "With Smith"}),
])
def test_not_found(name, args):
    registry, _ = make_registry()
    result = registry.run_tool(name, args)
    assert_shape(result)
    assert result["status"] == "not_found"
    assert result["sources"] == []


def test_not_found_when_lists_are_empty():
    registry, _ = make_registry(FakeApi(items=[], categories=[], rates=[]))
    assert registry.run_tool("list_categories", {})["status"] == "not_found"
    assert registry.run_tool("list_low_stock", {"threshold": 5})["status"] == "not_found"
    assert registry.run_tool("get_latest_metal_rate", {"metal": "Gold"})["status"] == "not_found"


# ---------- API down ----------

@pytest.mark.parametrize("name", list(VALID_ARGS))
def test_api_down_returns_error_not_exception(name):
    result = down_registry().run_tool(name, VALID_ARGS[name])
    assert_shape(result)
    assert result["status"] == "error"
    assert "not reachable" in result["data"]["message"]


def test_api_server_error_returns_error():
    client = ReadOnlyApiClient("http://api.test", transport=httpx.MockTransport(lambda r: httpx.Response(500)))
    result = ToolRegistry(client).run_tool("list_categories", {})
    assert result["status"] == "error"


# ---------- bad arguments ----------

@pytest.mark.parametrize("name,args", [
    ("search_items", {"unknown": 1}),                      # unknown field
    ("search_items", {"page": 0}),                          # below minimum
    ("search_items", {"pageSize": 500}),                    # above maximum
    ("search_items", {"status": "Sold"}),                   # not an allowed status
    ("get_item_details", {}),                               # neither id nor code
    ("get_item_details", {"itemId": 1, "code": "RNG-0142"}),  # both
    ("get_item_details", {"itemId": "abc"}),                # wrong type
    ("get_item_price_breakdown", {"itemId": 0}),
    ("get_stock_by_store", {}),                             # missing required
    ("list_low_stock", {"threshold": -1}),
    ("list_categories", {"extra": True}),                   # takes no arguments
    ("get_latest_metal_rate", {}),
    ("list_items_by_status", {"status": "Complete", "page": 2}),
])
def test_bad_arguments_return_error_without_calling_the_api(name, args):
    registry, fake = make_registry()
    result = registry.run_tool(name, args)
    assert_shape(result)
    assert result["status"] == "error"
    assert "Invalid arguments" in result["data"]["message"]
    assert fake.methods == []


def test_non_dict_arguments_return_error():
    registry, _ = make_registry()
    assert registry.run_tool("list_categories", ["not", "a", "dict"])["status"] == "error"  # type: ignore[arg-type]


# ---------- registry ----------

def test_unknown_tool_returns_error():
    registry, _ = make_registry()
    result = registry.run_tool("delete_everything", {})
    assert_shape(result)
    assert result["status"] == "error"


def test_declarations_list_all_eight_tools_with_json_schema():
    registry, _ = make_registry()
    declarations = registry.declarations()
    assert sorted(d["name"] for d in declarations) == sorted(VALID_ARGS)
    for d in declarations:
        assert d["description"]
        assert d["parameters"]["type"] == "object"
        assert "title" not in d["parameters"] and "additionalProperties" not in d["parameters"]
    search = next(d for d in declarations if d["name"] == "search_items")
    assert search["parameters"]["properties"]["status"]["enum"] == ["Draft", "With Smith", "Received", "Complete"]
    assert search["parameters"]["properties"]["query"]["type"] == "string"
    details = next(d for d in declarations if d["name"] == "get_item_details")
    assert "required" not in details["parameters"]


# ---------- read-only guard ----------

@pytest.mark.parametrize("method", ["POST", "PUT", "PATCH", "DELETE"])
def test_client_rejects_non_get_methods(method):
    fake = FakeApi()
    client = ReadOnlyApiClient("http://api.test", transport=httpx.MockTransport(fake))
    with pytest.raises(ReadOnlyViolation):
        client.request(method, "/api/items")
    assert fake.methods == []


def test_guard_also_blocks_writes_made_on_the_underlying_http_client():
    fake = FakeApi()
    client = ReadOnlyApiClient("http://api.test", transport=httpx.MockTransport(fake))
    with pytest.raises(ReadOnlyViolation):
        client._http.post("/api/items", json={})  # noqa: SLF001 - proves the hook cannot be bypassed
    assert fake.methods == []


def test_tools_only_send_get_requests():
    registry, fake = make_registry()
    for name, args in VALID_ARGS.items():
        registry.run_tool(name, args)
    assert fake.methods and set(fake.methods) == {"GET"}
