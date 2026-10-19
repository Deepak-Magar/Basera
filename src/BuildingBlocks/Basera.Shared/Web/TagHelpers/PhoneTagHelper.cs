using System.Text.Encodings.Web;
using Basera.Shared.Domain;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Basera.Shared.Web.TagHelpers;

/// <summary>
/// <c>&lt;phone value="Model.Phone" /&gt;</c> renders a tap-to-call link, which is how wardens and parents use a
/// number on a phone: <c>&lt;a href="tel:+9779812345678" class="phone"&gt;981-2345678&lt;/a&gt;</c>.
/// </summary>
[HtmlTargetElement("phone", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PhoneTagHelper : TagHelper
{
    public PhoneNumber? Value { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Value is null)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "a";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("href", $"tel:{Value.E164}");
        output.AddClass("phone", HtmlEncoder.Default);
        output.Content.Append(Value.ToDisplayString());
    }
}
