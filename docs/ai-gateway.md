<!-- Role: Describes the implemented gateway path, configuration, limits, verification and capabilities that stay unavailable. -->
# Shared AI gateway

## Implemented path
Both assistants share one provider seam and one server-side credential. They have separate identities, tools, knowledge collections, permissions and conversations.

- `POST /api/ai/erp/conversations` and `POST /api/ai/website/conversations` create owned conversations. Messages are read, sent and deleted under `/api/ai/{assistant}/conversations/{id}`. The browser sends only message text and a client request ID. History, prompts, models, tools and permissions are loaded or decided on the server.
- `POST /api/chat` stays as an authenticated ERP compatibility route. It runs the same pipeline (`ChatController` to `LegacyAssistant` to `ConversationService` to `PlatformOrchestrator`), ignores browser-supplied history and deletes its own transcript afterwards.
- `AssistantRegistry` holds immutable `erp` and `website` definitions: name, purpose, instructions, provider and model, tool allowlist, knowledge collections and limits. The website assistant reaches only public product tools and public knowledge. It never reaches ERP tools, purchase cost, reserved stock or internal status.
- The Gemini adapter is the only provider. `GatewayProvider` is the provider-selection seam; unknown provider IDs fail before any client call. `GEMINI_API_KEY` is read from the server environment only.
- The obsolete `AiGateway` and `BoundClient` classes have been removed without deleting files. `GatewayProvider`, `PlatformOrchestrator` and the shared parsing/guard helpers form the tested live path.

## Backend configuration
Use trusted server configuration or environment variables, for example `Assistants__erp__MaxOutputTokens`. Never accept these values from a request. `GEMINI_MODEL` is the default model.

| Setting | Default and validation |
| --- | --- |
| Name, Purpose, Instructions | Server-controlled strings |
| Provider | `gemini`; other providers are not implemented |
| Model | Backend `GEMINI_MODEL`; may be overridden per assistant |
| Tools | Comma-separated supported names; defaults are the ERP read-only tools and the three website tools |
| KnowledgeCollections | ERP `public,erp`; website `public` only |
| HistoryLimit | Default 6, range 0-6 (earlier user questions only) |
| MaxModelCalls | Default 8, range 1-8 |
| MaxToolCalls | Default 5, range 0-5 |
| MaxOutputTokens | Default 2048, range 128-8192 |
| MaxInputCharacters | Default 32000, range 1000-64000; heuristic bound, not exact token accounting |
| ModelTimeoutSeconds | Default 20, range 1-30 |

The whole question is limited to 45 seconds. One question-owned retry budget permits at most one additional HTTP attempt across all model calls, including unsupported-thinking fallback. The learned omit-thinking flag remains within that question and resets for the next one. With eight model calls, the maximum is nine HTTP attempts; the gateway adds no nested retries.

## Admission limits
All counters are stored in SQL (`AiAdmissionGates`) so they hold across instances. Defaults are configurable under `AiPlatform:` and are clamped to safe ranges.

| Limit | Default | Key |
| --- | --- | --- |
| Requests per minute per subject | 6 | `RequestsPerMinute` |
| Requests per day per subject | 200 | `UserRequestsPerDay` |
| Concurrent requests per subject | 2 | fixed |
| Requests per minute for one assistant | 60 | `AssistantRequestsPerMinute` |
| Requests per day for one assistant | 1000 | `AssistantRequestsPerDay` |
| Concurrent requests for one assistant | 8 | `AssistantConcurrency` |
| Reserved tokens per UTC day | 50,000,000 | `DailyReservedTokenBudget` |
| Anonymous requests per minute per network address | 30 | `AnonymousIpRequestsPerMinute` |
| Anonymous requests per day per network address | 500 | `AnonymousIpRequestsPerDay` |
| Anonymous conversations per day per network address | 30 | `AnonymousIpConversationsPerDay` |
| Requests per minute per address, all AI routes | 60 | fixed middleware |

A question reserves `MaxModelCalls x (MaxInputCharacters / 4 + MaxOutputTokens)` estimated tokens (80,384 with defaults). This heuristic is not an upper bound or a billing guarantee: language/tokenization and retries can increase actual usage. Reservations are not reconciled to actual usage. Provider-reported usage and optional configured cost estimates are recorded separately; use provider-side spend limits as well.

Anonymous callers are also limited by network address because a visitor cookie is free to discard. IPv6 callers share one key per /64 network.

## Client address behind a proxy
The site and API are served from one domain behind one proxy, and cookies stay `SameSite=Strict`. Set `TRUSTED_PROXIES` to the proxy address or CIDR range (comma separated). Only then are `X-Forwarded-For` and `X-Forwarded-Proto` honored, and only the last hop is read. With `TRUSTED_PROXIES` empty, forwarded headers are ignored and the socket address is used. An entry that is not an IP address or CIDR range stops startup. If the proxy is not listed, every client shares the proxy address and shares one limit.

## Security and telemetry
Tool declarations are filtered by the trusted allowlist. The orchestrator rejects any tool outside the allowlist, and each tool call re-resolves permissions and store scope before it runs. Prompt-leak checks use the configured instructions. Guard diagnostics do not log untrusted source names or record IDs. Usage rows hold assistant, model, trace ID, latency, token counts and an optional cost estimate from `AiPricing:{provider}:{model}`. Subjects are stored as hashes. Audit and usage rows are purged after `AiPlatform:AuditRetentionDays` (default 90).

Conversation text is encrypted with ASP.NET Data Protection. Keys persist as AES-GCM-protected XML in SQL under backend-only `AI_DATA_PROTECTION_SECRET`; no key directory is required. Production fails startup if the secret or SQL key ring cannot be used. Development without a secret explicitly warns that its ephemeral keys are not restart durable. Unreadable conversation history returns 410 and permits owner deletion and a fresh chat. See `platform-key-protection.md`. Expired conversations and stale admission gates are purged in bounded hourly batches; retention defaults to 7 days for visitors and 30 for signed-in users.

No semantic cache or vector database exists. The system is read-only: no AI write tools are enabled.

## Verification and extension
Deterministic tests use scripted model doubles. They cover registry separation, rejected providers and assistants, declaration and execution allowlists, cancellation, per-call timeout, model-call limits, token budgets, address limits behind a trusted proxy, cleanup, bulk reads and the `/api/chat` path end to end. No real provider call or API credential is used in tests. Live SQL, a real browser cookie session and a live Gemini call have not been verified.

Before adding a future assistant, add a trusted definition and an explicit authenticated or public route policy, separate tool projections, per-invocation authorization and conversation ownership checks, and negative authorization tests. Adding a name or system prompt alone is not enough. Unknown MVC controller names default to public in the compatibility filter, so every new controller needs explicit access enforcement before exposure.

See `unified-ai-progress.md` for review findings, test evidence and remaining work.
