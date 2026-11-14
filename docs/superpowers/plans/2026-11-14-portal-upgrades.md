<!-- Role: Defines the authorized ERP Vision and customer portal implementation, assumptions and verification gates. -->
# ERP Vision and customer portal implementation plan

Goal: editable Gemini suggestions from exact ERP image revisions, and public store discovery with real customer sessions gating try-on, bags and reservations.
Architecture: reuse .NET Identity cookies/CSRF and the existing Angular themes. Backend owns customer data, public store filtering and provider calls. Source-mode selection remains explicit. No SQL execution, worktrees, new cutout tooling or owner-file edits.
Spec: Muhammad's two-feature request and screenshots in this chat. Prior phase deferrals are superseded for these features. Read-only subagents only; root implements.

Assumed choices: OpenStreetMap embedded interactive map needs no key. Sample directory coordinates are explicitly illustrative, never verified NB Jewel locations. Real store sync evidence comes from a configured directory JSON with ERP store IDs and UTC sync timestamps, checked against inventory/publication. GoldAPI spot quotes use a backend-only METAL_RATES_API_KEY, bounded cache and explicit unavailable/stale states. Spot quotes never silently replace ERP selling rates. Customer sample accounts are opt-in, memory-only on the existing development identity provider; persisted customer portal records are scoped by server user ID.

- [x] 1. Vision: protected POST image/analyze requiring upload grants, CSRF and exact revision; separate Gemini client with GEMINI_ERP_API_KEY fallback GEMINI_API_KEY; validate short description/known collection JSON, return no inferred purity. ERP auto-analyzes newly uploaded preview once rendered, preserves intervening edits, offers regenerate and manual fallback. Test malformed/rate-limit/missing key and revision/permission boundaries.
- [x] 2. Customer backend: own profile, bag and reservations; validate product source/availability/store membership and enforce customer identity server-side. Add opt-in sample customer registration without weakening default staff-only mode. Test anonymous/ERP/ownership boundaries, idempotent reserve and invalid products.
- [x] 3. Store/rate backend: public validated directory, store-filtered published catalog, fresh sync metadata and optional timestamped market quotes. No keys in public DTOs. Test filtering, missing config, stale/invalid data and provider parsing.
- [x] 4. Portal: reusable sign-in/up modal, safe intent resumption including route query; account-scoped state and profile; desktop/top and mobile/bottom nav; map/store selection and public rates/story sections. All high-intent paths share gate, session loss fails closed. Preserve current catalog no-fallback guarantees and theme.
- [x] 5. Integration inventory, sample launcher, run instructions and .env.example. Count provider credentials separately from secrets/connections and inspect only configured/missing booleans in root .env.
- [x] 6. Run full backend sample tests, Angular typechecks/tests/builds and available lint. Read-only final review and responsive browser flow checks. Update handoff per checkpoint, commit/push explicit paths and release lock.

Review focus: stale image response must never overwrite a newer item or user edits; cancelled login must not trigger intent; verification-required registration must not count as login; account switches must not expose another bag/reservation; stale sync/provider data must not claim to be live.

Provider references: https://ai.google.dev/api/generate-content ; https://ai.google.dev/gemini-api/docs/structured-output ; https://wiki.openstreetmap.org/wiki/Export ; https://github.com/goldapi-io/gold-api-examples-js

Completion evidence: docs/portal-upgrades.md and the current AI-MEMORY-HANDOFF.md checkpoint. Live providers, SQL provisioning and physical-device quality remain manual checks, not claims of automated verification.
