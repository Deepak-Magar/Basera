using System.Text.Json;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Localization;
using Basera.Shared.Messaging;
using Basera.Shared.Results;
using Basera.Shared.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Basera.UnitTests.Shared.Web;

public sealed class WebBuildingBlockTests
{
    [Theory]
    [InlineData(ErrorCode.Validation, 400)]
    [InlineData(ErrorCode.Forbidden, 403)]
    [InlineData(ErrorCode.NotFound, 404)]
    [InlineData(ErrorCode.Conflict, 409)]
    [InlineData(ErrorCode.RuleViolation, 422)]
    [InlineData(ErrorCode.Internal, 500)]
    public void Every_error_code_has_one_http_status(ErrorCode code, int status) => code.ToHttpStatus().Should().Be(status);

    [Fact]
    public async Task Failed_result_becomes_an_envelope_with_code_message_and_target()
    {
        Result<int> result = DomainError.Validation("Enter a Nepal mobile number.", "phone");
        var http = new DefaultHttpContext
        {
            TraceIdentifier = "corr-1234",
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        http.Response.Body = new MemoryStream();

        await result.ToApiResult(http).ExecuteAsync(http);

        http.Response.StatusCode.Should().Be(400);
        http.Response.Body.Position = 0;
        var json = JsonDocument.Parse(http.Response.Body).RootElement;
        json.GetProperty("success").GetBoolean().Should().BeFalse();
        json.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("error").GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        json.GetProperty("error").GetProperty("target").GetString().Should().Be("phone");
        json.GetProperty("correlationId").GetString().Should().Be("corr-1234");
    }

    [Theory]
    [InlineData("/api", true)]
    [InlineData("/api/billing/invoices", true)]
    [InlineData("/API/x", true)]
    [InlineData("/apiary", false)]
    [InlineData("/owner", false)]
    public void Api_requests_are_recognised_by_path_segment(string path, bool isApi) =>
        new DefaultHttpContext { Request = { Path = path } }.Request.IsApiRequest().Should().Be(isApi);

    [Fact]
    public void Htmx_requests_and_boosts_are_told_apart()
    {
        var http = new DefaultHttpContext();
        http.Request.IsHtmx().Should().BeFalse();

        http.Request.Headers[HtmxHeaders.Request] = "true";
        http.Request.IsHtmx().Should().BeTrue();
        http.Request.IsHtmxBoosted().Should().BeFalse();

        http.Request.Headers[HtmxHeaders.Boosted] = "true";
        http.Request.IsHtmxBoosted().Should().BeTrue();
    }

    [Fact]
    public void Hx_trigger_is_json_with_the_event_name()
    {
        var http = new DefaultHttpContext();

        http.Response.HxTrigger("invoice:paid", new { invoiceId = 7 });

        http.Response.Headers[HtmxHeaders.Trigger].ToString().Should().Be("{\"invoice:paid\":{\"invoiceId\":7}}");
    }

    [Theory]
    [InlineData("/owner/residents", "en")]
    [InlineData("/staff", "ne")]
    [InlineData("/Resident/bill", "ne")]
    [InlineData("/parent", "ne")]
    [InlineData("/", "en")]
    [InlineData("/staffing", "en")]
    public void Area_default_language(string path, string culture) => BaseraCultures.DefaultFor(path).Should().Be(culture);

    [Theory]
    [InlineData("Your bill is due.", 1)]
    [InlineData("", 1)]
    public void Short_ascii_sms_is_one_part(string text, int parts) => SmsSegments.Count(text).Should().Be(parts);

    [Fact]
    public void Sms_parts_follow_gsm7_and_unicode_limits()
    {
        SmsSegments.Count(new string('a', 160)).Should().Be(1);
        SmsSegments.Count(new string('a', 161)).Should().Be(2);
        SmsSegments.Count(new string('a', 306)).Should().Be(2);
        SmsSegments.Count(new string('a', 307)).Should().Be(3);
        SmsSegments.Count(new string('€', 80)).Should().Be(1, "extended characters count twice: 160 septets");
        SmsSegments.Count(new string('€', 81)).Should().Be(2);
        SmsSegments.Count(new string('क', 70)).Should().Be(1);
        SmsSegments.Count(new string('क', 71)).Should().Be(2);
        SmsSegments.Count(new string('क', 135)).Should().Be(3);
    }

    [Theory]
    [InlineData("2083-06-27")]
    [InlineData("२०८३-०६-२७")]
    public async Task Model_binder_reads_ascii_and_nepali_digits(string input)
    {
        var context = BindingContext(input);

        await new NepaliDateModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeTrue();
        context.Result.Model.Should().Be(new NepaliDate(2083, 6, 27));
    }

    [Theory]
    [InlineData("2083-13-01")]
    [InlineData("2083-06-32")]
    [InlineData("27/06/2083")]
    public async Task Model_binder_reports_an_invalid_date_on_the_field(string input)
    {
        var context = BindingContext(input);

        await new NepaliDateModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.ModelState["dueDate"]!.Errors.Should().ContainSingle();
    }

    [Fact]
    public async Task Model_binder_leaves_an_empty_value_unset()
    {
        var context = BindingContext("");

        await new NepaliDateModelBinder().BindModelAsync(context);

        context.Result.IsModelSet.Should().BeFalse();
        context.ModelState.ErrorCount.Should().Be(0);
    }

    private static DefaultModelBindingContext BindingContext(string value) => new()
    {
        ModelName = "dueDate",
        ModelState = new ModelStateDictionary(),
        ValueProvider = new SingleValueProvider("dueDate", value),
        ActionContext = new Microsoft.AspNetCore.Mvc.ActionContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = new EmptyServices() },
        },
    };

    private sealed class SingleValueProvider(string key, string value) : IValueProvider
    {
        public bool ContainsPrefix(string prefix) => prefix == key;

        public ValueProviderResult GetValue(string name) =>
            name == key ? new ValueProviderResult(new StringValues(value)) : ValueProviderResult.None;
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
