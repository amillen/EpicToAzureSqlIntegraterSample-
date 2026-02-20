-- Epic Billing Analytics & Triage - Database Schema
-- Azure SQL compatible
-- Run this first to create all schemas and tables

-- Create schemas
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'staging')
    EXEC('CREATE SCHEMA staging');
GO

-- ==========================================
-- STAGING TABLES
-- ==========================================

-- Staging table for Epic Claim CSV files
CREATE TABLE staging.EpicClaimCsv (
    EpicClaimId NVARCHAR(50) NOT NULL,
    EpicEncounterId NVARCHAR(50) NOT NULL,
    EpicPatientId NVARCHAR(50) NOT NULL,
    PatientDOB DATE NULL,
    PatientGender NVARCHAR(10) NULL,
    PatientMRNHash NVARCHAR(100) NULL,
    AdmitDate DATETIME2 NULL,
    DischargeDate DATETIME2 NULL,
    Payer NVARCHAR(100) NULL,
    BillType NVARCHAR(20) NULL,
    TotalCharge DECIMAL(18,2) NULL,
    TotalAllowed DECIMAL(18,2) NULL,
    TotalPaid DECIMAL(18,2) NULL,
    ClaimStatus NVARCHAR(50) NULL,
    LastEpicUpdateUtc DATETIME2 NULL,
    LoadedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- Staging table for Epic Charge CSV files
CREATE TABLE staging.EpicChargeCsv (
    EpicClaimId NVARCHAR(50) NOT NULL,
    CPT NVARCHAR(10) NULL,
    Modifier NVARCHAR(10) NULL,
    Units INT NULL,
    ChargeAmount DECIMAL(18,2) NULL,
    AllowedAmount DECIMAL(18,2) NULL,
    PaidAmount DECIMAL(18,2) NULL,
    DenialCode NVARCHAR(20) NULL,
    ServiceDate DATE NULL,
    LoadedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ==========================================
-- CORE DOMAIN TABLES
-- ==========================================

-- Patient dimension
CREATE TABLE dbo.Patient (
    PatientId INT IDENTITY(1,1) PRIMARY KEY,
    EpicPatientId NVARCHAR(50) NOT NULL UNIQUE,
    DOB DATE NULL,
    Gender NVARCHAR(10) NULL,
    MRNHash NVARCHAR(100) NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_Patient_MRNHash ON dbo.Patient(MRNHash);
GO

-- Encounter dimension
CREATE TABLE dbo.Encounter (
    EncounterId INT IDENTITY(1,1) PRIMARY KEY,
    EpicEncounterId NVARCHAR(50) NOT NULL UNIQUE,
    PatientId INT NOT NULL,
    AdmitDate DATETIME2 NULL,
    DischargeDate DATETIME2 NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Encounter_Patient FOREIGN KEY (PatientId) REFERENCES dbo.Patient(PatientId)
);
GO

CREATE INDEX IX_Encounter_PatientId ON dbo.Encounter(PatientId);
CREATE INDEX IX_Encounter_AdmitDate ON dbo.Encounter(AdmitDate);
GO

-- Claim fact table
CREATE TABLE dbo.Claim (
    ClaimId INT IDENTITY(1,1) PRIMARY KEY,
    EpicClaimId NVARCHAR(50) NOT NULL UNIQUE,
    EncounterId INT NOT NULL,
    Payer NVARCHAR(100) NULL,
    BillType NVARCHAR(20) NULL,
    TotalCharge DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalAllowed DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalPaid DECIMAL(18,2) NOT NULL DEFAULT 0,
    ClaimStatus NVARCHAR(50) NULL,
    LastEpicUpdateUtc DATETIME2 NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Claim_Encounter FOREIGN KEY (EncounterId) REFERENCES dbo.Encounter(EncounterId)
);
GO

CREATE INDEX IX_Claim_EncounterId ON dbo.Claim(EncounterId);
CREATE INDEX IX_Claim_ClaimStatus ON dbo.Claim(ClaimStatus);
CREATE INDEX IX_Claim_Payer ON dbo.Claim(Payer);
GO

-- Charge line detail
CREATE TABLE dbo.ChargeLine (
    ChargeLineId INT IDENTITY(1,1) PRIMARY KEY,
    ClaimId INT NOT NULL,
    CPT NVARCHAR(10) NULL,
    Modifier NVARCHAR(10) NULL,
    Units INT NOT NULL DEFAULT 1,
    ChargeAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    AllowedAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaidAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DenialCode NVARCHAR(20) NULL,
    ServiceDate DATE NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ChargeLine_Claim FOREIGN KEY (ClaimId) REFERENCES dbo.Claim(ClaimId)
);
GO

CREATE INDEX IX_ChargeLine_ClaimId ON dbo.ChargeLine(ClaimId);
CREATE INDEX IX_ChargeLine_DenialCode ON dbo.ChargeLine(DenialCode) WHERE DenialCode IS NOT NULL;
GO

-- Work queue for triage
CREATE TABLE dbo.WorkQueueItem (
    WorkQueueItemId INT IDENTITY(1,1) PRIMARY KEY,
    ClaimId INT NOT NULL,
    QueueName NVARCHAR(50) NOT NULL,
    Priority INT NOT NULL DEFAULT 3,
    Status NVARCHAR(20) NOT NULL DEFAULT 'New',
    AssignedToUpn NVARCHAR(255) NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_WorkQueueItem_Claim FOREIGN KEY (ClaimId) REFERENCES dbo.Claim(ClaimId)
);
GO

CREATE INDEX IX_WorkQueueItem_ClaimId ON dbo.WorkQueueItem(ClaimId);
CREATE INDEX IX_WorkQueueItem_QueueName ON dbo.WorkQueueItem(QueueName);
CREATE INDEX IX_WorkQueueItem_Status ON dbo.WorkQueueItem(Status);
CREATE INDEX IX_WorkQueueItem_AssignedToUpn ON dbo.WorkQueueItem(AssignedToUpn);
GO

-- Triage notes
CREATE TABLE dbo.TriageNote (
    TriageNoteId INT IDENTITY(1,1) PRIMARY KEY,
    WorkQueueItemId INT NOT NULL,
    AuthorUpn NVARCHAR(255) NOT NULL,
    NoteText NVARCHAR(MAX) NOT NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_TriageNote_WorkQueueItem FOREIGN KEY (WorkQueueItemId) REFERENCES dbo.WorkQueueItem(WorkQueueItemId)
);
GO

CREATE INDEX IX_TriageNote_WorkQueueItemId ON dbo.TriageNote(WorkQueueItemId);
GO

-- Audit log for compliance
CREATE TABLE dbo.AuditLog (
    AuditLogId BIGINT IDENTITY(1,1) PRIMARY KEY,
    EntityName NVARCHAR(100) NOT NULL,
    EntityId NVARCHAR(50) NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    ActorUpn NVARCHAR(255) NOT NULL,
    BeforeJson NVARCHAR(MAX) NULL,
    AfterJson NVARCHAR(MAX) NULL,
    CreatedUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_AuditLog_EntityName ON dbo.AuditLog(EntityName);
CREATE INDEX IX_AuditLog_EntityId ON dbo.AuditLog(EntityId);
CREATE INDEX IX_AuditLog_ActorUpn ON dbo.AuditLog(ActorUpn);
CREATE INDEX IX_AuditLog_CreatedUtc ON dbo.AuditLog(CreatedUtc);
GO
