using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Finanzen;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class MahnstufeConfiguration : IEntityTypeConfiguration<Mahnstufe>
{
    public void Configure(EntityTypeBuilder<Mahnstufe> b)
    {
        b.ToTable("Mahnstufen");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Stufe).IsUnique();
        b.Property(x => x.Gebuehr).HasPrecision(18, 2);
        b.Property(x => x.Mahntext).HasMaxLength(2000);
    }
}
