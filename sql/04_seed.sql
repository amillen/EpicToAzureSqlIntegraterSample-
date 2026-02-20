-- Epic Billing Analytics & Triage - Seed Data
-- Development seed data (FAKE DATA ONLY - NO PHI)

-- Clear existing data (dev only)
DELETE FROM dbo.TriageNote;
DELETE FROM dbo.AuditLog;
DELETE FROM dbo.WorkQueueItem;
DELETE FROM dbo.ChargeLine;
DELETE FROM dbo.Claim;
DELETE FROM dbo.Encounter;
DELETE FROM dbo.Patient;
GO

-- Seed Patients (using hashed MRNs for demo)
INSERT INTO dbo.Patient (EpicPatientId, DOB, Gender, MRNHash)
VALUES
    ('EPT001', '1975-03-15', 'M', 'MRN_HASH_ABC123'),
    ('EPT002', '1982-07-22', 'F', 'MRN_HASH_DEF456'),
    ('EPT003', '1990-11-05', 'F', 'MRN_HASH_GHI789'),
    ('EPT004', '1968-01-30', 'M', 'MRN_HASH_JKL012'),
    ('EPT005', '1995-09-18', 'M', 'MRN_HASH_MNO345');
GO

-- Seed Encounters
INSERT INTO dbo.Encounter (EpicEncounterId, PatientId, AdmitDate, DischargeDate)
VALUES
    ('ENC001', 1, '2024-01-10 08:00:00', '2024-01-12 14:00:00'),
    ('ENC002', 2, '2024-01-15 10:30:00', '2024-01-15 16:00:00'),
    ('ENC003', 3, '2024-01-20 07:15:00', '2024-01-22 09:30:00'),
    ('ENC004', 4, '2024-01-25 09:00:00', '2024-01-27 11:00:00'),
    ('ENC005', 5, '2024-02-01 13:00:00', '2024-02-01 18:00:00');
GO

-- Seed Claims
INSERT INTO dbo.Claim (EpicClaimId, EncounterId, Payer, BillType, TotalCharge, TotalAllowed, TotalPaid, ClaimStatus, LastEpicUpdateUtc)
VALUES
    ('CLM001', 1, 'Blue Cross Blue Shield', '111', 15000.00, 12000.00, 11500.00, 'PROCESSED', '2024-01-25 10:00:00'),
    ('CLM002', 2, 'Medicare', '131', 3500.00, 3000.00, 2500.00, 'DENIED', '2024-01-28 14:30:00'),
    ('CLM003', 3, 'Aetna', '111', 8000.00, 7000.00, 5000.00, 'PROCESSED', '2024-02-02 09:15:00'),
    ('CLM004', 4, 'United Healthcare', '111', 12000.00, 10000.00, 10000.00, 'PROCESSED', '2024-02-05 11:00:00'),
    ('CLM005', 5, 'Cigna', '131', 2000.00, 1800.00, 0.00, 'PENDED', '2024-02-10 16:20:00');
GO

-- Seed Charge Lines
INSERT INTO dbo.ChargeLine (ClaimId, CPT, Modifier, Units, ChargeAmount, AllowedAmount, PaidAmount, DenialCode, ServiceDate)
VALUES
    -- CLM001 (mostly paid)
    (1, '99223', NULL, 1, 5000.00, 4000.00, 4000.00, NULL, '2024-01-10'),
    (1, '36415', NULL, 2, 500.00, 400.00, 400.00, NULL, '2024-01-10'),
    (1, '80053', NULL, 1, 2000.00, 1600.00, 1600.00, NULL, '2024-01-11'),
    (1, '99233', NULL, 2, 7500.00, 6000.00, 5500.00, NULL, '2024-01-11'),
    -- CLM002 (denied)
    (2, '99213', NULL, 1, 1500.00, 1200.00, 0.00, 'CO-16', '2024-01-15'),
    (2, '85025', NULL, 1, 500.00, 400.00, 0.00, 'CO-16', '2024-01-15'),
    (2, '36415', NULL, 1, 250.00, 200.00, 0.00, 'CO-16', '2024-01-15'),
    (2, '94060', NULL, 1, 1250.00, 1200.00, 2500.00, NULL, '2024-01-15'),
    -- CLM003 (underpaid)
    (3, '99285', NULL, 1, 4000.00, 3500.00, 2500.00, NULL, '2024-01-20'),
    (3, '71045', NULL, 1, 2000.00, 1750.00, 1250.00, NULL, '2024-01-20'),
    (3, '36415', NULL, 1, 250.00, 200.00, 200.00, NULL, '2024-01-20'),
    (3, '80053', NULL, 1, 1750.00, 1550.00, 1050.00, NULL, '2024-01-21'),
    -- CLM004 (clean - fully paid)
    (4, '99223', NULL, 1, 5000.00, 4000.00, 4000.00, NULL, '2024-01-25'),
    (4, '93000', NULL, 1, 3000.00, 2500.00, 2500.00, NULL, '2024-01-25'),
    (4, '71020', NULL, 1, 2000.00, 1750.00, 1750.00, NULL, '2024-01-26'),
    (4, '36415', NULL, 2, 500.00, 400.00, 400.00, NULL, '2024-01-26'),
    (4, '99233', NULL, 1, 1500.00, 1350.00, 1350.00, NULL, '2024-01-27'),
    -- CLM005 (pended - not paid)
    (5, '99213', NULL, 1, 2000.00, 1800.00, 0.00, NULL, '2024-02-01');
GO

-- Run work queue rebuild to create queue items
EXEC dbo.sp_RebuildWorkQueue;
GO

-- Add some triage notes
INSERT INTO dbo.TriageNote (WorkQueueItemId, AuthorUpn, NoteText)
SELECT
    WorkQueueItemId,
    'dev-analyst@example.com',
    'Initial review - checking payer contract'
FROM dbo.WorkQueueItem
WHERE ClaimId = 2;

INSERT INTO dbo.TriageNote (WorkQueueItemId, AuthorUpn, NoteText)
SELECT
    WorkQueueItemId,
    'dev-analyst@example.com',
    'Contacted payer - awaiting response on denial codes'
FROM dbo.WorkQueueItem
WHERE ClaimId = 2;

INSERT INTO dbo.TriageNote (WorkQueueItemId, AuthorUpn, NoteText)
SELECT
    WorkQueueItemId,
    'dev-analyst@example.com',
    'Underpayment identified - need to appeal'
FROM dbo.WorkQueueItem
WHERE ClaimId = 3;
GO

-- Add some audit log entries
INSERT INTO dbo.AuditLog (EntityName, EntityId, Action, ActorUpn, BeforeJson, AfterJson)
VALUES
    ('WorkQueueItem', '2', 'StatusChange', 'dev-analyst@example.com', '{"Status":"New"}', '{"Status":"In Progress"}'),
    ('Claim', '2', 'Update', 'dev-analyst@example.com', '{"ClaimStatus":"DENIED"}', '{"ClaimStatus":"DENIED"}'),
    ('WorkQueueItem', '2', 'Assign', 'dev-analyst@example.com', '{"AssignedToUpn":null}', '{"AssignedToUpn":"dev-analyst@example.com"}');
GO

PRINT 'Seed data loaded successfully';
PRINT 'Patients: 5';
PRINT 'Encounters: 5';
PRINT 'Claims: 5';
PRINT 'Charge Lines: 18';
SELECT 'Work Queue Items: ' + CAST(COUNT(*) AS NVARCHAR) FROM dbo.WorkQueueItem;
SELECT 'Triage Notes: ' + CAST(COUNT(*) AS NVARCHAR) FROM dbo.TriageNote;
