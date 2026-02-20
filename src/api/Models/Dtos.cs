namespace api.Models;

// DTO for work queue list item
public class WorkQueueListItemDto
{
    public int WorkQueueItemId { get; set; }
    public int ClaimId { get; set; }
    public string EpicClaimId { get; set; } = null!;
    public string QueueName { get; set; } = null!;
    public int Priority { get; set; }
    public string Status { get; set; } = null!;
    public string? AssignedToUpn { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public int AgeDays { get; set; }
    public decimal AtRisk { get; set; }
    public string? Payer { get; set; }
    public string? ClaimStatus { get; set; }
    public string? MRNHash { get; set; }
}

// DTO for work queue detail
public class WorkQueueDetailDto
{
    public int WorkQueueItemId { get; set; }
    public int ClaimId { get; set; }
    public string EpicClaimId { get; set; } = null!;
    public string QueueName { get; set; } = null!;
    public int Priority { get; set; }
    public string Status { get; set; } = null!;
    public string? AssignedToUpn { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    
    // Claim details
    public ClaimDto Claim { get; set; } = null!;
    
    // Notes
    public List<TriageNoteDto> Notes { get; set; } = new();
}

// DTO for claim
public class ClaimDto
{
    public int ClaimId { get; set; }
    public string EpicClaimId { get; set; } = null!;
    public string? Payer { get; set; }
    public string? BillType { get; set; }
    public decimal TotalCharge { get; set; }
    public decimal TotalAllowed { get; set; }
    public decimal TotalPaid { get; set; }
    public string? ClaimStatus { get; set; }
    public DateTime? LastEpicUpdateUtc { get; set; }
    
    // Encounter
    public string EpicEncounterId { get; set; } = null!;
    public DateTime? AdmitDate { get; set; }
    public DateTime? DischargeDate { get; set; }
    
    // Patient
    public string EpicPatientId { get; set; } = null!;
    public string? MRNHash { get; set; }
    public string? Gender { get; set; }
    public DateTime? DOB { get; set; }
    
    // Charge lines
    public List<ChargeLineDto> ChargeLines { get; set; } = new();
}

// DTO for charge line
public class ChargeLineDto
{
    public int ChargeLineId { get; set; }
    public string? CPT { get; set; }
    public string? Modifier { get; set; }
    public int Units { get; set; }
    public decimal ChargeAmount { get; set; }
    public decimal AllowedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string? DenialCode { get; set; }
    public DateTime? ServiceDate { get; set; }
}

// DTO for triage note
public class TriageNoteDto
{
    public int TriageNoteId { get; set; }
    public string AuthorUpn { get; set; } = null!;
    public string NoteText { get; set; } = null!;
    public DateTime CreatedUtc { get; set; }
}

// Request DTOs
public class AssignWorkQueueRequest
{
    // Empty - will assign to current user from JWT
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = null!;
}

public class AddNoteRequest
{
    public string NoteText { get; set; } = null!;
}

public class UpdateClaimRequest
{
    public string? ClaimStatus { get; set; }
    public string? Payer { get; set; }
}

// Analytics DTOs
public class WorkQueueOverviewDto
{
    public string QueueName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int ItemCount { get; set; }
    public decimal TotalAtRisk { get; set; }
    public int Count_0_7Days { get; set; }
    public int Count_8_30Days { get; set; }
    public int Count_31_60Days { get; set; }
    public int Count_Over60Days { get; set; }
    public int AvgAgeDays { get; set; }
}

public class ClaimFinancialsDto
{
    public int ClaimId { get; set; }
    public string EpicClaimId { get; set; } = null!;
    public string? Payer { get; set; }
    public string? BillType { get; set; }
    public string? ClaimStatus { get; set; }
    public decimal TotalCharge { get; set; }
    public decimal TotalAllowed { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Variance { get; set; }
    public decimal PaymentRate { get; set; }
    public string? QueueName { get; set; }
    public int? Priority { get; set; }
    public string? WorkQueueStatus { get; set; }
    public string? AssignedToUpn { get; set; }
    public int? WorkQueueAgeDays { get; set; }
}
