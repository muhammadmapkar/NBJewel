<!-- Role: Owner-run setup and manager QA for the single SQL-backed ERP/storefront demo. -->
# One SQL-backed catalog: setup and manager QA

The application changes and image files are prepared. Docker setup, SQL scripts, identity bootstrap and imports below are **owner-run steps and have not been executed by the agent**. Existing sample API sessions remain running. Do not present the current sample session as SQL-backed.

The intended path is ERP -> .NET API -> SQL Server in Docker -> public API -> customer storefront. Image bytes remain in persistent disk folders; SQL stores the item, image revision/hashes, approval and publication. Browsers never connect to SQL or receive connection credentials.

## 1. Preserve the current QA item before stopping Sample mode

The agent preserved `QA-NECKLACE-001` at:

`backend/NbJewel.Api/App_Data/sql-migration/20261115-153502-687583/`

This ignored local folder contains `public-product.json`, original image, generated preview, original asset metadata and `import.json`. It contains public catalog fields, not private HUID/design/stock information. If those fields matter, record/export them from the ERP before stopping the sample API. Keep the entire folder and original sample asset directory.

To capture a newer public snapshot yourself while the sample API is running:

```bash
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel
python3 scripts/export-published-demo.py --code QA-NECKLACE-001
```

The original sample database is memory-only. Its temporary staff account/password will not work in SQL mode. New SQL item IDs are assigned by SQL; the item code is preserved. Publication and quality approval must be repeated manually after importing the preserved image. The importer never recreates an old approval automatically.

## 2. Inspect Docker and prepare a dedicated demo database

Use only local sample data. These steps preserve existing records and do not drop databases or volumes. Do not use the older `setup-db.sh --reset` workflow.

```bash
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
```

The repository's container is `nbjewel-sqlserver`, port 1433. If it is stopped, start it yourself through Docker Desktop. If it does not exist, configure `MSSQL_SA_PASSWORD` privately and review the existing compose file before starting it. Never use `docker compose down -v` for this demo.

Copy the prepared scripts into the running container:

```bash
docker cp docs/sql-demo-schema.sql nbjewel-sqlserver:/tmp/sql-demo-schema.sql
docker cp docs/sql-demo-reference-data.sql nbjewel-sqlserver:/tmp/sql-demo-reference-data.sql
docker cp docs/sql-demo-inspect.sql nbjewel-sqlserver:/tmp/sql-demo-inspect.sql
docker exec -it nbjewel-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa
```

Enter your SQL Server password at its prompt. It is different from the ERP password. Inside sqlcmd:

```sql
SELECT name FROM sys.databases;
GO
IF DB_ID(N'NBJewelDemo') IS NULL CREATE DATABASE [NBJewelDemo];
GO
USE [NBJewelDemo];
GO
:r /tmp/sql-demo-schema.sql
GO
:r /tmp/sql-demo-reference-data.sql
GO
SELECT StoreId, Code, Name FROM dbo.Store;
GO
EXIT
```

Use this dedicated `NBJewelDemo` database for all three backend connections below. If it already contains valuable data, take a normal SQL backup first. The schema script creates missing tables/indexes and adds only the known missing inventory/publication columns. It checks required columns/types and rejects legacy identity-generated publication keys. An incompatible schema aborts the transaction; do not reset it to bypass the error. It does not seed or overwrite items/accounts.

Reference data adds only missing categories, the `DEMO-HERITAGE` design, `DEMO01` store and metal/purity rows. It adds historical sample rates only where a purity has no rate. Coordinates are explicitly assumed sample Dubai city-center coordinates. No verified store location, live sync, real price or business stock is claimed.

## 3. Configure and launch the SQL API

Set these backend variables yourself in the ignored root `.env` or your shell. The agent has not read or modified that secret file.

| Variable | Local demo setting |
|---|---|
| `Repository__Provider` | `Sql` |
| `NBJEWEL_SQL_CONNECTION` | SQL connection to `NBJewelDemo` |
| `NBJEWEL_IDENTITY_CONNECTION` | Same dedicated demo database |
| `AI_PLATFORM_CONNECTION` | Same dedicated demo database |
| `ITEM_ASSET_STORAGE_PATH` | Optional durable directory; launcher defaults to `backend/NbJewel.Api/App_Data/sql-demo/assets` |
| `CUTOUT_PYTHON_PATH` | Interpreter with the existing Pillow/numpy/scipy dependencies |
| `NBJEWEL_BOOTSTRAP_EMAIL` | Your chosen local-demo staff email |
| `NBJEWEL_BOOTSTRAP_PASSWORD` | Your own 12+ character password with upper/lower case, number and symbol |

Connection format, with your own password substituted privately:

```text
Server=localhost,1433;Database=NBJewelDemo;User Id=sa;Password=<your SQL password>;Encrypt=True;TrustServerCertificate=True
```

`sa` is for this local learning demo only. Set the three connection variables to the same value; the prepared inspection queries assume one database. Do not put these values in Angular configuration or commit them. Gemini keys remain optional and backend-only; manual image descriptions work without them.

In a new Terminal, load your private settings:

```bash
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel
set -a
source .env
set +a
python3 scripts/run-sql-api.py --check
```

`--check` checks that required variable names have values. It does not connect to SQL or validate passwords/schema. The SQL launcher refuses to take over port 5155. Once your QA snapshot is safe, stop the old API yourself in its Terminal with Ctrl+C.

Bootstrap one SQL staff account, with Sample identity disabled:

```bash
Repository__Provider=Sql SampleIdentity__Enabled=false ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development \
  dotnet run --project backend/NbJewel.Api --no-launch-profile -- --bootstrap-identity
python3 scripts/run-sql-api.py
```

Bootstrap creates an explicit all-store local ERP account. If that email already exists it refuses to elevate or replace it. Use your existing SQL ERP account instead. There is no committed default password. Local Development uses ephemeral cookie protection by default, so you may need to sign in again after API restart; SQL account/product data persists.

Keep SQL Server running and retain both its Docker volume and `ITEM_ASSET_STORAGE_PATH`. SQL alone does not back up image bytes.

Start the two web apps in separate terminals if they are not already running:

```bash
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel/frontend
npm start
```

```bash
cd /Users/muhammadnazirahmedmapkar/Documents/NBJewel/customer-tryon
npm start
```

ERP: `http://localhost:4200`. Storefront: `http://localhost:4300`. Both now use the same API at `http://localhost:5155`. No `source=erp` parameter is needed. Legacy `source=demo` links select the ERP catalog; obsolete demo product slugs do not identify new SQL rows. Missing/unavailable API data shows an error or empty state, never static products.

## 4. Import the images and preserved QA product

First validate offline:

```bash
python3 scripts/import-erp-demo.py
```

`sample-data/Jewellery Images/demo-import.json` points to 12 Met Open Access photographs and six original owner-provided photos in that folder. Each entry records a stable item code, image hash, provenance and license status. Weights, purity, charges and stock are **assumed sample values**. For a photo showing a multi-piece set, verify that the ERP cutout isolates the right piece before approval.

Use the `StoreId` for `DEMO01` from step 2. Replace `YOUR_STORE_ID` below with that number:

```bash
python3 scripts/import-erp-demo.py --apply --store-id YOUR_STORE_ID
```

The importer prompts for the SQL ERP email/password, keeps cookies only in memory and verifies the API reports `Sql`. It uses existing authenticated item/upload/status/stock APIs, including CSRF checks. It creates eight new Complete items pending review and four new Draft items. The six older images remain Draft because their licensing is unresolved. One assumed stock unit is added only to each newly created item when `--store-id` is supplied.

Re-running skips every matching item code, including retailer-edited items. It does not replace images, add duplicate stock or change existing item fields. If a run stops after creation, the printed item ID lets you finish that item manually in the ERP; retry deliberately skips it rather than overwriting partial/user work. Run only one importer at a time.

Import the preserved QA item separately:

```bash
python3 scripts/import-erp-demo.py --manifest backend/NbJewel.Api/App_Data/sql-migration/20261115-153502-687583/import.json --apply --store-id YOUR_STORE_ID
```

Restore any private design/HUID/stock fields you recorded in step 1. This preserved image imports as Draft. Review licensing and copy, set Complete, inspect the preview, approve that revision and confirm publication manually. If the code already exists in SQL, inspect that record; the importer preserves it unchanged.

For each prepared Complete item in ERP: Inventory -> Item Catalog -> item -> Images. Inspect the generated cutout, edit/save Description and Collection, tick the visual-quality checkbox, approve preview quality, then Confirm publish. Technical generation is not visual approval. Paired-earring photographs may require a suitable single-earring upload for realistic try-on; do not approve a poor fit merely to fill the grid.

## 5. Show managers the complete flow

1. Sign into ERP with your SQL staff account. Create `QA-SQL-001` with a unique name, valid reference fields and explicitly assumed weights/pricing.
2. Upload a permitted photo. Save description and collection. In SQL, run `docs/sql-demo-inspect.sql`: show the same item code, description, revision, hashes and pending review.
3. Set Complete, manually approve the current preview and publish. SQL should show `QualityReview=approved`, `Published=1`, and matching `RevisionId`/`PublishedRevision`.
4. Open `http://localhost:5155/api/storefront/products`, then `http://localhost:4300/shop`. Find the same name/code. Home and collections use this same API.
5. Edit the description and making charge in ERP, save, reload SQL/API/storefront and verify they agree.
6. Replace the photo: the new revision must be pending and the old publication must no longer expose the item. Approve/publish the new revision manually.
7. Revoke publication: the SQL item remains, but the public API, detail and preview become unavailable. Republish manually if desired.
8. Restart only the API, sign in again if needed, and verify the product/copy/approval and customer records remain. Stop/start Docker only yourself while retaining its volume; verify persistence again.
9. Check desktop and a narrow mobile browser viewport. Browsing stays public; try-on/bag/reservation require customer login and resume the selected product. Store-filtered catalogs require stock at that store.

An image file on disk does not itself create an ERP row. A SQL item is not public until it meets the existing publication policy. Prices remain recorded sample rates unless a separately configured provider supplies a reference quote.

## Verification and limits

Automated checks are recorded in `AI-MEMORY-HANDOFF.md`. Offline model tests and in-memory HTTP tests do not establish live SQL connectivity. A new opt-in SQL persistence test requires a separately prepared disposable `NBJewelCatalogTest` on `127.0.0.1,15433` using `NBJEWEL_CATALOG_TEST_CONNECTION`; never point it at the demo database. It leaves uniquely named test rows for inspection. Existing SQL test suites have their own isolated targets and must be reviewed before use.

No SQL/Docker step, account bootstrap, database import, real-camera fit or human quality approval was performed by the agent. No linter is configured in either Angular package. The separate business Web Portal customization plan remains unimplemented.
