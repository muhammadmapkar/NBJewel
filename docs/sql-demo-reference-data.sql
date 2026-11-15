-- Role: Adds only missing, explicitly assumed sample lookup data and historical rates.
-- Owner-run after sql-demo-schema.sql, in the same demo database. Never modifies existing rows.
SET XACT_ABORT ON;
BEGIN TRY
BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM dbo.Category WHERE Name=N'Necklaces') INSERT dbo.Category(Name) VALUES(N'Necklaces');
IF NOT EXISTS (SELECT 1 FROM dbo.Category WHERE Name=N'Earrings') INSERT dbo.Category(Name) VALUES(N'Earrings');
IF NOT EXISTS (SELECT 1 FROM dbo.Design WHERE DesignCode=N'DEMO-HERITAGE') INSERT dbo.Design(DesignCode,Name) VALUES(N'DEMO-HERITAGE',N'Assumed heritage demo design');
IF NOT EXISTS (SELECT 1 FROM dbo.Store WHERE Code=N'DEMO01') INSERT dbo.Store(Code,Name,City) VALUES(N'DEMO01',N'NB JEWEL demo store',N'Assumed demo location');
IF NOT EXISTS (SELECT 1 FROM dbo.Metal WHERE Name=N'Gold') INSERT dbo.Metal(Name) VALUES(N'Gold');
IF NOT EXISTS (SELECT 1 FROM dbo.Metal WHERE Name=N'Silver') INSERT dbo.Metal(Name) VALUES(N'Silver');
IF NOT EXISTS (SELECT 1 FROM dbo.Metal WHERE Name=N'Platinum') INSERT dbo.Metal(Name) VALUES(N'Platinum');
DECLARE @rates TABLE(Metal nvarchar(50), Purity nvarchar(20), Fineness decimal(5,4), Rate decimal(12,2));
INSERT @rates VALUES(N'Gold',N'22K',0.916,66),(N'Gold',N'18K',0.750,54),(N'Gold',N'24K',0.999,72),(N'Silver',N'925',0.925,0.90),(N'Platinum',N'950',0.950,110);
INSERT dbo.Purity(MetalId,Label,Fineness)
SELECT m.MetalId,r.Purity,r.Fineness FROM @rates r JOIN dbo.Metal m ON m.Name=r.Metal
WHERE NOT EXISTS (SELECT 1 FROM dbo.Purity p WHERE p.MetalId=m.MetalId AND p.Label=r.Purity);
INSERT dbo.MetalRate(PurityId,RatePerGram,Currency,EffectiveAt)
SELECT p.PurityId,r.Rate,N'AED',CONVERT(datetime2,'2026-09-30T00:00:00')
FROM @rates r JOIN dbo.Metal m ON m.Name=r.Metal JOIN dbo.Purity p ON p.MetalId=m.MetalId AND p.Label=r.Purity
WHERE NOT EXISTS (SELECT 1 FROM dbo.MetalRate existing WHERE existing.PurityId=p.PurityId);
-- Assumed city-center demo coordinates, not a verified retailer location or live sync claim.
INSERT dbo.StorefrontLocation(StoreId,Latitude,Longitude,IsSample,ErpEnabled)
SELECT StoreId,25.2048,55.2708,1,1 FROM dbo.Store s WHERE s.Code=N'DEMO01'
AND NOT EXISTS (SELECT 1 FROM dbo.StorefrontLocation l WHERE l.StoreId=s.StoreId);
COMMIT TRANSACTION;
END TRY
BEGIN CATCH
IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
