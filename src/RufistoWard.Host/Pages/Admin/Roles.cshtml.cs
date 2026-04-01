namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Tokens.Persistence;

public sealed class RolesModel(RufistoWardDbContext db) : PageModel
{
    public List<RoleItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public new int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }

    public async Task OnGetAsync(int page = 1, string? search = null, CancellationToken ct = default)
    {
        Page = page; Search = search;

        var query = db.Roles.Include(r => r.Permissions).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.Name.Contains(search));

        TotalCount = await query.CountAsync(ct);
        var roles = await query.OrderBy(r => r.Name).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        Items = roles.Select(r => new RoleItem
        {
            Id = r.Id, Name = r.Name, Description = r.Description,
            PermissionCount = r.Permissions.Count,
        }).ToList();
    }

    public sealed class RoleItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int PermissionCount { get; set; }
    }
}
