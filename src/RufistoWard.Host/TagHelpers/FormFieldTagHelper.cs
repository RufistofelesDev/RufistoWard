namespace RufistoWard.Core.TagHelpers;

using Microsoft.AspNetCore.Razor.TagHelpers;

[HtmlTargetElement("rw-form-field")]
public sealed class FormFieldTagHelper : TagHelper
{
    public string Label { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "text";
    public string? Value { get; set; }
    public string? Error { get; set; }
    public string? Help { get; set; }
    public bool Required { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "mb-4");

        var fieldId = $"field-{Name}";
        var errorId = $"error-{Name}";
        var helpId = $"help-{Name}";
        var requiredAttr = Required ? " required aria-required=\"true\"" : "";
        var requiredMark = Required ? " <span class=\"text-red-400\">*</span>" : "";

        var describedByParts = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(Help)) describedByParts.Add(helpId);
        if (!string.IsNullOrEmpty(Error)) describedByParts.Add(errorId);
        var ariaDescribedBy = describedByParts.Count > 0
            ? $" aria-describedby=\"{string.Join(" ", describedByParts)}\""
            : "";

        var borderClass = !string.IsNullOrEmpty(Error) ? "border-ward-error" : "border-ward-border";

        var helpHtml = !string.IsNullOrEmpty(Help)
            ? $"<p id=\"{helpId}\" class=\"text-xs text-ward-muted mb-1\">{Help}</p>"
            : "";

        var errorHtml = !string.IsNullOrEmpty(Error)
            ? $"<p id=\"{errorId}\" class=\"mt-1 text-sm text-ward-error\" role=\"alert\">{Error}</p>"
            : "";

        output.Content.SetHtmlContent($"""
            <label for="{fieldId}" class="block text-sm font-medium text-ward-text mb-1">{Label}{requiredMark}</label>
            {helpHtml}
            <input type="{Type}" id="{fieldId}" name="{Name}" value="{Value ?? ""}"
                class="w-full border-2 {borderClass} rounded-xl px-4 py-3 text-sm placeholder:text-ward-inactive
                       focus-visible:ring-2 focus-visible:ring-ward-primary focus-visible:border-ward-primary outline-none"
                data-testid="field-{Name}"{requiredAttr}{ariaDescribedBy} />
            {errorHtml}
            """);
    }
}
