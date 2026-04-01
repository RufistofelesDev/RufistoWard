namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class RoleNewModel(IRoleRepository roleRepo) : PageModel
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Permissions { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string name, string? description, string? permissions, CancellationToken ct)
    {
        Name = name; Description = description; Permissions = permissions;

        if (string.IsNullOrWhiteSpace(name)) { ErrorMessage = "Role name is required."; return Page(); }

        var existing = await roleRepo.FindByNameAsync(name, ct);
        if (existing is not null) { ErrorMessage = $"Role '{name}' already exists."; return Page(); }

        var role = Role.Create(name, description);
        foreach (var perm in (permissions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            role.AddPermission(perm);

        await roleRepo.AddAsync(role, ct);
        return RedirectToPage("/Admin/RoleDetail", new { id = role.Id });
    }
}
