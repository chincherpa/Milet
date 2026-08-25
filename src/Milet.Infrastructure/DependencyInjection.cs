using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milet.Application.Abstractions;
using Milet.Application.Admin;
using Milet.Application.Stammdaten;
using Milet.Application.Verkauf;
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

        return services;
    }
}
