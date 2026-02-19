using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using api.Data;
using api.Models;

namespace api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly BillingDbContext _context;

    public AnalyticsController(BillingDbContext context)
    {
        _context = context;
    }

    // GET /api/analytics/workqueue-overview
    [HttpGet("workqueue-overview")]
    public async Task<ActionResult<IEnumerable<WorkQueueOverviewDto>>> GetWorkQueueOverview()
    {
        // Read from the analytics view
        var results = await _context.Database
            .SqlQueryRaw<WorkQueueOverviewDto>(
                "SELECT QueueName, Status, ItemCount, TotalAtRisk, Count_0_7Days, Count_8_30Days, Count_31_60Days, Count_Over60Days, AvgAgeDays FROM dbo.vw_WorkQueueOverview")
            .ToListAsync();

        return Ok(results);
    }

    // GET /api/analytics/claim-financials
    [HttpGet("claim-financials")]
    public async Task<ActionResult<IEnumerable<ClaimFinancialsDto>>> GetClaimFinancials(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        // Read from the analytics view with pagination
        var skip = (page - 1) * pageSize;
        
        var results = await _context.Database
            .SqlQueryRaw<ClaimFinancialsDto>(
                @"SELECT ClaimId, EpicClaimId, Payer, BillType, ClaimStatus, TotalCharge, TotalAllowed, TotalPaid, 
                  Variance, PaymentRate, QueueName, Priority, WorkQueueStatus, AssignedToUpn, WorkQueueAgeDays
                  FROM dbo.vw_ClaimFinancials
                  ORDER BY ClaimId
                  OFFSET {0} ROWS FETCH NEXT {1} ROWS ONLY", 
                skip, pageSize)
            .ToListAsync();

        return Ok(results);
    }
}
