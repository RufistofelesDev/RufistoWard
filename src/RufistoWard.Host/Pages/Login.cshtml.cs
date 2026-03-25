namespace RufistoWard.Core.Pages;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RufistoWard.Auth.Protocol.Authorization;

public sealed class LoginModel(LoginHandler loginHandler) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string usernameOrEmail, string password, CancellationToken cancellationToken)
    {
        var result = await loginHandler.HandleAsync(new LoginRequest(usernameOrEmail, password), cancellationToken);

        if (!result.IsSuccess)
        {
            ErrorMessage = "Invalid credentials.";
            return Page();
        }

        var loginResult = result.Value!;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, loginResult.SubjectId),
            new("sub", loginResult.SubjectId),
            new(ClaimTypes.Name, loginResult.Username),
            new(ClaimTypes.Email, loginResult.Email),
        };

        var identity = new ClaimsIdentity(claims, "IdentityServerCookie");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("IdentityServerCookie", principal);

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return RedirectToPage("/Login");
    }
}
