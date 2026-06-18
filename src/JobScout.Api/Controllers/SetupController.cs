using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobScout.Core.DTOs;
using JobScout.Core.Models;
using JobScout.Infrastructure.Configuration;
using JobScout.Infrastructure.Data;
using JobScout.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace JobScout.Api.Controllers;

/// <summary>
/// First-run setup endpoints. Unauthenticated; only operates while the user table is empty.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SetupController : ControllerBase
{
    private readonly JobScoutDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISecretStore _secrets;
    private readonly IConfiguration _config;

    public SetupController(
        JobScoutDbContext db,
        UserManager<ApplicationUser> userManager,
        ISecretStore secrets,
        IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _secrets = secrets;
        _config = config;
    }

    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusDto>> Status()
    {
        var needsSetup = !await _db.Users.AnyAsync();
        return Ok(new SetupStatusDto { NeedsSetup = needsSetup });
    }

    [HttpPost("complete")]
    public async Task<ActionResult<AuthResponse>> Complete([FromBody] CompleteSetupRequest request)
    {
        // Setup is a one-time, unauthenticated bootstrap; refuse if any user already exists.
        if (await _db.Users.AnyAsync())
            return Conflict(new { message = "Setup has already been completed." });

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Email : request.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return BadRequest(new { errors = created.Errors.Select(e => e.Description) });

        // Persist any optional API keys the user supplied.
        if (!string.IsNullOrWhiteSpace(request.AnthropicApiKey))
            await _secrets.SetAsync("Anthropic:ApiKey", request.AnthropicApiKey);
        if (!string.IsNullOrWhiteSpace(request.SerpApiKey))
            await _secrets.SetAsync("SerpApi:ApiKey", request.SerpApiKey);
        if (!string.IsNullOrWhiteSpace(request.AdzunaAppId))
            await _secrets.SetAsync("Adzuna:AppId", request.AdzunaAppId);
        if (!string.IsNullOrWhiteSpace(request.AdzunaAppKey))
            await _secrets.SetAsync("Adzuna:AppKey", request.AdzunaAppKey);

        // Optional starter profile so the user lands on a populated feed.
        if (!string.IsNullOrWhiteSpace(request.FirstProfileName))
        {
            var now = DateTime.UtcNow;
            _db.SearchProfiles.Add(new SearchProfile
            {
                Id = Guid.NewGuid(),
                Name = request.FirstProfileName,
                UserId = user.Id,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            await _db.SaveChangesAsync();
        }

        return Ok(GenerateAuthResponse(user));
    }

    private AuthResponse GenerateAuthResponse(ApplicationUser user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT key is not configured.")));

        var expiration = DateTime.UtcNow.AddDays(7);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim("display_name", user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiration,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Expiration = expiration,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                DisplayName = user.DisplayName
            }
        };
    }
}
