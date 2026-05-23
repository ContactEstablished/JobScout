using JobScout.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace JobScout.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<SearchProfile> Profiles { get; set; } = [];
}
