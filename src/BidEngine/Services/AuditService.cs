using BidEngine.Data;
using BidEngine.Services.Interfaces;
using BidEngine.Shared;
using Microsoft.EntityFrameworkCore;

namespace BidEngine.Services;

/// <summary>
/// Audit logging service implementation
/// </summary>
public class AuditService : IAuditService
{
    private readonly AppDbContext _context;

    public AuditService(AppDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(Guid userId, string action, string entityType, Guid? entityId = null,
                              string? oldValues = null, string? newValues = null,
                              string? ipAddress = null, string? userAgent = null,
                              bool success = true, string? errorMessage = null)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Success = success,
            ErrorMessage = errorMessage
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(int page = 1, int pageSize = 50)
    {
        return await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(a => a.User)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(Guid userId, int page = 1, int pageSize = 50)
    {
        return await _context.AuditLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(a => a.User)
            .ToListAsync();
    }
}