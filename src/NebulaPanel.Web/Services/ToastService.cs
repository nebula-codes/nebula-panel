namespace NebulaPanel.Web.Services;

/// <summary>
/// Service for displaying toast notifications.
/// </summary>
public interface IToastService
{
    /// <summary>
    /// Event triggered when a toast should be displayed.
    /// </summary>
    event Action<ToastMessage>? OnShow;

    /// <summary>
    /// Event triggered when a toast should be dismissed.
    /// </summary>
    event Action<Guid>? OnDismiss;

    /// <summary>
    /// Shows a success toast notification.
    /// </summary>
    void ShowSuccess(string message, string? title = null, int durationMs = 5000);

    /// <summary>
    /// Shows an error toast notification.
    /// </summary>
    void ShowError(string message, string? title = null, int durationMs = 8000);

    /// <summary>
    /// Shows a warning toast notification.
    /// </summary>
    void ShowWarning(string message, string? title = null, int durationMs = 6000);

    /// <summary>
    /// Shows an info toast notification.
    /// </summary>
    void ShowInfo(string message, string? title = null, int durationMs = 5000);

    /// <summary>
    /// Dismisses a toast by its ID.
    /// </summary>
    void Dismiss(Guid id);
}

/// <summary>
/// Represents a toast notification message.
/// </summary>
public record ToastMessage(
    Guid Id,
    ToastType Type,
    string Message,
    string? Title,
    int DurationMs,
    DateTime CreatedAt)
{
    /// <summary>
    /// Number of grouped notifications this toast represents.
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// Key used for grouping similar toasts. Toasts with the same group key are merged.
    /// </summary>
    public string GroupKey => $"{Type}:{Title ?? ""}";
}

/// <summary>
/// Types of toast notifications.
/// </summary>
public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

/// <summary>
/// Implementation of the toast notification service.
/// </summary>
public class ToastService : IToastService
{
    public event Action<ToastMessage>? OnShow;
    public event Action<Guid>? OnDismiss;

    public void ShowSuccess(string message, string? title = null, int durationMs = 5000)
        => Show(ToastType.Success, message, title, durationMs);

    public void ShowError(string message, string? title = null, int durationMs = 8000)
        => Show(ToastType.Error, message, title, durationMs);

    public void ShowWarning(string message, string? title = null, int durationMs = 6000)
        => Show(ToastType.Warning, message, title, durationMs);

    public void ShowInfo(string message, string? title = null, int durationMs = 5000)
        => Show(ToastType.Info, message, title, durationMs);

    public void Dismiss(Guid id)
        => OnDismiss?.Invoke(id);

    private void Show(ToastType type, string message, string? title, int durationMs)
    {
        var toast = new ToastMessage(
            Guid.NewGuid(),
            type,
            message,
            title,
            durationMs,
            DateTime.UtcNow);

        OnShow?.Invoke(toast);
    }
}
