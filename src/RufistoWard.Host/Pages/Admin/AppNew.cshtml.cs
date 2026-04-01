namespace RufistoWard.Core.Pages.Admin;

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class AppNewModel(IClientRepository clientRepo) : PageModel
{
    public string? ClientName { get; set; }
    public string[]? SelectedGrantTypes { get; set; }
    public string? RedirectUris { get; set; }
    public string? AllowedScopes { get; set; }
    public bool RequireClientSecret { get; set; } = true;

    public string? CreatedSecret { get; set; }
    public string? CreatedClientId { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(
        string clientName, string[]? grantTypes, string? redirectUris, string? allowedScopes,
        bool requireClientSecret, CancellationToken ct)
    {
        ClientName = clientName;
        SelectedGrantTypes = grantTypes;
        RedirectUris = redirectUris;
        AllowedScopes = allowedScopes;
        RequireClientSecret = requireClientSecret;

        if (string.IsNullOrWhiteSpace(clientName))
        {
            ErrorMessage = "App name is required.";
            return Page();
        }

        var clientId = Guid.NewGuid().ToString("N")[..16];
        var client = Client.Create(clientId, clientName, requireClientSecret);

        foreach (var gt in grantTypes ?? []) client.AddGrantType(gt);

        foreach (var uri in (redirectUris ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            client.AddRedirectUri(uri);

        foreach (var scope in (allowedScopes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            client.AddScope(scope);

        string? plainSecret = null;
        if (requireClientSecret)
        {
            plainSecret = Guid.NewGuid().ToString("N");
            client.AddSecret(Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(plainSecret))));
        }

        await clientRepo.AddAsync(client, ct);

        CreatedClientId = clientId;
        CreatedSecret = plainSecret;

        return Page();
    }
}
