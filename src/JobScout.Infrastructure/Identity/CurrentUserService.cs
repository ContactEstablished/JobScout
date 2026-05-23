using System.Security.Claims;
using JobScout.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace JobScout.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        UserId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? throw new UnauthorizedAccessException("No authenticated user.");
    }

    public string UserId { get; }
}
