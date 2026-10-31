-- ===========================================================================
-- FinTrack — 04 Demo Seed (local development only)
-- Idempotent: exits when demo customers already exist.
-- ===========================================================================

SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM dbo.Customers WHERE ExternalId IN ('usr_amira', 'usr_tarek'))
BEGIN
    PRINT 'Demo data already present; nothing to seed.';
    RETURN;
END

DECLARE @amira UNIQUEIDENTIFIER = NEWID(),
        @tarek UNIQUEIDENTIFIER = NEWID(),
        @chk   UNIQUEIDENTIFIER = NEWID(),
        @sav   UNIQUEIDENTIFIER = NEWID(),
        @tchk  UNIQUEIDENTIFIER = NEWID();

INSERT dbo.Customers (Id, ExternalId, FullName, Email, CreatedAt)
VALUES (@amira, 'usr_amira', 'Amira Hassan', 'amira.hassan@example.com', SYSUTCDATETIME()),
       (@tarek, 'usr_tarek', 'Tarek Samir', 'tarek.samir@example.com', SYSUTCDATETIME());

INSERT dbo.Accounts (Id, CustomerId, Number, Type, Status, Currency, Balance, OpenedAt)
VALUES (@chk,  @amira, 'FB-1029384756', 1, 1, 'USD', 4250.00, DATEADD(MONTH, -18, SYSUTCDATETIME())),
       (@sav,  @amira, 'FB-5647382910', 2, 1, 'USD', 12800.50, DATEADD(MONTH, -14, SYSUTCDATETIME())),
       (@tchk, @tarek, 'FB-7483920165', 1, 1, 'USD', 3120.75, DATEADD(MONTH, -6, SYSUTCDATETIME()));

INSERT dbo.Transactions (AccountId, Type, Amount, BalanceAfter, Reference, Description, CreatedAt)
VALUES (@chk,  2, 4000.00,    4000.00,   'SEED-001', 'Initial deposit', DATEADD(MONTH, -18, SYSUTCDATETIME())),
       (@chk,  2, 250.00,     4250.00,   'SEED-002', 'Salary',          DATEADD(MONTH,  -3, SYSUTCDATETIME())),
       (@sav,  2, 12800.50,   12800.50,  'SEED-003', 'Savings transfer', DATEADD(MONTH, -14, SYSUTCDATETIME())),
       (@tchk, 2, 3200.00,    3200.00,   'SEED-004', 'Initial deposit', DATEADD(MONTH,  -6, SYSUTCDATETIME())),
       (@tchk, 6, -79.25,     3120.75,   'SEED-005', 'Maintenance fee', DATEADD(MONTH,  -2, SYSUTCDATETIME()));

PRINT 'Seeded demo customers and accounts.';
GO
