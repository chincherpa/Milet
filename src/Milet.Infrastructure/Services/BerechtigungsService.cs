using Milet.Application.Abstractions;
using Milet.Application.Common;

namespace Milet.Infrastructure.Services;

public sealed class BerechtigungsService(ICurrentSessionService session) : IBerechtigungsService
{
    public bool HatRecht(string rechtCode) => session.HatRecht(rechtCode);

    public void PruefeRecht(string rechtCode)
    {
        if (!session.HatRecht(rechtCode))
        {
            throw new KeinZugriffException(rechtCode);
        }
    }
}
