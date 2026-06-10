using Microsoft.AspNetCore.Identity;

namespace WhiteStiches.Infrastructure.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string name, string? description = null) : base(name)
    {
        Description = description;
    }

    public string? Description { get; set; }
}
