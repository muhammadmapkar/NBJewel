-- Role: Keeps SQL demo store coordinates and explicit sync evidence alongside ERP store IDs.
CREATE TABLE dbo.StorefrontLocation (
    StoreId int NOT NULL PRIMARY KEY REFERENCES dbo.Store(StoreId),
    Latitude float NOT NULL CHECK (Latitude BETWEEN -85 AND 85),
    Longitude float NOT NULL CHECK (Longitude BETWEEN -180 AND 180),
    IsSample bit NOT NULL DEFAULT 1,
    ErpEnabled bit NOT NULL DEFAULT 1,
    InventorySyncedAt datetimeoffset NULL,
    ImagesSyncedAt datetimeoffset NULL
);
