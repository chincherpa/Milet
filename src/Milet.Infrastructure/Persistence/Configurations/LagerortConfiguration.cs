using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class LagerortConfiguration : IEntityTypeConfiguration<Lagerort>
{
    public void Configure(EntityTypeBuilder<Lagerort> b)
    {
        b.ToTable("Lagerorte");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
