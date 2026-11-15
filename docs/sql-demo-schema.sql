-- Role: Generated additive SQL demo schema for one owner-selected database. Review before running.
-- Run with sqlcmd -b. Does not create/drop databases, seed data, or replace existing tables.
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Item', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Item', N'StorefrontJson') IS NULL EXEC(N'ALTER TABLE dbo.Item ADD StorefrontJson nvarchar(max) NULL CHECK (StorefrontJson IS NULL OR ISJSON(StorefrontJson)=1);');
IF OBJECT_ID(N'dbo.Item', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Item', N'StoneCharge') IS NULL EXEC(N'ALTER TABLE dbo.Item ADD StoneCharge decimal(12,2) NOT NULL DEFAULT 0 CHECK (StoneCharge >= 0);');
IF OBJECT_ID(N'dbo.Item', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Item', N'VatPercent') IS NULL EXEC(N'ALTER TABLE dbo.Item ADD VatPercent decimal(5,2) NOT NULL DEFAULT 0 CHECK (VatPercent BETWEEN 0 AND 100);');
IF OBJECT_ID(N'dbo.AiPublicationProducts', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.AiPublicationProducts', N'AssetRevisionId') IS NULL EXEC(N'ALTER TABLE dbo.AiPublicationProducts ADD AssetRevisionId nvarchar(32) NULL;');
IF OBJECT_ID(N'dbo.Category', N'U') IS NULL EXEC(N'CREATE TABLE dbo.Category (
    CategoryId   INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(100) NOT NULL UNIQUE,
    ParentId     INT NULL REFERENCES dbo.Category(CategoryId)
);');
IF COL_LENGTH(N'dbo.Category', N'CategoryId') IS NULL THROW 51000, 'Incompatible schema: dbo.Category.CategoryId missing', 1;
IF COL_LENGTH(N'dbo.Category', N'Name') IS NULL THROW 51000, 'Incompatible schema: dbo.Category.Name missing', 1;
IF COL_LENGTH(N'dbo.Category', N'ParentId') IS NULL THROW 51000, 'Incompatible schema: dbo.Category.ParentId missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Category') AND name=N'CategoryId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Category.CategoryId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Category') AND name=N'Name' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>200)) THROW 51000, 'Incompatible type: dbo.Category.Name', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Category') AND name=N'ParentId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Category.ParentId', 1;
IF OBJECT_ID(N'dbo.Metal', N'U') IS NULL EXEC(N'CREATE TABLE dbo.Metal (
    MetalId      INT IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(50) NOT NULL UNIQUE
);');
IF COL_LENGTH(N'dbo.Metal', N'MetalId') IS NULL THROW 51000, 'Incompatible schema: dbo.Metal.MetalId missing', 1;
IF COL_LENGTH(N'dbo.Metal', N'Name') IS NULL THROW 51000, 'Incompatible schema: dbo.Metal.Name missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Metal') AND name=N'MetalId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Metal.MetalId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Metal') AND name=N'Name' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>100)) THROW 51000, 'Incompatible type: dbo.Metal.Name', 1;
IF OBJECT_ID(N'dbo.Purity', N'U') IS NULL EXEC(N'CREATE TABLE dbo.Purity (
    PurityId     INT IDENTITY(1,1) PRIMARY KEY,
    MetalId      INT NOT NULL REFERENCES dbo.Metal(MetalId),
    Label        NVARCHAR(20) NOT NULL,
    Fineness     DECIMAL(5,4) NOT NULL,
    CONSTRAINT UQ_Purity UNIQUE (MetalId, Label)
);');
IF COL_LENGTH(N'dbo.Purity', N'PurityId') IS NULL THROW 51000, 'Incompatible schema: dbo.Purity.PurityId missing', 1;
IF COL_LENGTH(N'dbo.Purity', N'MetalId') IS NULL THROW 51000, 'Incompatible schema: dbo.Purity.MetalId missing', 1;
IF COL_LENGTH(N'dbo.Purity', N'Label') IS NULL THROW 51000, 'Incompatible schema: dbo.Purity.Label missing', 1;
IF COL_LENGTH(N'dbo.Purity', N'Fineness') IS NULL THROW 51000, 'Incompatible schema: dbo.Purity.Fineness missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Purity') AND name=N'PurityId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Purity.PurityId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Purity') AND name=N'MetalId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Purity.MetalId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Purity') AND name=N'Label' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>40)) THROW 51000, 'Incompatible type: dbo.Purity.Label', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Purity') AND name=N'Fineness' AND (TYPE_NAME(system_type_id)<>N'decimal' OR precision<>5 OR scale<>4)) THROW 51000, 'Incompatible type: dbo.Purity.Fineness', 1;
IF OBJECT_ID(N'dbo.Store', N'U') IS NULL EXEC(N'CREATE TABLE dbo.Store (
    StoreId      INT IDENTITY(1,1) PRIMARY KEY,
    Code         NVARCHAR(20) NOT NULL UNIQUE,
    Name         NVARCHAR(100) NOT NULL,
    City         NVARCHAR(100) NULL
);');
IF COL_LENGTH(N'dbo.Store', N'StoreId') IS NULL THROW 51000, 'Incompatible schema: dbo.Store.StoreId missing', 1;
IF COL_LENGTH(N'dbo.Store', N'Code') IS NULL THROW 51000, 'Incompatible schema: dbo.Store.Code missing', 1;
IF COL_LENGTH(N'dbo.Store', N'Name') IS NULL THROW 51000, 'Incompatible schema: dbo.Store.Name missing', 1;
IF COL_LENGTH(N'dbo.Store', N'City') IS NULL THROW 51000, 'Incompatible schema: dbo.Store.City missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Store') AND name=N'StoreId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Store.StoreId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Store') AND name=N'Code' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>40)) THROW 51000, 'Incompatible type: dbo.Store.Code', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Store') AND name=N'Name' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>200)) THROW 51000, 'Incompatible type: dbo.Store.Name', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Store') AND name=N'City' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>200)) THROW 51000, 'Incompatible type: dbo.Store.City', 1;
IF OBJECT_ID(N'dbo.Design', N'U') IS NULL EXEC(N'CREATE TABLE dbo.Design (
    DesignId     INT IDENTITY(1,1) PRIMARY KEY,
    DesignCode   NVARCHAR(30)  NOT NULL UNIQUE,
    Name         NVARCHAR(100) NOT NULL
);');
IF COL_LENGTH(N'dbo.Design', N'DesignId') IS NULL THROW 51000, 'Incompatible schema: dbo.Design.DesignId missing', 1;
IF COL_LENGTH(N'dbo.Design', N'DesignCode') IS NULL THROW 51000, 'Incompatible schema: dbo.Design.DesignCode missing', 1;
IF COL_LENGTH(N'dbo.Design', N'Name') IS NULL THROW 51000, 'Incompatible schema: dbo.Design.Name missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Design') AND name=N'DesignId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Design.DesignId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Design') AND name=N'DesignCode' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>60)) THROW 51000, 'Incompatible type: dbo.Design.DesignCode', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Design') AND name=N'Name' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>200)) THROW 51000, 'Incompatible type: dbo.Design.Name', 1;
IF OBJECT_ID(N'dbo.Item', N'U') IS NULL EXEC(N'CREATE TABLE dbo.Item (
    ItemId        INT IDENTITY(1,1) PRIMARY KEY,
    ItemCode      NVARCHAR(30)  NOT NULL UNIQUE,
    Name          NVARCHAR(200) NOT NULL,
    CategoryId    INT NOT NULL REFERENCES dbo.Category(CategoryId),
    DesignId      INT NOT NULL REFERENCES dbo.Design(DesignId),
    PurityId      INT NOT NULL REFERENCES dbo.Purity(PurityId),
    HuidNumber    NVARCHAR(20) NULL,




    Status        NVARCHAR(20) NOT NULL DEFAULT ''Draft''
                  CHECK (Status IN (''Draft'',''With Smith'',''Received'',''Complete'')),
    GrossWeightG  DECIMAL(10,3) NOT NULL CHECK (GrossWeightG > 0),
    StoneWeightG  DECIMAL(10,3) NOT NULL DEFAULT 0 CHECK (StoneWeightG >= 0),

    NetWeightG    AS (GrossWeightG - StoneWeightG) PERSISTED,
    MakingType    NVARCHAR(10) NOT NULL DEFAULT ''PERCENT''
                  CHECK (MakingType IN (''PERCENT'',''PER_GRAM'',''FIXED'')),
    MakingValue   DECIMAL(10,2) NOT NULL DEFAULT 0,
    StoneCharge   DECIMAL(12,2) NOT NULL DEFAULT 0 CHECK (StoneCharge >= 0),

    VatPercent    DECIMAL(5,2) NOT NULL DEFAULT 0 CHECK (VatPercent BETWEEN 0 AND 100),



    StorefrontJson NVARCHAR(MAX) NULL
                   CHECK (StorefrontJson IS NULL OR ISJSON(StorefrontJson) = 1),
    CreatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Stone_LE_Gross CHECK (StoneWeightG <= GrossWeightG)
);');
IF COL_LENGTH(N'dbo.Item', N'ItemId') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.ItemId missing', 1;
IF COL_LENGTH(N'dbo.Item', N'ItemCode') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.ItemCode missing', 1;
IF COL_LENGTH(N'dbo.Item', N'Name') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.Name missing', 1;
IF COL_LENGTH(N'dbo.Item', N'CategoryId') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.CategoryId missing', 1;
IF COL_LENGTH(N'dbo.Item', N'DesignId') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.DesignId missing', 1;
IF COL_LENGTH(N'dbo.Item', N'PurityId') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.PurityId missing', 1;
IF COL_LENGTH(N'dbo.Item', N'HuidNumber') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.HuidNumber missing', 1;
IF COL_LENGTH(N'dbo.Item', N'Status') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.Status missing', 1;
IF COL_LENGTH(N'dbo.Item', N'GrossWeightG') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.GrossWeightG missing', 1;
IF COL_LENGTH(N'dbo.Item', N'StoneWeightG') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.StoneWeightG missing', 1;
IF COL_LENGTH(N'dbo.Item', N'MakingType') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.MakingType missing', 1;
IF COL_LENGTH(N'dbo.Item', N'MakingValue') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.MakingValue missing', 1;
IF COL_LENGTH(N'dbo.Item', N'StoneCharge') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.StoneCharge missing', 1;
IF COL_LENGTH(N'dbo.Item', N'VatPercent') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.VatPercent missing', 1;
IF COL_LENGTH(N'dbo.Item', N'StorefrontJson') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.StorefrontJson missing', 1;
IF COL_LENGTH(N'dbo.Item', N'CreatedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.CreatedAt missing', 1;
IF COL_LENGTH(N'dbo.Item', N'UpdatedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.Item.UpdatedAt missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'ItemId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Item.ItemId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'ItemCode' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>60)) THROW 51000, 'Incompatible type: dbo.Item.ItemCode', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'Name' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>400)) THROW 51000, 'Incompatible type: dbo.Item.Name', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'CategoryId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Item.CategoryId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'DesignId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Item.DesignId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'PurityId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.Item.PurityId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'HuidNumber' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>40)) THROW 51000, 'Incompatible type: dbo.Item.HuidNumber', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'Status' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>40)) THROW 51000, 'Incompatible type: dbo.Item.Status', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'GrossWeightG' AND (TYPE_NAME(system_type_id)<>N'decimal' OR precision<>10 OR scale<>3)) THROW 51000, 'Incompatible type: dbo.Item.GrossWeightG', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'StoneWeightG' AND (TYPE_NAME(system_type_id)<>N'decimal' OR precision<>10 OR scale<>3)) THROW 51000, 'Incompatible type: dbo.Item.StoneWeightG', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'MakingType' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>20)) THROW 51000, 'Incompatible type: dbo.Item.MakingType', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'MakingValue' AND (TYPE_NAME(system_type_id)<>N'decimal' OR precision<>10 OR scale<>2)) THROW 51000, 'Incompatible type: dbo.Item.MakingValue', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'StoneCharge' AND (TYPE_NAME(system_type_id)<>N'decimal' OR precision<>12 OR scale<>2)) THROW 51000, 'Incompatible type: dbo.Item.StoneCharge', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'VatPercent' AND (TYPE_NAME(system_type_id)<>N'decimal' OR precision<>5 OR scale<>2)) THROW 51000, 'Incompatible type: dbo.Item.VatPercent', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'StorefrontJson' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.Item.StorefrontJson', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'CreatedAt' AND (TYPE_NAME(system_type_id)<>N'datetime2')) THROW 51000, 'Incompatible type: dbo.Item.CreatedAt', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'UpdatedAt' AND (TYPE_NAME(system_type_id)<>N'datetime2')) THROW 51000, 'Incompatible type: dbo.Item.UpdatedAt', 1;
IF OBJECT_ID(N'dbo.ItemVariant', N'U') IS NULL EXEC(N'CREATE TABLE dbo.ItemVariant (
    VariantId     INT IDENTITY(1,1) PRIMARY KEY,
    ItemId        INT NOT NULL REFERENCES dbo.Item(ItemId) ON DELETE CASCADE,
    Sku           NVARCHAR(40) NOT NULL UNIQUE,
    SizeLabel     NVARCHAR(20) NULL,
    Barcode       NVARCHAR(40) NULL,
    Color         NVARCHAR(30) NOT NULL DEFAULT N'''',
    PriceDelta    DECIMAL(12,2) NOT NULL DEFAULT 0
);');
IF COL_LENGTH(N'dbo.ItemVariant', N'VariantId') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemVariant.VariantId missing', 1;
IF COL_LENGTH(N'dbo.ItemVariant', N'ItemId') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemVariant.ItemId missing', 1;
IF COL_LENGTH(N'dbo.ItemVariant', N'Sku') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemVariant.Sku missing', 1;
IF COL_LENGTH(N'dbo.ItemVariant', N'SizeLabel') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemVariant.SizeLabel missing', 1;
IF COL_LENGTH(N'dbo.ItemVariant', N'Barcode') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemVariant.Barcode missing', 1;
IF COL_LENGTH(N'dbo.ItemVariant', N'Color') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemVariant.Color missing', 1;
IF COL_LENGTH(N'dbo.ItemVariant', N'PriceDelta') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemVariant.PriceDelta missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemVariant') AND name=N'VariantId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.ItemVariant.VariantId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemVariant') AND name=N'ItemId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.ItemVariant.ItemId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemVariant') AND name=N'Sku' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>80)) THROW 51000, 'Incompatible type: dbo.ItemVariant.Sku', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemVariant') AND name=N'SizeLabel' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>40)) THROW 51000, 'Incompatible type: dbo.ItemVariant.SizeLabel', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemVariant') AND name=N'Barcode' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>80)) THROW 51000, 'Incompatible type: dbo.ItemVariant.Barcode', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemVariant') AND name=N'Color' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>60)) THROW 51000, 'Incompatible type: dbo.ItemVariant.Color', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemVariant') AND name=N'PriceDelta' AND (TYPE_NAME(system_type_id)<>N'decimal' OR precision<>12 OR scale<>2)) THROW 51000, 'Incompatible type: dbo.ItemVariant.PriceDelta', 1;
IF OBJECT_ID(N'dbo.ItemImage', N'U') IS NULL EXEC(N'CREATE TABLE dbo.ItemImage (
    ImageId       INT IDENTITY(1,1) PRIMARY KEY,
    ItemId        INT NOT NULL REFERENCES dbo.Item(ItemId) ON DELETE CASCADE,
    Url           NVARCHAR(500) NOT NULL,
    IsPrimary     BIT NOT NULL DEFAULT 0,
    SortOrder     INT NOT NULL DEFAULT 0
);');
IF COL_LENGTH(N'dbo.ItemImage', N'ImageId') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImage.ImageId missing', 1;
IF COL_LENGTH(N'dbo.ItemImage', N'ItemId') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImage.ItemId missing', 1;
IF COL_LENGTH(N'dbo.ItemImage', N'Url') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImage.Url missing', 1;
IF COL_LENGTH(N'dbo.ItemImage', N'IsPrimary') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImage.IsPrimary missing', 1;
IF COL_LENGTH(N'dbo.ItemImage', N'SortOrder') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImage.SortOrder missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImage') AND name=N'ImageId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.ItemImage.ImageId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImage') AND name=N'ItemId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.ItemImage.ItemId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImage') AND name=N'Url' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>1000)) THROW 51000, 'Incompatible type: dbo.ItemImage.Url', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImage') AND name=N'IsPrimary' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.ItemImage.IsPrimary', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImage') AND name=N'SortOrder' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.ItemImage.SortOrder', 1;
IF OBJECT_ID(N'dbo.StockByStore', N'U') IS NULL EXEC(N'CREATE TABLE dbo.StockByStore (
    StockId       INT IDENTITY(1,1) PRIMARY KEY,
    ItemId        INT NOT NULL REFERENCES dbo.Item(ItemId) ON DELETE CASCADE,
    VariantId     INT NULL REFERENCES dbo.ItemVariant(VariantId),
    StoreId       INT NOT NULL REFERENCES dbo.Store(StoreId),
    Quantity      INT NOT NULL DEFAULT 0 CHECK (Quantity >= 0),
    Reserved      INT NOT NULL DEFAULT 0 CHECK (Reserved >= 0),
    VariantKey    AS ISNULL(VariantId, 0) PERSISTED
);');
IF COL_LENGTH(N'dbo.StockByStore', N'StockId') IS NULL THROW 51000, 'Incompatible schema: dbo.StockByStore.StockId missing', 1;
IF COL_LENGTH(N'dbo.StockByStore', N'ItemId') IS NULL THROW 51000, 'Incompatible schema: dbo.StockByStore.ItemId missing', 1;
IF COL_LENGTH(N'dbo.StockByStore', N'VariantId') IS NULL THROW 51000, 'Incompatible schema: dbo.StockByStore.VariantId missing', 1;
IF COL_LENGTH(N'dbo.StockByStore', N'StoreId') IS NULL THROW 51000, 'Incompatible schema: dbo.StockByStore.StoreId missing', 1;
IF COL_LENGTH(N'dbo.StockByStore', N'Quantity') IS NULL THROW 51000, 'Incompatible schema: dbo.StockByStore.Quantity missing', 1;
IF COL_LENGTH(N'dbo.StockByStore', N'Reserved') IS NULL THROW 51000, 'Incompatible schema: dbo.StockByStore.Reserved missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StockByStore') AND name=N'StockId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.StockByStore.StockId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StockByStore') AND name=N'ItemId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.StockByStore.ItemId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StockByStore') AND name=N'VariantId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.StockByStore.VariantId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StockByStore') AND name=N'StoreId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.StockByStore.StoreId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StockByStore') AND name=N'Quantity' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.StockByStore.Quantity', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StockByStore') AND name=N'Reserved' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.StockByStore.Reserved', 1;
IF OBJECT_ID(N'dbo.MetalRate', N'U') IS NULL EXEC(N'CREATE TABLE dbo.MetalRate (
    RateId        INT IDENTITY(1,1) PRIMARY KEY,
    PurityId      INT NOT NULL REFERENCES dbo.Purity(PurityId),
    RatePerGram   DECIMAL(12,2) NOT NULL CHECK (RatePerGram > 0),
    Currency      NVARCHAR(3) NOT NULL DEFAULT N''AED'',
    EffectiveAt   DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);');
IF COL_LENGTH(N'dbo.MetalRate', N'RateId') IS NULL THROW 51000, 'Incompatible schema: dbo.MetalRate.RateId missing', 1;
IF COL_LENGTH(N'dbo.MetalRate', N'PurityId') IS NULL THROW 51000, 'Incompatible schema: dbo.MetalRate.PurityId missing', 1;
IF COL_LENGTH(N'dbo.MetalRate', N'RatePerGram') IS NULL THROW 51000, 'Incompatible schema: dbo.MetalRate.RatePerGram missing', 1;
IF COL_LENGTH(N'dbo.MetalRate', N'Currency') IS NULL THROW 51000, 'Incompatible schema: dbo.MetalRate.Currency missing', 1;
IF COL_LENGTH(N'dbo.MetalRate', N'EffectiveAt') IS NULL THROW 51000, 'Incompatible schema: dbo.MetalRate.EffectiveAt missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.MetalRate') AND name=N'RateId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.MetalRate.RateId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.MetalRate') AND name=N'PurityId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.MetalRate.PurityId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.MetalRate') AND name=N'RatePerGram' AND (TYPE_NAME(system_type_id)<>N'decimal' OR precision<>12 OR scale<>2)) THROW 51000, 'Incompatible type: dbo.MetalRate.RatePerGram', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.MetalRate') AND name=N'Currency' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>6)) THROW 51000, 'Incompatible type: dbo.MetalRate.Currency', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.MetalRate') AND name=N'EffectiveAt' AND (TYPE_NAME(system_type_id)<>N'datetime2')) THROW 51000, 'Incompatible type: dbo.MetalRate.EffectiveAt', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'IX_Item_Category') EXEC(N'CREATE INDEX IX_Item_Category ON dbo.Item(CategoryId);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'IX_Item_Design') EXEC(N'CREATE INDEX IX_Item_Design   ON dbo.Item(DesignId);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'IX_Item_Purity') EXEC(N'CREATE INDEX IX_Item_Purity   ON dbo.Item(PurityId);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Item') AND name=N'IX_Item_Status') EXEC(N'CREATE INDEX IX_Item_Status   ON dbo.Item(Status);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ItemVariant') AND name=N'IX_Variant_Item') EXEC(N'CREATE INDEX IX_Variant_Item ON dbo.ItemVariant(ItemId);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ItemImage') AND name=N'UX_ItemImage_Primary') EXEC(N'CREATE UNIQUE INDEX UX_ItemImage_Primary
    ON dbo.ItemImage(ItemId) WHERE IsPrimary = 1;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.StockByStore') AND name=N'UX_Stock_Row') EXEC(N'CREATE UNIQUE INDEX UX_Stock_Row ON dbo.StockByStore(ItemId, VariantKey, StoreId);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MetalRate') AND name=N'IX_MetalRate_Latest') EXEC(N'CREATE INDEX IX_MetalRate_Latest ON dbo.MetalRate(PurityId, EffectiveAt DESC);');
IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL EXEC(N'CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AspNetRoles', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetRoles.Id missing', 1;
IF COL_LENGTH(N'dbo.AspNetRoles', N'Name') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetRoles.Name missing', 1;
IF COL_LENGTH(N'dbo.AspNetRoles', N'NormalizedName') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetRoles.NormalizedName missing', 1;
IF COL_LENGTH(N'dbo.AspNetRoles', N'ConcurrencyStamp') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetRoles.ConcurrencyStamp missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetRoles') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetRoles.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetRoles') AND name=N'Name' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AspNetRoles.Name', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetRoles') AND name=N'NormalizedName' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AspNetRoles.NormalizedName', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetRoles') AND name=N'ConcurrencyStamp' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetRoles.ConcurrencyStamp', 1;
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL EXEC(N'CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [AccountType] nvarchar(max) NOT NULL,
    [AllStores] bit NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AspNetUsers', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.Id missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'AccountType') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.AccountType missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'AllStores') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.AllStores missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'UserName') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.UserName missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'NormalizedUserName') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.NormalizedUserName missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'Email') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.Email missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'NormalizedEmail') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.NormalizedEmail missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'EmailConfirmed') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.EmailConfirmed missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'PasswordHash') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.PasswordHash missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'SecurityStamp') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.SecurityStamp missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'ConcurrencyStamp') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.ConcurrencyStamp missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'PhoneNumber') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.PhoneNumber missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'PhoneNumberConfirmed') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.PhoneNumberConfirmed missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'TwoFactorEnabled') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.TwoFactorEnabled missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'LockoutEnd') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.LockoutEnd missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'LockoutEnabled') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.LockoutEnabled missing', 1;
IF COL_LENGTH(N'dbo.AspNetUsers', N'AccessFailedCount') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUsers.AccessFailedCount missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'AccountType' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.AccountType', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'AllStores' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.AspNetUsers.AllStores', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'UserName' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.UserName', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'NormalizedUserName' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.NormalizedUserName', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'Email' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.Email', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'NormalizedEmail' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.NormalizedEmail', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'EmailConfirmed' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.AspNetUsers.EmailConfirmed', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'PasswordHash' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.PasswordHash', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'SecurityStamp' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.SecurityStamp', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'ConcurrencyStamp' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.ConcurrencyStamp', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'PhoneNumber' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetUsers.PhoneNumber', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'PhoneNumberConfirmed' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.AspNetUsers.PhoneNumberConfirmed', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'TwoFactorEnabled' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.AspNetUsers.TwoFactorEnabled', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'LockoutEnd' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AspNetUsers.LockoutEnd', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'LockoutEnabled' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.AspNetUsers.LockoutEnabled', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'AccessFailedCount' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AspNetUsers.AccessFailedCount', 1;
IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL EXEC(N'CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);');
IF COL_LENGTH(N'dbo.AspNetRoleClaims', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetRoleClaims.Id missing', 1;
IF COL_LENGTH(N'dbo.AspNetRoleClaims', N'RoleId') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetRoleClaims.RoleId missing', 1;
IF COL_LENGTH(N'dbo.AspNetRoleClaims', N'ClaimType') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetRoleClaims.ClaimType missing', 1;
IF COL_LENGTH(N'dbo.AspNetRoleClaims', N'ClaimValue') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetRoleClaims.ClaimValue missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetRoleClaims') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AspNetRoleClaims.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetRoleClaims') AND name=N'RoleId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetRoleClaims.RoleId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetRoleClaims') AND name=N'ClaimType' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetRoleClaims.ClaimType', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetRoleClaims') AND name=N'ClaimValue' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetRoleClaims.ClaimValue', 1;
IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL EXEC(N'CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);');
IF COL_LENGTH(N'dbo.AspNetUserClaims', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserClaims.Id missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserClaims', N'UserId') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserClaims.UserId missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserClaims', N'ClaimType') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserClaims.ClaimType missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserClaims', N'ClaimValue') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserClaims.ClaimValue missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserClaims') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AspNetUserClaims.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserClaims') AND name=N'UserId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUserClaims.UserId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserClaims') AND name=N'ClaimType' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetUserClaims.ClaimType', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserClaims') AND name=N'ClaimValue' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetUserClaims.ClaimValue', 1;
IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL EXEC(N'CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);');
IF COL_LENGTH(N'dbo.AspNetUserLogins', N'LoginProvider') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserLogins.LoginProvider missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserLogins', N'ProviderKey') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserLogins.ProviderKey missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserLogins', N'ProviderDisplayName') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserLogins.ProviderDisplayName missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserLogins', N'UserId') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserLogins.UserId missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserLogins') AND name=N'LoginProvider' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUserLogins.LoginProvider', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserLogins') AND name=N'ProviderKey' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUserLogins.ProviderKey', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserLogins') AND name=N'ProviderDisplayName' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetUserLogins.ProviderDisplayName', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserLogins') AND name=N'UserId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUserLogins.UserId', 1;
IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL EXEC(N'CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);');
IF COL_LENGTH(N'dbo.AspNetUserRoles', N'UserId') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserRoles.UserId missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserRoles', N'RoleId') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserRoles.RoleId missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserRoles') AND name=N'UserId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUserRoles.UserId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserRoles') AND name=N'RoleId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUserRoles.RoleId', 1;
IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL EXEC(N'CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);');
IF COL_LENGTH(N'dbo.AspNetUserTokens', N'UserId') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserTokens.UserId missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserTokens', N'LoginProvider') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserTokens.LoginProvider missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserTokens', N'Name') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserTokens.Name missing', 1;
IF COL_LENGTH(N'dbo.AspNetUserTokens', N'Value') IS NULL THROW 51000, 'Incompatible schema: dbo.AspNetUserTokens.Value missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserTokens') AND name=N'UserId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUserTokens.UserId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserTokens') AND name=N'LoginProvider' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUserTokens.LoginProvider', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserTokens') AND name=N'Name' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AspNetUserTokens.Name', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AspNetUserTokens') AND name=N'Value' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AspNetUserTokens.Value', 1;
IF OBJECT_ID(N'dbo.PermissionGrants', N'U') IS NULL EXEC(N'CREATE TABLE [PermissionGrants] (
    [UserId] nvarchar(450) NOT NULL,
    [Permission] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_PermissionGrants] PRIMARY KEY ([UserId], [Permission]),
    CONSTRAINT [FK_PermissionGrants_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);');
IF COL_LENGTH(N'dbo.PermissionGrants', N'UserId') IS NULL THROW 51000, 'Incompatible schema: dbo.PermissionGrants.UserId missing', 1;
IF COL_LENGTH(N'dbo.PermissionGrants', N'Permission') IS NULL THROW 51000, 'Incompatible schema: dbo.PermissionGrants.Permission missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.PermissionGrants') AND name=N'UserId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.PermissionGrants.UserId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.PermissionGrants') AND name=N'Permission' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>200)) THROW 51000, 'Incompatible type: dbo.PermissionGrants.Permission', 1;
IF OBJECT_ID(N'dbo.StoreGrants', N'U') IS NULL EXEC(N'CREATE TABLE [StoreGrants] (
    [UserId] nvarchar(450) NOT NULL,
    [StoreId] int NOT NULL,
    CONSTRAINT [PK_StoreGrants] PRIMARY KEY ([UserId], [StoreId]),
    CONSTRAINT [FK_StoreGrants_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);');
IF COL_LENGTH(N'dbo.StoreGrants', N'UserId') IS NULL THROW 51000, 'Incompatible schema: dbo.StoreGrants.UserId missing', 1;
IF COL_LENGTH(N'dbo.StoreGrants', N'StoreId') IS NULL THROW 51000, 'Incompatible schema: dbo.StoreGrants.StoreId missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StoreGrants') AND name=N'UserId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.StoreGrants.UserId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StoreGrants') AND name=N'StoreId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.StoreGrants.StoreId', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetRoleClaims') AND name=N'IX_AspNetRoleClaims_RoleId') EXEC(N'CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetRoles') AND name=N'RoleNameIndex') EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUserClaims') AND name=N'IX_AspNetUserClaims_UserId') EXEC(N'CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUserLogins') AND name=N'IX_AspNetUserLogins_UserId') EXEC(N'CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUserRoles') AND name=N'IX_AspNetUserRoles_RoleId') EXEC(N'CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'EmailIndex') EXEC(N'CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AspNetUsers') AND name=N'UserNameIndex') EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;');
IF OBJECT_ID(N'dbo.CustomerPortals', N'U') IS NULL EXEC(N'CREATE TABLE [CustomerPortals] (
    [Id] nvarchar(450) NOT NULL,
    [Json] nvarchar(max) NOT NULL,
    [Revision] bigint NOT NULL,
    CONSTRAINT [PK_CustomerPortals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CustomerPortals_AspNetUsers_Id] FOREIGN KEY ([Id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_CustomerPortals_Json] CHECK (ISJSON([Json]) = 1)
);');
IF COL_LENGTH(N'dbo.CustomerPortals', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.CustomerPortals.Id missing', 1;
IF COL_LENGTH(N'dbo.CustomerPortals', N'Json') IS NULL THROW 51000, 'Incompatible schema: dbo.CustomerPortals.Json missing', 1;
IF COL_LENGTH(N'dbo.CustomerPortals', N'Revision') IS NULL THROW 51000, 'Incompatible schema: dbo.CustomerPortals.Revision missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.CustomerPortals') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.CustomerPortals.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.CustomerPortals') AND name=N'Json' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.CustomerPortals.Json', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.CustomerPortals') AND name=N'Revision' AND (TYPE_NAME(system_type_id)<>N'bigint')) THROW 51000, 'Incompatible type: dbo.CustomerPortals.Revision', 1;
IF OBJECT_ID(N'dbo.AiAdmissionGates', N'U') IS NULL EXEC(N'CREATE TABLE [AiAdmissionGates] (
    [Id] nvarchar(256) NOT NULL,
    [MinuteStart] datetimeoffset NOT NULL,
    [MinuteCount] int NOT NULL,
    [DayStart] datetimeoffset NOT NULL,
    [DayCount] int NOT NULL,
    [DayTokens] bigint NOT NULL,
    [Revision] bigint NOT NULL,
    CONSTRAINT [PK_AiAdmissionGates] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AiAdmissionGates', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiAdmissionGates.Id missing', 1;
IF COL_LENGTH(N'dbo.AiAdmissionGates', N'MinuteStart') IS NULL THROW 51000, 'Incompatible schema: dbo.AiAdmissionGates.MinuteStart missing', 1;
IF COL_LENGTH(N'dbo.AiAdmissionGates', N'MinuteCount') IS NULL THROW 51000, 'Incompatible schema: dbo.AiAdmissionGates.MinuteCount missing', 1;
IF COL_LENGTH(N'dbo.AiAdmissionGates', N'DayStart') IS NULL THROW 51000, 'Incompatible schema: dbo.AiAdmissionGates.DayStart missing', 1;
IF COL_LENGTH(N'dbo.AiAdmissionGates', N'DayCount') IS NULL THROW 51000, 'Incompatible schema: dbo.AiAdmissionGates.DayCount missing', 1;
IF COL_LENGTH(N'dbo.AiAdmissionGates', N'DayTokens') IS NULL THROW 51000, 'Incompatible schema: dbo.AiAdmissionGates.DayTokens missing', 1;
IF COL_LENGTH(N'dbo.AiAdmissionGates', N'Revision') IS NULL THROW 51000, 'Incompatible schema: dbo.AiAdmissionGates.Revision missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiAdmissionGates') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiAdmissionGates.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiAdmissionGates') AND name=N'MinuteStart' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiAdmissionGates.MinuteStart', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiAdmissionGates') AND name=N'MinuteCount' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AiAdmissionGates.MinuteCount', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiAdmissionGates') AND name=N'DayStart' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiAdmissionGates.DayStart', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiAdmissionGates') AND name=N'DayCount' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AiAdmissionGates.DayCount', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiAdmissionGates') AND name=N'DayTokens' AND (TYPE_NAME(system_type_id)<>N'bigint')) THROW 51000, 'Incompatible type: dbo.AiAdmissionGates.DayTokens', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiAdmissionGates') AND name=N'Revision' AND (TYPE_NAME(system_type_id)<>N'bigint')) THROW 51000, 'Incompatible type: dbo.AiAdmissionGates.Revision', 1;
IF OBJECT_ID(N'dbo.AiConversations', N'U') IS NULL EXEC(N'CREATE TABLE [AiConversations] (
    [Id] nvarchar(256) NOT NULL,
    [AssistantId] nvarchar(256) NOT NULL,
    [OwnerHash] nvarchar(256) NOT NULL,
    [AccessFingerprint] nvarchar(256) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [LeaseOwner] nvarchar(256) NULL,
    [LeaseUntil] datetimeoffset NULL,
    [Revision] bigint NOT NULL,
    CONSTRAINT [PK_AiConversations] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AiConversations', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiConversations.Id missing', 1;
IF COL_LENGTH(N'dbo.AiConversations', N'AssistantId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiConversations.AssistantId missing', 1;
IF COL_LENGTH(N'dbo.AiConversations', N'OwnerHash') IS NULL THROW 51000, 'Incompatible schema: dbo.AiConversations.OwnerHash missing', 1;
IF COL_LENGTH(N'dbo.AiConversations', N'AccessFingerprint') IS NULL THROW 51000, 'Incompatible schema: dbo.AiConversations.AccessFingerprint missing', 1;
IF COL_LENGTH(N'dbo.AiConversations', N'CreatedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.AiConversations.CreatedAt missing', 1;
IF COL_LENGTH(N'dbo.AiConversations', N'ExpiresAt') IS NULL THROW 51000, 'Incompatible schema: dbo.AiConversations.ExpiresAt missing', 1;
IF COL_LENGTH(N'dbo.AiConversations', N'LeaseOwner') IS NULL THROW 51000, 'Incompatible schema: dbo.AiConversations.LeaseOwner missing', 1;
IF COL_LENGTH(N'dbo.AiConversations', N'LeaseUntil') IS NULL THROW 51000, 'Incompatible schema: dbo.AiConversations.LeaseUntil missing', 1;
IF COL_LENGTH(N'dbo.AiConversations', N'Revision') IS NULL THROW 51000, 'Incompatible schema: dbo.AiConversations.Revision missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiConversations.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'AssistantId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiConversations.AssistantId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'OwnerHash' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiConversations.OwnerHash', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'AccessFingerprint' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiConversations.AccessFingerprint', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'CreatedAt' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiConversations.CreatedAt', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'ExpiresAt' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiConversations.ExpiresAt', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'LeaseOwner' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiConversations.LeaseOwner', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'LeaseUntil' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiConversations.LeaseUntil', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'Revision' AND (TYPE_NAME(system_type_id)<>N'bigint')) THROW 51000, 'Incompatible type: dbo.AiConversations.Revision', 1;
IF OBJECT_ID(N'dbo.AiDataProtectionKeys', N'U') IS NULL EXEC(N'CREATE TABLE [AiDataProtectionKeys] (
    [Id] int NOT NULL IDENTITY,
    [FriendlyName] nvarchar(256) NOT NULL,
    [ProtectedXml] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AiDataProtectionKeys] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AiDataProtectionKeys', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiDataProtectionKeys.Id missing', 1;
IF COL_LENGTH(N'dbo.AiDataProtectionKeys', N'FriendlyName') IS NULL THROW 51000, 'Incompatible schema: dbo.AiDataProtectionKeys.FriendlyName missing', 1;
IF COL_LENGTH(N'dbo.AiDataProtectionKeys', N'ProtectedXml') IS NULL THROW 51000, 'Incompatible schema: dbo.AiDataProtectionKeys.ProtectedXml missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiDataProtectionKeys') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AiDataProtectionKeys.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiDataProtectionKeys') AND name=N'FriendlyName' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiDataProtectionKeys.FriendlyName', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiDataProtectionKeys') AND name=N'ProtectedXml' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiDataProtectionKeys.ProtectedXml', 1;
IF OBJECT_ID(N'dbo.AiSecurityAudit', N'U') IS NULL EXEC(N'CREATE TABLE [AiSecurityAudit] (
    [Id] nvarchar(256) NOT NULL,
    [AssistantId] nvarchar(256) NOT NULL,
    [SubjectHash] nvarchar(256) NOT NULL,
    [TraceId] nvarchar(256) NOT NULL,
    [Operation] nvarchar(256) NOT NULL,
    [Outcome] nvarchar(256) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiSecurityAudit] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AiSecurityAudit', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiSecurityAudit.Id missing', 1;
IF COL_LENGTH(N'dbo.AiSecurityAudit', N'AssistantId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiSecurityAudit.AssistantId missing', 1;
IF COL_LENGTH(N'dbo.AiSecurityAudit', N'SubjectHash') IS NULL THROW 51000, 'Incompatible schema: dbo.AiSecurityAudit.SubjectHash missing', 1;
IF COL_LENGTH(N'dbo.AiSecurityAudit', N'TraceId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiSecurityAudit.TraceId missing', 1;
IF COL_LENGTH(N'dbo.AiSecurityAudit', N'Operation') IS NULL THROW 51000, 'Incompatible schema: dbo.AiSecurityAudit.Operation missing', 1;
IF COL_LENGTH(N'dbo.AiSecurityAudit', N'Outcome') IS NULL THROW 51000, 'Incompatible schema: dbo.AiSecurityAudit.Outcome missing', 1;
IF COL_LENGTH(N'dbo.AiSecurityAudit', N'CreatedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.AiSecurityAudit.CreatedAt missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiSecurityAudit') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiSecurityAudit.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiSecurityAudit') AND name=N'AssistantId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiSecurityAudit.AssistantId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiSecurityAudit') AND name=N'SubjectHash' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiSecurityAudit.SubjectHash', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiSecurityAudit') AND name=N'TraceId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiSecurityAudit.TraceId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiSecurityAudit') AND name=N'Operation' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiSecurityAudit.Operation', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiSecurityAudit') AND name=N'Outcome' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiSecurityAudit.Outcome', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiSecurityAudit') AND name=N'CreatedAt' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiSecurityAudit.CreatedAt', 1;
IF OBJECT_ID(N'dbo.AiUsage', N'U') IS NULL EXEC(N'CREATE TABLE [AiUsage] (
    [Id] nvarchar(256) NOT NULL,
    [AssistantId] nvarchar(256) NOT NULL,
    [SubjectHash] nvarchar(256) NOT NULL,
    [Operation] nvarchar(256) NOT NULL,
    [TraceId] nvarchar(256) NOT NULL,
    [Model] nvarchar(256) NOT NULL,
    [InputTokens] int NULL,
    [OutputTokens] int NULL,
    [TotalTokens] int NULL,
    [EstimatedCost] decimal(18,8) NULL,
    [LatencyMs] bigint NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiUsage] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AiUsage', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.Id missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'AssistantId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.AssistantId missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'SubjectHash') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.SubjectHash missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'Operation') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.Operation missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'TraceId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.TraceId missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'Model') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.Model missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'InputTokens') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.InputTokens missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'OutputTokens') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.OutputTokens missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'TotalTokens') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.TotalTokens missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'EstimatedCost') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.EstimatedCost missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'LatencyMs') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.LatencyMs missing', 1;
IF COL_LENGTH(N'dbo.AiUsage', N'CreatedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.AiUsage.CreatedAt missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiUsage.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'AssistantId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiUsage.AssistantId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'SubjectHash' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiUsage.SubjectHash', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'Operation' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiUsage.Operation', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'TraceId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiUsage.TraceId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'Model' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiUsage.Model', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'InputTokens' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AiUsage.InputTokens', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'OutputTokens' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AiUsage.OutputTokens', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'TotalTokens' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AiUsage.TotalTokens', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'EstimatedCost' AND (TYPE_NAME(system_type_id)<>N'decimal' OR precision<>18 OR scale<>8)) THROW 51000, 'Incompatible type: dbo.AiUsage.EstimatedCost', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'LatencyMs' AND (TYPE_NAME(system_type_id)<>N'bigint')) THROW 51000, 'Incompatible type: dbo.AiUsage.LatencyMs', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'CreatedAt' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiUsage.CreatedAt', 1;
IF OBJECT_ID(N'dbo.AiMessages', N'U') IS NULL EXEC(N'CREATE TABLE [AiMessages] (
    [Id] nvarchar(256) NOT NULL,
    [ConversationId] nvarchar(256) NOT NULL,
    [Role] nvarchar(256) NOT NULL,
    [ProtectedText] nvarchar(max) NOT NULL,
    [ProtectedSources] nvarchar(max) NOT NULL,
    [ProtectedEvidence] nvarchar(max) NOT NULL,
    [Refused] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiMessages_AiConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [AiConversations] ([Id]) ON DELETE CASCADE
);');
IF COL_LENGTH(N'dbo.AiMessages', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiMessages.Id missing', 1;
IF COL_LENGTH(N'dbo.AiMessages', N'ConversationId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiMessages.ConversationId missing', 1;
IF COL_LENGTH(N'dbo.AiMessages', N'Role') IS NULL THROW 51000, 'Incompatible schema: dbo.AiMessages.Role missing', 1;
IF COL_LENGTH(N'dbo.AiMessages', N'ProtectedText') IS NULL THROW 51000, 'Incompatible schema: dbo.AiMessages.ProtectedText missing', 1;
IF COL_LENGTH(N'dbo.AiMessages', N'ProtectedSources') IS NULL THROW 51000, 'Incompatible schema: dbo.AiMessages.ProtectedSources missing', 1;
IF COL_LENGTH(N'dbo.AiMessages', N'ProtectedEvidence') IS NULL THROW 51000, 'Incompatible schema: dbo.AiMessages.ProtectedEvidence missing', 1;
IF COL_LENGTH(N'dbo.AiMessages', N'Refused') IS NULL THROW 51000, 'Incompatible schema: dbo.AiMessages.Refused missing', 1;
IF COL_LENGTH(N'dbo.AiMessages', N'CreatedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.AiMessages.CreatedAt missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiMessages') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiMessages.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiMessages') AND name=N'ConversationId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiMessages.ConversationId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiMessages') AND name=N'Role' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiMessages.Role', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiMessages') AND name=N'ProtectedText' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiMessages.ProtectedText', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiMessages') AND name=N'ProtectedSources' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiMessages.ProtectedSources', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiMessages') AND name=N'ProtectedEvidence' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiMessages.ProtectedEvidence', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiMessages') AND name=N'Refused' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.AiMessages.Refused', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiMessages') AND name=N'CreatedAt' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiMessages.CreatedAt', 1;
IF OBJECT_ID(N'dbo.AiTurns', N'U') IS NULL EXEC(N'CREATE TABLE [AiTurns] (
    [Id] nvarchar(256) NOT NULL,
    [ConversationId] nvarchar(256) NOT NULL,
    [ClientRequestId] nvarchar(256) NOT NULL,
    [QuestionHash] nvarchar(256) NOT NULL,
    [State] nvarchar(256) NOT NULL,
    [ProtectedReply] nvarchar(max) NULL,
    [ProtectedEvidence] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ReservedTokens] bigint NOT NULL,
    CONSTRAINT [PK_AiTurns] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiTurns_AiConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [AiConversations] ([Id]) ON DELETE CASCADE
);');
IF COL_LENGTH(N'dbo.AiTurns', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiTurns.Id missing', 1;
IF COL_LENGTH(N'dbo.AiTurns', N'ConversationId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiTurns.ConversationId missing', 1;
IF COL_LENGTH(N'dbo.AiTurns', N'ClientRequestId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiTurns.ClientRequestId missing', 1;
IF COL_LENGTH(N'dbo.AiTurns', N'QuestionHash') IS NULL THROW 51000, 'Incompatible schema: dbo.AiTurns.QuestionHash missing', 1;
IF COL_LENGTH(N'dbo.AiTurns', N'State') IS NULL THROW 51000, 'Incompatible schema: dbo.AiTurns.State missing', 1;
IF COL_LENGTH(N'dbo.AiTurns', N'ProtectedReply') IS NULL THROW 51000, 'Incompatible schema: dbo.AiTurns.ProtectedReply missing', 1;
IF COL_LENGTH(N'dbo.AiTurns', N'ProtectedEvidence') IS NULL THROW 51000, 'Incompatible schema: dbo.AiTurns.ProtectedEvidence missing', 1;
IF COL_LENGTH(N'dbo.AiTurns', N'CreatedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.AiTurns.CreatedAt missing', 1;
IF COL_LENGTH(N'dbo.AiTurns', N'ReservedTokens') IS NULL THROW 51000, 'Incompatible schema: dbo.AiTurns.ReservedTokens missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiTurns.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'ConversationId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiTurns.ConversationId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'ClientRequestId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiTurns.ClientRequestId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'QuestionHash' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiTurns.QuestionHash', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'State' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>512)) THROW 51000, 'Incompatible type: dbo.AiTurns.State', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'ProtectedReply' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiTurns.ProtectedReply', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'ProtectedEvidence' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiTurns.ProtectedEvidence', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'CreatedAt' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiTurns.CreatedAt', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'ReservedTokens' AND (TYPE_NAME(system_type_id)<>N'bigint')) THROW 51000, 'Incompatible type: dbo.AiTurns.ReservedTokens', 1;
IF OBJECT_ID(N'dbo.AiKnowledgeAudit', N'U') IS NULL EXEC(N'CREATE TABLE [AiKnowledgeAudit] (
    [Id] nvarchar(450) NOT NULL,
    [ActorHash] nvarchar(max) NOT NULL,
    [Operation] nvarchar(max) NOT NULL,
    [TargetId] nvarchar(max) NOT NULL,
    [Outcome] nvarchar(max) NOT NULL,
    [Timestamp] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiKnowledgeAudit] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AiKnowledgeAudit', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeAudit.Id missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeAudit', N'ActorHash') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeAudit.ActorHash missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeAudit', N'Operation') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeAudit.Operation missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeAudit', N'TargetId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeAudit.TargetId missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeAudit', N'Outcome') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeAudit.Outcome missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeAudit', N'Timestamp') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeAudit.Timestamp missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeAudit') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeAudit.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeAudit') AND name=N'ActorHash' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeAudit.ActorHash', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeAudit') AND name=N'Operation' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeAudit.Operation', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeAudit') AND name=N'TargetId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeAudit.TargetId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeAudit') AND name=N'Outcome' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeAudit.Outcome', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeAudit') AND name=N'Timestamp' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.AiKnowledgeAudit.Timestamp', 1;
IF OBJECT_ID(N'dbo.AiKnowledgeChunks', N'U') IS NULL EXEC(N'CREATE TABLE [AiKnowledgeChunks] (
    [Id] nvarchar(450) NOT NULL,
    [VersionId] nvarchar(450) NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AiKnowledgeChunks] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AiKnowledgeChunks', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeChunks.Id missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeChunks', N'VersionId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeChunks.VersionId missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeChunks', N'Text') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeChunks.Text missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeChunks') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeChunks.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeChunks') AND name=N'VersionId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeChunks.VersionId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeChunks') AND name=N'Text' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeChunks.Text', 1;
IF OBJECT_ID(N'dbo.AiKnowledgeDocuments', N'U') IS NULL EXEC(N'CREATE TABLE [AiKnowledgeDocuments] (
    [Id] nvarchar(450) NOT NULL,
    [PublishedVersionId] nvarchar(max) NULL,
    [LatestVersionId] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AiKnowledgeDocuments] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AiKnowledgeDocuments', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeDocuments.Id missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeDocuments', N'PublishedVersionId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeDocuments.PublishedVersionId missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeDocuments', N'LatestVersionId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeDocuments.LatestVersionId missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeDocuments') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeDocuments.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeDocuments') AND name=N'PublishedVersionId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeDocuments.PublishedVersionId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeDocuments') AND name=N'LatestVersionId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeDocuments.LatestVersionId', 1;
IF OBJECT_ID(N'dbo.AiKnowledgeVersions', N'U') IS NULL EXEC(N'CREATE TABLE [AiKnowledgeVersions] (
    [Id] nvarchar(450) NOT NULL,
    [DocumentId] nvarchar(450) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Collection] nvarchar(max) NOT NULL,
    [Audience] nvarchar(max) NOT NULL,
    [RequiredPermission] nvarchar(max) NULL,
    [StoreIds] nvarchar(max) NOT NULL,
    [OwnerId] nvarchar(max) NULL,
    CONSTRAINT [PK_AiKnowledgeVersions] PRIMARY KEY ([Id])
);');
IF COL_LENGTH(N'dbo.AiKnowledgeVersions', N'Id') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeVersions.Id missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeVersions', N'DocumentId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeVersions.DocumentId missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeVersions', N'Title') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeVersions.Title missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeVersions', N'Collection') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeVersions.Collection missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeVersions', N'Audience') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeVersions.Audience missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeVersions', N'RequiredPermission') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeVersions.RequiredPermission missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeVersions', N'StoreIds') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeVersions.StoreIds missing', 1;
IF COL_LENGTH(N'dbo.AiKnowledgeVersions', N'OwnerId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiKnowledgeVersions.OwnerId missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeVersions') AND name=N'Id' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeVersions.Id', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeVersions') AND name=N'DocumentId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>900)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeVersions.DocumentId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeVersions') AND name=N'Title' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeVersions.Title', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeVersions') AND name=N'Collection' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeVersions.Collection', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeVersions') AND name=N'Audience' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeVersions.Audience', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeVersions') AND name=N'RequiredPermission' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeVersions.RequiredPermission', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeVersions') AND name=N'StoreIds' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeVersions.StoreIds', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeVersions') AND name=N'OwnerId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>-1)) THROW 51000, 'Incompatible type: dbo.AiKnowledgeVersions.OwnerId', 1;
IF OBJECT_ID(N'dbo.AiPublicationProducts', N'U') IS NULL EXEC(N'CREATE TABLE [AiPublicationProducts] (
    [ItemId] int NOT NULL,
    [Published] bit NOT NULL,
    [AssetRevisionId] nvarchar(32) NULL,
    CONSTRAINT [PK_AiPublicationProducts] PRIMARY KEY ([ItemId])
);');
IF COL_LENGTH(N'dbo.AiPublicationProducts', N'ItemId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiPublicationProducts.ItemId missing', 1;
IF COL_LENGTH(N'dbo.AiPublicationProducts', N'Published') IS NULL THROW 51000, 'Incompatible schema: dbo.AiPublicationProducts.Published missing', 1;
IF COL_LENGTH(N'dbo.AiPublicationProducts', N'AssetRevisionId') IS NULL THROW 51000, 'Incompatible schema: dbo.AiPublicationProducts.AssetRevisionId missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiPublicationProducts') AND name=N'ItemId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.AiPublicationProducts.ItemId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiPublicationProducts') AND name=N'Published' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.AiPublicationProducts.Published', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AiPublicationProducts') AND name=N'AssetRevisionId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>64)) THROW 51000, 'Incompatible type: dbo.AiPublicationProducts.AssetRevisionId', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'IX_AiConversations_ExpiresAt') EXEC(N'CREATE INDEX [IX_AiConversations_ExpiresAt] ON [AiConversations] ([ExpiresAt]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiConversations') AND name=N'IX_AiConversations_OwnerHash_AssistantId') EXEC(N'CREATE INDEX [IX_AiConversations_OwnerHash_AssistantId] ON [AiConversations] ([OwnerHash], [AssistantId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiMessages') AND name=N'IX_AiMessages_ConversationId_CreatedAt') EXEC(N'CREATE INDEX [IX_AiMessages_ConversationId_CreatedAt] ON [AiMessages] ([ConversationId], [CreatedAt]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiSecurityAudit') AND name=N'IX_AiSecurityAudit_CreatedAt') EXEC(N'CREATE INDEX [IX_AiSecurityAudit_CreatedAt] ON [AiSecurityAudit] ([CreatedAt]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiTurns') AND name=N'IX_AiTurns_ConversationId_ClientRequestId') EXEC(N'CREATE UNIQUE INDEX [IX_AiTurns_ConversationId_ClientRequestId] ON [AiTurns] ([ConversationId], [ClientRequestId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'IX_AiUsage_AssistantId_CreatedAt') EXEC(N'CREATE INDEX [IX_AiUsage_AssistantId_CreatedAt] ON [AiUsage] ([AssistantId], [CreatedAt]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiUsage') AND name=N'IX_AiUsage_SubjectHash_CreatedAt') EXEC(N'CREATE INDEX [IX_AiUsage_SubjectHash_CreatedAt] ON [AiUsage] ([SubjectHash], [CreatedAt]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeChunks') AND name=N'IX_AiKnowledgeChunks_VersionId') EXEC(N'CREATE INDEX [IX_AiKnowledgeChunks_VersionId] ON [AiKnowledgeChunks] ([VersionId]);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AiKnowledgeVersions') AND name=N'IX_AiKnowledgeVersions_DocumentId') EXEC(N'CREATE INDEX [IX_AiKnowledgeVersions_DocumentId] ON [AiKnowledgeVersions] ([DocumentId]);');
IF OBJECT_ID(N'dbo.ItemImageAsset', N'U') IS NULL EXEC(N'CREATE TABLE dbo.ItemImageAsset (
    ItemId int NOT NULL PRIMARY KEY REFERENCES dbo.Item(ItemId),
    RevisionId nvarchar(32) NOT NULL,
    SourceSha256 nvarchar(64) NOT NULL,
    PreviewSha256 nvarchar(64) NULL,
    State nvarchar(20) NOT NULL CHECK (State IN (''processing'',''ready'',''failed'')),
    QualityReview nvarchar(20) NOT NULL CHECK (QualityReview IN (''pending'',''approved'')),
    LicenseStatus nvarchar(20) NOT NULL,
    Width int NOT NULL,
    Height int NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    FailureCode nvarchar(100) NULL
);');
IF COL_LENGTH(N'dbo.ItemImageAsset', N'ItemId') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.ItemId missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'RevisionId') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.RevisionId missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'SourceSha256') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.SourceSha256 missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'PreviewSha256') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.PreviewSha256 missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'State') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.State missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'QualityReview') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.QualityReview missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'LicenseStatus') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.LicenseStatus missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'Width') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.Width missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'Height') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.Height missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'CreatedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.CreatedAt missing', 1;
IF COL_LENGTH(N'dbo.ItemImageAsset', N'FailureCode') IS NULL THROW 51000, 'Incompatible schema: dbo.ItemImageAsset.FailureCode missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'ItemId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.ItemId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'RevisionId' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>64)) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.RevisionId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'SourceSha256' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>128)) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.SourceSha256', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'PreviewSha256' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>128)) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.PreviewSha256', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'State' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>40)) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.State', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'QualityReview' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>40)) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.QualityReview', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'LicenseStatus' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>40)) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.LicenseStatus', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'Width' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.Width', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'Height' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.Height', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'CreatedAt' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.CreatedAt', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.ItemImageAsset') AND name=N'FailureCode' AND (TYPE_NAME(system_type_id)<>N'nvarchar' OR max_length<>200)) THROW 51000, 'Incompatible type: dbo.ItemImageAsset.FailureCode', 1;
IF OBJECT_ID(N'dbo.StorefrontLocation', N'U') IS NULL EXEC(N'CREATE TABLE dbo.StorefrontLocation (
    StoreId int NOT NULL PRIMARY KEY REFERENCES dbo.Store(StoreId),
    Latitude float NOT NULL CHECK (Latitude BETWEEN -85 AND 85),
    Longitude float NOT NULL CHECK (Longitude BETWEEN -180 AND 180),
    IsSample bit NOT NULL DEFAULT 1,
    ErpEnabled bit NOT NULL DEFAULT 1,
    InventorySyncedAt datetimeoffset NULL,
    ImagesSyncedAt datetimeoffset NULL
);');
IF COL_LENGTH(N'dbo.StorefrontLocation', N'StoreId') IS NULL THROW 51000, 'Incompatible schema: dbo.StorefrontLocation.StoreId missing', 1;
IF COL_LENGTH(N'dbo.StorefrontLocation', N'Latitude') IS NULL THROW 51000, 'Incompatible schema: dbo.StorefrontLocation.Latitude missing', 1;
IF COL_LENGTH(N'dbo.StorefrontLocation', N'Longitude') IS NULL THROW 51000, 'Incompatible schema: dbo.StorefrontLocation.Longitude missing', 1;
IF COL_LENGTH(N'dbo.StorefrontLocation', N'IsSample') IS NULL THROW 51000, 'Incompatible schema: dbo.StorefrontLocation.IsSample missing', 1;
IF COL_LENGTH(N'dbo.StorefrontLocation', N'ErpEnabled') IS NULL THROW 51000, 'Incompatible schema: dbo.StorefrontLocation.ErpEnabled missing', 1;
IF COL_LENGTH(N'dbo.StorefrontLocation', N'InventorySyncedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.StorefrontLocation.InventorySyncedAt missing', 1;
IF COL_LENGTH(N'dbo.StorefrontLocation', N'ImagesSyncedAt') IS NULL THROW 51000, 'Incompatible schema: dbo.StorefrontLocation.ImagesSyncedAt missing', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StorefrontLocation') AND name=N'StoreId' AND (TYPE_NAME(system_type_id)<>N'int')) THROW 51000, 'Incompatible type: dbo.StorefrontLocation.StoreId', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StorefrontLocation') AND name=N'Latitude' AND (TYPE_NAME(system_type_id)<>N'float')) THROW 51000, 'Incompatible type: dbo.StorefrontLocation.Latitude', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StorefrontLocation') AND name=N'Longitude' AND (TYPE_NAME(system_type_id)<>N'float')) THROW 51000, 'Incompatible type: dbo.StorefrontLocation.Longitude', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StorefrontLocation') AND name=N'IsSample' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.StorefrontLocation.IsSample', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StorefrontLocation') AND name=N'ErpEnabled' AND (TYPE_NAME(system_type_id)<>N'bit')) THROW 51000, 'Incompatible type: dbo.StorefrontLocation.ErpEnabled', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StorefrontLocation') AND name=N'InventorySyncedAt' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.StorefrontLocation.InventorySyncedAt', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.StorefrontLocation') AND name=N'ImagesSyncedAt' AND (TYPE_NAME(system_type_id)<>N'datetimeoffset')) THROW 51000, 'Incompatible type: dbo.StorefrontLocation.ImagesSyncedAt', 1;
IF COLUMNPROPERTY(OBJECT_ID(N'dbo.AiPublicationProducts'), N'ItemId', 'IsIdentity') = 1 THROW 51000, 'Publication ItemId must be externally assigned; review legacy identity migration separately.', 1;
COMMIT TRANSACTION;
END TRY
BEGIN CATCH
IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
