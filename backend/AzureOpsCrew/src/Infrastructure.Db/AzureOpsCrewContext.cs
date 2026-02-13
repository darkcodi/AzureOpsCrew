using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Chats;
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

        public DbSet<Agent> Agents => Set<Agent>();
        public DbSet<Chat> Chats => Set<Chat>();


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultContainer("AppContainer");

            modelBuilder.ApplyConfiguration(new AgentEntityTypeConfiguration());
            modelBuilder.ApplyConfiguration(new ChatEntityTypeConfiguration());
        }
    }
}
