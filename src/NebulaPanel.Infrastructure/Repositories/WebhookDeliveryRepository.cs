using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class WebhookDeliveryRepository(NebulaPanelDbContext context) : IWebhookDeliveryRepository
{
    public async Task<IReadOnlyList<WebhookDelivery>> GetByEndpointIdAsync(Guid endpointId, int limit = 50, CancellationToken cancellationToken = default)
    {
        return await context.WebhookDeliveries
            .Where(d => d.EndpointId == endpointId)
            .OrderByDescending(d => d.AttemptedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        await context.WebhookDeliveries.AddAsync(delivery, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default)
    {
        await context.WebhookDeliveries
            .Where(d => d.AttemptedAt < threshold)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
