using FluentValidation;

namespace Milet.Application.Finanzen;

public sealed class ZahlungZuordnungValidator : AbstractValidator<ZahlungZuordnungDto>
{
    public ZahlungZuordnungValidator()
    {
        RuleFor(z => z.OffenerPostenId).GreaterThan(0);
        RuleFor(z => z.Betrag).GreaterThanOrEqualTo(0);
        RuleFor(z => z.SkontoBetrag).GreaterThanOrEqualTo(0);
        RuleFor(z => z).Must(z => z.Betrag + z.SkontoBetrag > 0)
            .WithMessage("Betrag oder Skonto muss größer 0 sein.");
    }
}

public sealed class ZahlungValidator : AbstractValidator<ZahlungDto>
{
    public ZahlungValidator()
    {
        RuleFor(z => z.KundeId).NotNull().GreaterThan(0).WithMessage("Kunde ist erforderlich.")
            .When(z => z.Typ == Domain.Entities.Finanzen.OffenerPostenTyp.Debitor);
        RuleFor(z => z.LieferantId).NotNull().GreaterThan(0).WithMessage("Lieferant ist erforderlich.")
            .When(z => z.Typ == Domain.Entities.Finanzen.OffenerPostenTyp.Kreditor);
        RuleFor(z => z.Zahlungsdatum).NotEqual(default(DateOnly))
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today)).WithMessage("Zahlungsdatum darf nicht in der Zukunft liegen.");
        RuleFor(z => z.Zuordnungen).NotEmpty().WithMessage("Mindestens eine Zuordnung zu einem offenen Posten erforderlich.");
        RuleForEach(z => z.Zuordnungen).SetValidator(new ZahlungZuordnungValidator());
    }
}
