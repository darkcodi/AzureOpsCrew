using AzureOpsCrew.Domain.LLMOutputs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class LlmOutputEntityTypeConfiguration : IEntityTypeConfiguration<LlmOutput>
{
    public void Configure(EntityTypeBuilder<LlmOutput> builder)
    {
        builder.ToTable("LlmOutputs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.RunId).IsRequired();

        builder.Property(x => x.Text).IsRequired();

        builder.Property(x => x.ToolCall).IsRequired(false);

        builder.Property(x => x.InputTokens).IsRequired(false);

        builder.Property(x => x.OutputTokens).IsRequired(false);

        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.RunId);
    }
}
