namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RufistoWard.Identity.Interfaces;

public sealed class UserNewModel(IUserRepository userRepo, IPasswordHasher passwordHasher) : PageModel
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string username, string email, string password, CancellationToken ct)
    {
        Username = username; Email = email;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "All fields are required.";
            return Page();
        }

        if (await userRepo.FindByUsernameAsync(username, ct) is not null)
        {
            ErrorMessage = $"Username '{username}' already exists.";
            return Page();
        }

        if (await userRepo.FindByEmailAsync(email, ct) is not null)
        {
            ErrorMessage = $"Email '{email}' already exists.";
            return Page();
        }

        var subjectId = Guid.NewGuid().ToString();
        var user = Identity.Entities.User.Create(subjectId, username, email, passwordHasher.Hash(password));
        await userRepo.AddAsync(user, ct);

        return RedirectToPage("/Admin/UserDetail", new { subjectId });
    }
}
