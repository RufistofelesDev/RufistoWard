namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Identity.Interfaces;
using RufistoWard.Tokens.Persistence;

public sealed class RoleDetailModel(RufistoWardDbContext db, IRoleRepository roleRepo) : PageModel
{
    public RoleView? Role { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(int id, CancellationToken ct)
    {
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is not null) Role = MapToView(role);
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, string? name, string? description, string? permissions, CancellationToken ct)
    {
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return RedirectToPage("/Admin/Roles");

        role.Update(name, description);

        if (permissions is not null)
        {
            var desired = permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
            var current = role.Permissions.Select(p => p.Permission).ToHashSet();
            foreach (var remove in current.Except(desired)) role.RemovePermission(remove);
            foreach (var add in desired.Except(current)) role.AddPermission(add);
        }

        await roleRepo.UpdateAsync(role, ct);
        Role = MapToView(role);
        SuccessMessage = "Role updated successfully.";
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        var role = await db.Roles.FindAsync([id], ct);
        if (role is null) return RedirectToPage("/Admin/Roles");

        var hasUsers = await db.UserRoles.AnyAsync(ur => ur.RoleId == id, ct);
        if (hasUsers)
        {
            Role = MapToView((await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct))!);
            ErrorMessage = "Cannot delete a role that has users assigned.";
            return Page();
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        return RedirectToPage("/Admin/Roles");
    }

    private static RoleView MapToView(Identity.Entities.Role r) => new()
    {
        Id = r.Id, Name = r.Name, Description = r.Description,
        Permissions = r.Permissions.Select(p => p.Permission).ToArray(),
    };

    public sealed class RoleView
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string[] Permissions { get; set; } = [];
    }
}
