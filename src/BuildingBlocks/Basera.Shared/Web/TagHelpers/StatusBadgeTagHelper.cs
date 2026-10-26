using System.Text.Encodings.Web;
using Basera.Shared.Localization;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace Basera.Shared.Web.TagHelpers;

/// <summary>
/// <c>&lt;status-badge status="@invoice.Status" /&gt;</c> renders a Bootstrap badge whose colour follows the
/// status and whose text is the status in the page language (<c>Shared.Status.&lt;name&gt;</c>). The text is
/// always there, so colour is never the only signal (ARCHITECTURE §9). Unknown statuses are grey.
/// </summary>
[HtmlTargetElement("status-badge", TagStructure = TagStructure.WithoutEndTag)]
public sealed class StatusBadgeTagHelper(IStringLocalizer<SharedResource> localizer) : TagHelper
{
    private static readonly Dictionary<string, string> Styles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Active"] = "text-bg-success",
        ["Paid"] = "text-bg-success",
        ["Reserved"] = "text-bg-warning",
        ["PartiallyPaid"] = "text-bg-warning",
        ["Pending"] = "text-bg-warning",
        ["Unpaid"] = "text-bg-danger",
        ["Overdue"] = "text-bg-danger",
        ["Vacant"] = "text-bg-light border",
        ["Free"] = "text-bg-success",
        ["Occupied"] = "text-bg-primary",
        ["Maintenance"] = "text-bg-dark",
        ["Cancelled"] = "text-bg-secondary",
        ["Reversed"] = "text-bg-secondary",
    };

    /// <summary>An enum value or its name, e.g. <c>InvoiceStatus.Overdue</c> or "Overdue".</summary>
    public object? Status { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var name = Status?.ToString();
        if (string.IsNullOrEmpty(name))
        {
            output.SuppressOutput();
            return;
        }

        var text = localizer[$"Shared.Status.{name}"];
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClass("badge", HtmlEncoder.Default);
        foreach (var style in (Styles.GetValueOrDefault(name) ?? "text-bg-secondary").Split(' '))
        {
            output.AddClass(style, HtmlEncoder.Default);
        }

        output.Attributes.SetAttribute("data-status", name);
        output.Content.Append(text.ResourceNotFound ? name : text.Value);
    }
}
