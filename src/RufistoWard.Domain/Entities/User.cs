namespace RufistoWard.Identity.Entities;

public sealed class User
{
    public int Id { get; private set; }
    public string SubjectId { get; private set; } = default!;
    public string Username { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public bool EmailConfirmed { get; private set; }
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<UserClaim> _claims = [];
    public IReadOnlyCollection<UserClaim> Claims => _claims.AsReadOnly();

    private readonly List<UserRole> _roles = [];
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    private User() { }

    public static User Create(string subjectId, string username, string email, string passwordHash)
    {
        return new User
        {
            SubjectId = subjectId,
            Username = username,
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void RecordFailedLogin(int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            LockoutEnd = DateTime.UtcNow.Add(lockoutDuration);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockoutEnd = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string? username, string? email, bool? emailConfirmed)
    {
        if (username is not null) Username = username;
        if (email is not null) Email = email.ToLowerInvariant();
        if (emailConfirmed.HasValue) EmailConfirmed = emailConfirmed.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        FailedLoginCount = 0;
        LockoutEnd = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignRole(int roleId)
    {
        if (_roles.All(r => r.RoleId != roleId))
            _roles.Add(new UserRole(roleId));
    }

    public void RemoveRole(int roleId)
    {
        var existing = _roles.FirstOrDefault(r => r.RoleId == roleId);
        if (existing is not null) _roles.Remove(existing);
    }
}
