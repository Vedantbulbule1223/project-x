using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SwiftRoute.API.Data;
using SwiftRoute.API.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SwiftRoute.API.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest req);
}

public class AuthService : IAuthService
{
    private readonly SwiftRouteDbContext _ctx;
    private readonly IConfiguration _cfg;

    public AuthService(SwiftRouteDbContext ctx, IConfiguration cfg)
    {
        _ctx = ctx;
        _cfg = cfg;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest req)
    {
        var user = await _ctx.Users
            .FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive)
            ?? throw new UnauthorizedAccessException("Invalid username or password.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username or password.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _cfg["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(8);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("fullName", user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"],
            audience: _cfg["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            user.Id, user.FullName, user.Role.ToString(), expires);
    }
}
