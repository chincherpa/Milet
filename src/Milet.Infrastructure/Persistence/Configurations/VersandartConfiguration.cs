using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Stammdaten;

namespace Nexus.Infrastructure.Persistence.Configurations;

public sealed class VersandartConfiguration : IEntityTypeConfiguration<Versandart>
{
    public void Configure(EntityTypeBuilder<Versandart> b)
    {
        b.ToTable("Versandarten");
        b.HasKey(v => v.Id);
        b.Property(v => v.Bezeichnung).HasMaxLength(100).IsRequired();
        b.Property(v => v.Kosten).HasPrecision(18, 2);
    }
}
