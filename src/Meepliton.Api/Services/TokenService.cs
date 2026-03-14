using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Meepliton.Api.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Meepliton.Api.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Generates a JWT for the given user. Lifetime is 30 days.
    /// Claims: sub (userId), email, displayName, avatarUrl, theme.
    /// </summary>
    public string GenerateToken(ApplicationUser user)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
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

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
