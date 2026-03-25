namespace RufistoWard.Core.TagHelpers;

using Microsoft.AspNetCore.Razor.TagHelpers;

[HtmlTargetElement("rw-status-badge")]
public sealed class StatusBadgeTagHelper : TagHelper
{
    public string Status { get; set; } = "unknown";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";

        var (dotColor, bgColor, textColor) = Status.ToLowerInvariant() switch
        {
            "active" or "enabled" or "healthy" => ("bg-ward-success", "bg-ward-success-light", "text-ward-success-text"),
            "inactive" or "disabled" or "locked" => ("bg-ward-error", "bg-ward-error-light", "text-ward-error-text"),
            "degraded" or "pending" => ("bg-ward-warning", "bg-ward-warning-light", "text-ward-warning-text"),
            _ => ("bg-ward-inactive", "bg-ward-inactive-light", "text-ward-inactive-text"),
        };

        output.Attributes.SetAttribute("class",
            $"inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium {bgColor} {textColor}");
        output.Attributes.SetAttribute("aria-label", $"Status: {Status}");
        output.Attributes.SetAttribute("data-testid", $"status-badge-{Status.ToLowerInvariant()}");
        output.Content.SetHtmlContent(
            $"<span class=\"w-1.5 h-1.5 rounded-full {dotColor}\" aria-hidden=\"true\"></span>{Status}");
    }
}
