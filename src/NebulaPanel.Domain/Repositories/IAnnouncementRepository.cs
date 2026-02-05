using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Repositories;

/// <summary>
/// Repository for announcement management.
/// </summary>
public interface IAnnouncementRepository
{
    /// <summary>
    /// Gets an announcement by its ID.
    /// </summary>
    Task<Announcement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active announcements that haven't expired, ordered by pinned status and display order.
    /// </summary>
    Task<IReadOnlyList<Announcement>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all announcements regardless of status.
    /// </summary>
    Task<IReadOnlyList<Announcement>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new announcement.
    /// </summary>
    Task AddAsync(Announcement announcement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing announcement.
    /// </summary>
    Task UpdateAsync(Announcement announcement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an announcement by its ID.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
