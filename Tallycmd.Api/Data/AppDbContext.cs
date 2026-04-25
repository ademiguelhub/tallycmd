namespace Tallycmd.Api.Data;

internal class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();
}
