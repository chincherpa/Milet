using Microsoft.EntityFrameworkCore;
using Milet.Application.Abstractions;
using Milet.Infrastructure.Persistence;

namespace Milet.Infrastructure.Services;

public sealed class SchemaVersionService(IDbContextFactory<MiletDbContext> dbContextFactory) : ISchemaVersionService
{
    public async Task<bool> IstAktuellAsync(CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var ausstehend = await db.Database.GetPendingMigrationsAsync(ct);
        return !ausstehend.Any();
    }
}
