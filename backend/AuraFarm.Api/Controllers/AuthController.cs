using AuraFarm.Api.Auth;
using AuraFarm.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Npgsql;

namespace AuraFarm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<JwtOptions> jwtOptions,
    IConfiguration configuration
) : ControllerBase
{
    private readonly string _connString = configuration.GetConnectionString("AuraFarm") ?? throw new InvalidOperationException("No Connection String");

    public sealed record RegisterRequest(string Email, string Password, string? Role);
    public sealed record LoginRequest(string Email, string Password);

    [HttpPost("staff/login")]
    public async Task<IActionResult> StaffLogin(LoginRequest req)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT staff_id, password_hash, role::text FROM Staff WHERE username = @u OR email = @u", conn);
        cmd.Parameters.AddWithValue("u", req.Email);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return Unauthorized();

        var staffId = reader.GetGuid(0).ToString();
        var hash = reader.GetString(1);
        var role = reader.GetString(2);
        await reader.CloseAsync();

        if (!BCrypt.Net.BCrypt.Verify(req.Password, hash)) return Unauthorized();

        return Ok(new { access_token = GenerateJwt(staffId, req.Email, new[] { role }), roles = new[] { role } });
    }

    [HttpPost("member/login")]
    public async Task<IActionResult> MemberLogin(LoginRequest req)
    {
        await using var conn = new NpgsqlConnection(_connString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("SELECT member_id, password_hash FROM Members WHERE username = @u OR email = @u", conn);
        cmd.Parameters.AddWithValue("u", req.Email);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return Unauthorized();

        var memberId = reader.GetGuid(0).ToString();
        var hash = reader.GetString(1);
        await reader.CloseAsync();

        if (!BCrypt.Net.BCrypt.Verify(req.Password, hash)) return Unauthorized();

        return Ok(new { access_token = GenerateJwt(memberId, req.Email, new[] { "member" }), roles = new[] { "member" } });
    }

    private string GenerateJwt(string sub, string email, IEnumerable<string> roles)
    {
        var jwt = jwtOptions.Value;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sub),
            new(JwtRegisteredClaimNames.Sub, sub),
            new(JwtRegisteredClaimNames.Email, email),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwt.ExpiresMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }


    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User.FindFirstValue(ClaimTypes.Email);
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        return Ok(new { email, roles });
    }
}

