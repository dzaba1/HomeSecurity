using System.Security.Cryptography;

namespace Dzaba.HomeSecurity.LogsIngestion.Auth;

internal interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyHashedPassword(string hashedPassword, string providedPassword);
}

internal sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bits
    private const int KeySize = 32; // 256 bits
    private const int Iterations = 600_000; // OWASP recommendation for PBKDF2-HMAC-SHA256

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[SaltSize];
        rng.GetBytes(salt);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool VerifyHashedPassword(string hashedPassword, string providedPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashedPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(providedPassword);

        var parts = hashedPassword.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var hash = Convert.FromBase64String(parts[2]);

        var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(
            password: providedPassword,
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: KeySize);

        return CryptographicOperations.FixedTimeEquals(hash, hashToCompare);
    }
}

