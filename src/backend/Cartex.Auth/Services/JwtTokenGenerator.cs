using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cartex.Auth.Settings;
using Microsoft.IdentityModel.Tokens;

namespace Cartex.Auth.Services;

public interface IJwtTokenGenerator
{
    string GenerateToken(long userId, string username, string role, IEnumerable<string> permissions,
        long businessId, IEnumerable<long> branchIds, long? defaultBranchId);
}

public class JwtTokenGenerator(JwtSettings settings) : IJwtTokenGenerator
{
    public string GenerateToken(long userId, string username, string role, IEnumerable<string> permissions,
        long businessId, IEnumerable<long> branchIds, long? defaultBranchId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("userId", userId.ToString()),
            new("username", username),
            new(ClaimTypes.Role, role),
            new("businessId", businessId.ToString()),
        };

        if (defaultBranchId is not null)
            claims.Add(new Claim("defaultBranchId", defaultBranchId.Value.ToString()));

        claims.AddRange(branchIds.Select(b => new Claim("branchId", b.ToString())));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(settings.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
