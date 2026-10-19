using System.Globalization;
using System.Text.Encodings.Web;
using Basera.Shared.Domain;
using Basera.Shared.Localization;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Basera.Shared.Web.TagHelpers;

/// <summary>
/// <c>&lt;money value="Model.Balance" /&gt;</c> renders <c>&lt;span class="money"&gt;रु 1,25,000.00&lt;/span&gt;</c>,
/// with Nepali digits in Nepali unless <c>nepali-digits="false"</c>. Negative amounts also get <c>text-danger</c>;
/// the minus sign is in the text, so colour is never the only signal.
/// </summary>
[HtmlTargetElement("money", TagStructure = TagStructure.WithoutEndTag)]
public sealed class MoneyTagHelper : TagHelper
{
    public Money? Value { get; set; }

    /// <summary>Null follows the page language.</summary>
    public bool? NepaliDigits { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Value is not { } money)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClass("money", HtmlEncoder.Default);
        if (money.IsNegative)
        {
            output.AddClass("text-danger", HtmlEncoder.Default);
        }

        output.Content.Append(money.Format(NepaliDigits ?? CultureInfo.CurrentUICulture.IsNepali()));
    }
}
