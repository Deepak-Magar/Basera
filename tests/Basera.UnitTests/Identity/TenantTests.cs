using Basera.Modules.Identity.Domain;
using Basera.Shared.Authorization;
using Basera.Shared.Domain;
using Basera.Shared.Results;

namespace Basera.UnitTests.Identity;

public sealed class TenantTests
{
    private static readonly PhoneNumber Phone = PhoneNumber.Create("9841234567").Value;
    private static readonly DateOnly Today = new(2026, 10, 25);

    [Fact]
    public void Registering_starts_a_60_day_starter_trial()
    {
        var tenant = Tenant.Register("  Sunrise Boys Hostel, Kirtipur ", Phone, Today).Value;

        tenant.Name.Should().Be("Sunrise Boys Hostel, Kirtipur");
        tenant.Plan.Should().Be(TenantPlan.Starter);
        tenant.Status.Should().Be(TenantStatus.Trial);
        tenant.TrialEndsOn.Should().Be(new DateOnly(2026, 12, 24));
        tenant.Phone.Should().Be(Phone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_hostel_name_is_required(string? name)
    {
        var result = Tenant.Register(name, Phone, Today);

        result.Error!.Code.Should().Be(ErrorCode.Validation);
        result.Error.Target.Should().Be("hostelName");
    }

    [Fact]
    public void A_hostel_name_longer_than_200_characters_is_refused()
    {
        Tenant.Register(new string('x', 201), Phone, Today).IsFailure.Should().BeTrue();
        Tenant.Register(new string('x', 200), Phone, Today).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678a")]
    [InlineData("१२३४५६७८९")]
    public void A_pan_must_be_nine_ascii_digits(string pan)
    {
        var tenant = Tenant.Register("Hostel", Phone, Today).Value;

        var result = tenant.UpdateLegalDetails("Hostel Pvt. Ltd.", pan, null);

        result.Error!.Target.Should().Be("pan");
        tenant.Pan.Should().BeNull();
    }

    [Fact]
    public void Legal_details_are_trimmed_and_blank_means_none()
    {
        var tenant = Tenant.Register("Hostel", Phone, Today).Value;

        tenant.UpdateLegalDetails(" Hostel Pvt. Ltd. ", " 601234567 ", "  ").IsSuccess.Should().BeTrue();

        tenant.LegalName.Should().Be("Hostel Pvt. Ltd.");
        tenant.Pan.Should().Be("601234567");
        tenant.Address.Should().BeNull();
    }

    [Fact]
    public void Every_role_is_a_claim_value_and_every_claim_value_is_a_role()
    {
        Enum.GetNames<Role>().Should().Equal(BaseraRoles.All);
    }
}
