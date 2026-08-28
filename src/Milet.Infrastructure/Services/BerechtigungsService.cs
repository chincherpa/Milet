using Milet.Application.Abstractions;
using Milet.Application.Common;

namespace Milet.Infrastructure.Services;

public sealed class BerechtigungsService(ICurrentSessionService session) : IBerechtigungsService
{
    // Vor einem Login (IstAngemeldet == false) läuft der Prozess als "System" — das betrifft ausschließlich
    // vertrauenswürdige Hintergrund-/Tooling-Kontexte wie Milet.Tools.Migrator (StammdatenSeed/DummyDatenSeed
    // rufen dieselben guarded Services wie die interaktive App auf, aber ohne vorherigen Login), nie die WinUI-App:
    // dort blockiert App.xaml.cs jede Navigation zu einer geschützten Seite bis LoginWindow erfolgreich war,
    // dieser Bypass wird von echten interaktiven Benutzern also nie erreicht.
    public bool HatRecht(string rechtCode) => !session.IstAngemeldet || session.HatRecht(rechtCode);

    public void PruefeRecht(string rechtCode)
    {
        if (!HatRecht(rechtCode))
        {
            throw new KeinZugriffException(rechtCode);
        }
    }
}
