using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AzureOpsCrew.Domain.Chats;

public sealed class ChatEntityTypeConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.ToContainer(nameof(Chat));

        builder.HasPartitionKey(c => c.ClientId);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ToJsonProperty("id")
            .HasConversion(
                g => g.ToString("D"),
                s => Guid.Parse(s));
    }
}
