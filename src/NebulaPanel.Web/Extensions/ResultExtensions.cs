namespace NebulaPanel.Web.Extensions;

using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.Common;

/// <summary>
/// Extension methods for converting Result types to ActionResult.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Result to an appropriate IActionResult based on success/failure and error code.
    /// </summary>
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new OkResult();

        return result.ErrorInfo?.ToActionResult()
            ?? new BadRequestObjectResult(new ProblemDetails { Detail = result.Error });
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to an appropriate IActionResult.
    /// </summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess?.Invoke(result.Value!) ?? new OkObjectResult(result.Value);

        return result.ErrorInfo?.ToActionResult()
            ?? new BadRequestObjectResult(new ProblemDetails { Detail = result.Error });
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to an appropriate IActionResult with a 201 Created response on success.
    /// </summary>
    public static IActionResult ToCreatedResult<T>(this Result<T> result, string? location = null)
    {
        if (result.IsSuccess)
        {
            if (location is not null)
                return new CreatedResult(location, result.Value);
            return new ObjectResult(result.Value) { StatusCode = StatusCodes.Status201Created };
        }

        return result.ErrorInfo?.ToActionResult()
            ?? new BadRequestObjectResult(new ProblemDetails { Detail = result.Error });
    }

    /// <summary>
    /// Converts a Result to a 204 No Content response on success.
    /// </summary>
    public static IActionResult ToNoContentResult(this Result result)
    {
        if (result.IsSuccess)
            return new NoContentResult();

        return result.ErrorInfo?.ToActionResult()
            ?? new BadRequestObjectResult(new ProblemDetails { Detail = result.Error });
    }

    /// <summary>
    /// Converts an Error to an appropriate IActionResult based on its ErrorCode.
    /// </summary>
    private static IActionResult ToActionResult(this Error error)
    {
        var problemDetails = new ProblemDetails
        {
            Title = GetTitleForErrorCode(error.Code),
            Detail = error.Message,
            Status = GetStatusCodeForErrorCode(error.Code),
            Extensions =
            {
                ["errorCode"] = error.Code.ToString()
            }
        };

        if (error.Details is not null)
        {
            problemDetails.Extensions["details"] = error.Details;
        }

        return error.Code switch
        {
            ErrorCode.NotFound => new NotFoundObjectResult(problemDetails),
            ErrorCode.Unauthorized or ErrorCode.InvalidCredentials or ErrorCode.TokenExpired
                => new UnauthorizedObjectResult(problemDetails),
            ErrorCode.Forbidden => new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status403Forbidden },
            ErrorCode.AlreadyExists or ErrorCode.Conflict or ErrorCode.ServerRunning or ErrorCode.PortInUse
                => new ConflictObjectResult(problemDetails),
            _ => new BadRequestObjectResult(problemDetails)
        };
    }

    private static int GetStatusCodeForErrorCode(ErrorCode code) => code switch
    {
        ErrorCode.NotFound => StatusCodes.Status404NotFound,
        ErrorCode.Unauthorized or ErrorCode.InvalidCredentials or ErrorCode.TokenExpired
            => StatusCodes.Status401Unauthorized,
        ErrorCode.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCode.AlreadyExists or ErrorCode.Conflict or ErrorCode.ServerRunning or ErrorCode.PortInUse
            => StatusCodes.Status409Conflict,
        ErrorCode.ExternalServiceError or ErrorCode.SteamCmdError or ErrorCode.DockerError or ErrorCode.RconError
            => StatusCodes.Status502BadGateway,
        _ => StatusCodes.Status400BadRequest
    };

    private static string GetTitleForErrorCode(ErrorCode code) => code switch
    {
        ErrorCode.NotFound => "Resource Not Found",
        ErrorCode.AlreadyExists => "Resource Already Exists",
        ErrorCode.Conflict => "Conflict",
        ErrorCode.ValidationError or ErrorCode.InvalidInput => "Validation Error",
        ErrorCode.InvalidOperation => "Invalid Operation",
        ErrorCode.Unauthorized => "Unauthorized",
        ErrorCode.Forbidden => "Forbidden",
        ErrorCode.InvalidCredentials => "Invalid Credentials",
        ErrorCode.TokenExpired => "Token Expired",
        ErrorCode.ServerRunning => "Server Running",
        ErrorCode.ServerStopped => "Server Stopped",
        ErrorCode.ServerNotInstalled => "Server Not Installed",
        ErrorCode.InstallationFailed => "Installation Failed",
        ErrorCode.PortInUse => "Port In Use",
        ErrorCode.ExternalServiceError => "External Service Error",
        ErrorCode.SteamCmdError => "SteamCMD Error",
        ErrorCode.DockerError => "Docker Error",
        ErrorCode.RconError => "RCON Error",
        _ => "Error"
    };
}
