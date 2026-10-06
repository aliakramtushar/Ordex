/* =====================================================================
   Ordex - 004 Seed data
   * Shared expense categories (CompanyId = 0 -> visible to everyone)
   * A starter company + business unit so the app is usable right away
   The SuperAdmin user is created by the application on first start
   (its password must be hashed in code).
   ===================================================================== */

IF NOT EXISTS (SELECT 1 FROM dbo.ExpenseCategory WHERE CompanyId = 0)
BEGIN
    INSERT INTO dbo.ExpenseCategory (CompanyId, BusinessUnitId, CategoryName)
    VALUES (0, 0, N'Cargo / Shipping'),
           (0, 0, N'Customs & Duty'),
           (0, 0, N'Courier / Delivery'),
           (0, 0, N'Packaging'),
           (0, 0, N'Page Boost / Marketing'),
           (0, 0, N'Mobile Banking Charge'),
           (0, 0, N'Others');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Company)
BEGIN
    DECLARE @CompanyId INT;

    INSERT INTO dbo.Company (CompanyCode, CompanyName, DefaultExchangeRate)
    VALUES (N'DEMO', N'My Company', 90);

    SET @CompanyId = SCOPE_IDENTITY();

    INSERT INTO dbo.BusinessUnit (CompanyId, UnitCode, UnitName)
    VALUES (@CompanyId, N'MAIN', N'Main Shop');
END
GO
