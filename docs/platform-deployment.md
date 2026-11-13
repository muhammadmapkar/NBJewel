<!-- Role: Lists explicit setup, verification and production gates for the unified read-only assistant platform. -->
# Platform deployment checklist

This is a readiness checklist, not evidence of deployment. Sample data only; NB Jewel policies and role mappings remain unverified.

## Prepare without secrets in source
1. Provision an explicitly designated disposable SQL Server for integration checks. Review docs/identity-schema.sql, docs/platform-schema.sql and the existing ERP catalog schema. Apply only after approval. The scripts create new tables and are not a migration over an unknown production database. Back up existing databases and plan a reviewed versioned migration before any real rollout.
2. Configure NBJEWEL_IDENTITY_CONNECTION and AI_PLATFORM_CONNECTION on the backend. The platform connection falls back to the identity connection. Repository__Provider=Sample keeps synthetic ERP data; Sql also requires NBJEWEL_SQL_CONNECTION and the catalog schema.
3. Supply AI_DATA_PROTECTION_SECRET as a securely generated base64-encoded 32-byte random secret through backend environment variables or user secrets. All instances need the same secret and SQL key ring. Never send it through chat, frontend config or logs. Keep database and secret backups separate. No key directory is needed.
4. Supply GEMINI_API_KEY and GEMINI_MODEL on the backend. Only Gemini is implemented. Configure per-assistant models and limits as described in ai-gateway.md. Do not grant a new assistant permissions merely by giving it a name.
5. Use a same-site HTTPS proxy for Angular and the API. Configure TRUSTED_PROXIES with the actual nearest trusted proxy addresses/CIDRs; unlisted forwarded headers are ignored. Configure exact CORS_ORIGINS where needed. Never use wildcard credential origins.
6. For verified customer accounts, configure SMTP_HOST, SMTP_FROM and HTTPS APP_PUBLIC_ORIGIN. SMTP_PORT defaults to 587 with TLS; SMTP_USERNAME and SMTP_PASSWORD are optional backend secrets. The origin must be the website origin without a path, query or fragment. AiSecurity__RequireEmailVerification defaults true outside Development. No sender means required registration fails closed before creating an account.

## Initialize authorized sample content
The explicit --bootstrap-identity command is Development-only, needs separately supplied bootstrap credentials, refuses to elevate an existing account, and must not be run against production. It creates assumed demo grants, including knowledge.manage. Production ERP users must come from a separately approved provisioning/identity process.

Knowledge administrators need explicit knowledge.manage plus all-store authority. Import plain text/Markdown as drafts, inspect the version and ACL, then publish that exact version with confirm=true. Product publication is explicit. Nothing in the ERP catalog becomes public automatically. See knowledge-platform.md for bodies and the version conflict contract.

## Verify before rollout
- Run backend tests and frontend tests/build. SQL tests use their separate sample database when NBJEWEL_SQL_CONNECTION is explicitly provided; review Repos.cs first and never point test tooling at production. Current recorded SQL-dependent tests were skipped.
- Verify SQL bulk stock reads, unique idempotency constraints, optimistic admission/publication concurrency, restart persistence, key backups and restore in a disposable environment. In-memory EF tests do not prove SQL behavior.
- In a real browser, check HTTPS HttpOnly/Secure/Strict cookies, CSRF, same-site proxy/CORS, anonymous-to-login isolation, expiry, deletion, 410 recovery and email-fragment cleanup.
- Test SMTP delivery with approved test addresses and the actual verification link. No real email was sent during implementation. Verify a real Gemini question with an explicit budget and only sample data.
- Confirm /health/live and /health/ready. Readiness checks provider configuration and required identity/platform/knowledge table access; it does not make a billable provider call. Swagger is disabled outside Development.
- Route security audit and operational logs to approved monitoring; do not enable raw body or secret-header logging. Review retention, source approvals and provider privacy terms with the business owner.

Keep the AI read-only for this release. Disable new routes on rollback before changing policies or schemas. Do not delete encryption keys during rollback; that would make cookies and history unrecoverable. No deployment, schema application, push or merge is authorized by this checklist itself.
