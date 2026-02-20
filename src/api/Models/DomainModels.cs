namespace api.Models;

public class Patient
{
    public int PatientId { get; set; }
    public string EpicPatientId { get; set; } = null!;
    public DateTime? DOB { get; set; }
    public string? Gender { get; set; }
    public string? MRNHash { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public ICollection<Encounter> Encounters { get; set; } = new List<Encounter>();
}

public class Encounter
{
    public int EncounterId { get; set; }
    public string EpicEncounterId { get; set; } = null!;
    public int PatientId { get; set; }
    public DateTime? AdmitDate { get; set; }
    public DateTime? DischargeDate { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public Patient Patient { get; set; } = null!;
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
}

public class Claim
{
    public int ClaimId { get; set; }
    public string EpicClaimId { get; set; } = null!;
    public int EncounterId { get; set; }
    public string? Payer { get; set; }
    public string? BillType { get; set; }
    public decimal TotalCharge { get; set; }
    public decimal TotalAllowed { get; set; }
    public decimal TotalPaid { get; set; }
    public string? ClaimStatus { get; set; }
    public DateTime? LastEpicUpdateUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public Encounter Encounter { get; set; } = null!;
    public ICollection<ChargeLine> ChargeLines { get; set; } = new List<ChargeLine>();
    public WorkQueueItem? WorkQueueItem { get; set; }
}

public class ChargeLine
{
    public int ChargeLineId { get; set; }
    public int ClaimId { get; set; }
    public string? CPT { get; set; }
    public string? Modifier { get; set; }
    public int Units { get; set; }
    public decimal ChargeAmount { get; set; }
    public decimal AllowedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? DenialCode { get; set; }
    public DateTime? ServiceDate { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Claim Claim { get; set; } = null!;
}

public class WorkQueueItem
{
    public int WorkQueueItemId { get; set; }
    public int ClaimId { get; set; }
    public string QueueName { get; set; } = null!;
    public int Priority { get; set; }
    public string Status { get; set; } = null!;
    public string? AssignedToUpn { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public Claim Claim { get; set; } = null!;
    public ICollection<TriageNote> TriageNotes { get; set; } = new List<TriageNote>();
}

public class TriageNote
{
    public int TriageNoteId { get; set; }
    public int WorkQueueItemId { get; set; }
    public string AuthorUpn { get; set; } = null!;
    public string NoteText { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }

    public WorkQueueItem WorkQueueItem { get; set; } = null!;
}

public class AuditLog
{
    public long AuditLogId { get; set; }
    public string EntityName { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string ActorUpn { get; set; } = null!;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime CreatedUtc { get; set; }
}
