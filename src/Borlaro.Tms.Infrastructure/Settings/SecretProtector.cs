using System.Security.Cryptography;
using System.Text;

namespace Borlaro.Tms.Infrastructure.Settings;

/// <summary>Cifra los ajustes secretos —clave del modelo, contraseña del SMTP— antes de
/// guardarlos. Sin esto, un volcado de la base entrega la clave de la API de IA en texto plano,
/// y esa clave se cobra.
///
/// AES-GCM con una clave derivada del material de `Secrets:Key`: autentica además de cifrar, así
/// que un valor manipulado a mano en la base falla al descifrar en vez de entrar como si nada.
/// La contrapartida honesta: si ese material cambia, los secretos guardados dejan de leerse y
/// hay que volver a cargarlos desde la interfaz. No hay recuperación, y es a propósito.</summary>
public class SecretProtector(string keyMaterial)
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    /// <summary>Devuelve null si el valor no se puede descifrar: la instancia arranca igual y el
    /// admin ve el ajuste como «no definido», que es mejor que no arrancar.</summary>
    public string? Unprotect(string protectedValue)
    {
        try
        {
            var raw = Convert.FromBase64String(protectedValue);
            if (raw.Length < NonceSize + TagSize) return null;

            var nonce = raw.AsSpan(0, NonceSize);
            var tag = raw.AsSpan(NonceSize, TagSize);
            var cipher = raw.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}
