-- Epic Billing Analytics & Triage - Analytics Views
-- These views are designed for Tableau and other BI tools

-- ==========================================
-- VIEW: Work Queue Overview
-- ==========================================
-- Provides summary statistics for work queue monitoring
CREATE OR ALTER VIEW dbo.vw_WorkQueueOverview
AS
SELECT
    wq.QueueName,
    wq.Status,
    COUNT(*) AS ItemCount,
    SUM(c.TotalAllowed - c.TotalPaid) AS TotalAtRisk,
    SUM(CASE WHEN DATEDIFF(day, wq.CreatedUtc, GETUTCDATE()) <= 7 THEN 1 ELSE 0 END) AS Count_0_7Days,
    SUM(CASE WHEN DATEDIFF(day, wq.CreatedUtc, GETUTCDATE()) BETWEEN 8 AND 30 THEN 1 ELSE 0 END) AS Count_8_30Days,
    SUM(CASE WHEN DATEDIFF(day, wq.CreatedUtc, GETUTCDATE()) BETWEEN 31 AND 60 THEN 1 ELSE 0 END) AS Count_31_60Days,
    SUM(CASE WHEN DATEDIFF(day, wq.CreatedUtc, GETUTCDATE()) > 60 THEN 1 ELSE 0 END) AS Count_Over60Days,
    AVG(DATEDIFF(day, wq.CreatedUtc, GETUTCDATE())) AS AvgAgeDays
FROM dbo.WorkQueueItem wq
INNER JOIN dbo.Claim c ON wq.ClaimId = c.ClaimId
GROUP BY wq.QueueName, wq.Status;
GO

-- ==========================================
-- VIEW: Claim Financials
-- ==========================================
-- Tableau-ready view with all claim financial details
CREATE OR ALTER VIEW dbo.vw_ClaimFinancials
AS
SELECT
    c.ClaimId,
    c.EpicClaimId,
    c.Payer,
    c.BillType,
    c.ClaimStatus,
    c.TotalCharge,
    c.TotalAllowed,
    c.TotalPaid,
    c.TotalAllowed - c.TotalPaid AS Variance,
    CASE
        WHEN c.TotalAllowed > 0 THEN (c.TotalPaid * 100.0 / c.TotalAllowed)
        ELSE 0
    END AS PaymentRate,
    e.EpicEncounterId,
    e.AdmitDate,
    e.DischargeDate,
    DATEDIFF(day, e.AdmitDate, COALESCE(e.DischargeDate, GETUTCDATE())) AS LengthOfStay,
    p.PatientId,
    p.EpicPatientId,
    p.MRNHash,
    p.Gender,
    DATEDIFF(year, p.DOB, COALESCE(e.AdmitDate, GETUTCDATE())) AS AgeAtAdmit,
    wq.WorkQueueItemId,
    wq.QueueName,
    wq.Priority,
    wq.Status AS WorkQueueStatus,
    wq.AssignedToUpn,
    DATEDIFF(day, wq.CreatedUtc, GETUTCDATE()) AS WorkQueueAgeDays,
    c.LastEpicUpdateUtc,
    c.CreatedUtc,
    c.UpdatedUtc
FROM dbo.Claim c
INNER JOIN dbo.Encounter e ON c.EncounterId = e.EncounterId
INNER JOIN dbo.Patient p ON e.PatientId = p.PatientId
LEFT JOIN dbo.WorkQueueItem wq ON c.ClaimId = wq.ClaimId;
GO

-- ==========================================
-- VIEW: Charge Line Details
-- ==========================================
-- Detail view for charge line analysis
CREATE OR ALTER VIEW dbo.vw_ChargeLineDetails
AS
SELECT
    cl.ChargeLineId,
    cl.ClaimId,
    c.EpicClaimId,
    cl.CPT,
    cl.Modifier,
    cl.Units,
    cl.ChargeAmount,
    cl.AllowedAmount,
    cl.PaidAmount,
    cl.AllowedAmount - cl.PaidAmount AS Variance,
    cl.DenialCode,
    cl.ServiceDate,
    c.Payer,
    c.ClaimStatus,
    e.EpicEncounterId,
    p.EpicPatientId,
    p.MRNHash,
    cl.CreatedUtc
FROM dbo.ChargeLine cl
INNER JOIN dbo.Claim c ON cl.ClaimId = c.ClaimId
INNER JOIN dbo.Encounter e ON c.EncounterId = e.EncounterId
INNER JOIN dbo.Patient p ON e.PatientId = p.PatientId;
GO

-- ==========================================
-- VIEW: Denial Analysis
-- ==========================================
-- Summary view for denial code analysis
CREATE OR ALTER VIEW dbo.vw_DenialAnalysis
AS
SELECT
    COALESCE(cl.DenialCode, 'NO_CODE') AS DenialCode,
    COUNT(DISTINCT cl.ClaimId) AS ClaimCount,
    COUNT(*) AS ChargeLineCount,
    SUM(cl.ChargeAmount) AS TotalCharge,
    SUM(cl.AllowedAmount) AS TotalAllowed,
    SUM(cl.PaidAmount) AS TotalPaid,
    SUM(cl.AllowedAmount - cl.PaidAmount) AS TotalVariance,
    c.Payer
FROM dbo.ChargeLine cl
INNER JOIN dbo.Claim c ON cl.ClaimId = c.ClaimId
WHERE cl.DenialCode IS NOT NULL OR c.ClaimStatus IN ('DENIED', 'PENDED')
GROUP BY COALESCE(cl.DenialCode, 'NO_CODE'), c.Payer;
GO

-- ==========================================
-- VIEW: Payer Performance
-- ==========================================
-- Payer-level summary for analytics
CREATE OR ALTER VIEW dbo.vw_PayerPerformance
AS
SELECT
    c.Payer,
    COUNT(DISTINCT c.ClaimId) AS ClaimCount,
    SUM(c.TotalCharge) AS TotalCharge,
    SUM(c.TotalAllowed) AS TotalAllowed,
    SUM(c.TotalPaid) AS TotalPaid,
    SUM(c.TotalAllowed - c.TotalPaid) AS TotalVariance,
    CASE
        WHEN SUM(c.TotalAllowed) > 0 THEN (SUM(c.TotalPaid) * 100.0 / SUM(c.TotalAllowed))
        ELSE 0
    END AS PaymentRate,
    AVG(DATEDIFF(day, c.CreatedUtc, c.UpdatedUtc)) AS AvgDaysToUpdate
FROM dbo.Claim c
GROUP BY c.Payer;
GO
