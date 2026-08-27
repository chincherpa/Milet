using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Einkauf;
using Milet.Application.Finanzen;
using Milet.Application.Lager;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
using Milet.Infrastructure.Email;
using Milet.Infrastructure.Persistence;
using Milet.Infrastructure.Persistence.Interceptors;
using Milet.Infrastructure.Services;

namespace Milet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Milet")
            ?? throw new InvalidOperationException("ConnectionStrings:Milet fehlt in der Konfiguration.");

        services.AddSingleton<ICurrentUserService, SystemCurrentUserService>();
        services.AddSingleton<AuditSaveChangesInterceptor>();
        services.AddSingleton<BelegImmutabilityInterceptor>();

        services.AddDbContextFactory<MiletDbContext>((sp, options) =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(
                    sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                    sp.GetRequiredService<BelegImmutabilityInterceptor>()));

        services.AddScoped<INumberRangeService, NumberRangeService>();

        services.AddScoped<IKundenService, KundenService>();
        services.AddScoped<ILieferantenService, LieferantenService>();
        services.AddScoped<IArtikelService, ArtikelService>();
        services.AddScoped<IStammdatenLookupService, StammdatenLookupService>();

        services.AddScoped<IEinheitenService, EinheitenService>();
        services.AddScoped<IMwStSaetzeService, MwStSaetzeService>();
        services.AddScoped<IZahlungsbedingungenService, ZahlungsbedingungenService>();
        services.AddScoped<IVersandartenService, VersandartenService>();
        services.AddScoped<IPreislistenService, PreislistenService>();
        services.AddScoped<IArtikelPreiseService, ArtikelPreiseService>();

        services.AddScoped<IBelegService, BelegService>();
        services.AddScoped<IVerkaufLookupService, VerkaufLookupService>();
        services.AddScoped<IFirmenstammService, FirmenstammService>();
        services.AddScoped<IBelegUeberleitungService, BelegUeberleitungService>();
        services.AddScoped<IRechnungBuchenService, RechnungBuchenService>();
        services.AddScoped<IPdfService, Pdf.PdfService>();
        services.AddScoped<IBestandService, BestandService>();
        services.AddScoped<ILagerortService, LagerortService>();
        services.AddScoped<ISeriennummernService, SeriennummernService>();
        services.AddScoped<ILieferscheinBuchenService, LieferscheinBuchenService>();
        services.AddScoped<IInventurService, InventurService>();

        services.AddScoped<IEinkaufLookupService, EinkaufLookupService>();
        services.AddScoped<IBestellVorschlagService, BestellVorschlagService>();
        services.AddScoped<IWareneingangBuchenService, WareneingangBuchenService>();
        services.AddScoped<IEingangsrechnungBuchenService, EingangsrechnungBuchenService>();

        services.AddScoped<IOffenePostenService, OffenePostenService>();
        services.AddScoped<IZahlungService, ZahlungService>();
        services.AddScoped<IMahnwesenService, MahnwesenService>();

        // E-Mail-Versand: nur registriert, wenn appsettings.json eine vollständige "Graph"-Sektion trägt —
        // sonst NichtKonfigurierterEmailService (wirft beim Versandversuch eine sprechende Exception,
        // blockiert aber nie Buchen/PDF/Drucken). Milet.App überschreibt IWindowHandleProvider mit der
        // echten WinUI-Fensterimplementierung (Registrierung nach AddInfrastructure gewinnt).
        services.AddSingleton<IWindowHandleProvider, NullWindowHandleProvider>();

        var graphSection = configuration.GetSection(GraphSettings.SectionName);
        var graphSettings = graphSection.Get<GraphSettings>();
        if (graphSection.Exists() && graphSettings is { ClientId.Length: > 0, TenantId.Length: > 0, RedirectUri.Length: > 0 })
        {
            services.AddSingleton(sp => PublicClientApplicationBuilder.Create(graphSettings.ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, graphSettings.TenantId)
                .WithRedirectUri(graphSettings.RedirectUri)
                .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
                .Build());
            services.AddScoped<IEmailService, GraphEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, NichtKonfigurierterEmailService>();
        }

        services.AddScoped<IEmailVersandService, EmailVersandService>();

        return services;
    }
}
