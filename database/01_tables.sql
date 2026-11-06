-- ===========================================================================
-- FinTrack — 01 Tables
-- Target: MS SQL Server 2019+
-- Convention: Ids are GUIDs (matches EF Core model), UNSIGNED decimals for money
--             are NOT used; decimal(18,2) with precision. UTC dates only (datetimeoffset).
-- Run: the EF Core baseline migration creates these same tables; these scripts are
--      the DBA-reviewable form used for environments outside the migration path.
-- ===========================================================================

IF OBJECT_ID(N'dbo.Transactions') IS NULL
BEGIN
    CREATE TABLE dbo.Transactions (
        Id              UNIQUEIDENTIFIER   NOT NULL CONSTRAINT PK_Transactions PRIMARY KEY,
        AccountId       UNIQUEIDENTIFIER   NOT NULL,
        Type            INT                NOT NULL, -- 1 Opening | 2 Deposit | 3 Withdrawal | 4 TransferIn | 5 TransferOut | 6 Fee
        Amount          DECIMAL(18, 2)     NOT NULL, -- signed: negative = money leaves the account
        BalanceAfter    DECIMAL(18, 2)     NOT NULL,
        Reference       NVARCHAR(64)       NOT NULL, -- transfer pairing key, e.g. TRF-<guid>
        Description     NVARCHAR(256)      NULL,
        CreatedAt       DATETIMEOFFSET     NOT NULL CONSTRAINT DF_Transactions_CreatedAt DEFAULT (SYSUTCDATETIME())
    );
END
GO

IF OBJECT_ID(N'dbo.Accounts') IS NULL
BEGIN
    CREATE TABLE dbo.Accounts (
        Id              UNIQUEIDENTIFIER   NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
        CustomerId      UNIQUEIDENTIFIER   NOT NULL,
        Number          NVARCHAR(24)       NOT NULL,
        Type            INT                NOT NULL, -- 1 Checking | 2 Savings | 3 FixedDeposit
        Status          INT                NOT NULL CONSTRAINT DF_Accounts_Status DEFAULT (1), -- 1 Active | 2 Frozen | 3 Closed
        Currency        NVARCHAR(3)        NOT NULL CONSTRAINT CK_Accounts_Currency CHECK (LEN(Currency) = 3),
        Balance         DECIMAL(18, 2)     NOT NULL CONSTRAINT CK_Accounts_Balance CHECK ([Balance] >= 0),
        OpenedAt        DATETIMEOFFSET     NOT NULL CONSTRAINT DF_Accounts_OpenedAt DEFAULT (SYSUTCDATETIME()),
        ClosedAt        DATETIMEOFFSET     NULL,
        CreatedAt       DATETIMEOFFSET     NOT NULL CONSTRAINT DF_Accounts_CreatedAt DEFAULT (SYSUTCDATETIME()),
        RowVersion      ROWVERSION         NOT NULL, -- optimistic concurrency token used by EF Core
        CONSTRAINT FK_Accounts_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id)
    );
END
GO
