namespace NebulaPanel.Application.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ReturnsIsSuccessTrue()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
        result.ErrorInfo.Should().BeNull();
    }

    [Fact]
    public void Failure_WithString_ReturnsIsFailureTrue()
    {
        var result = Result.Failure("something went wrong");

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("something went wrong");
    }

    [Fact]
    public void Failure_WithString_WrapsInErrorWithUnknownCode()
    {
        var result = Result.Failure("oops");

        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be(ErrorCode.Unknown);
        result.ErrorInfo.Message.Should().Be("oops");
    }

    [Fact]
    public void Failure_WithError_PreservesErrorCodeAndMessage()
    {
        var error = Error.NotFound("Server", "123");

        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be(ErrorCode.NotFound);
        result.Error.Should().Contain("Server");
    }

    [Fact]
    public void SuccessT_StoresValueAndIsSuccess()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
        result.ErrorInfo.Should().BeNull();
    }

    [Fact]
    public void SuccessT_WithReferenceType_StoresValue()
    {
        var result = Result.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void FailureT_WithString_HasDefaultValueAndIsFailure()
    {
        var result = Result.Failure<int>("bad input");

        result.IsFailure.Should().BeTrue();
        result.Value.Should().Be(default(int));
        result.Error.Should().Be("bad input");
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be(ErrorCode.Unknown);
    }

    [Fact]
    public void FailureT_WithReferenceType_HasNullValue()
    {
        var result = Result.Failure<string>("not found");

        result.IsFailure.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void FailureT_WithError_PreservesTypedError()
    {
        var error = Error.Validation("Name is required");

        var result = Result.Failure<string>(error);

        result.IsFailure.Should().BeTrue();
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be(ErrorCode.ValidationError);
        result.ErrorInfo.Message.Should().Be("Name is required");
        result.Error.Should().Be("Name is required");
    }

    [Fact]
    public void ImplicitConversion_FromValueToResultT_YieldsSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversion_FromStringToResultT_YieldsSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void SuccessT_WithNull_IsStillSuccess()
    {
        var result = Result.Success<string?>(null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
