using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class WebhookEndpointRepository(NebulaPanelDbContext context) : IWebhookEndpointRepository
{
    public async Task<IReadOnlyList<WebhookEndpoint>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await context.WebhookEndpoints
            .Where(e => e.OwnerId == ownerId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> GetEnabledByEventTypeAsync(WebhookEventType eventType, CancellationToken cancellationToken = default)
    {
        // Load all enabled endpoints and filter in memory since SubscribedEvents is JSON
        var endpoints = await context.WebhookEndpoints
            .Where(e => e.IsEnabled)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return endpoints.Where(e => e.SubscribedEvents.Contains(eventType)).ToList();
    }

    public async Task<WebhookEndpoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.WebhookEndpoints
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        await context.WebhookEndpoints.AddAsync(endpoint, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        context.WebhookEndpoints.Update(endpoint);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.WebhookEndpoints
            .Where(e => e.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
