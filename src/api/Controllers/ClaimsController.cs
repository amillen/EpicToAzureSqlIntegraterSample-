using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClaimsController : ControllerBase
{
    private readonly BillingDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IConfiguration _config;

    public ClaimsController(BillingDbContext context, IAuditService auditService, IConfiguration config)
    {
        _context = context;
        _auditService = auditService;
        _config = config;
    }

    private string GetCurrentUserUpn()
    {
        if (_config.GetValue<bool>("DEV_BYPASS_AUTH"))
        {
            return "dev-analyst@example.com";
        }
        return User.FindFirst("preferred_username")?.Value 
            ?? User.FindFirst("upn")?.Value 
            ?? User.Identity?.Name 
            ?? "unknown@example.com";
    }

    // PATCH /api/claims/{claimId}
    [HttpPatch("{claimId}")]
    public async Task<IActionResult> UpdateClaim(int claimId, [FromBody] UpdateClaimRequest request)
    {
        var claim = await _context.Claims.FindAsync(claimId);
        if (claim == null)
        {
            return NotFound();
        }

        var currentUser = GetCurrentUserUpn();
        var oldValue = new { claim.ClaimStatus, claim.Payer };

        // Update editable fields
        if (request.ClaimStatus != null)
        {
            claim.ClaimStatus = request.ClaimStatus;
        }

        if (request.Payer != null)
        {
            claim.Payer = request.Payer;
        }

        claim.UpdatedUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await _auditService.LogAsync("Claim", claimId.ToString(), "Update", currentUser, oldValue, new { claim.ClaimStatus, claim.Payer });

        return Ok();
    }
}
