<!-- Role: Explains knowledge publication and administration for the demo. -->
# Knowledge and product publication

Set `AI_PLATFORM_CONNECTION` to a SQL Server connection string in the backend environment. Generate and apply the platform schema through the deployment process before enabling this feature. Keep credentials outside the repository. Without database access, retrieval fails closed with an unavailable response. Searches also fail closed when more than 1,000 published document versions exist.

All endpoints below require a signed-in ERP account with `knowledge.manage` and all-store access. Send the existing CSRF token for mutations. Content is untrusted reference data, never instructions. Import plain text or Markdown only, up to 100,000 characters; the server does not fetch URLs or execute HTML.

- `GET /api/ai/knowledge/documents` lists documents and their current version metadata.
- `POST /api/ai/knowledge/documents` creates a draft; `PUT /api/ai/knowledge/documents/{id}` creates another draft. Send JSON with `title`, `text`, `collection` (`public` or `erp`), `audience` (`public` or `erp`), optional `requiredPermission`, `storeIds`, `ownerId`, and `confirm: true`. ERP audience requires a permission and at least one store ID.
- `POST /api/ai/knowledge/documents/{id}/publish` publishes the version the admin approved. The body is `{ "confirm": true, "versionId": "<latestVersionId from the document list>" }`. A missing `versionId` returns 400. If a newer draft was saved after the admin reviewed that version, the call returns 409 and publishes nothing; review the latest version and publish again. A save that lands during the publish is detected by concurrency tokens on the document row and also returns 409.
- `POST /api/ai/knowledge/documents/{id}/revoke` removes the document from retrieval and needs only `{ "confirm": true }`. Creating a new draft does not replace the published version until publication.
- `POST /api/ai/knowledge/products/{itemId}/publish` or `/revoke` requires `{ "confirm": true }`. Only explicitly published products are eligible for public retrieval.

Website retrieval sees only accessible public documents. ERP retrieval additionally requires the document permission and access to every listed store. Owner restrictions apply to both audiences. Published chunk citations cease to validate on replacement or revocation.
