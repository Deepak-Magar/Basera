using Basera.Modules.Identity.Domain;
using Basera.Shared.Domain;

namespace Basera.UnitTests.Identity;

public sealed class OtpChallengeTests
{
    private static readonly PhoneNumber Phone = PhoneNumber.Create("9801234567").Value;
    private static readonly DateTimeOffset Sent = new(2026, 10, 25, 8, 0, 0, TimeSpan.Zero);

    private static OtpChallenge Issue() => OtpChallenge.Issue(Phone, OtpPurpose.PasswordReset, "HASH", Sent);

    [Fact]
    public void A_code_is_valid_for_five_minutes()
    {
        var challenge = Issue();

        challenge.ExpiresAt.Should().Be(Sent.AddMinutes(5));
        challenge.Phone.Should().Be("+9779801234567");
        Issue().Check(codeMatches: true, Sent.AddMinutes(5).AddTicks(-1)).Should().Be(OtpCheck.Verified);
        Issue().Check(codeMatches: true, Sent.AddMinutes(5)).Should().Be(OtpCheck.Expired);
    }

    [Fact]
    public void A_code_works_once()
    {
        var challenge = Issue();

        challenge.Check(codeMatches: true, Sent.AddMinutes(1)).Should().Be(OtpCheck.Verified);
        challenge.Check(codeMatches: true, Sent.AddMinutes(2)).Should().Be(OtpCheck.AlreadyUsed);
        challenge.ConsumedAt.Should().Be(Sent.AddMinutes(1));
    }

    [Fact]
    public void The_fifth_wrong_code_ends_the_challenge_and_the_right_one_is_refused_after_it()
    {
        var challenge = Issue();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            challenge.Check(codeMatches: false, Sent).Should().Be(OtpCheck.WrongCode);
        }

        challenge.Check(codeMatches: false, Sent).Should().Be(OtpCheck.TooManyAttempts);
        challenge.Check(codeMatches: true, Sent).Should().Be(OtpCheck.TooManyAttempts);
        challenge.Attempts.Should().Be(5, "refused attempts after the limit are not counted");
    }

    [Fact]
    public void Expired_and_spent_codes_do_not_count_attempts()
    {
        var challenge = Issue();

        challenge.Check(codeMatches: false, Sent.AddMinutes(6)).Should().Be(OtpCheck.Expired);

        challenge.Attempts.Should().Be(0);
    }

    [Fact]
    public void A_superseded_code_no_longer_works()
    {
        var challenge = Issue();

        challenge.Supersede(Sent.AddMinutes(1));

        challenge.Check(codeMatches: true, Sent.AddMinutes(2)).Should().Be(OtpCheck.AlreadyUsed);
    }
}
