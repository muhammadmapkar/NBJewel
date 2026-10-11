/* ==========================================================
   NB Jewel item catalog. SAMPLE schema for demo.
   ALL table and column names are ASSUMED.
   Real NB Jewel schema is unconfirmed.
   Target: SQL Server (T-SQL). Sample data only.
   ========================================================== */

IF DB_ID('NBJewelSample') IS NULL CREATE DATABASE NBJewelSample;
GO
USE NBJewelSample;
GO

/* ---------- 1. LOOKUP TABLES ---------- */

CREATE TABLE dbo.Category (
    CategoryId   INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(100) NOT NULL UNIQUE,   -- Ring, Necklace
    ParentId     INT NULL REFERENCES dbo.Category(CategoryId)
);

CREATE TABLE dbo.Metal (
    MetalId      INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(50) NOT NULL UNIQUE     -- Gold, Silver, Platinum
);

CREATE TABLE dbo.Purity (
    PurityId     INT IDENTITY(1,1) PRIMARY KEY,
    MetalId      INT NOT NULL REFERENCES dbo.Metal(MetalId),
    Label        NVARCHAR(20) NOT NULL,           -- 22K, 18K, 925
    Fineness     DECIMAL(5,4) NOT NULL,           -- 0.9160 for 22K
    CONSTRAINT UQ_Purity UNIQUE (MetalId, Label)
);

CREATE TABLE dbo.Store (
    StoreId      INT IDENTITY(1,1) PRIMARY KEY,
    Code         NVARCHAR(20) NOT NULL UNIQUE,
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

/* ---------- 2. ITEM MASTER ---------- */

CREATE TABLE dbo.Item (
    ItemId        INT IDENTITY(1,1) PRIMARY KEY,
    ItemCode      NVARCHAR(30)  NOT NULL UNIQUE,  -- RNG-0142
    Name          NVARCHAR(200) NOT NULL,
    CategoryId    INT NOT NULL REFERENCES dbo.Category(CategoryId),
    DesignId      INT NOT NULL REFERENCES dbo.Design(DesignId),
    PurityId      INT NOT NULL REFERENCES dbo.Purity(PurityId),
    HuidNumber    NVARCHAR(20) NULL,              -- hallmark id, assumed
    -- ASSUMED workflow: Draft, With Smith, Received, Complete. A status can only
    -- move forward one step. The API enforces the order (ItemStatusRules in the
    -- .NET project) and answers 409 on a bad move. This CHECK only limits the values.
    Status        NVARCHAR(20) NOT NULL DEFAULT 'Draft'
                  CHECK (Status IN ('Draft','With Smith','Received','Complete')),
    GrossWeightG  DECIMAL(10,3) NOT NULL CHECK (GrossWeightG > 0),
    StoneWeightG  DECIMAL(10,3) NOT NULL DEFAULT 0 CHECK (StoneWeightG >= 0),
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

/* ---------- 3. VARIANTS (size etc.) ---------- */

CREATE TABLE dbo.ItemVariant (
    VariantId     INT IDENTITY(1,1) PRIMARY KEY,
    ItemId        INT NOT NULL REFERENCES dbo.Item(ItemId) ON DELETE CASCADE,
    Sku           NVARCHAR(40) NOT NULL UNIQUE,   -- RNG-0142-S14
    SizeLabel     NVARCHAR(20) NULL,
    Barcode       NVARCHAR(40) NULL
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

/* ---------- 5. STOCK PER STORE ---------- */

CREATE TABLE dbo.StockByStore (
    VariantId     INT NOT NULL REFERENCES dbo.ItemVariant(VariantId) ON DELETE CASCADE,
    StoreId       INT NOT NULL REFERENCES dbo.Store(StoreId),
    Quantity      INT NOT NULL DEFAULT 0 CHECK (Quantity >= 0),
    PRIMARY KEY (VariantId, StoreId)
);
-- Stock entry in the API ADDS to Quantity (see "HOW STOCK IS ADDED" below).
-- The API refuses an entry of 0 or less. The CHECK above is a second guard.

/* ---------- 6. DAILY METAL RATES ---------- */

CREATE TABLE dbo.MetalRate (
    RateId        INT IDENTITY(1,1) PRIMARY KEY,
    PurityId      INT NOT NULL REFERENCES dbo.Purity(PurityId),
    RatePerGram   DECIMAL(12,2) NOT NULL CHECK (RatePerGram > 0),
    EffectiveAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_MetalRate_Latest ON dbo.MetalRate(PurityId, EffectiveAt DESC);
GO

/* ==========================================================
   SAMPLE DATA
   ========================================================== */

INSERT dbo.Category (Name) VALUES (N'Ring'), (N'Necklace'), (N'Bangle');
INSERT dbo.Metal (Name) VALUES (N'Gold'), (N'Silver');

-- 10 sample designs (same as sample-data/designs.json).
INSERT dbo.Design (DesignCode, Name) VALUES
 (N'DSN-ZORK', N'Zork'),     (N'DSN-102', N'Classic'),    (N'DSN-103', N'Floral'),
 (N'DSN-104',  N'Twist'),    (N'DSN-105', N'Temple'),     (N'DSN-106', N'Minimal'),
 (N'DSN-107',  N'Vintage'),  (N'DSN-108', N'Bridal'),     (N'DSN-109', N'Daily Wear'),
 (N'DSN-110',  N'Coin Style');

INSERT dbo.Purity (MetalId, Label, Fineness) VALUES
 (1, N'22K', 0.9160),
 (1, N'18K', 0.7500),
 (2, N'925', 0.9250);

INSERT dbo.Store (Code, Name, City) VALUES
 (N'ST01', N'Main Store',   N'Chennai'),
 (N'ST02', N'Second Store', N'Bengaluru'),
 (N'ST03', N'Third Store',  N'Dubai');

INSERT dbo.MetalRate (PurityId, RatePerGram) VALUES
 (1, 6500.00),   -- 22K sample rate
 (2, 5300.00),   -- 18K sample rate
 (3,   85.00);   -- 925 sample rate
GO

/* ==========================================================
   HOW A NEW ITEM IS SAVED (what the API runs on POST)
   One transaction. All rows save, or none.
   ========================================================== */

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ItemId INT, @VariantId INT;

    -- Step 1. Item master row
    INSERT dbo.Item
        (ItemCode, Name, CategoryId, DesignId, PurityId, HuidNumber,
         GrossWeightG, StoneWeightG, MakingType, MakingValue, StoneCharge)
    VALUES
        (N'RNG-0142', N'Zork Ring', 1, 1, 1, N'HUID000142',
         5.200, 0.300, 'PERCENT', 12.00, 1500.00);   -- Status defaults to Draft
    SET @ItemId = SCOPE_IDENTITY();

    -- Step 2. Variant row
    INSERT dbo.ItemVariant (ItemId, Sku, SizeLabel)
    VALUES (@ItemId, N'RNG-0142-S14', N'14');
    SET @VariantId = SCOPE_IDENTITY();

    -- Step 3. Image row (path only)
    INSERT dbo.ItemImage (ItemId, Url, IsPrimary)
    VALUES (@ItemId, N'/images/rng-0142-1.jpg', 1);

    -- Step 4. Opening stock per store
    INSERT dbo.StockByStore (VariantId, StoreId, Quantity) VALUES
        (@VariantId, 1, 4),
        (@VariantId, 2, 2),
        (@VariantId, 3, 0);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;   -- API turns this into a validation error
END CATCH;
GO

/* ==========================================================
   HOW A STATUS MOVES AND HOW STOCK IS ADDED (what the API runs)
   The API checks the status order first, then runs one statement.
   ========================================================== */

-- Move an item one step forward (API has already checked Draft -> With Smith).
UPDATE dbo.Item
SET Status = N'With Smith', UpdatedAt = SYSUTCDATETIME()
WHERE ItemId = 1;

-- Add 5 units for variant 1 in store 1. Creates the row if it is missing.
MERGE dbo.StockByStore WITH (HOLDLOCK) AS t
USING (SELECT 1 AS VariantId, 1 AS StoreId) AS s
  ON t.VariantId = s.VariantId AND t.StoreId = s.StoreId
WHEN MATCHED THEN UPDATE SET Quantity = t.Quantity + 5
WHEN NOT MATCHED THEN INSERT (VariantId, StoreId, Quantity) VALUES (1, 1, 5);
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
    FROM dbo.ItemVariant v
    JOIN dbo.StockByStore sbs ON sbs.VariantId = v.VariantId
    WHERE v.ItemId = i.ItemId
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
    ORDER BY mr.EffectiveAt DESC
) r;
GO

/* ==========================================================
   CHECK QUERIES (run these live in the demo)
   ========================================================== */

-- 1. List screen data
SELECT * FROM dbo.vw_ItemList;

-- 2. Price breakdown
SELECT *, (MetalValue + MakingCharge + StoneCharge) AS PreTaxTotal
FROM dbo.vw_ItemPrice
WHERE ItemCode = N'RNG-0142';

-- 3. Items with their design code and status
SELECT ItemCode, Name, DesignCode, Status FROM dbo.vw_ItemList;

-- 4. Stock by store
SELECT st.Name AS Store, v.Sku, sbs.Quantity
FROM dbo.StockByStore sbs
JOIN dbo.ItemVariant v ON v.VariantId = sbs.VariantId
JOIN dbo.Store st      ON st.StoreId  = sbs.StoreId
JOIN dbo.Item i        ON i.ItemId    = v.ItemId
WHERE i.ItemCode = N'RNG-0142';

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
