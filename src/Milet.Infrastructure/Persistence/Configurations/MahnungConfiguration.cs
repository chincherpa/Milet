using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Finanzen;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class MahnungConfiguration : IEntityTypeConfiguration<Mahnung>
{
    public void Configure(EntityTypeBuilder<Mahnung> b)
    {
        b.ToTable("Mahnungen");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Kunde).WithMany().HasForeignKey(x => x.KundeId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Positionen).WithOne(p => p.Mahnung).HasForeignKey(p => p.MahnungId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Gebuehr).HasPrecision(18, 2);
        b.Property(x => x.Gesamtbetrag).HasPrecision(18, 2);
    }
}

public sealed class MahnungPositionConfiguration : IEntityTypeConfiguration<MahnungPosition>
{
    public void Configure(EntityTypeBuilder<MahnungPosition> b)
    {
        b.ToTable("MahnungPositionen");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.OffenerPosten).WithMany().HasForeignKey(x => x.OffenerPostenId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.BelegNummerSnapshot).HasMaxLength(50).IsRequired();
        b.Property(x => x.OffenerBetragSnapshot).HasPrecision(18, 2);
    }
}
