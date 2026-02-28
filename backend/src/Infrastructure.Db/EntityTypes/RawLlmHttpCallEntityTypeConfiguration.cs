using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class RawLlmHttpCallEntityTypeConfiguration : IEntityTypeConfiguration<RawLlmHttpCall>
{
    public void Configure(EntityTypeBuilder<RawLlmHttpCall> builder)
    {
        builder.ToTable("RawLlmHttpCalls");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
               .ValueGeneratedOnAdd();

        builder.Property(m => m.AgentId)
               .IsRequired();

        builder.Property(m => m.ThreadId)
               .IsRequired();

        builder.Property(m => m.RunId)
               .IsRequired();

        builder.Property(m => m.HttpRequest)
               .IsRequired();

        builder.Property(m => m.HttpResponse)
               .IsRequired();

        builder.HasIndex(m => m.AgentId);
        builder.HasIndex(m => m.ThreadId);
        builder.HasIndex(m => m.RunId);
    }
}
