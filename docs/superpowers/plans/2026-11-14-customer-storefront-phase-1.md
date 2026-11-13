<!-- Role: Records the approved Phase 1 design, implementation steps and review checks. -->
# Customer storefront Phase 1 implementation plan

**Goal:** Deliver requirements 1 and 2 from the owner's storefront request.
**Architecture:** Reuse the shared Angular card and existing camera photo dialog. Preserve historical jewelry records and image files, restricting public try-on data and images to earrings and necklaces.
**Tech stack:** Angular, CSS, .NET Web API, xUnit and Vitest.
**Spec:** Owner's requirements 1 and 2 and approval to begin Phase 1 in this conversation.

## Design and constraints

- Main checkout, branch `feature/customer-storefront` from `5afe975`.
- A fixed 4:5 image frame and 160px details block; reserve two lines per name and clamp longer names. This is an implementation choice to make images larger than details at existing card widths.
- Keep the existing theme and shared card on home, shop and wishlist.
- Reuse photo capture, preview, download and native sharing; label the return-to-camera action `Retake`.
- Remove the ring upload choice and reject excluded types at the API. Preserve legacy records and files for owner administration, hiding them from public lists and image URLs.
- Restore the FAQ exactly from `5f222dd~1`; chatbot research remains a later phase.
- Leave unrelated local files alone, including `old_proof.png`. No .env, SQL, Docker, deletion, push, merge or deployment steps.

## Tasks

### Task 1: Public try-on boundaries

- [x] Add regression tests for public list and known ring-image exclusion with owner records preserved; add rejected ring/watch upload cases.
- [x] Run focused tests and confirm failure for the missing policy.
- [x] Implement one customer-visible type predicate, used in the public controller, image endpoint and upload validator.
- [x] Remove the ring entry from frontend upload choices, keeping legacy record compatibility.
- [x] Run focused tests, including allowed images and inactive image protection. Expected: pass.

### Task 2: Cards and photo preview

- [x] Constrain intrinsic image dimensions within the fixed frame; fix details height, reserve name space and align buttons.
- [x] Replace the photo dialog Close action with Retake, retaining save and share.
- [x] Update the storefront README with behavior and a manual testing checklist.
- [x] No new pure frontend logic is required for these CSS and label changes; no source-text assertion tests.

### Task 3: Verify and hand off

- [x] Run all six Phase 0 checks in sample mode. Expected: all commands exit 0.
- [x] Check the diff and request the required read-only `nbjewel-lead` review.
- [x] Commit explicit Phase 1 paths only, report verification limits and stop for owner review.

## Review focus

- Mixed source image sizes must not change frame height across home, shop and wishlist.
- Short and long names must reserve equal space at phone and desktop widths.
- Retake must return to live camera; save/share must use the captured frame. Owner performs manual browser, camera and native sharing checks.
- Historical ring records and files must remain while public list and image requests exclude them.
- Accounts, live catalog/pricing, additional metals, chatbot and locator remain later phases.

## Verification results

- New public-list, ring-image and ring-upload regressions were observed failing before implementation.
- Focused backend run: 41 passed.
- Full sample backend run: 309 passed, 6 skipped. SQL connection variables were unset for the process.
- .NET solution build: passed, zero warnings or errors.
- ERP frontend: 66 tests passed, production build passed.
- Storefront: 50 tests passed, production build passed. Existing warnings remain for the unused ShopPage RouterLink import and Vitest configuration loading.
- No manual browser, camera, native sharing, SQL or Docker verification was performed.
- Read-only lead review: PASS, no blocking findings. Owner manual review remains pending.
