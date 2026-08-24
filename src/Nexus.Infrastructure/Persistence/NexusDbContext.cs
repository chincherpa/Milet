using Microsoft.EntityFrameworkCore;
using Nexus.Domain.Entities.Admin;
using Nexus.Domain.Entities.Stammdaten;

namespace Nexus.Infrastructure.Persistence;

public sealed class NexusDbContext(DbContextOptions<NexusDbContext> options) : DbContext(options)
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NexusDbContext).Assembly);
    }
}
