namespace Milet.Application.Abstractions;

/// <summary>
/// Liefert das Fenster-Handle des Hauptfensters — benötigt vom MSAL/WAM-Broker für interaktives Sign-In
/// (WithParentActivityOrWindow). In Milet.App durch die echte WinUI-Fensterimplementierung ersetzt;
/// ein No-Op-Fallback (IntPtr.Zero) genügt, solange kein interaktiver Login ausgelöst wird.
/// </summary>
public interface IWindowHandleProvider
{
    IntPtr GetHandle();
}
