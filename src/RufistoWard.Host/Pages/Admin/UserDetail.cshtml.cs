namespace RufistoWard.Core.Pages.Admin;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RufistoWard.Identity.Interfaces;

public sealed class UserDetailModel(IUserRepository userRepo) : PageModel
{
    public new UserView? User { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(string subjectId, CancellationToken ct)
    {
        var user = await userRepo.FindBySubjectIdAsync(subjectId, ct);
        if (user is not null) User = MapToView(user);
    }

    public async Task<IActionResult> OnPostUpdateAsync(string subjectId, string? username, string? email, bool emailConfirmed, CancellationToken ct)
    {
        var user = await userRepo.FindBySubjectIdAsync(subjectId, ct);
        if (user is null) return RedirectToPage("/Admin/Users");

        user.Update(username, email, emailConfirmed);
        await userRepo.UpdateAsync(user, ct);
        User = MapToView(user);
        SuccessMessage = "User updated successfully.";
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(string subjectId, CancellationToken ct)
    {
        var user = await userRepo.FindBySubjectIdAsync(subjectId, ct);
        if (user is null) return RedirectToPage("/Admin/Users");
        user.Deactivate();
        await userRepo.UpdateAsync(user, ct);
        User = MapToView(user);
        SuccessMessage = "User deactivated.";
        return Page();
    }

    public async Task<IActionResult> OnPostActivateAsync(string subjectId, CancellationToken ct)
    {
        var user = await userRepo.FindBySubjectIdAsync(subjectId, ct);
        if (user is null) return RedirectToPage("/Admin/Users");
        user.Activate();
        await userRepo.UpdateAsync(user, ct);
        User = MapToView(user);
        SuccessMessage = "User activated.";
        return Page();
    }

    private static UserView MapToView(Identity.Entities.User u) => new()
    {
        SubjectId = u.SubjectId, Username = u.Username, Email = u.Email,
        IsActive = u.IsActive, EmailConfirmed = u.EmailConfirmed, CreatedAt = u.CreatedAt,
    };

    public sealed class UserView
    {
        public string SubjectId { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsActive { get; set; }
        public bool EmailConfirmed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
