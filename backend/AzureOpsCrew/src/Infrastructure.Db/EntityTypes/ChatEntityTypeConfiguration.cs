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


        builder.Property(c => c.AgentIds)
            .HasConversion(
                v => v.Select(x => x.ToString("D")).ToArray(),
                v => v.Select(Guid.Parse).ToArray());

        builder.OwnsMany(c => c.Messages, mb =>
        {
            mb.ToJsonProperty(nameof(Chat.Messages));

            mb.Property(m => m.Id)
                .HasConversion(
                    g => g.ToString("D"),
                    s => Guid.Parse(s));

            mb.Property(m => m.Status)
                .HasConversion<int>();

            mb.OwnsOne(m => m.Sender, sb =>
            {
                sb.Property(s => s.SenderType)
                    .HasConversion<int>();

                sb.Property(s => s.AgentId)
                    .HasConversion(
                        g => g.HasValue ? g.Value.ToString("D") : null,
                        s => string.IsNullOrWhiteSpace(s) ? (Guid?)null : Guid.Parse(s));
            });
        });
    }
}
