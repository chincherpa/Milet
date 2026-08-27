using FluentValidation;
using Milet.Domain.Entities.Verkauf;

namespace Milet.Application.Verkauf;

public sealed class BelegPositionValidator : AbstractValidator<BelegPositionDto>
{
    public BelegPositionValidator()
    {
        RuleFor(p => p.Menge).GreaterThan(0);
        RuleFor(p => p.Einzelpreis).GreaterThanOrEqualTo(0);
        RuleFor(p => p.RabattProzent).InclusiveBetween(0, 100);
        RuleFor(p => p.Bezeichnung).NotEmpty().MaximumLength(200);
        RuleFor(p => p.ArtikelId).NotNull().When(p => p.PositionsTyp == PositionsTyp.Artikel);
    }
}

public sealed class BelegValidator : AbstractValidator<BelegDto>
{
    public BelegValidator()
    {
        RuleFor(b => b.KundeId).GreaterThan(0).WithMessage("Kunde ist erforderlich.")
            .When(b => !b.BelegTyp.IstEinkaufsBeleg());
        RuleFor(b => b.LieferantId).NotNull().GreaterThan(0).WithMessage("Lieferant ist erforderlich.")
            .When(b => b.BelegTyp.IstEinkaufsBeleg());
        RuleFor(b => b.BelegDatum).NotEqual(default(DateOnly));
        RuleFor(b => b.Positionen).NotEmpty().WithMessage("Beleg muss mindestens eine Position enthalten.");
        RuleForEach(b => b.Positionen).SetValidator(new BelegPositionValidator());
    }
}
