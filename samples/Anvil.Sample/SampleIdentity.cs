using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Anvil.Sample;

public sealed class SampleUser : IdentityUser
{
    public string TenantId { get; set; } = "demo";
}

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options)
    : AnvilIdentityDbContext<SampleUser>(options)
{
}
