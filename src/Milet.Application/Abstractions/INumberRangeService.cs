namespace Nexus.Application.Abstractions;

public interface INumberRangeService
{
    /// <summary>
    /// Vergibt atomar die nächste Nummer des Kreises (z. B. "KD" → "KD-10001").
    /// Wirft, wenn der Nummernkreis nicht existiert.
    /// </summary>
    Task<string> NaechsteNummerAsync(string code, CancellationToken cancellationToken = default);
}
