namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

public sealed class ScopeDetailModel(RufistoWardDbContext db, IScopeRepository scopeRepo) : PageModel
{
    public ScopeView? Scope { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(int id, CancellationToken ct)
    {
        var scope = await db.ApiScopes.FindAsync([id], ct);
        if (scope is not null) Scope = MapToView(scope);
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, string? displayName, string? description,
        bool enabled, bool required, bool emphasize, CancellationToken ct)
    {
        var scope = await db.ApiScopes.FindAsync([id], ct);
        if (scope is null) return RedirectToPage("/Admin/Scopes");

        scope.Update(displayName, description, enabled, required, emphasize);
        await scopeRepo.UpdateAsync(scope, ct);

        Scope = MapToView(scope);
        SuccessMessage = "Scope updated successfully.";
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        var scope = await db.ApiScopes.FindAsync([id], ct);
        if (scope is not null)
        {
            db.ApiScopes.Remove(scope);
            await db.SaveChangesAsync(ct);
        }
        return RedirectToPage("/Admin/Scopes");
    }

    private static ScopeView MapToView(Identity.Entities.ApiScope s) => new()
    {
        Id = s.Id, Name = s.Name, DisplayName = s.DisplayName,
        Description = s.Description, Enabled = s.Enabled,
        Required = s.Required, Emphasize = s.Emphasize,
    };

    public sealed class ScopeView
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public bool Enabled { get; set; }
        public bool Required { get; set; }
        public bool Emphasize { get; set; }
    }
}
