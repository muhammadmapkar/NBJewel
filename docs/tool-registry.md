# Tool registry

The AI service gives the language model eight tools. They are the only way the assistant can see data. The code is in `ai-service/app/tools/` (`catalog_tools.py`, `registry.py`, `api_client.py`).

## Rules that apply to every tool

- **All tools are GET only.** Every request goes through `ReadOnlyApiClient`. Any method other than GET raises an error before anything is sent. The same check also runs as a hook on the underlying HTTP client, so it cannot be bypassed. No tool writes data and none accepts SQL.
- **Arguments are validated** with pydantic. Unknown fields are rejected. Bad arguments return `status: "error"` and no request is made.
- **Return shape** is always:

  ```json
  {"status": "ok | not_found | error", "data": {}, "sources": [{"tool": "name", "recordId": 1}]}
  ```

  - `ok`: `data` holds the result and `sources` lists the records it came from.
  - `not_found`: nothing matched. `data` is `{"message": "..."}` and `sources` is empty.
  - `error`: the API is not reachable, returned a server error, or the arguments were invalid. `data` is `{"message": "..."}`.
- `run_tool(name, args)` never raises. An unknown tool name returns `status: "error"`.
- `recordId` is a number in tool results. The chat response turns it into a string.
- Requests use a 10 second timeout. The base URL is `NBJEWEL_API_URL` (default `http://localhost:5155`).
- Examples below come from the sample data. Long lists are shortened.

## 1. search_items

- **Purpose:** Search the catalog by text, category, metal, purity or status.
- **Arguments:**
  - `query` (text, up to 100 characters, optional): matched against item code and name.
  - `category` (text, up to 60 characters, optional): a category name such as "Rings", or an id.
  - `metal` (optional), `purity` (optional).
  - `status` (optional): `Active`, `Draft` or `Discontinued`. Case is fixed automatically.
  - `inStockOnly` (true or false, default false): keep only items with at least 1 unit on hand in any store.
  - `page` (number, 1 or more, default 1) and `pageSize` (1 to 50, default 10).
- **Endpoints:** `GET /api/items`. If `category` is a name, `GET /api/categories` first to find its id. Then `GET /api/items/{id}/stock` for each item returned, run side by side, to fill in `onHand`. With `inStockOnly`, the tool reads every matching item and its stock, and pages the result itself, because the API cannot filter by stock. Assumed, no backend endpoint for stock in lists.
- **Returns:** `data.items` (id, code, name, categoryId, metalType, purity, netWt, status, `onHand`), `page`, `pageSize`, `totalCount`. `onHand` is the total units in all stores. With `inStockOnly`, `totalCount` counts only in-stock items. `not_found` if nothing matches, the page is empty, or the category name is unknown.
- **Why it matters:** one call answers "how many items are in stock" for any filter, so the model needs no extra round trip per item.
- **Example call:** `run_tool("search_items", {"query": "zork"})`

  ```json
  {"status": "ok", "data": {"items": [{"id": 1, "code": "RNG-0142", "name": "Zork Ring", "categoryId": 2, "metalType": "Gold", "purity": "22K", "netWt": 42.758, "status": "Active", "onHand": 7}], "page": 1, "pageSize": 10, "totalCount": 1}, "sources": [{"tool": "search_items", "recordId": 1}]}
  ```

## 2. get_item_details

- **Purpose:** Get the fields of one item, by id or by code.
- **Arguments:** exactly one of `itemId` (number, 1 or more) or `code` (text, up to 40 characters). Giving both or neither is an error.
- **Endpoints:** `GET /api/items/{id}`, or `GET /api/items?search={code}&pageSize=100` followed by an exact code match.
- **Returns:** the fifteen item fields (id, code, name, categoryId, designCode, metalType, purity, grossWt, netWt, stoneWt, makingType, makingValue, huid, status, imageUrl). Other fields are dropped on purpose, so free text in unexpected fields cannot reach the model. `not_found` if no item matches.
- **Example call:** `run_tool("get_item_details", {"code": "RNG-0142"})`

  ```json
  {"status": "ok", "data": {"id": 1, "code": "RNG-0142", "name": "Zork Ring", "categoryId": 2, "designCode": "DSN-ZORK", "metalType": "Gold", "purity": "22K", "grossWt": 42.758, "netWt": 42.758, "stoneWt": 0.0, "makingType": "percent", "makingValue": 6.58, "huid": "D8PCF3", "status": "Active", "imageUrl": "/images/RNG-0001.jpg"}, "sources": [{"tool": "get_item_details", "recordId": 1}]}
  ```

## 3. get_item_price_breakdown

- **Purpose:** Show how the price of one item is made up.
- **Arguments:** `itemId` (number, 1 or more).
- **Endpoints:** `GET /api/items/{id}/price`, and `GET /api/items/{id}` to add the item code and name.
- **Returns:** itemCode, itemName, itemId, currency, netWeight, ratePerGram, metalValue, makingType, makingValue, makingCharge, totalPrice. `not_found` if the item does not exist (404) or has no metal rate (422, the API's message is passed on).
- **Example call:** `run_tool("get_item_price_breakdown", {"itemId": 1})`

  ```json
  {"status": "ok", "data": {"itemCode": "RNG-0142", "itemName": "Zork Ring", "itemId": 1, "currency": "AED", "netWeight": 42.758, "ratePerGram": 66.0, "metalValue": 2822.03, "makingType": "percent", "makingValue": 6.58, "makingCharge": 185.69, "totalPrice": 3007.72}, "sources": [{"tool": "get_item_price_breakdown", "recordId": 1}]}
  ```

## 4. get_stock_by_store

- **Purpose:** Show stock of one item in each store.
- **Arguments:** `itemId` (number, 1 or more).
- **Endpoints:** `GET /api/items/{id}/stock`, `GET /api/stores` (for store names), `GET /api/items/{id}` (for code and name).
- **Returns:** itemId, itemCode, itemName, `stores` (storeId, storeName, city, onHand, reserved, available) and totalOnHand. `not_found` if the item does not exist or has no stock records.
- **Example call:** `run_tool("get_stock_by_store", {"itemId": 1})`

  ```json
  {"status": "ok", "data": {"itemId": 1, "itemCode": "RNG-0142", "itemName": "Zork Ring", "stores": [{"storeId": 1, "storeName": "Dubai", "city": "Dubai", "onHand": 4, "reserved": 0, "available": 4}, {"storeId": 2, "storeName": "Chennai", "city": "Chennai", "onHand": 2, "reserved": 0, "available": 2}, {"storeId": 3, "storeName": "Bengaluru", "city": "Bengaluru", "onHand": 1, "reserved": 0, "available": 1}], "totalOnHand": 7}, "sources": [{"tool": "get_stock_by_store", "recordId": 1}]}
  ```

## 5. list_low_stock

- **Purpose:** List items with few units on hand, in all stores or in one store.
- **Arguments:** `threshold` (0 to 1000, default 5): items with this many units on hand or fewer are listed. `store` (store name or id, optional).
- **Endpoints:** Assumed, no backend endpoint. The tool composes the answer from `GET /api/items` (all pages), `GET /api/items/{id}/stock` for every item, and `GET /api/stores` when a store is given. That is one stock request per item, run side by side.
- **Returns:** threshold, store, `count` (all matches) and `items` (itemId, code, name, onHand), lowest first, at most 50 shown. Items with no stock records count as 0 on hand. `not_found` if nothing is low or the store is unknown.
- **Example call:** `run_tool("list_low_stock", {"threshold": 5})`

  ```json
  {"status": "ok", "data": {"threshold": 5, "store": null, "count": 3, "items": [{"itemId": 2, "code": "RNG-0002", "name": "Classic Ring", "onHand": 2}, {"itemId": 5, "code": "RNG-0005", "name": "Temple Ring", "onHand": 5}, {"itemId": 24, "code": "EAR-0024", "name": "Modern Earrings", "onHand": 5}]}, "sources": [{"tool": "list_low_stock", "recordId": 2}, {"tool": "list_low_stock", "recordId": 5}, {"tool": "list_low_stock", "recordId": 24}]}
  ```

## 6. list_categories

- **Purpose:** List all product categories.
- **Arguments:** none. Any argument is rejected.
- **Endpoint:** `GET /api/categories`.
- **Returns:** a list of categories (id, name, parentId). `not_found` if the list is empty.
- **Example call:** `run_tool("list_categories", {})` returns `{"status": "ok", "data": [{"id": 1, "name": "Jewelry", "parentId": null}, {"id": 2, "name": "Rings", "parentId": 1}, ...], "sources": [{"tool": "list_categories", "recordId": 1}, ...]}` (eight categories in the sample data).

## 7. get_latest_metal_rate

- **Purpose:** Get the latest rate per gram for a metal, optionally for one purity.
- **Arguments:** `metal` (required, for example "Gold"), `purity` (optional, for example "22K"). Matching ignores case.
- **Endpoint:** `GET /api/metal-rates/latest`, then the tool filters the list.
- **Returns:** a list of matching rates (id, metalType, purity, ratePerGram, currency, effectiveDate). `not_found` if none match.
- **Example call:** `run_tool("get_latest_metal_rate", {"metal": "Gold", "purity": "22K"})`

  ```json
  {"status": "ok", "data": [{"currency": "AED", "id": 2, "metalType": "Gold", "purity": "22K", "ratePerGram": 66.0, "effectiveDate": "2026-09-30"}], "sources": [{"tool": "get_latest_metal_rate", "recordId": 2}]}
  ```

  The demo rates (24K 72, 22K 66, 18K 54 AED per gram) are fake sample values. Assumed: 22K is 22/24 of the 24K rate. The grounding tests use a separate test-only fixture where 22K is 1 per gram (see `ai-service/tests/fixture_api.py`), to prove the assistant reports data values and not real-world prices.

## 8. list_items_by_status

- **Purpose:** List items that have a given status.
- **Arguments:** `status` (required): `Draft`, `With Smith`, `Received` or `Complete` (assumed workflow, see `architecture.md`).
- **Endpoint:** `GET /api/items?status={status}&page=1&pageSize=100`.
- **Returns:** status, `totalCount`, `items` (same short fields as `search_items`, including `onHand`) and `truncated` (true if there are more than 100). `not_found` if no item has that status.
- **Example call:** `run_tool("list_items_by_status", {"status": "Draft"})`

  ```json
  {"status": "ok", "data": {"status": "Draft", "totalCount": 2, "items": [{"id": 11, "code": "BNG-0011", "name": "Royal Bangle", "categoryId": 4, "metalType": "Gold", "purity": "18K", "netWt": 6.452, "status": "Draft", "onHand": 11}, {"id": 22, "code": "EAR-0022", "name": "Classic Earrings", "categoryId": 6, "metalType": "Gold", "purity": "22K", "netWt": 24.868, "status": "Draft", "onHand": 22}], "truncated": false}, "sources": [{"tool": "list_items_by_status", "recordId": 11}, {"tool": "list_items_by_status", "recordId": 22}]}
  ```

## Not built

There are no tools for images, variants, sales, purchases, customers or finance. The assistant must refuse questions about them. For example, "Which items have no images?" is refused because no tool can answer it.
