namespace NebulaPanel.Domain.Exceptions;

/// <summary>
/// Thrown when a concurrency conflict is detected during a persistence operation
/// (e.g., optimistic concurrency check failure).
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException()
        : base("A concurrency conflict occurred. The entity was modified by another request.")
    {
    }

    public ConcurrencyException(string message)
        : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
