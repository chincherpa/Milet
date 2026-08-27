using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Milet.App.Services;
using Milet.Application.Admin;
using Milet.Application.Common;

namespace Milet.App.ViewModels.Admin;

/// <summary>
/// Benutzerverwaltung, Rollen/Rechte und AuditLog-Viewer in einer Seite (Pivot mit drei Tabs) —
/// gleiches "kompakte Settings-Maske" Muster wie <see cref="Milet.App.ViewModels.Stammdaten.KleinstammViewModel"/>.
/// </summary>
public sealed partial class AdministrationViewModel : ObservableObject
{
    private readonly IBenutzerverwaltungService _benutzerService;
    private readonly IRollenverwaltungService _rollenService;
    private readonly IAuditLogService _auditLogService;
    private readonly IDialogService _dialogService;

    public AdministrationViewModel(
        IBenutzerverwaltungService benutzerService,
        IRollenverwaltungService rollenService,
        IAuditLogService auditLogService,
        IDialogService dialogService)
    {
        _benutzerService = benutzerService;
        _rollenService = rollenService;
        _auditLogService = auditLogService;
        _dialogService = dialogService;

        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        await RollenLadenAsync();
        await BenutzerLadenAsync();
        await AuditLogLadenAsync();
    }

    // ---- Benutzer ----

    [ObservableProperty]
    public partial IReadOnlyList<BenutzerDto> BenutzerListe { get; set; } = [];

    [ObservableProperty]
    public partial BenutzerDto? BenutzerAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string BenutzerBenutzername { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BenutzerAnzeigename { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? BenutzerEmail { get; set; }

    [ObservableProperty]
    public partial string? BenutzerNeuesPasswort { get; set; }

    [ObservableProperty]
    public partial int? BenutzerRolleId { get; set; }

    [ObservableProperty]
    public partial bool BenutzerAktiv { get; set; } = true;

    [ObservableProperty]
    public partial string? BenutzerFehler { get; set; }

    partial void OnBenutzerAusgewaehltChanged(BenutzerDto? value)
    {
        BenutzerFehler = null;
        BenutzerNeuesPasswort = null;
        BenutzerBenutzername = value?.Benutzername ?? string.Empty;
        BenutzerAnzeigename = value?.Anzeigename ?? string.Empty;
        BenutzerEmail = value?.Email;
        BenutzerRolleId = value?.RolleId;
        BenutzerAktiv = value?.Aktiv ?? true;
    }

    private async Task BenutzerLadenAsync() => BenutzerListe = await _benutzerService.ListeAsync();

    [RelayCommand]
    private void BenutzerNeu() => BenutzerAusgewaehlt = null;

    [RelayCommand]
    private async Task BenutzerSpeichernAsync()
    {
        BenutzerFehler = null;

        var dto = new BenutzerDto
        {
            Id = BenutzerAusgewaehlt?.Id ?? 0,
            RowVersion = BenutzerAusgewaehlt?.RowVersion ?? [],
            Benutzername = BenutzerBenutzername,
            Anzeigename = BenutzerAnzeigename,
            Email = BenutzerEmail,
            NeuesPasswort = string.IsNullOrWhiteSpace(BenutzerNeuesPasswort) ? null : BenutzerNeuesPasswort,
            RolleId = BenutzerRolleId ?? 0,
            Aktiv = BenutzerAktiv,
        };

        try
        {
            await _benutzerService.SpeichereAsync(dto);
            await BenutzerLadenAsync();
            BenutzerNeu();
        }
        catch (ValidationException ex)
        {
            BenutzerFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (KeinZugriffException ex)
        {
            BenutzerFehler = ex.Message;
        }
        catch (ConcurrencyConflictException)
        {
            BenutzerFehler = "Dieser Benutzer wurde zwischenzeitlich von einem anderen Benutzer geändert. Bitte neu laden.";
        }
        catch (Exception ex)
        {
            BenutzerFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task BenutzerLoeschenAsync()
    {
        if (BenutzerAusgewaehlt is not { } benutzer)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Benutzer löschen", $"Benutzer '{benutzer.Benutzername}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            BenutzerNeu();
            await _benutzerService.LoescheAsync(benutzer.Id);
            await BenutzerLadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- Rollen ----

    [ObservableProperty]
    public partial IReadOnlyList<RolleDto> RollenListe { get; set; } = [];

    [ObservableProperty]
    public partial RolleDto? RolleAusgewaehlt { get; set; }

    [ObservableProperty]
    public partial string RolleName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? RolleBeschreibung { get; set; }

    [ObservableProperty]
    public partial bool RechtStammdaten { get; set; }

    [ObservableProperty]
    public partial bool RechtVerkauf { get; set; }

    [ObservableProperty]
    public partial bool RechtEinkauf { get; set; }

    [ObservableProperty]
    public partial bool RechtLager { get; set; }

    [ObservableProperty]
    public partial bool RechtFinanzen { get; set; }

    [ObservableProperty]
    public partial bool RechtReporting { get; set; }

    [ObservableProperty]
    public partial bool RechtAdministration { get; set; }

    [ObservableProperty]
    public partial string? RolleFehler { get; set; }

    partial void OnRolleAusgewaehltChanged(RolleDto? value)
    {
        RolleFehler = null;
        RolleName = value?.Name ?? string.Empty;
        RolleBeschreibung = value?.Beschreibung;

        var rechte = value?.RechteCodes ?? [];
        RechtStammdaten = rechte.Contains(RechtCodes.Stammdaten);
        RechtVerkauf = rechte.Contains(RechtCodes.Verkauf);
        RechtEinkauf = rechte.Contains(RechtCodes.Einkauf);
        RechtLager = rechte.Contains(RechtCodes.Lager);
        RechtFinanzen = rechte.Contains(RechtCodes.Finanzen);
        RechtReporting = rechte.Contains(RechtCodes.Reporting);
        RechtAdministration = rechte.Contains(RechtCodes.Administration);
    }

    private async Task RollenLadenAsync() => RollenListe = await _rollenService.ListeAsync();

    [RelayCommand]
    private void RolleNeu() => RolleAusgewaehlt = null;

    [RelayCommand]
    private async Task RolleSpeichernAsync()
    {
        RolleFehler = null;

        var rechte = new List<string>();
        if (RechtStammdaten) rechte.Add(RechtCodes.Stammdaten);
        if (RechtVerkauf) rechte.Add(RechtCodes.Verkauf);
        if (RechtEinkauf) rechte.Add(RechtCodes.Einkauf);
        if (RechtLager) rechte.Add(RechtCodes.Lager);
        if (RechtFinanzen) rechte.Add(RechtCodes.Finanzen);
        if (RechtReporting) rechte.Add(RechtCodes.Reporting);
        if (RechtAdministration) rechte.Add(RechtCodes.Administration);

        var dto = new RolleDto
        {
            Id = RolleAusgewaehlt?.Id ?? 0,
            RowVersion = RolleAusgewaehlt?.RowVersion ?? [],
            Name = RolleName,
            Beschreibung = RolleBeschreibung,
            RechteCodes = rechte,
        };

        try
        {
            await _rollenService.SpeichereAsync(dto);
            await RollenLadenAsync();
            RolleNeu();
        }
        catch (ValidationException ex)
        {
            RolleFehler = string.Join(Environment.NewLine, ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (KeinZugriffException ex)
        {
            RolleFehler = ex.Message;
        }
        catch (ConcurrencyConflictException)
        {
            RolleFehler = "Diese Rolle wurde zwischenzeitlich von einem anderen Benutzer geändert. Bitte neu laden.";
        }
        catch (Exception ex)
        {
            RolleFehler = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RolleLoeschenAsync()
    {
        if (RolleAusgewaehlt is not { } rolle)
        {
            return;
        }

        var bestaetigt = await _dialogService.BestaetigenAsync("Rolle löschen", $"Rolle '{rolle.Name}' wirklich löschen?");
        if (!bestaetigt)
        {
            return;
        }

        try
        {
            RolleNeu();
            await _rollenService.LoescheAsync(rolle.Id);
            await RollenLadenAsync();
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Löschen", ex.Message);
        }
    }

    // ---- AuditLog ----

    [ObservableProperty]
    public partial IReadOnlyList<AuditLogDto> AuditLogListe { get; set; } = [];

    [ObservableProperty]
    public partial string? AuditFilterEntityName { get; set; }

    [RelayCommand]
    private async Task AuditLogLadenAsync()
    {
        var filter = string.IsNullOrWhiteSpace(AuditFilterEntityName)
            ? null
            : new AuditLogFilterDto { EntityName = AuditFilterEntityName.Trim() };

        try
        {
            AuditLogListe = await _auditLogService.ListeAsync(filter);
        }
        catch (Exception ex)
        {
            await _dialogService.ZeigeFehlerAsync("Fehler beim Laden des AuditLog", ex.Message);
        }
    }
}
