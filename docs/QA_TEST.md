<!-- Role: Gives Muhammad a short local QA checklist for ERP jewelry publication and the customer journey. -->
# QA: ERP jewelry to storefront

Allow 15 minutes. Use sample data. Pass = create in ERP, publish, see it on the website, edit it, then revoke it.

## 1. Start the apps

Keep existing frontend servers. Restart the API for fresh credentials/Gemini, using Ctrl+C in its original terminal first. Restart BEFORE testing: new sample items/accounts/reservations reset on restart.

**Terminal 1: backend and temporary logins**

```sh
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel
CUTOUT_PYTHON_PATH=/tmp/nbjewel-cutout-runtime-venv/bin/python python3 scripts/run-sample-api.py --customers --with-services
```

**Terminal 2: ERP**

```sh
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel/frontend
npm start
```

**Terminal 3: customer website**

```sh
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel/customer-tryon
npm start
```

The Python path exists locally and needs Pillow/numpy/scipy. No SQL/Docker needed. If port 5155 is occupied, stop the older API; a failed startup does not change its password.

## 2. URLs and credentials

| Purpose | URL / credentials |
| --- | --- |
| ERP login | [Open ERP](http://localhost:4200/sign-in) |
| ERP email | `sample.staff@nbjewel.invalid` |
| ERP password | Copy **Temporary staff password:** from Terminal 1 after successful startup. Changes on restart; never save it in GitHub. |
| ERP new item | [Create jewelry item](http://localhost:4200/catalog/new) after staff login |
| Actual ERP storefront | [Browse published ERP jewelry](http://localhost:4300/shop?source=erp) |
| Customer account | Private window → Add to bag → Sign up. Test email: `qa.customer@example.invalid`; test-only password: `QaOnly!Jewelry2026`. NOT pre-created: sign up first. Never reuse a real password. |
| Public API evidence | [Published products JSON](http://localhost:5155/api/storefront/products) |

Keep ERP in the normal browser and customers in a private window: localhost ports share login cookies.

**Use `shop?source=erp`, not Home:** Home shows bundled demo jewelry. An empty ERP catalog before publication is normal. No config edits needed.

## 3. Main test: create, upload, publish, update, revoke

Record your item ID from the ERP URL, for example `/catalog/32`. Use that actual ID wherever `ID` appears below.

| Check | Action | Expected result |
| --- | --- | --- |
| [ ] Create | Open Create jewelry item. Code: `QA-NECKLACE-001` (use a new suffix if taken); name: `QA Temple Necklace`; category: **Necklaces**; select an existing design code; metal: **Gold**; purity: **22K**; gross/net weight: **10 g** each; stone weight: **0**; making type: **Percent**; making value: **10**. Leave optional HUID blank. Save. | New item opens with its own ID and starts as Draft. |
| [ ] Complete | Click **Change Status**, select **Complete**, and save the status. Check Overview has a calculated price. | Status is Complete and price is available. |
| [ ] Upload | Open **Images**. Upload a permitted necklace PNG/JPG, at most 5 MB. | Preview generated; quality is pending. Upload alone does not publish. |
| [ ] Gemini | Wait after the preview renders. | Loading message, then editable Description/Collection suggestions. If provider unavailable, manual entry remains possible; record AI as blocked separately. |
| [ ] Save copy | Use description `QA sample necklace for storefront sync testing.` and collection `Temple`. Click **Save storefront details**. | Success message; copy remains when leaving and reopening Images. |
| [ ] Approve | Inspect the actual preview. If acceptable, tick **I checked this preview's visual quality**, then **Approve preview quality**. | Quality becomes approved for the current image. Do not approve a poor cutout just to pass the test. |
| [ ] Publish | Click **Confirm publish**. | Success message and **Storefront: published**. |
| [ ] View | In the private window, open the ERP storefront link above and refresh. Open your new item. | Correct name, image, saved description, collection and price breakdown. Public browsing requires no login. The API JSON contains the same item ID. |
| [ ] Update | In ERP edit the name to `QA Temple Necklace Updated` and making value to **20**; save. Refresh the customer product page. | Updated name and higher making charge/total, without rebuilding either app. |
| [ ] Revoke | Back in ERP Images, click **Revoke publication**. Refresh the ERP storefront list and the previously opened product page. | Item disappears from the list and its detail becomes unavailable. |

Direct product URL: `http://localhost:4300/p/ID?source=erp` (replace `ID`). Keep `source=erp` when testing; clicking Home switches back to the showcase.

**Pass only if publish → appearance → updated copy/price → revoke all work.** The six demo designs are not ERP-sync evidence.

## 4. Other quick checks

- [ ] Guest Add to bag / Try on → signup/login → original action resumes. Canceling login does nothing. Test camera permission/fit manually.
- [ ] Re-generate with AI works; typing during analysis preserves manual edits. Replacing the image hides the old publication until reviewed/published again.
- [ ] Public Stores Map opens without login and labels sample locations honestly. Below 768px, check Home / Stores / Try-On / Bag / Profile navigation.
- [ ] Use a bundled design from Home for the sample bag/reservation/Profile flow. Reservations are requests, not stock holds or real store notifications.

**Stock caveat:** a new item has no store stock automatically. Publishing makes it eligible for the all-ERP catalog, but store-specific listings/reservations also require available inventory. To inspect seeded stock use item **7**, `NKL-0007` / Modern Necklace → Stock tab. Do not run SQL for this test.

## 5. If something fails

- **Can't log in:** use the latest successfully started API's password, not an old terminal's; keep all URLs on `localhost`. Customer credentials work only after signup and expire on API restart.
- **Publish disabled:** check Complete status, nonempty saved description, supported category/metal, a rendered ready preview and manual quality approval.
- **Only demo designs visible:** reopen `http://localhost:4300/shop?source=erp`.
- **Published but missing:** check the public API JSON. If absent, check publication/eligibility. If present, clear UI filters, verify `source=erp` and reload.
- **AI unavailable:** root `.env` needs `GEMINI_MODEL` and either `GEMINI_ERP_API_KEY` or fallback `GEMINI_API_KEY`; start with `--with-services`. Manual publication testing can continue. Never put keys in frontend files.
- **Live metal feed unavailable:** `METAL_RATES_API_KEY` is optional and currently missing; recorded sample selling rates still work. This does not block the main sync test.

Report a failure with: checkbox/step, item ID/code, page URL, expected versus actual result, screenshot and visible error text. Exclude passwords/API keys. Full setup inventory: [portal upgrades](portal-upgrades.md).
