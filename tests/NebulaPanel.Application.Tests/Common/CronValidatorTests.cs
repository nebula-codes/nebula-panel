namespace NebulaPanel.Application.Tests.Common;

public class CronValidatorTests
{
    [Theory]
    [InlineData("* * * * *")]
    [InlineData("0 4 * * *")]
    [InlineData("0 0 * * 0")]
    [InlineData("0 0 1 * *")]
    [InlineData("*/5 * * * *")]
    public void ValidateCronExpression_WithValidExpression_ReturnsSuccess(string cron)
    {
        var result = CronValidator.ValidateCronExpression(cron);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateCronExpression_WithNullOrWhitespace_ReturnsFailure(string? cron)
    {
        var result = CronValidator.ValidateCronExpression(cron);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("required");
    }

    [Theory]
    [InlineData("not a cron")]
    [InlineData("60 * * * *")]
    [InlineData("* * * *")]
    public void ValidateCronExpression_WithInvalidExpression_ReturnsFailure(string cron)
    {
        var result = CronValidator.ValidateCronExpression(cron);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid cron");
    }

    [Fact]
    public void GetNextOccurrence_WithValidExpression_ReturnsFutureDate()
    {
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var next = CronValidator.GetNextOccurrence("0 4 * * *", from);

        next.Should().NotBeNull();
        next!.Value.Should().BeAfter(from);
    }

    [Fact]
    public void GetNextOccurrence_WithEveryMinute_ReturnsNextMinute()
    {
        var from = new DateTime(2025, 1, 1, 12, 30, 0, DateTimeKind.Utc);

        var next = CronValidator.GetNextOccurrence("* * * * *", from);

        next.Should().NotBeNull();
        next!.Value.Should().Be(new DateTime(2025, 1, 1, 12, 31, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetNextOccurrence_WithInvalidExpression_ReturnsNull()
    {
        var from = DateTime.UtcNow;

        var next = CronValidator.GetNextOccurrence("invalid", from);

        next.Should().BeNull();
    }

    [Theory]
    [InlineData("* * * * *", "Every minute")]
    [InlineData("0 * * * *", "Every hour")]
    [InlineData("0 0 * * *", "Daily at midnight")]
    [InlineData("0 0 * * 0", "Weekly on Sunday")]
    [InlineData("0 0 1 * *", "Monthly on the 1st")]
    [InlineData("0 4 * * *", "Daily at 4:00 AM")]
    public void GetDescription_WithKnownPattern_ReturnsDescription(string cron, string expected)
    {
        var description = CronValidator.GetDescription(cron);

        description.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDescription_WithNullOrWhitespace_ReturnsNoSchedule(string? cron)
    {
        var description = CronValidator.GetDescription(cron);

        description.Should().Be("No schedule");
    }

    [Fact]
    public void GetDescription_WithUnknownPattern_ReturnsExpressionItself()
    {
        var description = CronValidator.GetDescription("*/15 * * * *");

        description.Should().Be("*/15 * * * *");
    }
}
