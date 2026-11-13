<!-- Role: Records the approved unified-assistant architecture, delivery sequence and security acceptance gates. -->
# Unified AI platform implementation plan

## Architecture
Keep the platform inside the existing .NET API. Reuse Angular, the sample/SQL domain repositories, pricing logic, Gemini REST adapter and tests. ERP and website assistants share provider infrastructure and a backend-only key, never permissions, histories or knowledge visibility. The initial AI release is read-only.

## Delivery sequence
1. Identity and authorization: SQL-backed ASP.NET Core Identity, customer/ERP account boundaries, secure cookies, CSRF, rate limits, explicit permissions and store scope. Protect direct ERP endpoints and owner image operations as well as chat. Supply an external identity adapter contract. No real ERP role mapping is assumed.
2. Gateway and registry: trusted server assistant configurations, provider abstraction, model settings, deadlines, retries, cancellation, tool limits and usage accounting. Start with Gemini, 45-second request deadlines, eight model calls and five tool calls.
3. Authorized tools: strict schemas, permission and assistant allowlists, resource scope before aggregation, minimized outputs and fresh checks per invocation. Keep unimplemented sales, profit, supplier and order tools disabled. Define, but do not enable, confirmed writes with expiry, fresh authorization, transactions and idempotency.
4. Website assistant: separate public product/price DTOs, approved FAQs, isolated customer chat and support fallback. No private ERP data or unrestricted internal tool reuse.
5. Knowledge and conversations: implement conversation ownership before exposing conversation routes, then versioned approved text/Markdown ingestion, SQL lexical retrieval, document ACLs, source attribution, atomic publication and revocation. Default retention: anonymous seven days, authenticated 30 days. No summaries or embeddings until justified.
6. Operations: pseudonymous usage and security audits, token accounting, configurable cost estimates, six requests/minute and two concurrent requests/subject initially, plus assistant budgets and anonymous abuse protection. No semantic cache initially. Durable SQL, encryption keys, HTTPS, least-privilege credentials and explicit deployment migrations are required.

## Interfaces
Expose distinct `/api/ai/erp/conversations` and `/api/ai/website/conversations` namespaces with owned message/deletion endpoints. Requests supply text and a client request ID, not identity, permissions, system instructions, model settings or trusted history. Preserve `/api/chat` only as an authenticated ERP compatibility adapter; client history is not authoritative. Anonymous conversations use opaque cookies. Login starts a separate authenticated conversation. Return only authorized answers, sources, refusal status and a trace identifier.

## Adaptive implementation loop
Inspect, choose a dependency-ready task, select an available model and reasoning level, assign exclusive files, implement/test, independently review security changes, integrate/verify, record progress, then select the next task. Workers choose implementation details within the approved contract and recommend the next task/model. The coordinator validates and spawns it; workers do not pretend to change their running model. Prefer Sol for implementation, Astra for difficult security review, Luna for bounded scans when available. Allow at most two corrections before reassessing/escalating. Respect project phase limits and explicit approvals. Never silently expand scope, permissions or infrastructure.

## Acceptance and boundaries
Baseline existing tests first. Use deterministic provider doubles. Cover direct endpoint bypasses, per-tool authorization, resource scope, assistant switching, cross-user conversations, prompt injection, stale/revoked evidence, deadlines, retries and budgets. Use only explicitly designated disposable SQL for integration tests. Require backend tests, frontend tests/build and security review for accepted milestones. Do not touch secrets, apply SQL changes, mutate Docker state, delete, push, merge or deploy without required approval. Record failures and blockers accurately.

Deliver the architecture audit, permission matrix, setup/contracts, migrations, test evidence, FAQ research and future-assistant guide incrementally. Storefront, preview, maps, currencies and checkout follow this foundation. Production readiness is not implied by this document or unit-test success.
