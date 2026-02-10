using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

public class AnnouncementService(
    IAnnouncementRepository announcementRepository) : IAnnouncementService
{
    private readonly IAnnouncementRepository _announcementRepository = announcementRepository;

    public async Task<IReadOnlyList<AnnouncementDto>> GetActiveAnnouncementsAsync(CancellationToken cancellationToken = default)
    {
        var announcements = await _announcementRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        return announcements.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AnnouncementDto>> GetAllAnnouncementsAsync(CancellationToken cancellationToken = default)
    {
        var announcements = await _announcementRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return announcements.Select(MapToDto).ToList();
    }

    public async Task<Result<AnnouncementDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var announcement = await _announcementRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (announcement is null)
        {
            return Result.Failure<AnnouncementDto>(Error.NotFound("Announcement", id.ToString()));
        }

        return MapToDto(announcement);
    }

    public async Task<Result<AnnouncementDto>> CreateAsync(CreateAnnouncementRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AnnouncementType>(request.Type, ignoreCase: true, out var announcementType))
        {
            return Result.Failure<AnnouncementDto>(Error.InvalidInput($"Invalid announcement type: {request.Type}"));
        }

        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            Type = announcementType,
            IsActive = true,
            IsPinned = request.IsPinned,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            CreatedByUserId = userId,
            DisplayOrder = request.DisplayOrder
        };

        await _announcementRepository.AddAsync(announcement, cancellationToken).ConfigureAwait(false);

        // Re-fetch to include navigation properties
        var created = await _announcementRepository.GetByIdAsync(announcement.Id, cancellationToken).ConfigureAwait(false);
        return MapToDto(created!);
    }

    public async Task<Result<AnnouncementDto>> UpdateAsync(Guid id, UpdateAnnouncementRequest request, CancellationToken cancellationToken = default)
    {
        var announcement = await _announcementRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (announcement is null)
        {
            return Result.Failure<AnnouncementDto>(Error.NotFound("Announcement", id.ToString()));
        }

        if (!Enum.TryParse<AnnouncementType>(request.Type, ignoreCase: true, out var announcementType))
        {
            return Result.Failure<AnnouncementDto>(Error.InvalidInput($"Invalid announcement type: {request.Type}"));
        }

        announcement.Title = request.Title;
        announcement.Content = request.Content;
        announcement.Type = announcementType;
        announcement.IsActive = request.IsActive;
        announcement.IsPinned = request.IsPinned;
        announcement.ExpiresAt = request.ExpiresAt;
        announcement.DisplayOrder = request.DisplayOrder;

        await _announcementRepository.UpdateAsync(announcement, cancellationToken).ConfigureAwait(false);

        return MapToDto(announcement);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var announcement = await _announcementRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (announcement is null)
        {
            return Result.Failure(Error.NotFound("Announcement", id.ToString()));
        }

        await _announcementRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    private static AnnouncementDto MapToDto(Announcement announcement)
    {
        return new AnnouncementDto(
            Id: announcement.Id,
            Title: announcement.Title,
            Content: announcement.Content,
            Type: announcement.Type.ToString(),
            IsPinned: announcement.IsPinned,
            CreatedAt: announcement.CreatedAt,
            ExpiresAt: announcement.ExpiresAt,
            CreatedByUsername: announcement.CreatedByUser?.Username ?? "Unknown"
        );
    }
}
