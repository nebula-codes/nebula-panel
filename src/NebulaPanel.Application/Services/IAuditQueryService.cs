using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

public interface IAuditQueryService
{
    Task<PagedResult<AuditEventDto>> QueryAsync(
        AuditLogQueryRequest request,
        CancellationToken cancellationToken = default);
}
