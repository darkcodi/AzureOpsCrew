using AzureOpsCrew.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class UserExternalIdentityEntityTypeConfiguration : IEntityTypeConfiguration<UserExternalIdentity>
{
    public void Configure(EntityTypeBuilder<UserExternalIdentity> builder)
    {
        builder.ToTable("AppUserExternalIdentity");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProviderSubject)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Email)
            .HasMaxLength(320);

        builder.Property(x => x.DateCreated)
            .IsRequired();

        builder.Property(x => x.DateModified);

        builder.HasIndex(x => new { x.Provider, x.ProviderSubject })
            .IsUnique();

        builder.HasIndex(x => x.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
