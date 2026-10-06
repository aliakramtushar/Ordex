/* =====================================================================
   Ordex - 001 Schema
   ---------------------------------------------------------------------
   * Every business table carries CompanyId + BusinessUnitId.
   * SuperAdmin works with CompanyId = 0 and BusinessUnitId = 0 (= all).
   * Script is idempotent: safe to run many times.
   * Tables are only CREATED here. To change an existing table later, add a
     NEW numbered script (e.g. 005_AddColumnX.sql) with a guarded ALTER:
       IF COL_LENGTH(N'dbo.PreOrder', N'NewCol') IS NULL
           ALTER TABLE dbo.PreOrder ADD NewCol NVARCHAR(50) NULL;
   ===================================================================== */

/* ---------- Company ---------- */
IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Company
    (
        Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Company PRIMARY KEY,
        CompanyCode         NVARCHAR(20)   NOT NULL,
        CompanyName         NVARCHAR(150)  NOT NULL,
        Phone               NVARCHAR(30)   NULL,
        Email               NVARCHAR(150)  NULL,
        Address             NVARCHAR(300)  NULL,
        DefaultExchangeRate DECIMAL(18,4)  NOT NULL CONSTRAINT DF_Company_Rate DEFAULT (0),
        IsActive            BIT            NOT NULL CONSTRAINT DF_Company_IsActive DEFAULT (1),
        CreatedBy           INT            NOT NULL CONSTRAINT DF_Company_CreatedBy DEFAULT (0),
        CreatedAt           DATETIME2(0)   NOT NULL CONSTRAINT DF_Company_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy           INT            NULL,
        UpdatedAt           DATETIME2(0)   NULL,
        CONSTRAINT UQ_Company_Code UNIQUE (CompanyCode)
    );
END
GO

/* ---------- Business Unit (shop / brand / page) ---------- */
IF OBJECT_ID(N'dbo.BusinessUnit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BusinessUnit
    (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BusinessUnit PRIMARY KEY,
        CompanyId   INT            NOT NULL CONSTRAINT FK_BusinessUnit_Company REFERENCES dbo.Company(Id),
        UnitCode    NVARCHAR(20)   NOT NULL,
        UnitName    NVARCHAR(150)  NOT NULL,
        IsActive    BIT            NOT NULL CONSTRAINT DF_BusinessUnit_IsActive DEFAULT (1),
        CreatedBy   INT            NOT NULL CONSTRAINT DF_BusinessUnit_CreatedBy DEFAULT (0),
        CreatedAt   DATETIME2(0)   NOT NULL CONSTRAINT DF_BusinessUnit_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy   INT            NULL,
        UpdatedAt   DATETIME2(0)   NULL,
        CONSTRAINT UQ_BusinessUnit_Code UNIQUE (CompanyId, UnitCode)
    );
END
GO

/* ---------- Users ----------
   Role: 1 = SuperAdmin, 2 = Admin, 3 = Staff
   CompanyId / BusinessUnitId = 0 means "all".                         */
IF OBJECT_ID(N'dbo.AppUser', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUser
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUser PRIMARY KEY,
        CompanyId         INT            NOT NULL CONSTRAINT DF_AppUser_CompanyId DEFAULT (0),
        BusinessUnitId    INT            NOT NULL CONSTRAINT DF_AppUser_BusinessUnitId DEFAULT (0),
        FullName          NVARCHAR(150)  NOT NULL,
        UserName          NVARCHAR(60)   NOT NULL,
        Email             NVARCHAR(150)  NULL,
        Phone             NVARCHAR(30)   NULL,
        PasswordHash      NVARCHAR(500)  NOT NULL,
        Role              TINYINT        NOT NULL,
        AccessFailedCount INT            NOT NULL CONSTRAINT DF_AppUser_Failed DEFAULT (0),
        LockoutEnd        DATETIME2(0)   NULL,
        LastLoginAt       DATETIME2(0)   NULL,
        IsActive          BIT            NOT NULL CONSTRAINT DF_AppUser_IsActive DEFAULT (1),
        CreatedBy         INT            NOT NULL CONSTRAINT DF_AppUser_CreatedBy DEFAULT (0),
        CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_AppUser_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy         INT            NULL,
        UpdatedAt         DATETIME2(0)   NULL,
        CONSTRAINT UQ_AppUser_UserName UNIQUE (UserName),
        CONSTRAINT CK_AppUser_Role CHECK (Role IN (1, 2, 3))
    );
END
GO

/* ---------- Customer ---------- */
IF OBJECT_ID(N'dbo.Customer', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customer
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
        CompanyId       INT            NOT NULL,
        BusinessUnitId  INT            NOT NULL,
        CustomerName    NVARCHAR(150)  NOT NULL,
        Mobile          NVARCHAR(30)   NOT NULL,
        SocialLink      NVARCHAR(500)  NULL,
        Address         NVARCHAR(500)  NULL,
        IsActive        BIT            NOT NULL CONSTRAINT DF_Customer_IsActive DEFAULT (1),
        CreatedBy       INT            NOT NULL CONSTRAINT DF_Customer_CreatedBy DEFAULT (0),
        CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Customer_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy       INT            NULL,
        UpdatedAt       DATETIME2(0)   NULL
    );
    CREATE INDEX IX_Customer_Scope_Mobile ON dbo.Customer (CompanyId, BusinessUnitId, Mobile);
END
GO

/* ---------- Purchase Batch (one buying trip / one exchange rate) ---------- */
IF OBJECT_ID(N'dbo.PurchaseBatch', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PurchaseBatch
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PurchaseBatch PRIMARY KEY,
        CompanyId       INT            NOT NULL,
        BusinessUnitId  INT            NOT NULL,
        BatchNo         NVARCHAR(30)   NOT NULL,
        BatchDate       DATE           NOT NULL,
        ExchangeRate    DECIMAL(18,4)  NOT NULL,          -- BDT per 1 SGD
        IsPaid          BIT            NOT NULL CONSTRAINT DF_PurchaseBatch_IsPaid DEFAULT (0),
        PaidDate        DATE           NULL,
        Notes           NVARCHAR(500)  NULL,
        IsActive        BIT            NOT NULL CONSTRAINT DF_PurchaseBatch_IsActive DEFAULT (1),
        CreatedBy       INT            NOT NULL CONSTRAINT DF_PurchaseBatch_CreatedBy DEFAULT (0),
        CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_PurchaseBatch_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy       INT            NULL,
        UpdatedAt       DATETIME2(0)   NULL,
        CONSTRAINT CK_PurchaseBatch_Rate CHECK (ExchangeRate > 0)
    );
    CREATE INDEX IX_PurchaseBatch_Scope_Date ON dbo.PurchaseBatch (CompanyId, BusinessUnitId, BatchDate);
END
GO

/* ---------- Pre-Order ----------
   Status: 1 = Pre-order, 2 = Out for delivery, 3 = Delivered, 4 = Returned */
IF OBJECT_ID(N'dbo.PreOrder', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PreOrder
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PreOrder PRIMARY KEY,
        CompanyId         INT            NOT NULL,
        BusinessUnitId    INT            NOT NULL,
        OrderNo           NVARCHAR(30)   NOT NULL,
        OrderDate         DATE           NOT NULL,
        CustomerId        INT            NOT NULL CONSTRAINT FK_PreOrder_Customer REFERENCES dbo.Customer(Id),
        BatchId           INT            NULL     CONSTRAINT FK_PreOrder_Batch REFERENCES dbo.PurchaseBatch(Id),
        DeliveryAddress   NVARCHAR(500)  NULL,
        ProductName       NVARCHAR(200)  NOT NULL,
        ProductImage      NVARCHAR(300)  NULL,
        ProductLink       NVARCHAR(500)  NULL,
        ProductSize       NVARCHAR(50)   NULL,
        PurchasePriceSgd  DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PreOrder_Sgd DEFAULT (0),
        ProductPriceBdt   DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PreOrder_Bdt DEFAULT (0),
        SellingPrice      DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PreOrder_Selling DEFAULT (0),
        AdvanceAmount     DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PreOrder_Advance DEFAULT (0),
        DueAmount         AS (SellingPrice - AdvanceAmount) PERSISTED,
        Profit            AS (SellingPrice - ProductPriceBdt) PERSISTED,
        Status            TINYINT        NOT NULL CONSTRAINT DF_PreOrder_Status DEFAULT (1),
        DispatchedAt      DATETIME2(0)   NULL,
        DeliveredAt       DATETIME2(0)   NULL,
        CollectedAmount   DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PreOrder_Collected DEFAULT (0),  -- collected at delivery
        ReturnedAt        DATETIME2(0)   NULL,
        ReturnReason      NVARCHAR(500)  NULL,
        RefundAmount      DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PreOrder_Refund DEFAULT (0),     -- advance given back
        Notes             NVARCHAR(500)  NULL,
        IsActive          BIT            NOT NULL CONSTRAINT DF_PreOrder_IsActive DEFAULT (1),
        CreatedBy         INT            NOT NULL CONSTRAINT DF_PreOrder_CreatedBy DEFAULT (0),
        CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_PreOrder_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy         INT            NULL,
        UpdatedAt         DATETIME2(0)   NULL,
        CONSTRAINT CK_PreOrder_Status CHECK (Status IN (1, 2, 3, 4)),
        CONSTRAINT CK_PreOrder_Amounts CHECK (PurchasePriceSgd >= 0 AND ProductPriceBdt >= 0
                                              AND SellingPrice >= 0 AND AdvanceAmount >= 0)
    );
    CREATE UNIQUE INDEX UX_PreOrder_OrderNo ON dbo.PreOrder (CompanyId, OrderNo);
    CREATE INDEX IX_PreOrder_Scope_Status ON dbo.PreOrder (CompanyId, BusinessUnitId, Status, OrderDate) INCLUDE (IsActive);
    CREATE INDEX IX_PreOrder_Batch ON dbo.PreOrder (BatchId) WHERE BatchId IS NOT NULL;
    CREATE INDEX IX_PreOrder_Customer ON dbo.PreOrder (CustomerId);
END
GO

/* ---------- Order status history (audit trail) ---------- */
IF OBJECT_ID(N'dbo.OrderStatusLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderStatusLog
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderStatusLog PRIMARY KEY,
        CompanyId       INT            NOT NULL,
        BusinessUnitId  INT            NOT NULL,
        OrderId         INT            NOT NULL CONSTRAINT FK_OrderStatusLog_Order REFERENCES dbo.PreOrder(Id),
        FromStatus      TINYINT        NULL,
        ToStatus        TINYINT        NOT NULL,
        Remarks         NVARCHAR(500)  NULL,
        ChangedBy       INT            NOT NULL,
        ChangedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_OrderStatusLog_ChangedAt DEFAULT (SYSDATETIME())
    );
    CREATE INDEX IX_OrderStatusLog_Order ON dbo.OrderStatusLog (OrderId);
END
GO

/* ---------- Stock (extra products + returned products) ----------
   SourceType: 1 = Extra purchase, 2 = Customer return
   Status:     1 = In stock,       2 = Sold                            */
IF OBJECT_ID(N'dbo.StockItem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockItem
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockItem PRIMARY KEY,
        CompanyId       INT            NOT NULL,
        BusinessUnitId  INT            NOT NULL,
        SourceType      TINYINT        NOT NULL,
        SourceOrderId   INT            NULL CONSTRAINT FK_StockItem_Order REFERENCES dbo.PreOrder(Id),
        BatchId         INT            NULL CONSTRAINT FK_StockItem_Batch REFERENCES dbo.PurchaseBatch(Id),
        EntryDate       DATE           NOT NULL,
        ProductName     NVARCHAR(200)  NOT NULL,
        ProductImage    NVARCHAR(300)  NULL,
        ProductLink     NVARCHAR(500)  NULL,
        ProductSize     NVARCHAR(50)   NULL,
        PriceSgd        DECIMAL(18,2)  NOT NULL CONSTRAINT DF_StockItem_Sgd DEFAULT (0),
        PriceBdt        DECIMAL(18,2)  NOT NULL CONSTRAINT DF_StockItem_Bdt DEFAULT (0),
        ReturnReason    NVARCHAR(500)  NULL,
        Status          TINYINT        NOT NULL CONSTRAINT DF_StockItem_Status DEFAULT (1),
        SoldPrice       DECIMAL(18,2)  NULL,
        SoldDate        DATE           NULL,
        SoldTo          NVARCHAR(150)  NULL,
        Notes           NVARCHAR(500)  NULL,
        IsActive        BIT            NOT NULL CONSTRAINT DF_StockItem_IsActive DEFAULT (1),
        CreatedBy       INT            NOT NULL CONSTRAINT DF_StockItem_CreatedBy DEFAULT (0),
        CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_StockItem_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy       INT            NULL,
        UpdatedAt       DATETIME2(0)   NULL,
        CONSTRAINT CK_StockItem_Source CHECK (SourceType IN (1, 2)),
        CONSTRAINT CK_StockItem_Status CHECK (Status IN (1, 2))
    );
    CREATE INDEX IX_StockItem_Scope ON dbo.StockItem (CompanyId, BusinessUnitId, Status, SourceType);
END
GO

/* ---------- Expense category (CompanyId = 0 -> shared by everyone) ---------- */
IF OBJECT_ID(N'dbo.ExpenseCategory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpenseCategory
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExpenseCategory PRIMARY KEY,
        CompanyId       INT            NOT NULL CONSTRAINT DF_ExpenseCategory_CompanyId DEFAULT (0),
        BusinessUnitId  INT            NOT NULL CONSTRAINT DF_ExpenseCategory_BusinessUnitId DEFAULT (0),
        CategoryName    NVARCHAR(100)  NOT NULL,
        IsActive        BIT            NOT NULL CONSTRAINT DF_ExpenseCategory_IsActive DEFAULT (1),
        CreatedBy       INT            NOT NULL CONSTRAINT DF_ExpenseCategory_CreatedBy DEFAULT (0),
        CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_ExpenseCategory_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy       INT            NULL,
        UpdatedAt       DATETIME2(0)   NULL
    );
END
GO

/* ---------- Expense ---------- */
IF OBJECT_ID(N'dbo.Expense', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Expense
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Expense PRIMARY KEY,
        CompanyId       INT            NOT NULL,
        BusinessUnitId  INT            NOT NULL,
        ExpenseDate     DATE           NOT NULL,
        CategoryId      INT            NOT NULL CONSTRAINT FK_Expense_Category REFERENCES dbo.ExpenseCategory(Id),
        BatchId         INT            NULL     CONSTRAINT FK_Expense_Batch REFERENCES dbo.PurchaseBatch(Id),
        Amount          DECIMAL(18,2)  NOT NULL,
        Description     NVARCHAR(500)  NULL,
        IsActive        BIT            NOT NULL CONSTRAINT DF_Expense_IsActive DEFAULT (1),
        CreatedBy       INT            NOT NULL CONSTRAINT DF_Expense_CreatedBy DEFAULT (0),
        CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Expense_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedBy       INT            NULL,
        UpdatedAt       DATETIME2(0)   NULL,
        CONSTRAINT CK_Expense_Amount CHECK (Amount > 0)
    );
    CREATE INDEX IX_Expense_Scope_Date ON dbo.Expense (CompanyId, BusinessUnitId, ExpenseDate);
END
GO

/* ---------- Document number sequence (ORD-2610-0001 ...) ---------- */
IF OBJECT_ID(N'dbo.DocumentSequence', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentSequence
    (
        CompanyId   INT           NOT NULL,
        Prefix      NVARCHAR(10)  NOT NULL,
        Period      CHAR(4)       NOT NULL,   -- yyMM
        LastNumber  INT           NOT NULL,
        CONSTRAINT PK_DocumentSequence PRIMARY KEY (CompanyId, Prefix, Period)
    );
END
GO
