using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Abstractions;
using Nexus.Application.Stammdaten;
using Nexus.Infrastructure.Persistence;
using Nexus.Infrastructure.Persistence.Interceptors;
using Nexus.Infrastructure.Services;

namespace Nexus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Nexus")
            ?? throw new InvalidOperationException("ConnectionStrings:Nexus fehlt in der Konfiguration.");

        services.AddSingleton<ICurrentUserService, SystemCurrentUserService>();
        services.AddSingleton<AuditSaveChangesInterceptor>();

        services.AddDbContextFactory<NexusDbContext>((sp, options) =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddScoped<INumberRangeService, NumberRangeService>();

        services.AddScoped<IKundenService, KundenService>();
        services.AddScoped<ILieferantenService, LieferantenService>();
        services.AddScoped<IArtikelService, ArtikelService>();
        services.AddScoped<IStammdatenLookupService, StammdatenLookupService>();

        return services;
    }
}
