-- ===========================================================================
-- FinTrack — 03 Indexed Views
-- SCHEMABINDING + unique clustered index: the planner uses the indexed view
-- directly for daily rollups (dashboard charts) without rewriting group-by
-- queries over millions of ledger rows at read time.
-- ===========================================================================

CREATE OR ALTER VIEW dbo.vw_AccountDailyTotals
WITH SCHEMABINDING
AS
SELECT
    t.AccountId,
    CONVERT(DATE, t.CreatedAt)      AS TxnDate,
    COUNT_BIG(*)                    AS TxnCount,
    SUM(CAST(t.Amount AS DECIMAL(18,2))) AS TotalNetAmount
FROM dbo.Transactions t
GROUP BY t.AccountId, CONVERT(DATE, t.CreatedAt);
GO

CREATE UNIQUE CLUSTERED INDEX IX_vw_AccountDailyTotals
    ON dbo.vw_AccountDailyTotals (AccountId, TxnDate);
GO

-- Reporting roll-up per customer (join through Accounts).
CREATE OR ALTER VIEW dbo.vw_CustomerDailyTotals
AS
SELECT
    a.CustomerId,
    v.AccountId,
    v.TxnDate,
    v.TxnCount,
    v.TotalNetAmount
FROM dbo.vw_AccountDailyTotals v
INNER JOIN dbo.Accounts a WITH (NOEXPAND) ON a.Id = v.AccountId;
GO
