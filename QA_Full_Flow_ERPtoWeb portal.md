<!-- Role: Defines owner-perspective acceptance scenarios for the full ERP-to-web-portal journey, with honest results and business gaps. -->
# QA full flow: ERP to web portal

**Owner's question:** Can my team put the right jewelry online, keep prices and availability trustworthy, help customers choose a design, and stay in control of what the public sees?

**Prepared:** 2026-11-16. **Results:** Not run in this document.

These are **assumed business-owner acceptance expectations**, grounded in the agreed portal scope. They are not claims about NB Jewel's internal procedures. Muhammad can amend the expectations. A feature being present, an API responding, or an automated test passing does not by itself mean the business journey passes.

Use the application as an owner, a staff member and a customer. Judge what they can achieve through the screens. Record a missing capability as a **Gap** instead of changing the expectation to match the software. This document does not authorize implementing additional features.

## 1. What a successful day looks like

1. I sign in and immediately know which business I am managing.
2. My team adds a design once in ERP, with correct specifications, price and branch availability.
3. We prepare its photo and description, inspect the result, and deliberately approve publication.
4. I prepare my shop's branding and content privately, preview the customer experience, then publish it.
5. A customer finds the right design, sees information I stand behind, and contacts us or requests a visit without confusion.
6. When information changes, the website reflects the change without a developer rebuilding it.
7. I can withdraw a design or take the shop offline, while another business stays unaffected.
8. Tomorrow, my business records are still there and my team can continue working.

The last expectation is essential for real business use. A temporary demo that loses accounts or shopping records on restart must be recorded as a demo limitation and a business-readiness gap.

## 2. Prepare a realistic test

Arrange access before the owner session. The owner should receive a usable login and shop link, rather than being expected to configure software. Keep installation and service commands in the separate [local QA guide](docs/QA_TEST.md).

| Person or item | Preparation |
| --- | --- |
| Owner A | Owner of retailer A, allowed to manage and publish its portal. |
| Staff A | Staff of retailer A, allowed to prepare the portal but not publish or take it offline. Arrange other item permissions explicitly for the relevant steps. |
| Owner B | Owner of a separate wholesale business B. Use a different browser profile when testing both staff identities together. |
| Customer | One test customer who shops at both businesses. Use a private window separate from ERP. |
| Visitor | Signed-out browser with no staff access. |
| Design A | A new sample necklace, unique code `QA-NECK-YYYYMMDD-01`, with a permitted photo. Record its actual assigned ID. |
| Design B | A visibly different sample design belonging to B. Its local item ID may intentionally overlap A's ID. |
| Branches | A has two sample branches with deliberately different availability. B has its own sample branch. Record names and stock before testing. |
| Branding | Distinct assumed names, approved sample logo/banner images and different available color presets for A and B. |
| Contacts | A test destination controlled by the tester, if enquiries are being tested. Never send a message to an invented number or a real customer. |
| Devices | Desktop and a phone. Emulation helps with layout, but camera and sharing checks need a real device. |

The existing sample slugs are `nb-jewel-demo` and `sample-wholesale`. Confirm their owners and usable catalog have actually been arranged. A registry entry alone is not a usable second business. If the tester cannot obtain the required access or stock setup, mark the affected scenarios **Blocked**.

Record before starting:

| Run details | Fill in |
| --- | --- |
| Tester and date | |
| Application version or commit | |
| Environment and whether data survives a restart | |
| Business A name and shop link | |
| Business B name and shop link | |
| Design A code/ID and Design B code/ID | |
| Branch stock before testing | |
| Browser, phone and screen size | |

### How to record results

- **Pass:** the owner/customer outcome was observed, including the relevant negative check.
- **Fail:** an available function produced the wrong outcome.
- **Blocked:** a prerequisite or environment prevented testing; state what is missing.
- **Gap:** the owner needs the capability, but the product does not provide it.
- **Not run:** no evidence yet. This is the starting status for every scenario below.

Keep a result for each scenario ID in section 16. Do not mark an entire section Pass after testing only one row. Record the business, item code, page link and visible evidence; exclude passwords and private customer details.

## 3. Open my business and trust my team's access

**Owner goal:** My staff can do their jobs without accidentally managing another shop or publishing without approval.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| ACCESS-01 | Sign in as Owner A and open Web Portal. | I recognize business A, its saved settings and its current online/offline state. I can find the portal without guessing a URL. |
| ACCESS-02 | Sign in as Staff A and prepare a small draft change. Try publishing and taking the shop offline. | Staff can save permitted preparation work. They cannot publish or take the shop offline, including through an old bookmarked page. |
| ACCESS-03 | As Owner B, open B's items and Web Portal. Follow a copied private management link belonging to A. | B shows only B's records. A's private content and controls stay inaccessible. A similar item number never means the same business record. |
| ACCESS-04 | Sign out of ERP, then revisit its draft/preview pages as a visitor or customer. | Private business content is unavailable. A customer account cannot become a staff account by navigating to a staff page. |

## 4. Add a design once and get its facts right

**Owner goal:** ERP is the working record for my team. The website must not require a second, inconsistent copy of the same product.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| ITEM-01 | Open Inventory, Item Catalog, Add. Enter Design A's code, name, category, design, metal, purity, weights and making charge. Save, leave and reopen it. | The saved record contains the values entered. It is easy to find by code/name and starts as unfinished rather than immediately public. |
| ITEM-02 | Try a duplicate code, missing required field and impossible weight, such as net weight greater than gross weight. | Clear field feedback prevents an invalid or duplicate record. Valid work is not silently lost. |
| ITEM-03 | Complete the design's specifications and change its status to Complete. Check the public shop before approving publication. | The ERP item is ready for review, but completing internal work does not itself announce a new product to customers. |
| ITEM-04 | Open a restricted or unsupported design, for example an unsupported try-on category, and attempt the relevant publication action. | The screen explains what is supported. A product never receives a misleading try-on promise merely because it exists in ERP. |

## 5. Check price and branch availability before promising anything

**Owner goal:** My website must not quote a wrong amount or send someone to a branch that cannot show the design.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| VALUE-01 | Record ERP's actual metal/purity rate, effective date, net weight, making charge and configured stones/tax. Independently add the displayed amounts. | The breakdown and total reconcile in the stated currency. No tax rate, purity conversion or live-rate claim is invented. |
| VALUE-02 | Check another metal/purity. Check a design without a valid matching rate. | Each design uses its own matching rate. Missing pricing is explained; a guessed amount or another metal's rate is never presented as its price. |
| VALUE-03 | Arrange positive available stock for Design A at one branch and none at the other. Inspect ERP stock and customer branch-filtered listings. | The branch filter shows the right available designs and branch name. A branch with no available stock does not advertise the design as available there. |
| VALUE-04 | Reduce the available stock and refresh the selected branch on the website. | The refreshed result follows the new availability. A bag or visit request is not presented as a guaranteed stock allocation. |

Use the actual approved sample rate for arithmetic. If a needed price or stock input cannot be maintained through the intended owner/staff workflow, record that usability gap; do not silently substitute a developer-only workaround.

## 6. Prepare a photo and copy that I am happy to show customers

**Owner goal:** A fast upload should save my team time without publishing an ugly image or an unverified description.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| CONTENT-01 | Open the design's Images tab and upload a permitted photo. Inspect the actual generated preview, including edges and important jewelry details. | I see the correct design and can judge its quality. Uploading does not automatically approve or publish it. A poor result remains unapproved. |
| CONTENT-02 | Review any generated description/collection suggestion. Correct it, save, leave Images and return. | Suggestions are editable. The saved wording is retained. Unsupported material, gemstone, hallmark or product claims are not accepted just because AI suggested them. |
| CONTENT-03 | Repeat with AI unavailable and with an invalid or oversized file. | Manual copy entry remains usable. Bad uploads give a helpful message and do not replace a good approved product with a broken image. |
| CONTENT-04 | Approve the inspected current preview, then replace the image with a new one. | Approval belongs to the image I actually reviewed. The replacement needs a new review before it can be public; the customer is never shown an unapproved replacement. |

An acceptable synthetic test picture proves the workflow only. Real permitted jewelry photos still need the owner's visual and licensing approval.

## 7. Make the portal feel like my shop

**Owner goal:** I can maintain the normal shop content myself without editing code or asking a developer to rebuild the site.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| PORTAL-01 | Open Web Portal. Set the business display name, an available color preset, logo/banner, headline, introduction, story, contact details and hours. Save. | My settings are retained and understandable. Success is clearly acknowledged. Existing live content stays as it was until I publish. |
| PORTAL-02 | Choose visible collections, featured products and branches. Try choosing an unavailable product or another business's reference. | The choices belong to my shop and are eligible for public use. Invalid selections cannot quietly put the wrong business's product or branch on my site. |
| PORTAL-03 | Turn individual home sections off, save and preview. | The hidden section disappears cleanly, with no blank block or misleading invitation. The shop still has a usable way to browse its designs. |
| PORTAL-04 | Enable WhatsApp enquiries without a destination, then enter the controlled test destination. | Incomplete enquiry setup is explained before publication. Once valid, the customer action has a real intended destination. No message is sent just by saving or previewing. |

## 8. Preview privately and publish deliberately

**Owner goal:** I know exactly what I am approving, and a mistake does not replace my working shop.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| RELEASE-01 | Save a noticeable draft headline. Open desktop/mobile preview and separately open the live public shop as a visitor. | Preview shows the saved draft with my business's products. The public shop still shows the previous approved content. Customer transactions are disabled in preview. |
| RELEASE-02 | Publish the valid draft, then reload the public home, shop and product pages. | The approved branding/content appears consistently. No rebuild or developer intervention is needed. Another business's site stays unchanged. |
| RELEASE-03 | Leave a required setting invalid or a selected product unavailable, then try publishing. | The problem is explained. My previous working publication stays online and intact. Saving an incomplete draft is distinguishable from successfully publishing it. |
| RELEASE-04 | Open the editor in two tabs. Save different changes in the first, then save the stale second tab. | One person's changes do not silently overwrite another's. The second editor can reload, preserve their own changes for review and deliberately save again. |
| RELEASE-05 | Prepare an edit, navigate away or encounter a connection failure while saving. | I can tell what was saved and what was not. Unsaved work is protected or clearly warned about; success is never shown for a failed save. Record missing protection as a Gap. |

## 9. Walk into the shop as a new customer

**Owner goal:** A customer can discover a trustworthy design and take the next step without knowing how our ERP works.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| SHOP-01 | Sign out and open the public shop on desktop and phone. Use Home, Shop, a collection and a product. | My branding is recognizable. Only approved, available-to-publish designs appear. Navigation remains inside my business and does not expose staff tools. |
| SHOP-02 | Search for Design A by code/name, use the offered filters and open its details. | The right image, name, metal, purity, weights, description and price policy are consistent with ERP. No confidential ERP-only information is visible. |
| SHOP-03 | Choose a branch, open a design, copy its link and reopen it in another tab. | The customer stays with the intended business, product and branch. Sharing a design does not take the recipient to an unrelated shop. |
| SHOP-04 | Browse an empty collection and temporarily unavailable shop/catalog. | The customer gets an understandable state and next step. Old sample designs or another business's stock are never used to make the shop look populated. |
| SHOP-05 | Use normal phone scrolling, keyboard navigation and larger text. | Names, prices, form errors and action buttons remain readable and usable without overlap or sideways scrolling. |

## 10. Test a wholesale business that does not display prices

**Owner goal:** I can show designs to buyers without disclosing selling prices or another shop's commercial information.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| WHOLESALE-01 | As Owner B, publish distinct branding with Show prices off. Browse home, shop and product pages. | B's own designs and specifications remain visible. Selling prices, making amounts, price breakdowns and business selling-rate displays are absent. |
| WHOLESALE-02 | If enabled for B, inspect its bag, enquiry text and try-on displays. | No price or total leaks through a secondary screen or prepared message. An action B disabled is not advertised. |
| WHOLESALE-03 | Enable enquiries using the controlled destination and inspect the prepared link/message without sending it. | It identifies B, the chosen design and its own shop link. It uses B's destination and contains no hidden price. |
| WHOLESALE-04 | Return to A and inspect its equivalent product and pricing. | A retains its own content and price policy. Similar IDs and a shared customer login do not mix the businesses. |

The current wholesale preset has shopping actions off. Arrange any action needed for a scenario explicitly; do not assume a wholesale business must offer every retail feature.

## 11. Let one customer save designs from two shops

**Owner goal:** Customers can come back to their choices, and each shop receives only the context belonging to it.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| CUSTOMER-01 | As a guest, choose Add to bag, then sign up or sign in. Repeat and cancel the login prompt. | Successful sign-in resumes the intended action for the original shop. Cancelling makes no change. Normal public browsing requires no login. |
| CUSTOMER-02 | Add A's design to A's bag. Visit B and add B's design, using the same customer account. Return to each bag. | Each bag contains only that shop's selections, with its own price policy. An overlapping product number does not combine them. |
| CUSTOMER-03 | Save favourites in each shop, reload and sign out/in as appropriate. | Favourites stay scoped to the right shop and account/device. A previous customer's private bag never appears for the next account. |
| CUSTOMER-04 | Update the customer's name/phone, then inspect both shop contexts. | Profile details belong to the customer globally. Updating the profile does not replace either shop's bag or visit-request history. |
| CUSTOMER-05 | Sign out and reopen a protected customer page or use another account. | Private shopping information stays private. The user understands when signing out affects other open shop tabs in the same browser. |

## 12. Turn interest into a clear next step

**Owner goal:** A customer knows how to reach us, and we do not promise a confirmed booking or sale when we have only received an expression of interest.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| FOLLOWUP-01 | Open the stores page and select a branch with Design A available. Inspect address, hours and map. | The branch and contact details are usable and belong to this business. Sample or unverified details are labelled honestly. |
| FOLLOWUP-02 | From the bag, request a visit with a chosen branch, date and contact details. Reload the customer's history. | The customer sees a retained reference and requested status. The app clearly says that store confirmation is still required, with no payment or stock guarantee. |
| FOLLOWUP-03 | Try a past date, missing details, no selected branch and a design unavailable at that branch. Retry the same submission after a slow response. | Clear guidance prevents invalid requests and accidental duplicates. An unavailable selection is not represented as a confirmed reservation. |
| FOLLOWUP-04 | Inspect a prepared WhatsApp enquiry for the selected design and branch. | The message has enough correct context for staff to help: business, design/code, shop link and relevant selection. Never send a test message without permission. |
| FOLLOWUP-05 | As the owner, try to find the customer's visit request and complete the promised follow-up. | I have a dependable way to receive, identify and respond to the request. If there is no staff inbox, notification or agreed manual handoff, record a Gap before treating this as an operational lead flow. |

## 13. Try on a design without misleading the buyer

**Owner goal:** The experience helps the buyer choose while representing the real product honestly.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| TRYON-01 | For an enabled supported design, sign in if prompted, allow the camera deliberately and try the offered pieces. | The intended approved design loads with the right shop context. Controls work on the target phone; fit is visually reviewed rather than assumed from a passing test. |
| TRYON-02 | Refuse camera access or use an unsupported device. | Clear guidance lets the customer continue browsing. The site does not trap them or pretend a camera preview succeeded. |
| TRYON-03 | Save/retake/share a sample photo using the offered controls. | The result contains the intended design and framing, without another customer's image or private details. The customer controls any sharing destination. |
| TRYON-04 | Turn try-on off for the shop, publish, then retry from an old page/link. | The current policy is enforced. The customer gets a sensible route back to the shop, and no disabled action is completed. |

## 14. Change my mind safely after launch

**Owner goal:** I can correct a mistake quickly and trust that the public sees the current decision.

| ID | Do this through the application | Owner's expected outcome |
| --- | --- | --- |
| CHANGE-01 | Edit the published design's name/copy and a price input in ERP. Refresh home, shop, detail and an existing bag. | Refreshed views consistently show the new information and calculated price. No manual second entry or rebuild is needed. Old quoted information is not presented as a guaranteed sale price. |
| CHANGE-02 | Revoke the design's publication. Revisit its saved product/image links and public listings. | The withdrawn design and its public image are unavailable. A public link or saved favourite cannot bypass withdrawal. B's corresponding item remains unaffected. |
| CHANGE-03 | Disable bag/reservations/enquiries as appropriate and publish. Retry from a customer tab opened before the change. | The latest owner decision wins. The customer is told to refresh or choose an available action rather than receiving a false success. |
| CHANGE-04 | Take A offline. Open its home, products and image links as a visitor; inspect B separately. | A's public content is unavailable, its saved work is retained, and B is unaffected. Authorized staff can still prepare A privately. |
| CHANGE-05 | Bring A online again through the intended publication control. | The deliberately approved content returns. Hidden prices/actions remain hidden or disabled according to the published settings. |

## 15. Check whether I could rely on this tomorrow

These are **assumed business-readiness requirements**. They expose gaps beyond a successful local demonstration and do not authorize infrastructure changes.

| ID | Owner's acceptance check | Evidence required |
| --- | --- | --- |
| READY-01 | My accounts, ERP edits, customer records and approved shop survive an authorized restart. | A separately authorized persistence test demonstrates this for the intended business environment. Current Sample accounts, item edits and customer records are temporary, so a Sample-only pass is insufficient. |
| READY-02 | I can recover the approved shop and understand who changed it. | Demonstrated backup/restore procedure and an understandable change record for the business owner or responsible support operator. No destructive recovery test on real data. |
| READY-03 | My team has a practical way to manage access and deal with lost credentials. | A demonstrated operator/support workflow with the right business boundaries. Owner self-service staff invitations or password recovery must not be assumed to exist. |
| READY-04 | I can operate every business I am being promised, with durable separated data. | Evidence from the intended environment. Current multi-business verification is Sample-only; current SQL mode supports one business. Do not sell the Sample demonstration as proven multi-business SQL hosting. |
| READY-05 | Real contact, image, camera and provider services work where customers will use them. | Owner-approved photos/destinations, target-device results, and separately authorized live service/hosting checks. A synthetic image or placeholder contact is insufficient. |
| READY-06 | We know what happens after an enquiry or visit request. | Named responsible staff and an actual follow-up channel. Stock holds, confirmed appointments, checkout, payments and order fulfillment need their own agreed workflows if required. |

Known boundaries to record honestly:

- This portal currently supports catalog discovery, shopping state, enquiries and requested visits. Do not call a bag a paid order or a request a confirmed booking.
- Store-side reservation confirmation/stock holds and a complete customer-facing assistant are not established by the completed portal work. Record them as Gaps if the business needs them.
- The website assistant is not a per-business portal feature; any existing website assistant remains tied to the demo business. Do not promise B its own assistant.
- Sample data resets and unverified live persistence are readiness gaps, even when the temporary demo behaves correctly.
- Independent security/code review remains pending. This owner checklist does not replace it.

## 16. Results and the owner's decision

Use one line per scenario ID and retain the evidence for the tested version. All scenario results start as **Not run**; do not copy old development passes into this run.

| Scenario ID | Result | What actually happened | Evidence/link | Business impact | Responsible person and retest |
| --- | --- | --- | --- | --- | --- |
| | Not run | | | | |

Classify issues by the effect on the business:

- **Critical:** another business/customer's data is exposed, an unapproved product is public, hidden prices leak, a wrong amount is represented as current, or saved business data is lost.
- **High:** customers cannot discover an approved design, staff cannot safely publish/withdraw it, the customer is sent to the wrong branch/contact, or a promised lead has no usable follow-up path.
- **Normal:** a task works but needs confusing extra steps, has unclear feedback, or is awkward on a phone.

Record the release decision separately:

| Decision | Owner records |
| --- | --- |
| Suitable for an internal Sample demonstration? | Yes / No, with evidence and known limitations. |
| Suitable for a real customer pilot? | Yes / No, with explicit acceptance of any noncritical gaps and the intended environment. |
| Suitable for normal business operation? | Yes / No. Durability, access, real contacts, operational follow-up and independent review must be accounted for. |
| Unresolved Critical/High issues | Scenario IDs and business impact. |
| Deliberately deferred capabilities | What is deferred, why, who accepted it and how customers/staff avoid a false promise. |
| Sign-off | Owner name, date, tested version and next retest. |

**Owner's final test:** Would I be comfortable giving this shop link to a customer today, and could my staff handle what happens next without a developer sitting beside them?
