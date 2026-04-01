namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RufistoWard.Identity.Interfaces;

public sealed class AppDetailModel(IClientRepository clientRepo) : PageModel
{
    public AppView? App { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string clientId, CancellationToken ct)
    {
        var client = await clientRepo.FindByClientIdAsync(clientId, ct);
        if (client is not null) App = MapToView(client);
    }

    public async Task<IActionResult> OnPostUpdateAsync(string clientId, string? clientName,
        bool? enabled, bool? requirePkce, int? accessTokenLifetimeSeconds, int? refreshTokenLifetimeSeconds,
        CancellationToken ct)
    {
        var client = await clientRepo.FindByClientIdAsync(clientId, ct);
        if (client is null) return RedirectToPage("/Admin/Apps");

        client.Update(clientName, enabled, requirePkce, accessTokenLifetimeSeconds, refreshTokenLifetimeSeconds);
        await clientRepo.UpdateAsync(client, ct);

        App = MapToView(client);
        SuccessMessage = "App updated successfully.";
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string clientId, CancellationToken ct)
    {
        var client = await clientRepo.FindByClientIdAsync(clientId, ct);
        if (client is not null) await clientRepo.DeleteAsync(client, ct);
        return RedirectToPage("/Admin/Apps");
    }

    private static AppView MapToView(Identity.Entities.Client c) => new()
    {
        ClientId = c.ClientId,
        ClientName = c.ClientName,
        Enabled = c.Enabled,
        RequirePkce = c.RequirePkce,
        AccessTokenLifetimeSeconds = c.AccessTokenLifetimeSeconds,
        RefreshTokenLifetimeSeconds = c.RefreshTokenLifetimeSeconds,
        GrantTypes = c.GrantTypes.Select(g => g.GrantType).ToArray(),
        RedirectUris = c.RedirectUris.Select(r => r.Uri).ToArray(),
        AllowedScopes = c.Scopes.Select(s => s.Scope).ToArray(),
    };

    public sealed class AppView
    {
        public string ClientId { get; set; } = "";
        public string ClientName { get; set; } = "";
        public bool Enabled { get; set; }
        public bool RequirePkce { get; set; }
        public int AccessTokenLifetimeSeconds { get; set; }
        public int RefreshTokenLifetimeSeconds { get; set; }
        public string[] GrantTypes { get; set; } = [];
        public string[] RedirectUris { get; set; } = [];
        public string[] AllowedScopes { get; set; } = [];
    }
}
