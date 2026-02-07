namespace NebulaPanel.Application.Services;

public interface IApiKeyValidator
{
    Task<(bool Success, string? Message)> ValidateAsync(
        string provider, string apiKey, CancellationToken cancellationToken = default);
}
