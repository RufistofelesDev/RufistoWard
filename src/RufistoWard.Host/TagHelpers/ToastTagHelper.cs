namespace RufistoWard.Core.TagHelpers;

using Microsoft.AspNetCore.Razor.TagHelpers;

[HtmlTargetElement("rw-toast")]
public sealed class ToastTagHelper : TagHelper
{
    public string Type { get; set; } = "info";
    public string Message { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";

        var (bgColor, borderColor, textColor, role) = Type.ToLowerInvariant() switch
        {
            "success" => ("bg-emerald-50", "border-emerald-200", "text-emerald-800", "status"),
            "error" => ("bg-red-50", "border-red-200", "text-red-800", "alert"),
            _ => ("bg-ward-indigo-50", "border-ward-indigo-100", "text-ward-indigo-900", "status"),
        };

        output.Attributes.SetAttribute("class",
            $"px-4 py-3 rounded-lg border shadow-sm text-sm {bgColor} {borderColor} {textColor}");
        output.Attributes.SetAttribute("role", role);
        output.Attributes.SetAttribute("aria-live", role == "alert" ? "assertive" : "polite");
        output.Attributes.SetAttribute("data-testid", "toast");
        output.Attributes.SetAttribute("data-toast-duration", "5000");
        output.Content.SetContent(Message);
    }
}
