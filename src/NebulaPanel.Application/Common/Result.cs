namespace NebulaPanel.Application.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The error message when the result is a failure.
    /// For backward compatibility, this returns the message string.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// The typed error information when the result is a failure.
    /// Use this for new code to get error codes and detailed error info.
    /// </summary>
    public Error? ErrorInfo { get; }

    protected Result(bool isSuccess, string? error, Error? errorInfo = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorInfo = errorInfo;
    }

    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failure result from a string message (backward compatible).
    /// </summary>
    public static Result Failure(string error) => new(false, error, new Error(ErrorCode.Unknown, error));

    /// <summary>
    /// Creates a failure result with a typed error.
    /// </summary>
    public static Result Failure(Error error) => new(false, error.Message, error);

    /// <summary>
    /// Creates a success result with a value.
    /// </summary>
    public static Result<T> Success<T>(T value) => new(value, true, null, null);

    /// <summary>
    /// Creates a failure result from a string message (backward compatible).
    /// </summary>
    public static Result<T> Failure<T>(string error) => new(default, false, error, new Error(ErrorCode.Unknown, error));

    /// <summary>
    /// Creates a failure result with a typed error.
    /// </summary>
    public static Result<T> Failure<T>(Error error) => new(default, false, error.Message, error);
}

/// <summary>
/// Represents the result of an operation that returns a value.
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    internal Result(T? value, bool isSuccess, string? error, Error? errorInfo)
        : base(isSuccess, error, errorInfo)
    {
        Value = value;
    }

    public static implicit operator Result<T>(T value) => Success(value);
}
