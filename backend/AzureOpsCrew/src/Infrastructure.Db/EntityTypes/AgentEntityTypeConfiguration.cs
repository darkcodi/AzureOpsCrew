using AzureOpsCrew.Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes
{
    public sealed class AgentEntityTypeConfiguration : IEntityTypeConfiguration<Agent>
    {
        public void Configure(EntityTypeBuilder<Agent> builder)
        {
            builder.ToContainer(nameof(Agent));

            builder.HasPartitionKey(a => a.ClientId);

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id)
                   .ToJsonProperty("id")
                   .HasConversion(
                        g => g.ToString("D"),
                        s => Guid.Parse(s));

            builder.OwnsOne(a => a.Info);
        }
    }
}
