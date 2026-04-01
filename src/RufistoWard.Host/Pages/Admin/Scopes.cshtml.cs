namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Tokens.Persistence;

public sealed class ScopesModel(RufistoWardDbContext db) : PageModel
{
    public List<ScopeItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public new int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }

    public async Task OnGetAsync(int page = 1, string? search = null, CancellationToken ct = default)
    {
        Page = page;
        Search = search;

        var query = db.ApiScopes.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) || (s.DisplayName != null && s.DisplayName.Contains(search)));

        TotalCount = await query.CountAsync(ct);
        var scopes = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        Items = scopes.Select(s => new ScopeItem
        {
            Id = s.Id, Name = s.Name, DisplayName = s.DisplayName, Enabled = s.Enabled,
        }).ToList();
    }

    public sealed class ScopeItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? DisplayName { get; set; }
        public bool Enabled { get; set; }
    }
}
