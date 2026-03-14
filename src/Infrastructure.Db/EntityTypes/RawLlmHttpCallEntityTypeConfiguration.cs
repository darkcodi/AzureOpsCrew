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
               .ValueGeneratedOnAdd()
               .HasColumnType("uuid");

        builder.Property(m => m.AgentId)
               .IsRequired()
               .HasColumnType("uuid");

        builder.Property(m => m.ThreadId)
               .IsRequired()
               .HasColumnType("uuid");

        builder.Property(m => m.RunId)
               .IsRequired()
               .HasColumnType("uuid");

        builder.Property(m => m.HttpRequest)
               .IsRequired();

        builder.Property(m => m.HttpResponse)
               .IsRequired();

        builder.Property(m => m.CreatedAt)
               .IsRequired();

        builder.HasIndex(m => m.AgentId);
        builder.HasIndex(m => m.ThreadId);
        builder.HasIndex(m => m.RunId);
    }
}
