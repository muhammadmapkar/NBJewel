<!-- Role: Explains the inspected baseline, reusable components and security gaps for the unified AI platform. -->
# Existing system audit

## Verified baseline
The accessible project is a single Angular 20 application and .NET 10 API. The ERP shell and public `/tryon` experience share Angular but use different routes. No separate production ERP or customer website has been supplied. Compatibility with other applications must be established through documented contracts, not assumed framework or database equivalence.

`IItemRepository` has sample and SQL Server implementations. Existing catalog, price, stock and reference endpoints return domain objects. SQL is optional in the original application. Try-on metadata and images use `FileJewelryRepository`, separate from catalog persistence. This audit did not inspect secrets or connect to SQL.

The chatbot already lives in the API. `GeminiLlmClient` uses REST with a backend key, `ILlmClient` supports test doubles, `ChatOrchestrator` caps model/tool calls, `ChatTools` declares fixed read-only tools, and guards validate evidence. These are reusable. No separate AI microservice or unrestricted SQL interface is needed.

## Baseline security findings
Before this implementation, no authentication or authorization middleware was registered. Item creation, updates, stock entries and status changes were publicly callable. ERP reads and chat were public. Owner image management used a shared PIN, which is not an application identity or permission model.

Chat accepted browser history and invoked global repository tools without a user/store context. Prompt guards and source validation do not repair this authorization gap. A scoped user must not receive global stock totals through item search, low-stock lists, chat or direct endpoints.

There is no verified role matrix, customer identity, order domain, sales/profit domain, document ingestion pipeline or owned conversation database in the baseline. Role names and policies must not be presented as actual NB Jewel internals. Authentication forms displayed in static UI do not establish backend identity.

## Integration and deployment requirements
- Local identity must persist in SQL and use cookies/CSRF. Public registration creates customer accounts only. Permission and resource checks apply to direct endpoints and tools independently.
- A future external ERP adapter must resolve a verified subject, account type, explicit capabilities and resource scope. It must not translate a browser-supplied role or arbitrary assistant ID into authority.
- External websites consume only public/customer assistant routes. A separate host will need explicitly configured credential transport and origins; CORS alone does not authenticate an application.
- Public catalog responses require separate field projections. Internal statuses and store reservations must not become public through domain-object serialization.
- Persistent cookies need persistent protected encryption keys. Conversations, audit and identity need durable SQL. Ephemeral upload storage and demo hosting are not sufficient for production.
- All tests use sample data. Live ERP writes, real payments and customer order retrieval remain unavailable until their actual services and authorization rules exist.

## Reuse map
Reuse repositories and domain validation for direct ERP workflows; reuse pricing rules for consistent quotations; reuse the Gemini transport behind the gateway; reuse fixed tool definitions behind deterministic authorization; reuse guard and poisoned-data tests as defense-in-depth; reuse the Angular theme and shell for identity UI.

## Deferred capabilities
This audit does not implement the gateway registry, scoped AI executor, website assistant, RAG, order tools or production monitoring. The progress document records which work has actually been accepted. The wider customer storefront, maps, cutout automation and shopping flow are subsequent projects.
