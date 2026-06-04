namespace RufistoWard.Tokens.Tests;

/// <summary>
/// Honest scope markers for coverage carved out of Story 10.4. These are intentionally skipped:
/// the behaviours they name require source changes that belong to the closing stories. Each closing
/// story unskips its tripwire as the first step of its red phase, so the skip cannot be silently lost.
/// Do NOT delete or implement these here — see <c>10-4a-...</c> and <c>10-4b-...</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class Tripwires
{
    [Fact(Skip = "Tracked by Story 10.4a — TimeProvider refactor")]
    public void Signing_key_rotation_overlap_validates_in_flight_tokens()
    {
    }

    [Fact(Skip = "Tracked by Story 10.4b — chain revocation domain change")]
    public void Refresh_token_reuse_revokes_entire_family()
    {
    }
}
