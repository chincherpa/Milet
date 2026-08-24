using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Infrastructure.Persistence.Configurations;

public sealed class PreislisteConfiguration : IEntityTypeConfiguration<Preisliste>
{
    public void Configure(EntityTypeBuilder<Preisliste> b)
    {
        b.ToTable("Preislisten");
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).HasMaxLength(100).IsRequired();
    }
}

public sealed class ArtikelPreisConfiguration : IEntityTypeConfiguration<ArtikelPreis>
{
    public void Configure(EntityTypeBuilder<ArtikelPreis> b)
    {
        b.ToTable("ArtikelPreise");
        b.HasKey(p => p.Id);
        b.Property(p => p.AbMenge).HasPrecision(18, 3);
        b.Property(p => p.Preis).HasPrecision(18, 4);

        b.HasOne(p => p.Preisliste).WithMany(pl => pl.Preise).HasForeignKey(p => p.PreislisteId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(p => p.Artikel).WithMany().HasForeignKey(p => p.ArtikelId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(p => new { p.PreislisteId, p.ArtikelId, p.AbMenge }).IsUnique();
    }
}
