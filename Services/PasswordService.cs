using System.Security.Cryptography;

namespace web.Services;

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string hash);
    // Olmayan hesap için zamanlama farkını gizlemekte kullanılan sabit hash.
    string DummyHash { get; }
}

// Format: v1.{iterations}.{saltBase64}.{hashBase64}
public class PasswordService : IPasswordService
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public PasswordService() => DummyHash = Hash(Guid.NewGuid().ToString());

    public string DummyHash { get; }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations))
            return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
