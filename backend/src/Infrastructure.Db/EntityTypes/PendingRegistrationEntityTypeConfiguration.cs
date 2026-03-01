using AzureOpsCrew.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class PendingRegistrationEntityTypeConfiguration : IEntityTypeConfiguration<PendingRegistration>
{
    public void Configure(EntityTypeBuilder<PendingRegistration> builder)
    {
        builder.ToTable("PendingRegistration");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(x => x.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(320);

        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique();

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.VerificationCodeHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.VerificationCodeExpiresAt)
            .IsRequired();

        builder.Property(x => x.VerificationCodeSentAt)
            .IsRequired();

        builder.Property(x => x.VerificationAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.DateCreated)
            .IsRequired();

        builder.Property(x => x.DateModified);
    }
}
