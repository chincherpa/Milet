using FluentValidation;

namespace Milet.Application.Gaertnerei;

public sealed class KulturstufeValidator : AbstractValidator<KulturstufeDto>
{
    public KulturstufeValidator()
    {
        RuleFor(k => k.Code).NotEmpty().MaximumLength(10);
        RuleFor(k => k.Bezeichnung).NotEmpty().MaximumLength(50);
        RuleFor(k => k.Reihenfolge).GreaterThan(0);
        RuleFor(k => k.FarbeHex).Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Farbe muss im Format #RRGGBB angegeben werden.");
    }
}

public sealed class GaertnereiplanValidator : AbstractValidator<GaertnereiplanDto>
{
    public GaertnereiplanValidator()
    {
        RuleFor(p => p.Bezeichnung).NotEmpty().MaximumLength(100);
        RuleFor(p => p.BreiteMeter).GreaterThan(0);
        RuleFor(p => p.HoeheMeter).GreaterThan(0);
    }
}

public sealed class FeldValidator : AbstractValidator<FeldDto>
{
    public FeldValidator()
    {
        RuleFor(f => f.Code).NotEmpty().MaximumLength(10);
        RuleFor(f => f.Bezeichnung).NotEmpty().MaximumLength(100);
        RuleFor(f => f.BreiteMeter).GreaterThan(0);
        RuleFor(f => f.HoeheMeter).GreaterThan(0);
    }
}

public sealed class SektionValidator : AbstractValidator<SektionDto>
{
    public SektionValidator()
    {
        RuleFor(s => s.LagerortId).GreaterThan(0);
        RuleFor(s => s.Code).NotEmpty().MaximumLength(10);
        RuleFor(s => s.Bezeichnung).NotEmpty().MaximumLength(100);
        RuleFor(s => s.BreiteMeter).GreaterThan(0);
        RuleFor(s => s.HoeheMeter).GreaterThan(0);
    }
}

public sealed class KulturZugangValidator : AbstractValidator<KulturZugangDto>
{
    public KulturZugangValidator()
    {
        RuleFor(d => d.ArtikelId).GreaterThan(0);
        RuleFor(d => d.FeldId).GreaterThan(0);
        RuleFor(d => d.KulturstufeId).GreaterThan(0);
        RuleFor(d => d.Menge).GreaterThan(0);
    }
}

public sealed class StufenwechselValidator : AbstractValidator<StufenwechselDto>
{
    public StufenwechselValidator()
    {
        RuleFor(d => d.ArtikelId).GreaterThan(0);
        RuleFor(d => d.VonFeldId).GreaterThan(0);
        RuleFor(d => d.VonKulturstufeId).GreaterThan(0);
        RuleFor(d => d.NachFeldId).GreaterThan(0);
        RuleFor(d => d.NachKulturstufeId).GreaterThan(0);
        RuleFor(d => d.Menge).GreaterThan(0);
    }
}

public sealed class UmsetzenValidator : AbstractValidator<UmsetzenDto>
{
    public UmsetzenValidator()
    {
        RuleFor(d => d.ArtikelId).GreaterThan(0);
        RuleFor(d => d.VonFeldId).GreaterThan(0);
        RuleFor(d => d.NachFeldId).GreaterThan(0);
        RuleFor(d => d.KulturstufeId).GreaterThan(0);
        RuleFor(d => d.Menge).GreaterThan(0);
    }
}

public sealed class AusfallValidator : AbstractValidator<AusfallDto>
{
    public AusfallValidator()
    {
        RuleFor(d => d.ArtikelId).GreaterThan(0);
        RuleFor(d => d.FeldId).GreaterThan(0);
        RuleFor(d => d.KulturstufeId).GreaterThan(0);
        RuleFor(d => d.Menge).GreaterThan(0);
    }
}
