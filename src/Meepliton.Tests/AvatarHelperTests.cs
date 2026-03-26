using FluentAssertions;
using Meepliton.Api.Helpers;

namespace Meepliton.Tests;

public class AvatarHelperTests
{
    // ------------------------------------------------------------------ //
    // Stored URL takes priority
    // ------------------------------------------------------------------ //

    [Fact]
    public void ResolveAvatarUrl_ReturnsStoredUrl_WhenStoredUrlIsPresent()
    {
        const string googleUrl = "https://lh3.googleusercontent.com/a/abc123";

        var result = AvatarHelper.ResolveAvatarUrl(googleUrl, "user@example.com");

        result.Should().Be(googleUrl);
    }

    [Fact]
    public void ResolveAvatarUrl_ReturnsStoredUrl_WhenEmailIsNull()
    {
        const string storedUrl = "https://example.com/avatar.png";

        var result = AvatarHelper.ResolveAvatarUrl(storedUrl, null);

        result.Should().Be(storedUrl);
    }

    // ------------------------------------------------------------------ //
    // Gravatar fallback
    // ------------------------------------------------------------------ //

    [Fact]
    public void ResolveAvatarUrl_ReturnsGravatar_WhenStoredUrlIsNull()
    {
        // MD5("test@example.com") = 55502f40dc8b7c769880b10874abc9d0
        const string expected =
            "https://www.gravatar.com/avatar/55502f40dc8b7c769880b10874abc9d0?d=identicon&s=80";

        var result = AvatarHelper.ResolveAvatarUrl(null, "test@example.com");

        result.Should().Be(expected);
    }

    [Fact]
    public void ResolveAvatarUrl_ReturnsGravatar_WhenStoredUrlIsEmpty()
    {
        // MD5("empty_stored_url@test.org") = 20df115de762aadf4137c9fefe0246b9
        const string expected =
            "https://www.gravatar.com/avatar/20df115de762aadf4137c9fefe0246b9?d=identicon&s=80";

        var result = AvatarHelper.ResolveAvatarUrl(string.Empty, "empty_stored_url@test.org");

        result.Should().Be(expected);
    }

    [Fact]
    public void ResolveAvatarUrl_NormalizesEmailBeforeHashing()
    {
        // "  USER@Example.com  " trims + lowercases to "user@example.com"
        // MD5("user@example.com") = b58996c504c5638798eb6b511e6f49af
        const string expected =
            "https://www.gravatar.com/avatar/b58996c504c5638798eb6b511e6f49af?d=identicon&s=80";

        var resultFromRaw    = AvatarHelper.ResolveAvatarUrl(null, "  USER@Example.com  ");
        var resultFromNormal = AvatarHelper.ResolveAvatarUrl(null, "user@example.com");

        resultFromRaw.Should().Be(expected);
        resultFromNormal.Should().Be(expected, "normalized form must yield the same Gravatar URL");
    }

    // ------------------------------------------------------------------ //
    // Both inputs absent → null
    // ------------------------------------------------------------------ //

    [Fact]
    public void ResolveAvatarUrl_ReturnsNull_WhenBothInputsAreNull()
    {
        var result = AvatarHelper.ResolveAvatarUrl(null, null);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveAvatarUrl_ReturnsNull_WhenStoredUrlIsNullAndEmailIsEmpty()
    {
        var result = AvatarHelper.ResolveAvatarUrl(null, string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveAvatarUrl_ReturnsNull_WhenStoredUrlIsNullAndEmailIsWhitespace()
    {
        // string.IsNullOrWhiteSpace guards the Gravatar branch
        var result = AvatarHelper.ResolveAvatarUrl(null, "   ");

        result.Should().BeNull();
    }
}
