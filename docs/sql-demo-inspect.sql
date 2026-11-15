-- Role: Read-only manager demonstration queries; run in the configured demo database.
SELECT DB_NAME() AS DatabaseName, @@VERSION AS SqlServerVersion;
SELECT TOP (30) i.ItemId,i.ItemCode,i.Name,i.Status,
    JSON_VALUE(i.StorefrontJson,'description') AS Description,
    JSON_VALUE(i.StorefrontJson,'collection') AS Collection,
    a.RevisionId,a.State AS PreviewState,a.QualityReview,a.LicenseStatus,
    a.SourceSha256,a.PreviewSha256,p.Published,p.AssetRevisionId AS PublishedRevision
FROM dbo.Item i
LEFT JOIN dbo.ItemImageAsset a ON a.ItemId=i.ItemId
LEFT JOIN dbo.AiPublicationProducts p ON p.ItemId=i.ItemId
ORDER BY i.UpdatedAt DESC,i.ItemId DESC;
-- Existing database IDs are retained. New imports use identity-assigned IDs; locate by stable ItemCode.
SELECT StoreId,Code,Name,City FROM dbo.Store ORDER BY StoreId;
SELECT COUNT(*) AS SqlCustomerAccounts FROM dbo.AspNetUsers WHERE AccountType=N'customer';
