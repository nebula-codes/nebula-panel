using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

public interface IFikaHeadlessService
{
    Task<IReadOnlyList<FikaParentServerDto>> GetAvailableParentServersAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FikaHeadlessProfileDto>>> GetAvailableProfilesAsync(
        Guid parentServerId,
        CancellationToken cancellationToken = default);
}
