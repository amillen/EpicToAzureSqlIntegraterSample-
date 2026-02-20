using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkQueueController : ControllerBase
{
    private readonly BillingDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _config;

    public WorkQueueController(BillingDbContext context, IAuditService auditService, IConfiguration config)
    {
        _context = context;
        _auditService = auditService;
        _config = config;
    }

    private string GetCurrentUserUpn()
    {
        // In dev bypass mode, use a test user
        if (_config.GetValue<bool>("DEV_BYPASS_AUTH"))
        {
            return "dev-analyst@example.com";
        }

        // Get from JWT claims
        return User.FindFirst("preferred_username")?.Value 
            ?? User.FindFirst("upn")?.Value 
            ?? User.Identity?.Name 
            ?? "unknown@example.com";
    }

    // GET /api/workqueue
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkQueueListItemDto>>> GetWorkQueue(
        [FromQuery] string? queue = null,
        [FromQuery] string? status = null,
        [FromQuery] string? assignedTo = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.WorkQueueItems
            .Include(w => w.Claim)
                .ThenInclude(c => c.Encounter)
                .ThenInclude(e => e.Patient)
            .AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(queue))
        {
            query = query.Where(w => w.QueueName == queue);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(w => w.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(assignedTo))
        {
            var currentUser = GetCurrentUserUpn();
            if (assignedTo.Equals("me", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(w => w.AssignedToUpn == currentUser);
            }
            else if (assignedTo.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(w => w.AssignedToUpn == null);
            }
            // Otherwise "all" - no filter
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(w => w.Claim.EpicClaimId.Contains(search) 
                || (w.Claim.Encounter.Patient.MRNHash != null && w.Claim.Encounter.Patient.MRNHash.Contains(search)));
        }

        if (from.HasValue)
        {
            query = query.Where(w => w.CreatedUtc >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(w => w.CreatedUtc <= to.Value);
        }

        // Pagination
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(w => w.Priority)
            .ThenBy(w => w.CreatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new WorkQueueListItemDto
            {
                WorkQueueItemId = w.WorkQueueItemId,
                ClaimId = w.ClaimId,
                EpicClaimId = w.Claim.EpicClaimId,
                QueueName = w.QueueName,
                Priority = w.Priority,
                Status = w.Status,
                AssignedToUpn = w.AssignedToUpn,
                CreatedUtc = w.CreatedUtc,
                UpdatedUtc = w.UpdatedUtc,
                AgeDays = EF.Functions.DateDiffDay(w.CreatedUtc, DateTime.UtcNow),
                AtRisk = w.Claim.TotalAllowed - w.Claim.TotalPaid,
                Payer = w.Claim.Payer,
                ClaimStatus = w.Claim.ClaimStatus,
                MRNHash = w.Claim.Encounter.Patient.MRNHash
            })
            .ToListAsync();

        Response.Headers.Append("X-Total-Count", totalCount.ToString());
        Response.Headers.Append("X-Page", page.ToString());
        Response.Headers.Append("X-Page-Size", pageSize.ToString());

        return Ok(items);
    }

    // GET /api/workqueue/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<WorkQueueDetailDto>> GetWorkQueueItem(int id)
    {
        var item = await _context.WorkQueueItems
            .Include(w => w.Claim)
                .ThenInclude(c => c.Encounter)
                .ThenInclude(e => e.Patient)
            .Include(w => w.Claim)
                .ThenInclude(c => c.ChargeLines)
            .Include(w => w.TriageNotes)
            .FirstOrDefaultAsync(w => w.WorkQueueItemId == id);

        if (item == null)
        {
            return NotFound();
        }

        var dto = new WorkQueueDetailDto
        {
            WorkQueueItemId = item.WorkQueueItemId,
            ClaimId = item.ClaimId,
            EpicClaimId = item.Claim.EpicClaimId,
            QueueName = item.QueueName,
            Priority = item.Priority,
            Status = item.Status,
            AssignedToUpn = item.AssignedToUpn,
            CreatedUtc = item.CreatedUtc,
            UpdatedUtc = item.UpdatedUtc,
            Claim = new ClaimDto
            {
                ClaimId = item.Claim.ClaimId,
                EpicClaimId = item.Claim.EpicClaimId,
                Payer = item.Claim.Payer,
                BillType = item.Claim.BillType,
                TotalCharge = item.Claim.TotalCharge,
                TotalAllowed = item.Claim.TotalAllowed,
                TotalPaid = item.Claim.TotalPaid,
                ClaimStatus = item.Claim.ClaimStatus,
                LastEpicUpdateUtc = item.Claim.LastEpicUpdateUtc,
                EpicEncounterId = item.Claim.Encounter.EpicEncounterId,
                AdmitDate = item.Claim.Encounter.AdmitDate,
                DischargeDate = item.Claim.Encounter.DischargeDate,
                EpicPatientId = item.Claim.Encounter.Patient.EpicPatientId,
                MRNHash = item.Claim.Encounter.Patient.MRNHash,
                Gender = item.Claim.Encounter.Patient.Gender,
                DOB = item.Claim.Encounter.Patient.DOB,
                ChargeLines = item.Claim.ChargeLines.Select(cl => new ChargeLineDto
                {
                    ChargeLineId = cl.ChargeLineId,
                    CPT = cl.CPT,
                    Modifier = cl.Modifier,
                    Units = cl.Units,
                    ChargeAmount = cl.ChargeAmount,
                    AllowedAmount = cl.AllowedAmount,
                    PaidAmount = cl.PaidAmount,
                    DenialCode = cl.DenialCode,
                    ServiceDate = cl.ServiceDate
                }).ToList()
            },
            Notes = item.TriageNotes.Select(n => new TriageNoteDto
            {
                TriageNoteId = n.TriageNoteId,
                AuthorUpn = n.AuthorUpn,
                NoteText = n.NoteText,
                CreatedUtc = n.CreatedUtc
            }).OrderByDescending(n => n.CreatedUtc).ToList()
        };

        return Ok(dto);
    }

    // POST /api/workqueue/{id}/assign
    [HttpPost("{id}/assign")]
    public async Task<IActionResult> AssignToMe(int id)
    {
        var item = await _context.WorkQueueItems.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        var currentUser = GetCurrentUserUpn();
        var oldValue = new { item.AssignedToUpn };
        
        item.AssignedToUpn = currentUser;
        item.UpdatedUtc = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("WorkQueueItem", id.ToString(), "Assign", currentUser, oldValue, new { AssignedToUpn = currentUser });

        return Ok();
    }

    // POST /api/workqueue/{id}/status
    [HttpPost("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var item = await _context.WorkQueueItems.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        var currentUser = GetCurrentUserUpn();
        var oldValue = new { item.Status };
        
        item.Status = request.Status;
        item.UpdatedUtc = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("WorkQueueItem", id.ToString(), "StatusChange", currentUser, oldValue, new { Status = request.Status });

        return Ok();
    }

    // POST /api/workqueue/{id}/notes
    [HttpPost("{id}/notes")]
    public async Task<ActionResult<TriageNoteDto>> AddNote(int id, [FromBody] AddNoteRequest request)
    {
        var item = await _context.WorkQueueItems.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        var currentUser = GetCurrentUserUpn();
        var note = new TriageNote
        {
            WorkQueueItemId = id,
            AuthorUpn = currentUser,
            NoteText = request.NoteText,
            CreatedUtc = DateTime.UtcNow
        };

        _context.TriageNotes.Add(note);
        await _context.SaveChangesAsync();
        await _auditService.LogAsync("TriageNote", note.TriageNoteId.ToString(), "Create", currentUser, null, note);

        var dto = new TriageNoteDto
        {
            TriageNoteId = note.TriageNoteId,
            AuthorUpn = note.AuthorUpn,
            NoteText = note.NoteText,
            CreatedUtc = note.CreatedUtc
        };

        return CreatedAtAction(nameof(GetWorkQueueItem), new { id }, dto);
    }
}
