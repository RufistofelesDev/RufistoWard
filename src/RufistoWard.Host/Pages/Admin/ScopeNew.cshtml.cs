namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class ScopeNewModel(IScopeRepository scopeRepo) : PageModel
{
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string name, string? displayName, string? description, CancellationToken ct)
    {
        Name = name; DisplayName = displayName; Description = description;

        if (string.IsNullOrWhiteSpace(name)) { ErrorMessage = "Scope name is required."; return Page(); }

        var existing = await scopeRepo.FindByNameAsync(name, ct);
        if (existing is not null) { ErrorMessage = $"Scope '{name}' already exists."; return Page(); }

        var scope = ApiScope.Create(name, displayName, description);
        await scopeRepo.AddAsync(scope, ct);

        return RedirectToPage("/Admin/ScopeDetail", new { id = scope.Id });
    }
}
