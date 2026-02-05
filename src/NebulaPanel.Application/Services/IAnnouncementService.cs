using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing dashboard announcements.
/// </summary>
public interface IAnnouncementService
{
    /// <summary>
    /// Gets all active announcements that haven't expired.
    /// </summary>
    Task<IReadOnlyList<AnnouncementDto>> GetActiveAnnouncementsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all announcements for admin management.
    /// </summary>
    Task<IReadOnlyList<AnnouncementDto>> GetAllAnnouncementsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an announcement by ID.
    /// </summary>
    Task<Result<AnnouncementDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new announcement.
    /// </summary>
    /// <param name="request">The create request.</param>
    /// <param name="userId">The user creating the announcement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Result<AnnouncementDto>> CreateAsync(CreateAnnouncementRequest request, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing announcement.
    /// </summary>
    Task<Result<AnnouncementDto>> UpdateAsync(Guid id, UpdateAnnouncementRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an announcement.
    /// </summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
