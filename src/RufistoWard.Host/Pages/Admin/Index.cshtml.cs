namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Tokens.Persistence;

public sealed class IndexModel(RufistoWardDbContext db) : PageModel
{
    public int ClientCount { get; set; }
    public int ScopeCount { get; set; }
    public int UserCount { get; set; }
    public int RoleCount { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ClientCount = await db.Clients.CountAsync(cancellationToken);
        ScopeCount = await db.ApiScopes.CountAsync(cancellationToken);
        UserCount = await db.Users.CountAsync(cancellationToken);
        RoleCount = await db.Roles.CountAsync(cancellationToken);
    }
}
