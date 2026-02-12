using AzureOpsCrew.Domain.Dimmies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes
{
    public class DummyEntityTypeConfiguration : IEntityTypeConfiguration<Dummy>
    {
        public void Configure(EntityTypeBuilder<Dummy> builder)
        {
            builder.ToContainer(nameof(Dummy));
            builder.HasKey(e => e.Id);
        }
    }
}
