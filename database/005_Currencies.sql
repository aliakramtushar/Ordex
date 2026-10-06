/* =====================================================================
   Ordex – multi-currency (re-runnable)
   Each company buys abroad in a purchase currency and sells in a sales
   currency: BDT, USD or SGD. Existing companies keep SGD → BDT.
   Amount columns keep their historic names (PurchasePriceSgd = amount in
   the purchase currency, *Bdt = amount in the sales currency).
   ===================================================================== */
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.Company', N'PurchaseCurrency') IS NULL
    ALTER TABLE dbo.Company ADD PurchaseCurrency CHAR(3) NOT NULL
        CONSTRAINT DF_Company_PurchaseCurrency DEFAULT ('SGD') WITH VALUES;
GO

IF COL_LENGTH(N'dbo.Company', N'SalesCurrency') IS NULL
    ALTER TABLE dbo.Company ADD SalesCurrency CHAR(3) NOT NULL
        CONSTRAINT DF_Company_SalesCurrency DEFAULT ('BDT') WITH VALUES;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Company_PurchaseCurrency')
    ALTER TABLE dbo.Company ADD CONSTRAINT CK_Company_PurchaseCurrency CHECK (PurchaseCurrency IN ('BDT', 'USD', 'SGD'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Company_SalesCurrency')
    ALTER TABLE dbo.Company ADD CONSTRAINT CK_Company_SalesCurrency CHECK (SalesCurrency IN ('BDT', 'USD', 'SGD'));
GO
