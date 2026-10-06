/* =====================================================================
   Ordex – 006 customer tracking links (re-runnable)
   Each order gets a secret token; the customer opens /t/{token} to see
   the order's status and price without signing in.
   ===================================================================== */
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.PreOrder', N'TrackingToken') IS NULL
    ALTER TABLE dbo.PreOrder ADD TrackingToken VARCHAR(40) NULL;
GO

/* Existing orders: a random GUID per row (122 random bits) as 32 hex characters.
   New orders get a 128-bit token from the app. Both fit the same link format. */
UPDATE dbo.PreOrder
   SET TrackingToken = LOWER(REPLACE(CONVERT(VARCHAR(36), NEWID()), '-', ''))
 WHERE TrackingToken IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PreOrder_TrackingToken' AND object_id = OBJECT_ID(N'dbo.PreOrder'))
    CREATE UNIQUE INDEX UX_PreOrder_TrackingToken ON dbo.PreOrder (TrackingToken) WHERE TrackingToken IS NOT NULL;
GO
