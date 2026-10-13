using Basera.Shared.Domain;
using Basera.Shared.Results;

namespace Basera.UnitTests.Shared;

public sealed class PhoneNumberTests
{
    [Theory]
    [InlineData("9841234567")]
    [InlineData("984-1234567")]
    [InlineData("984 123 4567")]
    [InlineData("+977 9841234567")]
    [InlineData("+977-984-1234567")]
    [InlineData("977 9841234567")]
    [InlineData("00977 9841234567")]
    [InlineData("(984) 1234567")]
    [InlineData("९८४१२३४५६७")]
    public void Accepts_common_ways_of_writing_a_nepal_mobile(string input)
    {
        var result = PhoneNumber.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.E164.Should().Be("+9779841234567");
    }

    [Fact]
    public void Accepts_97_prefix()
    {
        PhoneNumber.Create("9741234567").Value.E164.Should().Be("+9779741234567");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("984123456")]      // 9 digits
    [InlineData("98412345678")]    // 11 digits
    [InlineData("014412345")]      // landline
    [InlineData("9641234567")]     // not 97/98
    [InlineData("+91 9841234567")] // another country
    [InlineData("98412x4567")]
    public void Rejects_anything_else_with_a_validation_error(string? input)
    {
        var result = PhoneNumber.Create(input);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(ErrorCode.Validation);
        result.Error.Target.Should().Be("phone");
    }

    [Fact]
    public void Display_formats()
    {
        var phone = PhoneNumber.Create("9841234567").Value;

        phone.ToDisplayString().Should().Be("984-1234567");
        phone.ToInternationalString().Should().Be("+977 984-1234567");
        phone.ToString().Should().Be("+9779841234567");
    }

    [Fact]
    public void Equal_numbers_typed_differently_are_equal()
    {
        PhoneNumber.Create("984-1234567").Value.Should().Be(PhoneNumber.Create("+9779841234567").Value);
    }
}
