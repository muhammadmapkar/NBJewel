"""Role: The eight read-only catalog tools. Each tool has a pydantic argument
model (unknown fields are rejected) and a handler that calls GET endpoints of
the .NET API through ReadOnlyApiClient. No tool writes data or runs SQL.

Every handler returns {"status": "ok" | "not_found" | "error", "data": ..., "sources": [...]}.
"""

from dataclasses import dataclass
from typing import Any, Callable, Literal

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

from app.tools.api_client import ReadOnlyApiClient

ToolResult = dict[str, Any]
Status = Literal["Active", "Draft", "Discontinued"]
MAX_PAGE_SIZE = 100
LOW_STOCK_LIMIT = 50


# ---------- result helpers ----------

def source(tool: str, record_id: int | str) -> dict[str, Any]:
    return {"tool": tool, "recordId": record_id}


def ok(data: Any, sources: list[dict[str, Any]]) -> ToolResult:
    return {"status": "ok", "data": data, "sources": sources}


def not_found(message: str) -> ToolResult:
    return {"status": "not_found", "data": {"message": message}, "sources": []}


def error(message: str) -> ToolResult:
    return {"status": "error", "data": {"message": message}, "sources": []}


# ---------- argument models ----------

class _Args(BaseModel):
    model_config = ConfigDict(extra="forbid")


def _clean_text(value: Any) -> Any:
    if isinstance(value, str):
        value = value.strip()
        return value or None
    return value


def _title_status(value: Any) -> Any:
    value = _clean_text(value)
    return value.title() if isinstance(value, str) else value


class SearchItemsArgs(_Args):
    query: str | None = Field(None, max_length=100, description="Text to match in the item code or name.")
    category: str | None = Field(None, max_length=60, description="Category name (for example Rings) or category id.")
    metal: str | None = Field(None, max_length=30, description="Metal type, for example Gold or Silver.")
    purity: str | None = Field(None, max_length=10, description="Purity, for example 22K, 18K or 925.")
    status: Status | None = Field(None, description="Item status.")
    page: int = Field(1, ge=1, description="Page number, starting at 1.")
    pageSize: int = Field(10, ge=1, le=50, description="Items per page, 1 to 50.")

    _trim = field_validator("query", "category", "metal", "purity", mode="before")(_clean_text)
    _status = field_validator("status", mode="before")(_title_status)


class GetItemDetailsArgs(_Args):
    itemId: int | None = Field(None, ge=1, description="Numeric item id.")
    code: str | None = Field(None, max_length=40, description="Item code, for example RNG-0142.")

    _trim = field_validator("code", mode="before")(_clean_text)

    @model_validator(mode="after")
    def _exactly_one(self) -> "GetItemDetailsArgs":
        if (self.itemId is None) == (self.code is None):
            raise ValueError("Give either itemId or code, not both and not neither.")
        return self


class ItemIdArgs(_Args):
    itemId: int = Field(..., ge=1, description="Numeric item id.")


class ListLowStockArgs(_Args):
    threshold: int = Field(5, ge=0, le=1000, description="Items with this many units on hand or fewer are listed.")
    store: str | None = Field(None, max_length=60, description="Optional store name or store id to check only that store.")

    _trim = field_validator("store", mode="before")(_clean_text)


class NoArgs(_Args):
    pass


class GetLatestMetalRateArgs(_Args):
    metal: str = Field(..., min_length=1, max_length=30, description="Metal type, for example Gold or Silver.")
    purity: str | None = Field(None, max_length=10, description="Optional purity, for example 22K.")

    _trim = field_validator("metal", "purity", mode="before")(_clean_text)


class ListItemsByStatusArgs(_Args):
    status: Status = Field(..., description="Item status.")

    _status = field_validator("status", mode="before")(_title_status)


# ---------- handlers ----------

def _item_summary(item: dict[str, Any]) -> dict[str, Any]:
    keys = ("id", "code", "name", "categoryId", "metalType", "purity", "netWt", "status")
    return {k: item.get(k) for k in keys}


# Only these item fields are passed to the model. Free-text fields that are not part of the item
# contract (for example a description) could carry planted instructions, so they are dropped.
ITEM_FIELDS = ("id", "code", "name", "categoryId", "designCode", "metalType", "purity", "grossWt", "netWt",
               "stoneWt", "makingType", "makingValue", "huid", "status", "imageUrl")


def _clean_item(item: dict[str, Any]) -> dict[str, Any]:
    return {k: item.get(k) for k in ITEM_FIELDS}


def _item_label(client: ReadOnlyApiClient, item_id: int) -> dict[str, Any]:
    """The code and name of an item, so answers can name the item the numbers belong to."""
    status, item = client.get(f"/api/items/{item_id}")
    return {"itemCode": item.get("code"), "itemName": item.get("name")} if status == 200 and isinstance(item, dict) else {}


def _resolve_category(client: ReadOnlyApiClient, value: str) -> int | None:
    if value.isdigit():
        return int(value)
    _, categories = client.get("/api/categories")
    for category in categories if isinstance(categories, list) else []:
        if str(category.get("name", "")).lower() == value.lower():
            return int(category["id"])
    return None


def _fetch_all_items(client: ReadOnlyApiClient, params: dict[str, Any] | None = None) -> list[dict[str, Any]]:
    items: list[dict[str, Any]] = []
    page = 1
    while True:
        _, body = client.get("/api/items", {**(params or {}), "page": page, "pageSize": MAX_PAGE_SIZE})
        items.extend(body.get("items", []))
        if page * MAX_PAGE_SIZE >= body.get("totalCount", 0):
            return items
        page += 1


def search_items(client: ReadOnlyApiClient, args: SearchItemsArgs) -> ToolResult:
    category_id = None
    if args.category:
        category_id = _resolve_category(client, args.category)
        if category_id is None:
            return not_found(f"No category named '{args.category}'.")
    _, body = client.get("/api/items", {
        "search": args.query, "category": category_id, "metal": args.metal, "purity": args.purity,
        "status": args.status, "page": args.page, "pageSize": args.pageSize,
    })
    items = body.get("items", [])
    if not items:
        return not_found("No items match the search.")
    data = {"items": [_item_summary(i) for i in items], "page": body.get("page"),
            "pageSize": body.get("pageSize"), "totalCount": body.get("totalCount")}
    return ok(data, [source("search_items", i["id"]) for i in items])


def get_item_details(client: ReadOnlyApiClient, args: GetItemDetailsArgs) -> ToolResult:
    if args.itemId is not None:
        status, body = client.get(f"/api/items/{args.itemId}")
        if status == 404:
            return not_found(f"No item with id {args.itemId}.")
        item = body
    else:
        # The search matches part of a code, so keep only an exact code match.
        _, result = client.get("/api/items", {"search": args.code, "pageSize": MAX_PAGE_SIZE})
        matches = [i for i in result.get("items", []) if str(i.get("code", "")).lower() == (args.code or "").lower()]
        if not matches:
            return not_found(f"No item with code '{args.code}'.")
        item = matches[0]
    item = _clean_item(item)
    return ok(item, [source("get_item_details", item["id"])])


def get_item_price_breakdown(client: ReadOnlyApiClient, args: ItemIdArgs) -> ToolResult:
    status, body = client.get(f"/api/items/{args.itemId}/price")
    if status == 404:
        return not_found(f"No item with id {args.itemId}.")
    if status == 422:
        return not_found(str(body) if isinstance(body, str) else "No price can be calculated for this item.")
    return ok({**_item_label(client, args.itemId), **body}, [source("get_item_price_breakdown", args.itemId)])


def _stores_by_id(client: ReadOnlyApiClient) -> dict[int, dict[str, Any]]:
    _, stores = client.get("/api/stores")
    return {s["id"]: s for s in stores} if isinstance(stores, list) else {}


def get_stock_by_store(client: ReadOnlyApiClient, args: ItemIdArgs) -> ToolResult:
    status, records = client.get(f"/api/items/{args.itemId}/stock")
    if status == 404:
        return not_found(f"No item with id {args.itemId}.")
    if not records:
        return not_found("This item has no stock records.")
    stores = _stores_by_id(client)
    rows = [{
        "storeId": r["storeId"],
        "storeName": stores.get(r["storeId"], {}).get("name"),
        "city": stores.get(r["storeId"], {}).get("city"),
        "onHand": r["onHand"], "reserved": r["reserved"], "available": r["onHand"] - r["reserved"],
    } for r in records]
    data = {"itemId": args.itemId, **_item_label(client, args.itemId), "stores": rows, "totalOnHand": sum(r["onHand"] for r in rows)}
    return ok(data, [source("get_stock_by_store", args.itemId)])


def list_low_stock(client: ReadOnlyApiClient, args: ListLowStockArgs) -> ToolResult:
    # Assumed, no backend endpoint: this is composed from GET /api/items and each item's stock.
    store_id: int | None = None
    if args.store:
        stores = _stores_by_id(client)
        if args.store.isdigit() and int(args.store) in stores:
            store_id = int(args.store)
        else:
            store_id = next((sid for sid, s in stores.items() if str(s.get("name", "")).lower() == args.store.lower()), None)
        if store_id is None:
            return not_found(f"No store named '{args.store}'.")
    low: list[dict[str, Any]] = []
    for item in _fetch_all_items(client):
        _, records = client.get(f"/api/items/{item['id']}/stock")
        counted = [r for r in records if store_id is None or r["storeId"] == store_id]
        on_hand = sum(r["onHand"] for r in counted)
        if on_hand <= args.threshold:
            low.append({"itemId": item["id"], "code": item["code"], "name": item["name"], "onHand": on_hand})
    if not low:
        return not_found(f"No items have {args.threshold} or fewer units on hand.")
    low.sort(key=lambda r: (r["onHand"], r["itemId"]))
    shown = low[:LOW_STOCK_LIMIT]
    data = {"threshold": args.threshold, "store": args.store, "count": len(low), "items": shown}
    return ok(data, [source("list_low_stock", r["itemId"]) for r in shown])


def list_categories(client: ReadOnlyApiClient, args: NoArgs) -> ToolResult:
    _, categories = client.get("/api/categories")
    if not categories:
        return not_found("No categories exist.")
    return ok(categories, [source("list_categories", c["id"]) for c in categories])


def get_latest_metal_rate(client: ReadOnlyApiClient, args: GetLatestMetalRateArgs) -> ToolResult:
    _, rates = client.get("/api/metal-rates/latest")
    matches = [r for r in rates if str(r.get("metalType", "")).lower() == args.metal.lower()
               and (args.purity is None or str(r.get("purity", "")).lower() == args.purity.lower())]
    if not matches:
        return not_found("No metal rate matches that metal and purity.")
    return ok(matches, [source("get_latest_metal_rate", r["id"]) for r in matches])


def list_items_by_status(client: ReadOnlyApiClient, args: ListItemsByStatusArgs) -> ToolResult:
    _, body = client.get("/api/items", {"status": args.status, "page": 1, "pageSize": MAX_PAGE_SIZE})
    items = body.get("items", [])
    if not items:
        return not_found(f"No items have status {args.status}.")
    data = {"status": args.status, "totalCount": body.get("totalCount"),
            "items": [_item_summary(i) for i in items], "truncated": body.get("totalCount", 0) > len(items)}
    return ok(data, [source("list_items_by_status", i["id"]) for i in items])


# ---------- tool definitions ----------

@dataclass(frozen=True)
class Tool:
    name: str
    description: str
    args_model: type[BaseModel]
    handler: Callable[[ReadOnlyApiClient, Any], ToolResult]


ALL_TOOLS: list[Tool] = [
    Tool("search_items", "Search the item catalog by text, category, metal, purity or status. Returns a page of items.", SearchItemsArgs, search_items),
    Tool("get_item_details", "Get every field of one item, by item id or by item code.", GetItemDetailsArgs, get_item_details),
    Tool("get_item_price_breakdown", "Get the price breakdown of one item: net weight, rate per gram, metal value, making charge and total.", ItemIdArgs, get_item_price_breakdown),
    Tool("get_stock_by_store", "Get on hand, reserved and available stock of one item in each store.", ItemIdArgs, get_stock_by_store),
    Tool("list_low_stock", "List items with a threshold number of units on hand or fewer, across all stores or in one store.", ListLowStockArgs, list_low_stock),
    Tool("list_categories", "List all product categories.", NoArgs, list_categories),
    Tool("get_latest_metal_rate", "Get the latest rate per gram for a metal, optionally for one purity.", GetLatestMetalRateArgs, get_latest_metal_rate),
    Tool("list_items_by_status", "List items that have a given status (Active, Draft or Discontinued).", ListItemsByStatusArgs, list_items_by_status),
]
