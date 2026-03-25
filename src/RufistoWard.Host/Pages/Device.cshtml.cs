namespace RufistoWard.Core.Pages;

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RufistoWard.Auth.Protocol.Device;
using RufistoWard.Identity.Entities;
using RufistoWard.Identity.Interfaces;

public sealed class DeviceModel(IPersistedGrantRepository grantRepository) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? UserCode { get; set; }

    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsComplete { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string userCode, CancellationToken cancellationToken)
    {
        UserCode = userCode;

        if (string.IsNullOrWhiteSpace(userCode))
        {
            ErrorMessage = "Please enter a valid code.";
            return Page();
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return Redirect($"/login?returnUrl=/device?user_code={userCode}");
        }

        var subjectId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "";

        // Search active device_code grants for matching user_code
        var grants = await grantRepository.FindByTypeAsync("device_code", cancellationToken);
        PersistedGrant? matchingGrant = null;
        DeviceCodeData? matchingData = null;

        foreach (var grant in grants)
        {
            var data = JsonSerializer.Deserialize<DeviceCodeData>(grant.Data);
            if (data is not null && string.Equals(data.UserCode, userCode, StringComparison.OrdinalIgnoreCase))
            {
                matchingGrant = grant;
                matchingData = data;
                break;
            }
        }

        if (matchingGrant is null || matchingData is null)
        {
            ErrorMessage = "Invalid or expired code.";
            return Page();
        }

        if (matchingGrant.IsExpired)
        {
            ErrorMessage = "This code has expired.";
            return Page();
        }

        // Approve the device code
        matchingData.Approved = true;
        matchingData.SubjectId = subjectId;

        var updatedGrant = PersistedGrant.Create(
            matchingGrant.Id, matchingGrant.Type, matchingGrant.ClientId, subjectId,
            JsonSerializer.Serialize(matchingData), matchingGrant.Expiration);
        await grantRepository.UpdateAsync(updatedGrant, cancellationToken);

        Message = "Device authorized successfully! You can close this page.";
        IsComplete = true;
        return Page();
    }
}
