using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Milet.Application.Abstractions;
using Milet.Application.Admin;

namespace Milet.App.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICurrentSessionService _session;
    private readonly ISchemaVersionService _schemaVersionService;

    public LoginViewModel(IAuthService authService, ICurrentSessionService session, ISchemaVersionService schemaVersionService)
    {
        _authService = authService;
        _session = session;
        _schemaVersionService = schemaVersionService;
        _ = SchemaPruefenAsync();
    }

    public event Action? AngemeldetErfolgreich;

    [ObservableProperty]
    public partial string Benutzername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Passwort { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Fehlermeldung { get; set; }

    [ObservableProperty]
    public partial bool AnmeldungLaeuft { get; set; }

    [ObservableProperty]
    public partial bool SchemaAktuell { get; set; } = true;

    // ---- Erzwungener Passwortwechsel ----
    // Zweite Stufe im selben Fenster statt eines eigenen Dialogs: der Benutzer ist zu diesem Zeitpunkt noch
    // nicht angemeldet (die Sitzung wird erst nach dem Wechsel geöffnet), ein ContentDialog bräuchte aber ein
    // XamlRoot aus einem bereits sichtbaren Fenster.

    [ObservableProperty]
    public partial bool PasswortwechselErforderlich { get; set; }

    [ObservableProperty]
    public partial string NeuesPasswort { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NeuesPasswortWiederholung { get; set; } = string.Empty;

    /// <summary>Zwischengeparkt, bis der Wechsel durch ist — danach wird damit die Sitzung geöffnet.</summary>
    private BenutzerSessionDto? _wartendeSession;

    private async Task SchemaPruefenAsync()
    {
        try
        {
            SchemaAktuell = await _schemaVersionService.IstAktuellAsync();
            if (!SchemaAktuell)
            {
                Fehlermeldung = "Das Datenbankschema ist nicht aktuell. Bitte zuerst Milet.Tools.Migrator ausführen.";
            }
        }
        catch (Exception ex)
        {
            SchemaAktuell = false;
            Fehlermeldung = $"Datenbank nicht erreichbar: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AnmeldenAsync()
    {
        if (!SchemaAktuell)
        {
            return;
        }

        Fehlermeldung = null;
        AnmeldungLaeuft = true;
        try
        {
            var ergebnis = await _authService.AnmeldenAsync(Benutzername, Passwort);

            if (ergebnis.GesperrtBis is { } gesperrtBis)
            {
                // Nur bei korrektem Passwort — der Dienst legt die Sperre sonst gar nicht offen.
                Fehlermeldung = $"Zugang wegen zu vieler Fehlversuche gesperrt bis {gesperrtBis:HH:mm}.";
                return;
            }

            if (ergebnis.Session is not { } session)
            {
                Fehlermeldung = "Benutzername oder Passwort falsch, oder Benutzer ist deaktiviert.";
                return;
            }

            if (ergebnis.PasswortAenderungErforderlich)
            {
                _wartendeSession = session;
                PasswortwechselErforderlich = true;
                Fehlermeldung = "Das Passwort muss vor der ersten Nutzung geändert werden.";
                return;
            }

            SitzungOeffnen(session);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            AnmeldungLaeuft = false;
        }
    }

    [RelayCommand]
    private async Task PasswortWechselnAsync()
    {
        if (_wartendeSession is not { } session)
        {
            return;
        }

        Fehlermeldung = null;

        if (!string.Equals(NeuesPasswort, NeuesPasswortWiederholung, StringComparison.Ordinal))
        {
            Fehlermeldung = "Die beiden Eingaben stimmen nicht überein.";
            return;
        }

        AnmeldungLaeuft = true;
        try
        {
            await _authService.PasswortAendernAsync(Benutzername, Passwort, NeuesPasswort);
            SitzungOeffnen(session);
        }
        catch (Exception ex)
        {
            Fehlermeldung = ex.Message;
        }
        finally
        {
            AnmeldungLaeuft = false;
        }
    }

    private void SitzungOeffnen(BenutzerSessionDto session)
    {
        _session.Anmelden(session.BenutzerId, session.BenutzerName, session.RollenName, session.Rechte);
        AngemeldetErfolgreich?.Invoke();
    }
}
