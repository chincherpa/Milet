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
            var session = await _authService.AnmeldenAsync(Benutzername, Passwort);
            if (session is null)
            {
                Fehlermeldung = "Benutzername oder Passwort falsch, oder Benutzer ist deaktiviert.";
                return;
            }

            _session.Anmelden(session.BenutzerId, session.BenutzerName, session.RollenName, session.Rechte);
            AngemeldetErfolgreich?.Invoke();
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
}
