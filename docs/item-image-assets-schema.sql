-- Role: Prepared SQL image metadata schema. Apply only as part of the reviewed SQL demo setup.
-- Bytes stay in ITEM_ASSET_STORAGE_PATH/revisions/<RevisionId>/; no automatic quality approval.
CREATE TABLE dbo.ItemImageAsset (
    ItemId int NOT NULL PRIMARY KEY REFERENCES dbo.Item(ItemId),
    RevisionId nvarchar(32) NOT NULL,
    SourceSha256 nvarchar(64) NOT NULL,
    PreviewSha256 nvarchar(64) NULL,
    State nvarchar(20) NOT NULL CHECK (State IN ('processing','ready','failed')),
    QualityReview nvarchar(20) NOT NULL CHECK (QualityReview IN ('pending','approved')),
    LicenseStatus nvarchar(20) NOT NULL,
    Width int NOT NULL,
    Height int NOT NULL,
    CreatedAt datetimeoffset NOT NULL,
    FailureCode nvarchar(100) NULL
);
