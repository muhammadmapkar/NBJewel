# API contracts placeholder

Future endpoints should be typed, parameterized, and read-only. The assistant
should call named tools with explicit inputs and should never accept arbitrary
SQL.

**Assumed:** Initial read-only contracts may expose category trees, item lookup,
store stock, metal rates, and bounded operational summaries. Exact routes and
authentication are deferred until the backend phase.

## Endpoints added for the product manager changes

Assumed shapes, built for the demo. Reads are used by the assistant. Writes are used only by the UI.

| Method and route | Purpose | Errors |
|---|---|---|
| `GET /api/designs` | Design master list for the dropdown | none |
| `GET /api/items/{id}/variants` | Variants of an item | 404 unknown item |
| `GET /api/items/{id}/stock` | Stock rows per variant and store | 404 unknown item |
| `POST /api/items/{id}/stock` | Add stock. Body `{variantId, storeId, quantity}`. Returns the new on-hand record. | 400 quantity 0 or less, unknown store, wrong or missing variant. 404 unknown item |
| `PATCH /api/items/{id}/status` | Set a status. Body `{status}`. Any of the four, from any status (assumed). The same status returns 200 with no change. | 400 unknown status. 404 unknown item |
| `GET /api/items?search=` | Search matches code, name and design code | 400 bad paging |

Error bodies are plain text messages, for example `Unknown status 'Pending'. Allowed statuses: Draft, With Smith, Received, Complete.`
