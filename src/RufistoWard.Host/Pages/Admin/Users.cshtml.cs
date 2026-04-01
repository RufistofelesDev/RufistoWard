namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RufistoWard.Tokens.Persistence;

public sealed class UsersModel(RufistoWardDbContext db) : PageModel
{
    public List<UserItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public new int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? Status { get; set; }

    public async Task OnGetAsync(int page = 1, string? search = null, string? status = null, CancellationToken ct = default)
    {
        Page = page; Search = search; Status = status;

        var query = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Username.Contains(search) || u.Email.Contains(search));
        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => u.IsActive);
        else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
            query = query.Where(u => !u.IsActive);

        TotalCount = await query.CountAsync(ct);
        var users = await query.OrderBy(u => u.Username).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        Items = users.Select(u => new UserItem
        {
            SubjectId = u.SubjectId, Username = u.Username, Email = u.Email,
            IsActive = u.IsActive, CreatedAt = u.CreatedAt,
        }).ToList();
    }

    public sealed class UserItem
    {
        public string SubjectId { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
