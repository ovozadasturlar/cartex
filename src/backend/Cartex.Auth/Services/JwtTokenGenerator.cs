using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cartex.Auth.Settings;
using Microsoft.IdentityModel.Tokens;

namespace Cartex.Auth.Services;

public interface IJwtTokenGenerator
{
    string GenerateToken(long userId, string username, string fullName, IEnumerable<string> roles,
        string? startPage, IEnumerable<string> permissions, string authorizationStamp,
        long businessId, IEnumerable<long> branchIds, long? defaultBranchId, string? deviceId);

    string GenerateCustomerToken(long customerId, string fullName);
}

public class JwtTokenGenerator(JwtSettings settings) : IJwtTokenGenerator
{
    public string GenerateToken(long userId, string username, string fullName, IEnumerable<string> roles,
        string? startPage, IEnumerable<string> permissions, string authorizationStamp,
        long businessId, IEnumerable<long> branchIds, long? defaultBranchId, string? deviceId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("userId", userId.ToString()),
            new("username", username),
            new("fullName", fullName),
            new("businessId", businessId.ToString()),
            new("authorizationStamp", authorizationStamp),
        };

        if (startPage is not null)
            claims.Add(new Claim("startPage", startPage));

        if (defaultBranchId is not null)
            claims.Add(new Claim("defaultBranchId", defaultBranchId.Value.ToString()));

        if (!string.IsNullOrWhiteSpace(deviceId))
            claims.Add(new Claim("deviceId", deviceId.Trim()));

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
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

    public string GenerateCustomerToken(long customerId, string fullName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("customerId", customerId.ToString()),
            new("fullName", fullName),
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.CustomerAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(settings.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
