using Cronos;

namespace NebulaPanel.Application.Common;

public static class CronValidator
{
    /// <summary>
    /// Validates a cron expression and returns any error message.
    /// </summary>
    public static Result ValidateCronExpression(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return Result.Failure(Error.Validation("Cron expression is required for recurring tasks."));
        }

        try
        {
            // Cronos supports standard 5-field cron expressions
            _ = CronExpression.Parse(cronExpression, CronFormat.Standard);
            return Result.Success();
        }
        catch (CronFormatException ex)
        {
            return Result.Failure(Error.Validation($"Invalid cron expression: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets the next occurrence for a cron expression.
    /// </summary>
    public static DateTime? GetNextOccurrence(string cronExpression, DateTime from)
    {
        try
        {
            var expression = CronExpression.Parse(cronExpression, CronFormat.Standard);
            return expression.GetNextOccurrence(from, TimeZoneInfo.Utc);
        }
        catch (CronFormatException)
        {
            // Invalid cron expressions return null - callers handle this case
            return null;
        }
    }

    /// <summary>
    /// Gets a human-readable description of a cron expression.
    /// </summary>
    public static string GetDescription(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return "No schedule";

        // Basic descriptions for common patterns
        return cronExpression switch
        {
            "* * * * *" => "Every minute",
            "0 * * * *" => "Every hour",
            "0 0 * * *" => "Daily at midnight",
            "0 0 * * 0" => "Weekly on Sunday",
            "0 0 1 * *" => "Monthly on the 1st",
            "0 4 * * *" => "Daily at 4:00 AM",
            _ => cronExpression
        };
    }
}
