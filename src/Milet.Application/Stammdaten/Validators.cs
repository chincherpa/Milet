using FluentValidation;

namespace Milet.Application.Stammdaten;

public sealed class AdresseValidator : AbstractValidator<AdresseDto>
{
    public AdresseValidator()
    {
        RuleFor(a => a.Name1).NotEmpty().WithMessage("Name ist erforderlich.").MaximumLength(100);
        RuleFor(a => a.Name2).MaximumLength(100);
        RuleFor(a => a.Strasse).MaximumLength(100);
        RuleFor(a => a.Plz).MaximumLength(10);
        RuleFor(a => a.Ort).MaximumLength(100);
        RuleFor(a => a.Land).NotEmpty().Length(2).WithMessage("Land als ISO-Code (z. B. DE).");
    }
}

public sealed class KundeValidator : AbstractValidator<KundeDto>
{
    public KundeValidator()
    {
        RuleFor(k => k.Adresse).SetValidator(new AdresseValidator());
        RuleFor(k => k.Email).EmailAddress().When(k => !string.IsNullOrWhiteSpace(k.Email));
        RuleFor(k => k.EmailRechnung).EmailAddress().When(k => !string.IsNullOrWhiteSpace(k.EmailRechnung));
        RuleFor(k => k.RabattProzent).InclusiveBetween(0, 100);
        RuleFor(k => k.Kreditlimit).GreaterThanOrEqualTo(0).When(k => k.Kreditlimit.HasValue);
    }
}

public sealed class LieferantValidator : AbstractValidator<LieferantDto>
{
    public LieferantValidator()
    {
        RuleFor(l => l.Adresse).SetValidator(new AdresseValidator());
        RuleFor(l => l.Email).EmailAddress().When(l => !string.IsNullOrWhiteSpace(l.Email));
    }
}

public sealed class ArtikelValidator : AbstractValidator<ArtikelDto>
{
    public ArtikelValidator()
    {
        RuleFor(a => a.Bezeichnung).NotEmpty().WithMessage("Bezeichnung ist erforderlich.").MaximumLength(200);
        RuleFor(a => a.EinheitId).GreaterThan(0).WithMessage("Einheit wählen.");
        RuleFor(a => a.MwStSatzId).GreaterThan(0).WithMessage("MwSt-Satz wählen.");
        RuleFor(a => a.Einkaufspreis).GreaterThanOrEqualTo(0);
        RuleFor(a => a.Listenpreis).GreaterThanOrEqualTo(0);
        RuleFor(a => a.Mindestbestand).GreaterThanOrEqualTo(0).When(a => a.Mindestbestand.HasValue);
    }
}

public sealed class EinheitValidator : AbstractValidator<EinheitDto>
{
    public EinheitValidator()
    {
        RuleFor(e => e.Kuerzel).NotEmpty().WithMessage("Kürzel ist erforderlich.").MaximumLength(10);
        RuleFor(e => e.Bezeichnung).NotEmpty().WithMessage("Bezeichnung ist erforderlich.").MaximumLength(100);
        RuleFor(e => e.NachkommaStellen).InclusiveBetween(0, 4);
    }
}

public sealed class MwStSatzValidator : AbstractValidator<MwStSatzDto>
{
    public MwStSatzValidator()
    {
        RuleFor(m => m.Bezeichnung).NotEmpty().WithMessage("Bezeichnung ist erforderlich.").MaximumLength(100);
        RuleFor(m => m.Satz).InclusiveBetween(0, 100);
    }
}

public sealed class ZahlungsbedingungValidator : AbstractValidator<ZahlungsbedingungDto>
{
    public ZahlungsbedingungValidator()
    {
        RuleFor(z => z.Bezeichnung).NotEmpty().WithMessage("Bezeichnung ist erforderlich.").MaximumLength(100);
        RuleFor(z => z.ZielTage).GreaterThanOrEqualTo(0);
        RuleFor(z => z.SkontoTage).GreaterThanOrEqualTo(0).When(z => z.SkontoTage.HasValue);
        RuleFor(z => z.SkontoProzent).InclusiveBetween(0, 100).When(z => z.SkontoProzent.HasValue);
    }
}

public sealed class VersandartValidator : AbstractValidator<VersandartDto>
{
    public VersandartValidator()
    {
        RuleFor(v => v.Bezeichnung).NotEmpty().WithMessage("Bezeichnung ist erforderlich.").MaximumLength(100);
        RuleFor(v => v.Kosten).GreaterThanOrEqualTo(0).When(v => v.Kosten.HasValue);
    }
}

public sealed class PreislisteValidator : AbstractValidator<PreislisteDto>
{
    public PreislisteValidator()
    {
        RuleFor(p => p.Name).NotEmpty().WithMessage("Name ist erforderlich.").MaximumLength(100);
    }
}

public sealed class ArtikelPreisValidator : AbstractValidator<ArtikelPreisDto>
{
    public ArtikelPreisValidator()
    {
        RuleFor(p => p.PreislisteId).GreaterThan(0);
        RuleFor(p => p.ArtikelId).GreaterThan(0).WithMessage("Artikel wählen.");
        RuleFor(p => p.AbMenge).GreaterThan(0).WithMessage("Ab-Menge muss größer 0 sein.");
        RuleFor(p => p.Preis).GreaterThanOrEqualTo(0);
    }
}
