using Microsoft.AspNetCore.Identity;

namespace Meepliton.Api.Identity;

public class ApplicationUser : IdentityUser
{
    public string  DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl   { get; set; }
    public string  Theme       { get; set; } = "system"; // "light" | "dark" | "system"
    public DateTimeOffset CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}
