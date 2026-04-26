using System.Security.Cryptography;
using System.Text;

namespace Banco.Core.LocalStore;

internal static class LocalSecretProtector
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] StaticSalt = "Banco.AI.LocalSecret.v1"u8.ToArray();

    public static bool IsProtected(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith(Prefix, StringComparison.Ordinal);
    }

    public static string Protect(string value, string scope)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (IsProtected(value))
        {
            return value;
        }

        var key = DeriveKey(scope);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plainText = Encoding.UTF8.GetBytes(value);
        var cipherText = new byte[plainText.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainText, cipherText, tag);

        return $"{Prefix}{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(tag)}:{Convert.ToBase64String(cipherText)}";
    }

    public static string Unprotect(string value, string scope)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!IsProtected(value))
        {
            return value;
        }

        var parts = value[Prefix.Length..].Split(':', 3);
        if (parts.Length != 3)
        {
            return string.Empty;
        }

        try
        {
            var nonce = Convert.FromBase64String(parts[0]);
            var tag = Convert.FromBase64String(parts[1]);
            var cipherText = Convert.FromBase64String(parts[2]);
            var plainText = new byte[cipherText.Length];

            using var aes = new AesGcm(DeriveKey(scope), TagSize);
            aes.Decrypt(nonce, cipherText, tag, plainText);
            return Encoding.UTF8.GetString(plainText);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static byte[] DeriveKey(string scope)
    {
        var normalizedScope = string.IsNullOrWhiteSpace(scope)
            ? AppContext.BaseDirectory
            : scope;
        var material = $"{Environment.UserName}|{Environment.MachineName}|{normalizedScope}";
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(material),
            StaticSalt,
            100_000,
            HashAlgorithmName.SHA256,
            32);
    }
}
