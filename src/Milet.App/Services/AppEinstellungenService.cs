using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Milet.App.Services;

/// <summary>Benutzerbezogene UI-Einstellungen. Bewusst getrennt von <c>appsettings.json</c>, das neben der
/// Exe liegt und maschinenweite Konfiguration (Verbindungszeichenfolge, Logging) trägt.</summary>
public sealed record AppEinstellungen
{
    /// <summary>Serialisierter <c>ElementTheme</c>: "Default" (Systemvorgabe), "Light" oder "Dark".</summary>
    public string Theme { get; init; } = "Default";
}

public interface IAppEinstellungenService
{
    AppEinstellungen Laden();

    void Speichern(AppEinstellungen einstellungen);
}

/// <summary>Persistiert die UI-Einstellungen als JSON unter
/// <c>%LOCALAPPDATA%\Milet\ui-einstellungen.json</c>.
///
/// Nicht über <c>ApplicationData.Current</c>: die App ist unpackaged (<c>WindowsPackageType=None</c>),
/// diese API steht ohne Paketidentität nicht zur Verfügung. Die Einstellung gilt damit pro
/// Windows-Benutzer und Rechner, nicht pro Milet-Benutzer.
///
/// Grundsatz: Die Darstellung ist eine Bequemlichkeit — ein Lese- oder Schreibfehler darf die App
/// niemals am Starten oder Arbeiten hindern. Alle Fehler werden geloggt und geschluckt.</summary>
public sealed class AppEinstellungenService : IAppEinstellungenService
{
    private static readonly JsonSerializerOptions JsonOptionen = new() { WriteIndented = true };

    private readonly ILogger<AppEinstellungenService> _logger;
    private readonly string _dateipfad;

    public AppEinstellungenService(ILogger<AppEinstellungenService> logger)
    {
        _logger = logger;
        _dateipfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Milet",
            "ui-einstellungen.json");
    }

    public AppEinstellungen Laden()
    {
        try
        {
            if (!File.Exists(_dateipfad))
            {
                return new AppEinstellungen();
            }

            var json = File.ReadAllText(_dateipfad);
            return JsonSerializer.Deserialize<AppEinstellungen>(json) ?? new AppEinstellungen();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "UI-Einstellungen aus {Pfad} nicht lesbar — Standardwerte werden verwendet.", _dateipfad);
            return new AppEinstellungen();
        }
    }

    public void Speichern(AppEinstellungen einstellungen)
    {
        try
        {
            var verzeichnis = Path.GetDirectoryName(_dateipfad)!;
            Directory.CreateDirectory(verzeichnis);

            // Erst vollständig in eine Nebendatei schreiben, dann atomar ersetzen — ein Absturz mitten im
            // Schreiben hinterlässt sonst eine halbe Datei, die beim nächsten Start nicht mehr lesbar ist.
            var temporaer = _dateipfad + ".tmp";
            File.WriteAllText(temporaer, JsonSerializer.Serialize(einstellungen, JsonOptionen));
            File.Move(temporaer, _dateipfad, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Die Änderung wirkt für diese Sitzung trotzdem, sie übersteht nur keinen Neustart.
            _logger.LogWarning(ex, "UI-Einstellungen konnten nicht nach {Pfad} geschrieben werden.", _dateipfad);
        }
    }
}
