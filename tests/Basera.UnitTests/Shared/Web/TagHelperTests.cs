using System.Globalization;
using Basera.Shared.Domain;
using Basera.Shared.Localization;
using Basera.Shared.Web.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace Basera.UnitTests.Shared.Web;

public sealed class TagHelperTests
{
    [Fact]
    public void Bs_date_shows_bs_first_and_ad_second()
    {
        var html = Render(new BsDateTagHelper { Value = new DateOnly(2026, 10, 13) }, "en");

        html.Should().Be("<time datetime=\"2026-10-13\" class=\"bs-date\">27 Asoj 2083 <small class=\"text-body-secondary\">13 Oct 2026</small></time>");
    }

    [Fact]
    public void Bs_date_in_nepali_uses_nepali_month_and_digits()
    {
        var html = Render(new BsDateTagHelper { Value = new DateOnly(2026, 10, 13), ShowAd = false }, "ne");

        html.Should().Be("<time datetime=\"2026-10-13\" class=\"bs-date\">२७ असोज २०८३</time>");
    }

    [Fact]
    public void Money_follows_the_page_language_and_marks_negatives()
    {
        Render(new MoneyTagHelper { Value = Money.FromRupees(125_000m) }, "en")
            .Should().Be("<span class=\"money\">रु 1,25,000.00</span>");
        Render(new MoneyTagHelper { Value = Money.FromRupees(-500m) }, "ne")
            .Should().Be("<span class=\"money text-danger\">-रु ५००.००</span>");
        Render(new MoneyTagHelper { Value = Money.FromRupees(500m), NepaliDigits = false }, "ne")
            .Should().Be("<span class=\"money\">रु 500.00</span>");
    }

    [Fact]
    public void Phone_is_a_tap_to_call_link()
    {
        Render(new PhoneTagHelper { Value = PhoneNumber.Create("9812345678").Value }, "en")
            .Should().Be("<a href=\"tel:&#x2B;9779812345678\" class=\"phone\">981-2345678</a>", "'+' is entity-encoded in attributes; browsers read it as '+'");
    }

    [Fact]
    public void Status_badge_has_colour_and_localized_text()
    {
        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call => new LocalizedString(call.Arg<string>(), call.Arg<string>(), resourceNotFound: true));
        localizer["Shared.Status.Overdue"].Returns(new LocalizedString("Shared.Status.Overdue", "म्याद नाघेको"));

        Render(new StatusBadgeTagHelper(localizer) { Status = "Overdue" }, "ne")
            .Should().Be("<span class=\"badge text-bg-danger\" data-status=\"Overdue\">म्याद नाघेको</span>");
        Render(new StatusBadgeTagHelper(localizer) { Status = DayOfWeek.Monday }, "en")
            .Should().Contain("text-bg-secondary");
        Render(new StatusBadgeTagHelper(localizer) { Status = "OnHold" }, "en")
            .Should().Contain(">OnHold</span>", "an untranslated status still shows its name");
    }

    [Fact]
    public void Missing_values_render_nothing()
    {
        Render(new BsDateTagHelper(), "en").Should().BeEmpty();
        Render(new MoneyTagHelper(), "en").Should().BeEmpty();
        Render(new PhoneTagHelper(), "en").Should().BeEmpty();
    }

    private static string Render(TagHelper helper, string culture)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            var context = new TagHelperContext([], new Dictionary<object, object>(), "id");
            var output = new TagHelperOutput("x", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
            helper.Process(context, output);
            using var writer = new StringWriter();
            output.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Create(System.Text.Unicode.UnicodeRanges.All));
            return writer.ToString();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
