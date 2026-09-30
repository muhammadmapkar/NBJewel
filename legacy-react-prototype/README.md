# NB Jewel Insights (Prototype)

A dark-themed, mobile-friendly executive dashboard for a jewelry retail ERP. This prototype is powered by deterministic **sample data only**: no backend, database, or login is required.

## Run locally

```bash
npm install
npm run dev
```

Then open the local URL printed by Vite. A production build can be validated with `npm run build`.

---

## Application Structure & Navigation

The dashboard is structured into four functional modules across seven views:

1. **Customers**
   - **Customer Segments (RFM)**: 24-month RFM scoring, quintile rankings, gold scheme boosts, segment playbooks, and CSV export.
   - **Festival Demand Forecast**: Seasonal festival windows (+/- 28 days), category reorder planning, simulated inventory buffers, and backtest WAPE validation.

2. **Stock**
   - **Fine-Gold Reconciliation**: Daily physical vs mathematical vault ledger, drilldown day transaction audit logs, workshop metal issue/receipt tracking, and Karigar 3-Sigma Statistical Process Control (SPC) charts.
   - **Melt, Markdown, or Hold Optimizer**: Outcome simulations for aged SKUs comparing bullion melt recovery, markdown promotional velocity, and holding carry costs with real-time sensitivity sliders.
   - **Design Similarity Search**: Client-side vector cosine similarity search on 300 catalogue items, procedural SVG thumbnails, dominant canvas color matching, matching set suggestions, and Design DNA analytics.

3. **Compliance**
   - **Exception Engine**: Automated surveillance across 1,500 UAE and India store invoices detecting VAT irregularities, missing B2B declarations, BIS HUID non-compliance, and Section 269ST cash caps. Features a printable monthly audit pack.

4. **AI**
   - **Ask Your Data**: Deterministic natural language query assistant powered by a tested metric registry and intent matcher with zero SQL hallucinations.

---

## Production Pipeline Architecture

For every tab, the intended production path follows the pattern:
**SQL view -> nightly summary table -> .NET API -> React page**.

### 1. Customer Segments (RFM)
- **SQL view**: Aggregates customer invoice histories over a rolling 730-day window (`vw_CustomerTransactionRollup`).
- **Nightly summary table**: Computes RFM quintiles, score tiers, and segment classifications into `FactCustomerSegmentDaily`.
- **.NET API**: `GET /api/customers/rfm?storeId={storeId}&categoryId={categoryId}` returns precalculated customer metrics.
- **React page**: Renders KPI cards, distribution bar charts, and actionable segment playbooks.

### 2. Festival Demand Forecast
- **SQL view**: Aligns historical sales around moving festival dates (`vw_FestivalSalesWindow`).
- **Nightly summary table**: Applies seasonal multipliers and gold-rate elasticity indices into `FactFestivalCategoryForecast`.
- **.NET API**: `GET /api/forecast/festival?name={festivalName}&storeId={storeId}` streams series forecast and stock reorder requirements.
- **React page**: Graphs actual vs forecast demand and displays category stock-out risks.

### 3. Fine-Gold Reconciliation
- **SQL view**: Combines bullion consignments, POS daily gross/fine sales, and workshop issue/receipt tickets (`vw_DailyMetalMovement`).
- **Nightly summary table**: Reconciles theoretical closing balance against physical vault scale audits into `FactVaultVarianceDaily` and computes 60-day artisan loss baselines into `DimKarigarSPC`.
- **.NET API**: `GET /api/reconciliation/gold?storeId={storeId}&karigarId={karigarId}` feeds daily ledger discrepancies and SPC control limits.
- **React page**: Visualizes vault balance gaps and plots artisan loss percentages against Upper and Lower Control Limits.

### 4. Compliance Exception Engine
- **SQL view**: Cross-examines invoice line items, tax codes, recipient TRNs, and cash receipts against statutory requirements (`vw_InvoiceComplianceCheck`).
- **Nightly summary table**: Flags statutory violations into `FactComplianceExceptionLog` with exposure value calculations.
- **.NET API**: `GET /api/compliance/exceptions?country={country}&ruleId={ruleId}&severity={severity}` delivers active exceptions.
- **React page**: Interactive audit table, severity breakdown, and printable monthly compliance pack.

### 5. Melt, Markdown, or Hold Optimizer
- **SQL view**: Tracks SKU aging, cumulative holding costs, and historical category sell-through velocity (`vw_AgedInventoryDepreciation`).
- **Nightly summary table**: Periodically refreshes salvage valuations and logistic sale probabilities into `FactAgedStockRecommendations`.
- **.NET API**: `POST /api/inventory/optimize` evaluates real-time sensitivity parameters against warehouse inventories.
- **React page**: Sliders for markdown %, refining loss %, holding costs, and gold spot rates with immediate outcome recalculation.

### 6. Design Similarity Search
- **SQL view**: Joins product master attributes (category, motif, stone, metal colour, weight) with sales velocity (`vw_ProductDesignCatalog`).
- **Nightly summary table**: Pre-indexes item embeddings in a vector store (e.g. pgvector or Azure AI Search) into `DimDesignVectorIndex`.
- **.NET API**: `GET /api/designs/similar?sku={sku}&limit=8` performs approximate nearest neighbor vector lookup.
- **React page**: Renders procedural SVG thumbnails, top similarity scores, matching cross-sell sets, and Design DNA distributions.

### 7. Ask Your Data (AI Assistant)
- **SQL view / Stored Procs**: Fixed repository of parameter-driven analytical stored procedures matching the metric registry.
- **Nightly summary table**: Materialized operational views backing each certified metric.
- **.NET API**: `POST /api/assistant/query` where an LLM agent uses function/tool calling to execute predefined methods in `MetricRegistry.cs`. The assistant **never** executes unconstrained raw SQL.
- **React page**: Chat interface rendering answers, charts, data tables, and explicit "Metric used" attribution.

---

## What is Simulated versus Real

| Feature | Simulated in this Prototype | Real Production Implementation |
| :--- | :--- | :--- |
| **Data Source** | Deterministic pseudo-random generators (`makeRng`) with fixed seeds | Live ERP transactional databases (SQL Server / PostgreSQL / Oracle) |
| **Gold Rate** | Static sample rate (AED 312/g) with manual slider adjustment | Live financial market ticker feeds (LBMA / Dubai Gold & Commodities Exchange) |
| **Karigar Loss Baseline** | Hardcoded 90-day transactions with injected upward drift on artisans K01 and K02 | ERP workshop job management module tracking physical scale weights at dispatch and receipt |
| **Design Thumbnails** | Client-side procedural SVGs drawn dynamically from metadata | High-resolution studio photography and CAD 3D render assets |
| **Design Similarity** | 16-dimensional attribute vectors with in-browser JavaScript cosine similarity | Multimodal deep learning visual embeddings (e.g., CLIP / ResNet / ViT) hosted on a vector database |
| **Photo Upload Matching** | HTML5 Canvas average RGB extraction matching attribute colors | Computer vision image embedding model extracting silhouette, texture, and facet geometry |
| **AI Assistant** | Deterministic keyword scoring and synonym dictionary matcher | Enterprise LLM (e.g., Gemini 1.5 Pro) with strict function / tool calling against a verified API registry |
| **Network Communication** | In-memory asynchronous boundaries with simulated latency (`wait(300)`) | Authenticated REST / gRPC API endpoints with JWT session authentication |

---

## Rules and Thresholds Must Be Verified Before Production

All statutory compliance rules, thresholds, and tax mechanisms included in this prototype are based on general regional regulatory frameworks and must be verified against current official notices before real deployment:

1. **UAE FTA VAT (Federal Decree-Law No. 8 of 2017)**:
   - Standard 5% VAT applies to both fine metal value and craftsmanship charges for retail sales.
   - B2B reverse charge mechanisms (Cabinet Decision No. 25 of 2018) require verified recipient VAT registration and active written declarations on file confirming intent to resell or manufacture.
   - E-invoicing schema requirements (UUID, cryptographic stamp, QR code) are subject to UAE Federal Tax Authority rollout phases.

2. **India BIS Hallmarking (Bureau of Indian Standards)**:
   - Mandatory hallmarking order enforces a 6-digit alphanumeric HUID on gold jewellery sales.
   - **Silver Hallmarking (Rule I6)** is toggled **OFF by default** in this prototype because official regulatory guidance and voluntary implementation timelines have differed across administrative notices and jurisdictions.

3. **India Cash Transaction Ceilings (Income Tax Act, 1961)**:
   - **Section 269ST**: Restricts cash receipts of ₹200,000 or more from a person in a day, in respect of a single transaction, or relating to one event. Breaches carry a 100% statutory penalty under Section 271DA.
   - **Section 206C(1D)** / TCS Review: Flagged for transactions requiring tax collection at source and mandatory customer PAN/Aadhaar compliance.
