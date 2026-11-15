<!-- Role: Records the agreed business web portal design and future implementation steps; execution is not authorized. -->
# Business web portals implementation plan

> **Status: planning only. Do not implement until Muhammad explicitly authorizes it.**
> Saving or reading this document, an unchecked task, or a generic instruction to continue is not implementation approval. No product code, schema migration, provisioning or deployment is authorized by this document.
>
> **For agentic workers:** After authorization, use the executing-plans skill for sequential implementation. Keep core implementation unified; use subagents only for isolated read-only review/verification if permitted by the current instructions.

**Goal:** Give each retailer/wholesaler its own branded customer portal, managed from a Web Portal tab in ERP, using one shared NB JEWEL template.

**Architecture:** One shared application supports multiple isolated businesses. Each business owns its portal configuration, catalog and branches. Global customer identity is reused, while shopping data remains business-specific. Draft and published configuration are separate.

**Stack:** Existing Angular standalone components/signals and .NET Web API; existing Sample repository for local verification; SQL changes prepared as reviewable files only.

**Spec:** The agreed design and confirmed choices are recorded below from Muhammad's planning conversation on 2026-11-15. This is a self-contained plan, not a statement that the feature exists.

## 1. Confirmed choices

| Decision | Agreed behavior |
| --- | --- |
| Portal ownership | One portal per retailer/wholesaler business, covering multiple branches |
| Hosting model | One shared NB JEWEL platform for many businesses |
| Template | One maintained responsive template; editable content and section switches |
| Layout freedom | Fixed section order; no drag-and-drop builder or alternate templates |
| Wholesale v1 | Catalog and enquiries, without minimum quantities or trade pricing rules |
| Customer identity | One platform login; separate bags, wishlists and reservations per business |
| Business onboarding | NB JEWEL provisions businesses and owner access; no public business signup |

### ERP editor

Add a **Web Portal** sidebar entry leading to these areas:

| Area | Controls |
| --- | --- |
| Branding | Business name, logo and approved color presets |
| Homepage | Banner image, headline, introduction and shop story |
| Collections and products | Visible collections/products and featured designs |
| Customer actions | Show prices; enable enquiries, try-on, bag and reservation requests |
| Contact and branches | Contact details, WhatsApp number, opening hours and visible branches |
| Preview and publication | Save draft, preview desktop/mobile, publish changes and take the portal offline |

Customers visit `/shops/{businessSlug}`. Branch selection stays within that business. Existing responsive components and product publication controls are reused.

## 2. Defaults and boundaries

These are architectural defaults proposed in the agreed plan, not existing NB JEWEL business policy:

- Retailer preset: prices and the existing customer actions enabled. Wholesaler preset: prices hidden and enquiries enabled; try-on, bag and reservations off initially. Owners can change switches.
- A new business starts offline. Its slug is unique and immutable in v1. Use `nb-jewel-demo` for the existing demo business.
- Keep the current homepage section order and component designs. Allow branding/content changes and section visibility only; no HTML, JavaScript, custom CSS or arbitrary embeds.
- Branding uploads reuse bounded PNG/JPG validation and local storage conventions. Logos/banners do not require the jewelry cutout pipeline.
- Portal visibility never bypasses product eligibility, current-image approval or explicit product publication. Revocation remains immediate, even if the portal configuration is unchanged.
- Store-specific catalogs and reservations continue to require actual available stock. Publishing a product does not manufacture inventory.
- Price hiding is enforced in public responses and alternative access paths, not only CSS. Disabled customer actions are rejected by the backend, including requests from stale browser tabs.
- Existing contact/WhatsApp links provide v1 enquiries. Do not introduce a new message-delivery service or claim an enquiry has been sent automatically.
- Provider credentials remain backend configuration. This editor does not expose Gemini, metal-feed or other service keys.
- Custom domains, payments, full wholesale ordering, self-service business signup and production provisioning are excluded.

### Global constraints

- Main checkout on `feature/customer-storefront`; follow the current AGENTS.md and handoff protocol when execution is authorized.
- Preserve protected owner files, actual `.env` files and running user sessions.
- No SQL execution, Docker, deployment, destructive migration, worktree creation or force push.
- Reuse the ERP shell/theme and customer components. Keep role comments on new files and no em-dashes in UI/docs.
- Use sample-only verification with SQL connection settings unset. An implementation plan is not permission to apply its prepared SQL files.

## 3. Ownership, storage and interface contracts

### Business boundary

Introduce businesses and staff memberships. A trusted request-scoped `BusinessContext` supplies the business identity after resolving a public slug or checking authenticated membership. Never trust a browser-supplied business ID without that check.

Scope products, branches, stock, selling rates, image revisions, publication records, knowledge and conversation access to the owning business. The current global repository/storage registrations need replacement or business-aware dispatch. Sample instances, filesystem paths and caches must not share mutable business data. Generic market reference quotes may remain shared; business selling rates may not.

Use business-qualified keys and relational constraints for business-owned data. Staff permissions and store grants are resolved within membership: **all stores means all branches in that business**, not the entire platform. `portal.manage` permits draft edits/preview; `portal.publish` permits publication/offline changes. Owners receive both.

Global customer accounts remain separate from staff membership. Profile identity stays global; bags, reservations and local wishlist keys include customer ID plus business ID. Retailers cannot access another business's shopping activity.

### New interface groups

| Interface | Responsibility |
| --- | --- |
| Operator provisioning command | Create a business, reserve its slug and grant an existing staff account owner membership; no public signup endpoint |
| Protected ERP portal API | Read/save draft, upload branding assets, authenticated preview, publish a revision, take portal offline |
| Public shop API | Resolve the published business portal and its catalog/images/stores by slug |
| Business-scoped customer API | Apply customer ownership and current published action/price policy for bag/reservations |

Use existing cookie/CSRF authentication. Draft writes and publication carry an expected revision; conflicting edits return 409 without discarding another editor's work. The public DTO contains published customer-facing fields only.

Portal settings include branding/content, section switches, collection/product visibility, featured product IDs, action/price switches, contact details and selected branch IDs. Every referenced product, branch and uploaded asset must belong to the business.

Save draft changes separately from a published snapshot. Publish validates the complete draft and atomically switches the published revision. A failed publish leaves the current live revision intact. Unknown/offline shops expose no catalog and never fall back to another business or bundled demo data.

Preview uses the same storefront renderer via authenticated, non-cacheable draft endpoints. It is read-only and cannot submit shopping actions. A public `preview=true` parameter must never grant draft access.

### Compatibility

Preserve existing behavior within the default demo business. Map legacy storefront URLs/APIs to that business only, including `shop?source=erp`; never leave an unscoped endpoint that aggregates all businesses. Preserve source, business and branch context across product links, try-on piece/design changes, authentication resume and refresh.

Prepare SQL ownership/configuration migrations and legacy-file import instructions as files. Existing sample assets/publications must be assigned explicitly to the default demo business, not copied into every new business. Do not apply migrations as part of this plan's documentation task.

## 4. Future implementation tasks

All tasks below are **pending and unauthorized** until Muhammad gives the go-ahead. Each authorized task uses a failing regression test, minimal implementation, affected checks and an explicit-path checkpoint/push.

### Task A: business identity and repository isolation

**Touchpoints:** backend Identity, Repositories, Jewelry, Knowledge, Platform and Program composition. Add a focused Businesses module and sample fixtures; prepare SQL files.

**Produces:** business registry, memberships, scoped permissions and `BusinessContext`; isolated repository, asset and publication access.

- [ ] Add two-business tests using overlapping local product/store IDs. A staff account in A must not read/write B by changing routes, request bodies or asset IDs.
- [ ] Implement membership resolution and business-qualified access across existing API and assistant paths, including background jobs and cached data.
- [ ] Seed a default demo business plus an independent sample business. Prepare reviewed backfill/constraint changes without opening SQL connections.
- [ ] Verify the isolation tests and existing backend suite before adding editor routes.

### Task B: provisioning and portal configuration lifecycle

**Touchpoints:** new backend Businesses/Storefront configuration services, protected controller and scoped persistence; backend tests.

**Consumes:** Task A's trusted business context and permissions.
**Produces:** provisioning command, versioned draft/published configuration, branding asset ownership and audit events.

- [ ] Test duplicate slugs, owner-only access, cross-business asset references, stale revisions, failed publication and offline public reads.
- [ ] Implement operator provisioning without public business signup. Do not create hardcoded passwords or send invitation emails implicitly.
- [ ] Implement draft save, atomic publication/offline state and bounded branding uploads. Audit business ID, actor and revision without secrets.
- [ ] Verify new lifecycle tests and the backend suite.

### Task C: business-specific storefront and customer data

**Touchpoints:** existing public catalog/customer controllers; customer app routing, config, catalog, session and store services; template components.

**Consumes:** business context, published portal settings and existing product approval/publication checks.
**Produces:** `/shops/{businessSlug}` routes, business-specific public/customer APIs and reusable configurable template.

- [ ] Test unknown/offline shops, hidden products/prices, stale-tab actions and absence of cross-business fallback.
- [ ] Apply the published settings to homepage/header/contact/navigation and public product projections. Maintain one shared layout.
- [ ] Scope bags/wishlists/reservations and preserve business/branch context through login resume, reload and try-on navigation.
- [ ] Add compatibility mapping for the default demo business. Verify backend and customer tests/typecheck/build.

### Task D: ERP Web Portal editor and authenticated preview

**Touchpoints:** existing sidebar menu in `frontend/src/app/core/mock/mock-data.ts`; a new frontend Web Portal feature and API service; shared customer renderer/preview entry.

**Consumes:** Task B's protected APIs and Task C's shared renderer.
**Produces:** working business-owned editor, draft preview and explicit publish/offline controls.

- [ ] Test owner permissions, fixed section switches, validation, 409 recovery and draft/live separation.
- [ ] Implement all editor areas listed in section 1 using existing ERP components. Product visibility/featured choices select business-owned eligible products; image approval remains in the existing item workflow.
- [ ] Add desktop/mobile preview with authenticated draft reads, no public draft exposure and no customer mutations.
- [ ] Verify ERP tests/typecheck/build and compare preview to the live template after publishing.

### Task E: two-business QA and release gate

**Produces:** completed acceptance evidence, updated setup/QA documentation and reviewed schema files. No production rollout is implicit.

- [ ] Run a read-only review of all business boundaries, legacy endpoints and shared caches; fix material findings with regressions.
- [ ] Run backend sample tests/build and both Angular suites, strict typechecks and production builds. Run lint only if configured; report absence honestly.
- [ ] Demonstrate two differently branded businesses, isolated data and the existing ERP upload → review → publish → edit → revoke flow.
- [ ] Extend `docs/QA_TEST.md` with owner/other-business/customer checks, draft versus live checks and offline behavior.
- [ ] Record migration requirements and validation limits; update the sole handoff file, checkpoint/push and release the lock. Do not deploy or run SQL.

## 5. Review focus and acceptance

| Risk | Required proof / owning task |
| --- | --- |
| Cross-business access through legacy routes, assistants, files or cache | A/C: overlapping-ID negative tests and no global fallback |
| Hidden prices leaking through direct detail, rates, breakdown or assistant responses | C: inspect every public projection; ERP authorized pricing remains available |
| Draft/private branding visible publicly | B/D: guest preview denied; draft assets/config not publicly exposed before publication |
| Old browser state bypassing newly disabled actions | C: backend rejects action using current published policy |
| Losing shop context during login, navigation or reload | C/E: original business, product and branch survive every transition |

Acceptance requires two businesses using the same template independently; unpublished edits staying private; published changes affecting only the owning shop; correct product and stock eligibility; global login with separate business shopping state; and passing regression/build/browser checks. Prepared SQL alone is not evidence of production persistence or deployment readiness.

## 6. Execution hold

**Current authorization is limited to saving this plan. Implementation has not started.**

Muhammad will explicitly decide whether and when to execute it. Until then, do not provision businesses, edit application code, create migrations or alter any running service for this feature.
