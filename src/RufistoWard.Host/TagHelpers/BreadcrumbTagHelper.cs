namespace RufistoWard.Core.TagHelpers;

using Microsoft.AspNetCore.Razor.TagHelpers;

[HtmlTargetElement("rw-breadcrumb")]
public sealed class BreadcrumbTagHelper : TagHelper
{
    /// <summary>
    /// Semicolon-separated list of "label|url" pairs. Last item is the current page (no link).
    /// Example: "Apps|/admin/apps;Web Portal|"
    /// </summary>
    public string Items { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "nav";
        output.Attributes.SetAttribute("aria-label", "Breadcrumb");
        output.Attributes.SetAttribute("class", "mb-6");
        output.Attributes.SetAttribute("data-testid", "breadcrumb");

        var segments = Items.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            output.SuppressOutput();
            return;
        }

        var html = "<ol class=\"flex items-center gap-2 text-sm text-ward-muted\">";

        for (var i = 0; i < segments.Length; i++)
        {
            var parts = segments[i].Split('|', 2);
            var label = parts[0].Trim();
            var url = parts.Length > 1 ? parts[1].Trim() : "";
            var isLast = i == segments.Length - 1;

            // Truncate long names
            var displayLabel = label.Length > 30 ? label[..27] + "..." : label;
            var titleAttr = label.Length > 30 ? $" title=\"{label}\"" : "";

            if (isLast)
            {
                html += $"<li aria-current=\"page\" class=\"text-ward-text font-medium\"{titleAttr}>{displayLabel}</li>";
            }
            else
            {
                html += $"<li{titleAttr}><a href=\"{url}\" class=\"hover:text-ward-primary transition-colors\">{displayLabel}</a></li>";
                html += "<li aria-hidden=\"true\" class=\"text-ward-inactive\">\u203a</li>";
            }
        }

        html += "</ol>";
        output.Content.SetHtmlContent(html);
    }
}
