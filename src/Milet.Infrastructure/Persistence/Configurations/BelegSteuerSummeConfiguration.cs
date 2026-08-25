using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class BelegSteuerSummeConfiguration : IEntityTypeConfiguration<BelegSteuerSumme>
{
    public void Configure(EntityTypeBuilder<BelegSteuerSumme> b)
    {
        b.ToTable("BelegSteuerSummen");
        b.HasKey(x => x.Id);
        b.Property(x => x.MwStSatzWert).HasPrecision(5, 2);
        b.Property(x => x.NettoSumme).HasPrecision(18, 2);
        b.Property(x => x.MwStBetrag).HasPrecision(18, 2);
    }
}
