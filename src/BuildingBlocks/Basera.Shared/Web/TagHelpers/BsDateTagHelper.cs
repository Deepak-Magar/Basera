using System.Globalization;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Localization;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Basera.Shared.Web.TagHelpers;

/// <summary>
/// <c>&lt;bs-date value="Model.DueDate" /&gt;</c> renders a stored AD date the way Nepal reads it (ARCHITECTURE
/// §9): BS first, AD second. <c>&lt;time datetime="2026-10-13"&gt;27 Asoj 2083 &lt;small&gt;13 Oct 2026&lt;/small&gt;&lt;/time&gt;</c>.
/// In Nepali the BS part uses Nepali month names and digits. <c>show-ad="false"</c> hides the AD part.
/// </summary>
[HtmlTargetElement("bs-date", TagStructure = TagStructure.WithoutEndTag)]
public sealed class BsDateTagHelper : TagHelper
{
    public DateOnly? Value { get; set; }

    public bool ShowAd { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Value is not { } date)
        {
            output.SuppressOutput();
            return;
        }

        var bs = NepaliDate.FromDateOnly(date).ToLongDateString(nepali: CultureInfo.CurrentUICulture.IsNepali());
        output.TagName = "time";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("datetime", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        output.AddClass("bs-date", System.Text.Encodings.Web.HtmlEncoder.Default);
        output.Content.Append(bs);
        if (ShowAd)
        {
            output.Content.AppendHtml(" <small class=\"text-body-secondary\">");
            output.Content.Append(date.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
            output.Content.AppendHtml("</small>");
        }
    }
}
