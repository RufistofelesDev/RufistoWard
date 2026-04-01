namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Tokens.Persistence;

public sealed class AppsModel(RufistoWardDbContext db) : PageModel
{
    public List<AppItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public new int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }

    public async Task OnGetAsync(int page = 1, string? search = null, CancellationToken ct = default)
    {
        Page = page;
        Search = search;

        var query = db.Clients
            .Include(c => c.GrantTypes)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.ClientName.Contains(search) || c.ClientId.Contains(search));

        TotalCount = await query.CountAsync(ct);
        var clients = await query
            .OrderBy(c => c.ClientName)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        Items = clients.Select(c => new AppItem
        {
            ClientId = c.ClientId,
            ClientName = c.ClientName,
            Enabled = c.Enabled,
            GrantTypes = c.GrantTypes.Select(g => g.GrantType).ToArray(),
        }).ToList();
    }

    public sealed class AppItem
    {
        public string ClientId { get; set; } = "";
        public string ClientName { get; set; } = "";
        public bool Enabled { get; set; }
        public string[] GrantTypes { get; set; } = [];
    }
}
