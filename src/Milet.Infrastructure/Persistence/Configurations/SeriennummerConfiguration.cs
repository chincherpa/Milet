using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Lager;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class SeriennummerConfiguration : IEntityTypeConfiguration<Seriennummer>
{
    public void Configure(EntityTypeBuilder<Seriennummer> b)
    {
        b.ToTable("Seriennummern");
        b.HasKey(x => x.Id);
        b.Property(x => x.Nummer).HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.ArtikelId, x.Nummer }).IsUnique();

        b.HasOne(x => x.Artikel).WithMany().HasForeignKey(x => x.ArtikelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Lagerort).WithMany().HasForeignKey(x => x.LagerortId).OnDelete(DeleteBehavior.Restrict);

        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
