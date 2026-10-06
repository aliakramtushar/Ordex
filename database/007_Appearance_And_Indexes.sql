/* =====================================================================
   Ordex – 007 per-user themes + indexes for large data (re-runnable)
   ===================================================================== */
SET NOCOUNT ON;
GO

/* ---------- Per-user look ---------- */
IF COL_LENGTH(N'dbo.AppUser', N'Theme') IS NULL
    ALTER TABLE dbo.AppUser ADD Theme VARCHAR(20) NOT NULL
        CONSTRAINT DF_AppUser_Theme DEFAULT ('brattle') WITH VALUES;
GO

IF COL_LENGTH(N'dbo.AppUser', N'ColorMode') IS NULL
    ALTER TABLE dbo.AppUser ADD ColorMode VARCHAR(10) NOT NULL
        CONSTRAINT DF_AppUser_ColorMode DEFAULT ('light') WITH VALUES;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AppUser_ColorMode')
    ALTER TABLE dbo.AppUser ADD CONSTRAINT CK_AppUser_ColorMode CHECK (ColorMode IN ('light', 'dark', 'system'));
GO

/* ---------- Indexes for paging big lists ----------
   Lists are filtered by company/unit and sorted newest first; these let SQL Server
   read just one page instead of scanning and sorting the whole table. */

-- Orders list (no status filter), newest first
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PreOrder_Scope_Date' AND object_id = OBJECT_ID(N'dbo.PreOrder'))
    CREATE INDEX IX_PreOrder_Scope_Date ON dbo.PreOrder (CompanyId, BusinessUnitId, OrderDate DESC, Id DESC)
        INCLUDE (Status, CustomerId, BatchId, IsActive);
GO

-- Orders of one customer (customer page, tracking page)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PreOrder_Customer_Date' AND object_id = OBJECT_ID(N'dbo.PreOrder'))
    CREATE INDEX IX_PreOrder_Customer_Date ON dbo.PreOrder (CustomerId, OrderDate DESC, Id DESC)
        INCLUDE (Status, IsActive, SellingPrice);
GO

-- Customers list sorted by name
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Customer_Scope_Name' AND object_id = OBJECT_ID(N'dbo.Customer'))
    CREATE INDEX IX_Customer_Scope_Name ON dbo.Customer (CompanyId, BusinessUnitId, CustomerName, Id)
        INCLUDE (Mobile, IsActive);
GO

-- Stock tabs, newest first
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockItem_Scope_Date' AND object_id = OBJECT_ID(N'dbo.StockItem'))
    CREATE INDEX IX_StockItem_Scope_Date ON dbo.StockItem (CompanyId, BusinessUnitId, Status, EntryDate DESC, Id DESC)
        INCLUDE (SourceType, IsActive);
GO

-- Status history of an order
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderStatusLog_Order_Id' AND object_id = OBJECT_ID(N'dbo.OrderStatusLog'))
    CREATE INDEX IX_OrderStatusLog_Order_Id ON dbo.OrderStatusLog (OrderId, Id DESC);
GO
