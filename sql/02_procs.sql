-- Epic Billing Analytics & Triage - Stored Procedures
-- These handle the ETL logic from staging to core tables

-- ==========================================
-- PROCEDURE: Process Epic Claims
-- ==========================================
CREATE OR ALTER PROCEDURE dbo.sp_ProcessEpicClaims
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Upsert Patients
        MERGE dbo.Patient AS target
        USING (
            SELECT DISTINCT
                EpicPatientId,
                PatientDOB,
                PatientGender,
                PatientMRNHash
            FROM staging.EpicClaimCsv
        ) AS source
        ON target.EpicPatientId = source.EpicPatientId
        WHEN MATCHED THEN
            UPDATE SET
                DOB = COALESCE(source.PatientDOB, target.DOB),
                Gender = COALESCE(source.PatientGender, target.Gender),
                MRNHash = COALESCE(source.PatientMRNHash, target.MRNHash),
                UpdatedUtc = GETUTCDATE()
        WHEN NOT MATCHED THEN
            INSERT (EpicPatientId, DOB, Gender, MRNHash)
            VALUES (source.EpicPatientId, source.PatientDOB, source.PatientGender, source.PatientMRNHash);
        
        -- Upsert Encounters
        MERGE dbo.Encounter AS target
        USING (
            SELECT DISTINCT
                s.EpicEncounterId,
                p.PatientId,
                s.AdmitDate,
                s.DischargeDate
            FROM staging.EpicClaimCsv s
            INNER JOIN dbo.Patient p ON s.EpicPatientId = p.EpicPatientId
        ) AS source
        ON target.EpicEncounterId = source.EpicEncounterId
        WHEN MATCHED THEN
            UPDATE SET
                AdmitDate = COALESCE(source.AdmitDate, target.AdmitDate),
                DischargeDate = COALESCE(source.DischargeDate, target.DischargeDate),
                UpdatedUtc = GETUTCDATE()
        WHEN NOT MATCHED THEN
            INSERT (EpicEncounterId, PatientId, AdmitDate, DischargeDate)
            VALUES (source.EpicEncounterId, source.PatientId, source.AdmitDate, source.DischargeDate);
        
        -- Upsert Claims
        MERGE dbo.Claim AS target
        USING (
            SELECT DISTINCT
                s.EpicClaimId,
                e.EncounterId,
                s.Payer,
                s.BillType,
                s.TotalCharge,
                s.TotalAllowed,
                s.TotalPaid,
                s.ClaimStatus,
                s.LastEpicUpdateUtc
            FROM staging.EpicClaimCsv s
            INNER JOIN dbo.Encounter e ON s.EpicEncounterId = e.EpicEncounterId
        ) AS source
        ON target.EpicClaimId = source.EpicClaimId
        WHEN MATCHED THEN
            UPDATE SET
                Payer = source.Payer,
                BillType = source.BillType,
                TotalCharge = source.TotalCharge,
                TotalAllowed = source.TotalAllowed,
                TotalPaid = source.TotalPaid,
                ClaimStatus = source.ClaimStatus,
                LastEpicUpdateUtc = source.LastEpicUpdateUtc,
                UpdatedUtc = GETUTCDATE()
        WHEN NOT MATCHED THEN
            INSERT (EpicClaimId, EncounterId, Payer, BillType, TotalCharge, TotalAllowed, TotalPaid, ClaimStatus, LastEpicUpdateUtc)
            VALUES (source.EpicClaimId, source.EncounterId, source.Payer, source.BillType, source.TotalCharge, source.TotalAllowed, source.TotalPaid, source.ClaimStatus, source.LastEpicUpdateUtc);
        
        -- Clear staging table
        TRUNCATE TABLE staging.EpicClaimCsv;
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        THROW;
    END CATCH
END;
GO

-- ==========================================
-- PROCEDURE: Process Epic Charges
-- ==========================================
CREATE OR ALTER PROCEDURE dbo.sp_ProcessEpicCharges
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Delete existing charge lines for claims in staging (full refresh per claim)
        DELETE cl
        FROM dbo.ChargeLine cl
        INNER JOIN dbo.Claim c ON cl.ClaimId = c.ClaimId
        WHERE c.EpicClaimId IN (SELECT DISTINCT EpicClaimId FROM staging.EpicChargeCsv);
        
        -- Insert new charge lines
        INSERT INTO dbo.ChargeLine (ClaimId, CPT, Modifier, Units, ChargeAmount, AllowedAmount, PaidAmount, DenialCode, ServiceDate)
        SELECT
            c.ClaimId,
            s.CPT,
            s.Modifier,
            COALESCE(s.Units, 1),
            COALESCE(s.ChargeAmount, 0),
            COALESCE(s.AllowedAmount, 0),
            COALESCE(s.PaidAmount, 0),
            s.DenialCode,
            s.ServiceDate
        FROM staging.EpicChargeCsv s
        INNER JOIN dbo.Claim c ON s.EpicClaimId = c.EpicClaimId;
        
        -- Clear staging table
        TRUNCATE TABLE staging.EpicChargeCsv;
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        THROW;
    END CATCH
END;
GO

-- ==========================================
-- PROCEDURE: Rebuild Work Queue
-- ==========================================
-- This creates or updates WorkQueueItem records based on business rules
CREATE OR ALTER PROCEDURE dbo.sp_RebuildWorkQueue
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Determine queue assignment and priority for each claim
        WITH ClaimQueue AS (
            SELECT
                c.ClaimId,
                CASE
                    -- Rule 1: Denials or Pended claims
                    WHEN c.ClaimStatus IN ('DENIED', 'PENDED') THEN 'Denials'
                    -- Rule 2: Has any charge with denial code
                    WHEN EXISTS (
                        SELECT 1 FROM dbo.ChargeLine cl
                        WHERE cl.ClaimId = c.ClaimId AND cl.DenialCode IS NOT NULL
                    ) THEN 'Denials'
                    -- Rule 3: Underpaid (paid less than allowed)
                    WHEN c.TotalPaid < c.TotalAllowed THEN 'Underpaid'
                    -- Default: Clean
                    ELSE 'Clean'
                END AS QueueName,
                CASE
                    -- Priority: Denials=1, Underpaid=2, Clean=3
                    WHEN c.ClaimStatus IN ('DENIED', 'PENDED') OR
                         EXISTS (SELECT 1 FROM dbo.ChargeLine cl WHERE cl.ClaimId = c.ClaimId AND cl.DenialCode IS NOT NULL)
                        THEN 1
                    WHEN c.TotalPaid < c.TotalAllowed THEN 2
                    ELSE 3
                END AS Priority
            FROM dbo.Claim c
        )
        -- Merge into WorkQueueItem
        MERGE dbo.WorkQueueItem AS target
        USING ClaimQueue AS source
        ON target.ClaimId = source.ClaimId
        WHEN MATCHED AND (target.QueueName != source.QueueName OR target.Priority != source.Priority) THEN
            UPDATE SET
                QueueName = source.QueueName,
                Priority = source.Priority,
                UpdatedUtc = GETUTCDATE()
        WHEN NOT MATCHED THEN
            INSERT (ClaimId, QueueName, Priority, Status)
            VALUES (source.ClaimId, source.QueueName, source.Priority, 'New');
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        THROW;
    END CATCH
END;
GO
