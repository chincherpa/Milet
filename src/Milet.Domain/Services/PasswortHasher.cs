using System.Security.Cryptography;

namespace Milet.Domain.Services;

/// <summary>
/// PBKDF2-HMACSHA256-Passworthashing. Speicherformat ist selbstbeschreibend
/// ("Iterationen.SalzBase64.HashBase64"), damit die Iterationszahl später ohne
/// Migration der Bestandsdaten erhöht werden kann.
/// </summary>
public static class PasswortHasher
{
    private const int Iterationen = 210_000;
    private const int SalzLaenge = 16;
    private const int HashLaenge = 32;

    public static string Hash(string klartext)
    {
        ArgumentException.ThrowIfNullOrEmpty(klartext);

        var salz = RandomNumberGenerator.GetBytes(SalzLaenge);
        var hash = Rfc2898DeriveBytes.Pbkdf2(klartext, salz, Iterationen, HashAlgorithmName.SHA256, HashLaenge);

        return $"{Iterationen}.{Convert.ToBase64String(salz)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string klartext, string gespeicherterHash)
    {
        if (string.IsNullOrEmpty(klartext) || string.IsNullOrEmpty(gespeicherterHash))
        {
            return false;
        }

        var teile = gespeicherterHash.Split('.');
        if (teile.Length != 3 || !int.TryParse(teile[0], out var iterationen))
        {
            return false;
        }

        byte[] salz;
        byte[] erwarteterHash;
        try
        {
            salz = Convert.FromBase64String(teile[1]);
            erwarteterHash = Convert.FromBase64String(teile[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var berechneterHash = Rfc2898DeriveBytes.Pbkdf2(
            klartext, salz, iterationen, HashAlgorithmName.SHA256, erwarteterHash.Length);

        return CryptographicOperations.FixedTimeEquals(berechneterHash, erwarteterHash);
    }
}
