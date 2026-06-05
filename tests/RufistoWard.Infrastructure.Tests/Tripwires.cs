namespace RufistoWard.Tokens.Tests;

/// <summary>
/// Honest scope markers for coverage carved out of Story 10.4. These are intentionally skipped:
/// the behaviour they name requires source changes that belong to the closing story, which unskips
/// its tripwire as the first step of its red phase so the skip cannot be silently lost.
/// Do NOT delete or implement here — see <c>10-4a-...</c>.
///
/// Story 10.4b's tripwire (<c>Refresh_token_reuse_revokes_entire_family</c>) was fulfilled on
/// 2026-06-03: family revocation is now real. Repository coverage lives in
/// <see cref="Persistence.PersistedGrantRepositoryTests"/> (RevokeFamilyAsync) and the handler-level
/// reuse-detection test of the same name lives in the Application test project.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Tripwires
{
    [Fact(Skip = "Tracked by Story 10.4a — TimeProvider refactor")]
    public void Signing_key_rotation_overlap_validates_in_flight_tokens()
    {
    }
}
