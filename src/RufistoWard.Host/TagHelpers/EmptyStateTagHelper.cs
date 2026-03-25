namespace RufistoWard.Core.TagHelpers;

using Microsoft.AspNetCore.Razor.TagHelpers;

[HtmlTargetElement("rw-empty-state")]
public sealed class EmptyStateTagHelper : TagHelper
{
    public string Message { get; set; } = "No items yet.";
    public string? ActionUrl { get; set; }
    public string? ActionLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "text-center py-12");
        output.Attributes.SetAttribute("data-testid", "empty-state");

        var actionHtml = !string.IsNullOrEmpty(ActionUrl)
            ? $"""<a href="{ActionUrl}" class="mt-4 inline-block bg-ward-accent hover:bg-ward-accent-hover text-slate-900 px-6 py-3 rounded-xl text-sm font-bold shadow-sm" data-testid="empty-state-action">{ActionLabel ?? "Create"}</a>"""
            : "";

        output.Content.SetHtmlContent($"""
            <p class="text-ward-muted text-lg font-heading">{Message}</p>
            {actionHtml}
            """);
    }
}
