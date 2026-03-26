using System.Security.Cryptography;
using System.Text;

namespace Meepliton.Api.Helpers;

/// <summary>
/// Pure static helper for resolving a player's effective avatar URL.
/// </summary>
public static class AvatarHelper
{
    /// <summary>
    /// Returns the effective avatar URL for a player.
    /// Priority: stored URL → Gravatar derived from email → null.
    /// </summary>
    /// <param name="storedAvatarUrl">The URL already persisted on the user record (e.g. Google profile picture).</param>
    /// <param name="email">The user's email address, used to generate a Gravatar URL when no stored URL exists.</param>
    public static string? ResolveAvatarUrl(string? storedAvatarUrl, string? email)
    {
        if (!string.IsNullOrEmpty(storedAvatarUrl))
            return storedAvatarUrl;

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLowerInvariant();
            var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(normalized)))
                              .ToLowerInvariant();
            return $"https://www.gravatar.com/avatar/{hash}?d=identicon&s=80";
        }

        return null;
    }
}
