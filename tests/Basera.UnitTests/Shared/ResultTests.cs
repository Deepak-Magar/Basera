using Basera.Shared.Results;

namespace Basera.UnitTests.Shared;

public sealed class ResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Domain_error_converts_to_a_failed_result()
    {
        Result result = DomainError.NotFound("Resident not found.");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public void Value_converts_to_a_successful_result()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_is_a_bug()
    {
        Result<int> result = DomainError.Conflict("Bed already taken.");

        var act = () => result.Value;

        act.Should().Throw<InvalidOperationException>().WithMessage("*CONFLICT*");
    }

    [Fact]
    public void Map_and_bind_pass_failures_through()
    {
        Result<int> ok = 2;
        Result<int> failed = DomainError.Validation("Bad.", "amount");

        ok.Map(x => x * 10).Value.Should().Be(20);
        ok.Bind(x => x > 1 ? Result.Success(x.ToString()) : DomainError.RuleViolation("Too small.")).Value.Should().Be("2");
        failed.Map(x => x * 10).Error.Should().Be(failed.Error);
    }

    [Fact]
    public void Match_picks_a_branch()
    {
        Result<int> failed = DomainError.Forbidden("No.");

        failed.Match(v => "ok", e => e.Code.ToWireCode()).Should().Be("FORBIDDEN");
    }

    [Theory]
    [InlineData(ErrorCode.Validation, "VALIDATION_ERROR")]
    [InlineData(ErrorCode.NotFound, "NOT_FOUND")]
    [InlineData(ErrorCode.Conflict, "CONFLICT")]
    [InlineData(ErrorCode.Forbidden, "FORBIDDEN")]
    [InlineData(ErrorCode.RuleViolation, "RULE_VIOLATION")]
    [InlineData(ErrorCode.Internal, "INTERNAL")]
    public void Error_codes_have_stable_wire_names(ErrorCode code, string wire)
    {
        code.ToWireCode().Should().Be(wire);
    }
}
