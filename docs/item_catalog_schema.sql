/* ==========================================================
   NB Jewel item catalog. SAMPLE schema for demo.
   ALL table and column names are ASSUMED.
   Real NB Jewel schema is unconfirmed.
   Target: SQL Server (T-SQL). Sample data only.

   HOW THIS FILE IS USED
   - It holds tables and views only. It does not create the database
     and it holds no sample rows.
   - "dotnet run --project backend/NbJewel.Api -- --setup-db" creates the
     database named in NBJEWEL_SQL_CONNECTION, runs this file (GO splits it
     into batches), then loads sample-data/*.json (the seed step).
   - Run it only on an empty database. The setup tool skips it when
     dbo.Item already exists.
   ========================================================== */

/* ---------- 1. LOOKUP TABLES ---------- */

CREATE TABLE dbo.Category (
    CategoryId   INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(100) NOT NULL UNIQUE,   -- Rings, Necklaces
    ParentId     INT NULL REFERENCES dbo.Category(CategoryId)
);

CREATE TABLE dbo.Metal (
    MetalId      INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(50) NOT NULL UNIQUE     -- Gold, Silver, Platinum
);

CREATE TABLE dbo.Purity (
    PurityId     INT IDENTITY(1,1) PRIMARY KEY,
    MetalId      INT NOT NULL REFERENCES dbo.Metal(MetalId),
    Label        NVARCHAR(20) NOT NULL,           -- 24K, 22K, 18K, 925
    Fineness     DECIMAL(5,4) NOT NULL,           -- 0.9160 for 22K
    CONSTRAINT UQ_Purity UNIQUE (MetalId, Label)
);

CREATE TABLE dbo.Store (
    StoreId      INT IDENTITY(1,1) PRIMARY KEY,
    Code         NVARCHAR(20) NOT NULL UNIQUE,    -- ASSUMED: the seed makes ST01, ST02 ...
    Name         NVARCHAR(100) NOT NULL,
    City         NVARCHAR(100) NULL
);

-- Design master. Feeds the design code dropdown on the item form.
-- ASSUMED: the product manager's "master chit" means this design master.
CREATE TABLE dbo.Design (
    DesignId     INT IDENTITY(1,1) PRIMARY KEY,
    DesignCode   NVARCHAR(30)  NOT NULL UNIQUE,   -- DSN-ZORK
    Name         NVARCHAR(100) NOT NULL
);
GO

/* ---------- 2. ITEM MASTER ---------- */

CREATE TABLE dbo.Item (
    ItemId        INT IDENTITY(1,1) PRIMARY KEY,
    ItemCode      NVARCHAR(30)  NOT NULL UNIQUE,  -- RNG-0142
    Name          NVARCHAR(200) NOT NULL,
    CategoryId    INT NOT NULL REFERENCES dbo.Category(CategoryId),
    DesignId      INT NOT NULL REFERENCES dbo.Design(DesignId),
    PurityId      INT NOT NULL REFERENCES dbo.Purity(PurityId),
    HuidNumber    NVARCHAR(20) NULL,              -- hallmark id, assumed
    -- ASSUMED statuses: Draft, With Smith, Received, Complete. ASSUMED any-to-any:
    -- any status can be set from any other, and Complete is not final. The API
    -- (ItemStatusRules in the .NET project) refuses an unknown value with 400.
    -- This CHECK is a second guard on the values.
    Status        NVARCHAR(20) NOT NULL DEFAULT 'Draft'
                  CHECK (Status IN ('Draft','With Smith','Received','Complete')),
    GrossWeightG  DECIMAL(10,3) NOT NULL CHECK (GrossWeightG > 0),
    StoneWeightG  DECIMAL(10,3) NOT NULL DEFAULT 0 CHECK (StoneWeightG >= 0),
    -- Net weight is always computed: gross minus stone. The API never writes it.
    NetWeightG    AS (GrossWeightG - StoneWeightG) PERSISTED,
    MakingType    NVARCHAR(10) NOT NULL DEFAULT 'PERCENT'
                  CHECK (MakingType IN ('PERCENT','PER_GRAM','FIXED')),
    MakingValue   DECIMAL(10,2) NOT NULL DEFAULT 0,
    StoneCharge   DECIMAL(12,2) NOT NULL DEFAULT 0,
    CreatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Stone_LE_Gross CHECK (StoneWeightG <= GrossWeightG)
);

CREATE INDEX IX_Item_Category ON dbo.Item(CategoryId);
CREATE INDEX IX_Item_Design   ON dbo.Item(DesignId);
CREATE INDEX IX_Item_Purity   ON dbo.Item(PurityId);
CREATE INDEX IX_Item_Status   ON dbo.Item(Status);
GO

/* ---------- 3. VARIANTS (size etc.) ---------- */

CREATE TABLE dbo.ItemVariant (
    VariantId     INT IDENTITY(1,1) PRIMARY KEY,
    ItemId        INT NOT NULL REFERENCES dbo.Item(ItemId) ON DELETE CASCADE,
    Sku           NVARCHAR(40) NOT NULL UNIQUE,   -- RNG-0142-S14 (the API calls it variantCode)
    SizeLabel     NVARCHAR(20) NULL,
    Barcode       NVARCHAR(40) NULL,
    Color         NVARCHAR(30) NOT NULL DEFAULT N'',        -- ASSUMED column, added to match the API
    PriceDelta    DECIMAL(12,2) NOT NULL DEFAULT 0          -- ASSUMED column, added to match the API
);
CREATE INDEX IX_Variant_Item ON dbo.ItemVariant(ItemId);

/* ---------- 4. IMAGES ---------- */

CREATE TABLE dbo.ItemImage (
    ImageId       INT IDENTITY(1,1) PRIMARY KEY,
    ItemId        INT NOT NULL REFERENCES dbo.Item(ItemId) ON DELETE CASCADE,
    Url           NVARCHAR(500) NOT NULL,         -- file path or blob URL, not the image
    IsPrimary     BIT NOT NULL DEFAULT 0,
    SortOrder     INT NOT NULL DEFAULT 0
);
-- Only one primary image per item
CREATE UNIQUE INDEX UX_ItemImage_Primary
    ON dbo.ItemImage(ItemId) WHERE IsPrimary = 1;
GO

/* ---------- 5. STOCK PER STORE ---------- */

-- ASSUMED redesign: stock is kept per item, optional variant, and store.
-- VariantId is NULL for an item that has no variants (the sample data has such
-- items, and the sample repository stores their stock at item level).
-- VariantKey turns NULL into 0 so the unique index treats "no variant" as one value.
CREATE TABLE dbo.StockByStore (
    StockId       INT IDENTITY(1,1) PRIMARY KEY,
    ItemId        INT NOT NULL REFERENCES dbo.Item(ItemId) ON DELETE CASCADE,
    VariantId     INT NULL REFERENCES dbo.ItemVariant(VariantId),
    StoreId       INT NOT NULL REFERENCES dbo.Store(StoreId),
    Quantity      INT NOT NULL DEFAULT 0 CHECK (Quantity >= 0),   -- on hand
    Reserved      INT NOT NULL DEFAULT 0 CHECK (Reserved >= 0),   -- ASSUMED column, added to match the API
    VariantKey    AS ISNULL(VariantId, 0) PERSISTED
);
CREATE UNIQUE INDEX UX_Stock_Row ON dbo.StockByStore(ItemId, VariantKey, StoreId);
-- Stock entry in the API ADDS to Quantity. The API refuses an entry of 0 or less.
-- The CHECK above is a second guard. A low-stock row means Quantity - Reserved is below the threshold.

/* ---------- 6. DAILY METAL RATES ---------- */

CREATE TABLE dbo.MetalRate (
    RateId        INT IDENTITY(1,1) PRIMARY KEY,
    PurityId      INT NOT NULL REFERENCES dbo.Purity(PurityId),
    RatePerGram   DECIMAL(12,2) NOT NULL CHECK (RatePerGram > 0),
    Currency      NVARCHAR(3) NOT NULL DEFAULT N'AED',   -- ASSUMED column, added to match the API
    EffectiveAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
-- "Latest rate" rule, same in both providers: newest EffectiveAt per purity, higher RateId wins a tie.
CREATE INDEX IX_MetalRate_Latest ON dbo.MetalRate(PurityId, EffectiveAt DESC);
GO

/* ==========================================================
   VIEWS (read-only, used by list screen and chatbot tools)
   ========================================================== */

CREATE VIEW dbo.vw_ItemList AS
SELECT
    i.ItemId, i.ItemCode, i.Name,
    d.DesignCode,
    c.Name  AS Category,
    m.Name  AS Metal,
    p.Label AS Purity,
    i.NetWeightG, i.Status,
    ISNULL(s.TotalQty, 0) AS TotalStock,
    img.Url AS PrimaryImage
FROM dbo.Item i
JOIN dbo.Category c ON c.CategoryId = i.CategoryId
JOIN dbo.Design   d ON d.DesignId   = i.DesignId
JOIN dbo.Purity   p ON p.PurityId   = i.PurityId
JOIN dbo.Metal    m ON m.MetalId    = p.MetalId
LEFT JOIN dbo.ItemImage img ON img.ItemId = i.ItemId AND img.IsPrimary = 1
OUTER APPLY (
    SELECT SUM(sbs.Quantity) AS TotalQty
    FROM dbo.StockByStore sbs
    WHERE sbs.ItemId = i.ItemId
) s;
GO

CREATE VIEW dbo.vw_ItemPrice AS
SELECT
    i.ItemId, i.ItemCode,
    i.NetWeightG,
    r.RatePerGram,
    CAST(i.NetWeightG * r.RatePerGram AS DECIMAL(14,2)) AS MetalValue,
    CAST(CASE i.MakingType
            WHEN 'PERCENT'  THEN i.NetWeightG * r.RatePerGram * i.MakingValue / 100
            WHEN 'PER_GRAM' THEN i.NetWeightG * i.MakingValue
            ELSE i.MakingValue
         END AS DECIMAL(14,2)) AS MakingCharge,
    i.StoneCharge
FROM dbo.Item i
CROSS APPLY (
    SELECT TOP 1 RatePerGram
    FROM dbo.MetalRate mr
    WHERE mr.PurityId = i.PurityId
    ORDER BY mr.EffectiveAt DESC, mr.RateId DESC
) r;
GO

/* ==========================================================
   CHECK QUERIES (run these live in the demo, after the seed step)
   ========================================================== */

-- 1. List screen data
--   SELECT * FROM dbo.vw_ItemList;
-- 2. Price breakdown
--   SELECT *, (MetalValue + MakingCharge + StoneCharge) AS PreTaxTotal
--   FROM dbo.vw_ItemPrice WHERE ItemCode = N'RNG-0142';
-- 3. Stock by store
--   SELECT st.Name AS Store, i.ItemCode, v.Sku, sbs.Quantity, sbs.Reserved
--   FROM dbo.StockByStore sbs
--   JOIN dbo.Store st ON st.StoreId = sbs.StoreId
--   JOIN dbo.Item i   ON i.ItemId   = sbs.ItemId
--   LEFT JOIN dbo.ItemVariant v ON v.VariantId = sbs.VariantId
--   WHERE i.ItemCode = N'RNG-0142';

/* ==========================================================
   READ-ONLY LOGIN FOR THE CHATBOT (Rule 3)
   Change the password. Never commit real passwords.
   The chatbot never writes. Only the UI API writes (item, status, stock),
   and it uses its own login given in the NBJEWEL_SQL_CONNECTION
   environment variable, never this one.
   ========================================================== */
-- CREATE LOGIN chatbot_ro WITH PASSWORD = '<set-in-env>';
-- CREATE USER  chatbot_ro FOR LOGIN chatbot_ro;
-- GRANT SELECT ON dbo.vw_ItemList  TO chatbot_ro;
-- GRANT SELECT ON dbo.vw_ItemPrice TO chatbot_ro;
-- GRANT SELECT ON dbo.StockByStore TO chatbot_ro;
-- GRANT SELECT ON dbo.Store        TO chatbot_ro;
-- GRANT SELECT ON dbo.MetalRate    TO chatbot_ro;
