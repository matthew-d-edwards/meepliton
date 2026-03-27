using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Meepliton.Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Meepliton.Api.Services;

public class TokenService(IConfiguration config, UserManager<ApplicationUser> userManager)
{
    /// <summary>
    /// Generates a JWT for the given user. Lifetime is 30 days.
    /// Claims: sub (userId), email, displayName, avatarUrl, theme, and one role claim per role.
    /// </summary>
    public async Task<string> GenerateTokenAsync(ApplicationUser user)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddDays(30);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("displayName",                 user.DisplayName),
            new("theme",                       user.Theme),
        };

        if (user.AvatarUrl is not null)
            claims.Add(new Claim("avatarUrl", user.AvatarUrl));

        // Embed role claims so authorization policies work without an extra DB call per request.
        var roles = await userManager.GetRolesAsync(user);
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:             config["Jwt:Issuer"],
            audience:           config["Jwt:Audience"],
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
