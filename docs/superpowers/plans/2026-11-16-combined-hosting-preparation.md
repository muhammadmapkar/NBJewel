<!-- Role: Records the authorized assumed single-origin ERP/storefront hosting preparation and local verification, without deployment. -->
# Combined hosting preparation

Muhammad authorized the proposed preparation on 2026-11-16: hosted ERP at `/erp/`, storefront at `/shops/{slug}`, shared API at `/api`, base-aware assets/config, generated nonsecret production settings, and existing Python cutout dependencies in the Dockerfile. This is repository preparation only. Main checkout and feature/customer-storefront only; preserve owner services/files. No SQL, Docker execution, deployment, secrets, merges or worktrees. Independent portal review remains pending for Claude.

## Assumed implementation choices

- Keep `npm start` and ordinary `npm run build` at their existing local roots/configuration.
- `npm run build:deploy` generates app-specific settings under ignored `.hosting/`, compiles the hosted Angular configuration, then emits Vercel Build Output API v3 static files/routing under ignored `.vercel/output/`. Existing Vercel JSON files select this build with Framework Other. No additional dependency or protected lockfile change.
- One shared Node build/settings module validates public/upstream origins. Hosted browser API calls use the public origin. API and ERP upstreams are configured only for gateway routing; missing/invalid origins and self-proxy loops fail closed. HTTP is accepted only for loopback local verification.
- Nonsecret inputs: NBJEWEL_PUBLIC_ORIGIN (NBJEWEL_API_URL compatibility alias), NBJEWEL_API_UPSTREAM and NBJEWEL_ERP_UPSTREAM. Never load .env. Node build children receive the existing environment, but only explicit public settings are serialized.
- Hosted ERP uses `/erp/`; Angular router links keep internal route paths. Fix the root-absolute portal settings request and preserve safe Web Portal return navigation. Existing API/CSRF boundaries stay intact.
- Gateway routes ERP/API/legacy public images before static-file lookup and storefront SPA fallback. Private API/config/image paths use no-store. Missing static assets fail as files rather than receiving HTML.
- Pin Docker distro to .NET 10 noble; include only existing script and Pillow/NumPy/SciPy via distro packages. Preserve durable filesystem settings as operator configuration, never bake credentials or runtime data into the image.

## Steps and evidence

- [x] H1 RED: configuration validation, emitted browser settings, generated gateway forwarding/query/prefix behavior, local config preservation, portal config below /erp/, safe portal return path.
- [x] H1 implementation: shared build/config module, hosted Angular configurations and scripts, base-aware ERP config request, Vercel output; focused GREEN and both app tests/typechecks/builds.
- [x] H2: update Dockerfile and ignore rules, publish backend to /tmp and verify script/sample content; existing real cutout/backend tests. Static container review only; Docker build/run owner-run.
- [x] H3: isolated local Sample API and one-origin gateway browser check for sign-in, refresh nested ERP route, authenticated preview, product/private images and public storefront. No owner listener mutation.
- [x] Final gates: clean Sample backend test/build; ERP and storefront full tests, typechecks, normal and hosted builds; whitespace/dash/role checks. Record logs and manual Docker/hosting limits in docs/combined-hosting.md and handoff; commit/push/release Lock.

## Verification rules

Write assertions before implementation and read every RED/GREEN result. Routine helper handles locations and commands only, never lock/handoff/commits. Full command output goes to /tmp/nbjewel-hosting-<step>.log. Backend uses clean env with Repository__Provider=Sample and CUTOUT_PYTHON_PATH=/tmp/nbjewel-cutout-runtime-venv/bin/python; SQL connection variables absent and SQL tests skipped. Browser uses synthetic sample fixtures and isolated ports/storage only.

No deployment, production identity/database readiness, real camera/photo quality or container execution is implied by local success.

Evidence: H1 RED /tmp/nbjewel-hosting-H1-node-red.log and H1-erp-red2.log; GREEN Node 5, ERP 80, storefront 88, both typechecks/normal/hosted builds. H2 clean backend 447 passed, 7 SQL skipped, build/Release publish passed. H3 real-cookie/CSRF sign-in, saved preview, public/private synthetic images, customer bag/ERP denial, nested refresh and sign-out checks passed; only our temporary listeners stopped. See docs/combined-hosting.md for exact limits and log paths.
