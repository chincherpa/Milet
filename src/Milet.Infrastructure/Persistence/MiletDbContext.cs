using Microsoft.EntityFrameworkCore;
using Milet.Domain.Entities.Admin;
using Milet.Domain.Entities.Stammdaten;

namespace Milet.Infrastructure.Persistence;

public sealed class MiletDbContext(DbContextOptions<MiletDbContext> options) : DbContext(options)
{
    public DbSet<Einheit> Einheiten => Set<Einheit>();

    public DbSet<MwStSatz> MwStSaetze => Set<MwStSatz>();

    public DbSet<Zahlungsbedingung> Zahlungsbedingungen => Set<Zahlungsbedingung>();

    public DbSet<Versandart> Versandarten => Set<Versandart>();

    public DbSet<Kunde> Kunden => Set<Kunde>();

    public DbSet<Lieferant> Lieferanten => Set<Lieferant>();

    public DbSet<Artikel> Artikel => Set<Artikel>();

    public DbSet<Preisliste> Preislisten => Set<Preisliste>();

    public DbSet<ArtikelPreis> ArtikelPreise => Set<ArtikelPreis>();

    public DbSet<Nummernkreis> Nummernkreise => Set<Nummernkreis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiletDbContext).Assembly);
    }
}
