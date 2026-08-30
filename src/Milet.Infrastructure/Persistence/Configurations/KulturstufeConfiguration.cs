using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Gaertnerei;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class KulturstufeConfiguration : IEntityTypeConfiguration<Kulturstufe>
{
    public void Configure(EntityTypeBuilder<Kulturstufe> b)
    {
        b.ToTable("Kulturstufen");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Bezeichnung).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Reihenfolge).IsUnique();
        b.Property(x => x.FarbeHex).HasMaxLength(7).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
