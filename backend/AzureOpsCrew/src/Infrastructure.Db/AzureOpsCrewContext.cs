using AzureOpsCrew.Domain.Dimmies;
using AzureOpsCrew.Infrastructure.Db.EntityTypes;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Infrastructure.Db
{
    public class AzureOpsCrewContext : DbContext
    {
        public AzureOpsCrewContext(DbContextOptions<AzureOpsCrewContext> options)
            : base(options)
        {
        }

        public DbSet<Dummy> Dummies => Set<Dummy>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultContainer("AppContainer");

            modelBuilder.ApplyConfiguration(new DummyEntityTypeConfiguration());
        }
    }
}
