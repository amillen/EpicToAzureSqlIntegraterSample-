using api.Data;
using api.Models;
using System.Text.Json;

namespace api.Services;

public interface IAuditService
{
    Task LogAsync(string entityName, string entityId, string action, string actorUpn, object? before = null, object? after = null);
}

public class AuditService : IAuditService
{
    private readonly BillingDbContext _context;

    public AuditService(BillingDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string entityName, string entityId, string action, string actorUpn, object? before = null, object? after = null)
    {
        var auditLog = new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            ActorUpn = actorUpn,
            BeforeJson = before != null ? JsonSerializer.Serialize(before) : null,
            AfterJson = after != null ? JsonSerializer.Serialize(after) : null,
            CreatedUtc = DateTime.UtcNow
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }
}
