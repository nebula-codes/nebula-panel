using Microsoft.EntityFrameworkCore;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Auth;

public class AuditQueryService(NebulaPanelDbContext context) : IAuditQueryService
{
    public async Task<PagedResult<AuditEventDto>> QueryAsync(
        AuditLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = context.SecurityAuditEvents.AsQueryable();

        if (request.UserId.HasValue)
            query = query.Where(e => e.UserId == request.UserId.Value);

        if (request.EventType.HasValue)
            query = query.Where(e => e.EventType == request.EventType.Value);

        if (request.From.HasValue)
            query = query.Where(e => e.OccurredAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(e => e.OccurredAt <= request.To.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(e =>
                (e.Username != null && e.Username.ToLower().Contains(term)) ||
                (e.Details != null && e.Details.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new AuditEventDto(
                e.Id,
                e.UserId,
                e.Username,
                e.EventType,
                e.IpAddress,
                e.Details,
                e.Success,
                e.OccurredAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<AuditEventDto>(items, totalCount, request.Page, request.PageSize);
    }
}
